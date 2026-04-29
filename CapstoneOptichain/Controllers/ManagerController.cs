using CapstoneOptichain.Data;
using CapstoneOptichain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Controllers
{
    public class ManagerController : Controller
    {
        private readonly ProjectContext _context;
        private readonly ILogger<ManagerController> _logger;
        private readonly IConfiguration _configuration;

        public ManagerController(ProjectContext context, ILogger<ManagerController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        private bool RequireManagerLogin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");
            
            if (userId == null || userType != "manager")
            {
                HttpContext.Response.Redirect("/Dashboard/Index");
                return true;
            }
            return false;
        }

        private async Task<bool> RequireActiveSubscription()
        {
            var managerId = HttpContext.Session.GetInt32("UserId");
            if (!managerId.HasValue) return false;

            var subscription = await _context.ManagerSubscriptions
                .Where(s => s.ManagerId == managerId.Value)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            // If no subscription or subscription is cancelled/expired, redirect to subscription page
            if (subscription == null || 
                subscription.Status == "Cancelled" || 
                subscription.Status == "Expired" ||
                subscription.SubscriptionEndDate <= DateTime.UtcNow)
            {
                return false;
            }

            return true;
        }

        // Manager support chat page
        public async Task<IActionResult> Support()
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return RedirectToAction("Subscription");
            }
            
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSupportMessages()
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }
            
            var managerId = HttpContext.Session.GetInt32("UserId");
            var messages = await _context.SupportMessages
                .Where(m => m.ManagerId == managerId)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new { id = m.Id, content = m.Content, sender = m.SenderType, createdAt = m.CreatedAt })
                .ToListAsync();
            return Json(new { success = true, data = messages });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSupportMessage([FromForm] string content)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }
            
            var managerId = HttpContext.Session.GetInt32("UserId");
            if (string.IsNullOrWhiteSpace(content)) return Json(new { success = false, message = "Message is empty" });
            var msg = new SupportMessage { ManagerId = managerId!.Value, SenderType = "manager", Content = content, CreatedAt = DateTime.UtcNow };
            _context.SupportMessages.Add(msg);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        public async Task<IActionResult> Workers()
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return RedirectToAction("Subscription");
            }
            
            return View();
        }

        public async Task<IActionResult> Subscription()
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                if (!managerId.HasValue)
                {
                    return RedirectToAction("Index", "Dashboard");
                }
                
                // Get manager's subscription
                var subscription = await _context.ManagerSubscriptions
                    .Where(s => s.ManagerId == managerId.Value)
                    .OrderByDescending(s => s.CreatedAt)
                    .FirstOrDefaultAsync();
                
                // If no subscription exists, create a default one for payment
                if (subscription == null)
                {
                    subscription = new ManagerSubscription
                    {
                        ID = 0, // Temporary ID for payment
                        ManagerId = managerId.Value,
                        Amount = 29.99m, // Default amount
                        PlanType = "Basic",
                        MaxStores = 3,
                        MaxWorkersPerStore = 5,
                        SubscriptionStartDate = DateTime.UtcNow,
                        SubscriptionEndDate = DateTime.UtcNow.AddDays(30),
                        Status = "Inactive",
                        CreatedAt = DateTime.UtcNow
                    };
                }
                
                // Get manager's stores count
                var storesCount = await _context.Stores
                    .Where(s => s.ManagerId == managerId.Value)
                    .CountAsync();
                
                // Get total workers count across all stores
                var storeIds = await _context.Stores
                    .Where(s => s.ManagerId == managerId.Value)
                    .Select(s => s.StoreId)
                    .ToListAsync();
                
                var totalWorkers = await _context.Workers
                    .Where(w => w.StoreId.HasValue && storeIds.Contains(w.StoreId.Value))
                    .CountAsync();
                
                // Get manager's payment methods
                var paymentMethods = await _context.PaymentMethods
                    .Where(p => p.ManagerId == managerId.Value)
                    .OrderByDescending(p => p.IsDefault)
                    .ToListAsync();
                
                ViewBag.Subscription = subscription;
                ViewBag.StoresCount = storesCount;
                ViewBag.TotalWorkers = totalWorkers;
                ViewBag.ManagerId = managerId.Value;
                ViewBag.PaymentMethods = paymentMethods;
                ViewBag.VodafoneCashNumber = _configuration["PaymentSettings:VodafoneCashNumber"] ?? "";
                ViewBag.FawryCode = _configuration["PaymentSettings:FawryCode"] ?? "";
                
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting subscription data for manager");
                TempData["ErrorMessage"] = "Error loading subscription data";
                return View();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessPayment([FromBody] PaymentRequest request)
        {
            if (RequireManagerLogin()) return Json(new { success = false, message = "Not authorized" });

            try
            {
                // Validate request
                if (request == null)
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                if (request.ManagerId <= 0 || request.Amount <= 0)
                {
                    return Json(new { success = false, message = "Invalid payment details" });
                }
                
                // Allow SubscriptionId = 0 for new subscriptions
                if (request.SubscriptionId < 0)
                {
                    return Json(new { success = false, message = "Invalid subscription ID" });
                }

                if (string.IsNullOrWhiteSpace(request.PaymentMethod))
                {
                    return Json(new { success = false, message = "Payment method is required" });
                }

                // Validate payment method
                var validMethods = new[] { "VodafoneCash", "Fawry", "Visa" };
                if (!validMethods.Contains(request.PaymentMethod))
                {
                    return Json(new { success = false, message = "Invalid payment method" });
                }

                // Additional validation for Visa
                if (request.PaymentMethod == "Visa")
                {
                    if (string.IsNullOrWhiteSpace(request.PaymentDetails))
                    {
                        return Json(new { success = false, message = "Visa payment requires card details" });
                    }
                    
                    // Basic Visa card validation (16 digits starting with 4)
                    var cardNumber = request.PaymentDetails.Replace(" ", "").Replace("-", "");
                    if (cardNumber.Length != 16 || !cardNumber.StartsWith("4"))
                    {
                        return Json(new { success = false, message = "Invalid Visa card number" });
                    }
                }

                // Verify manager exists
                var manager = await _context.Managers
                    .Include(m => m.ManagerSubscription)
                    .FirstOrDefaultAsync(m => m.ID == request.ManagerId);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found" });
                }

                // Verify subscription exists
                var subscription = await _context.ManagerSubscriptions
                    .Include(ms => ms.Manager)
                    .FirstOrDefaultAsync(ms => ms.ID == request.SubscriptionId);
                if (subscription == null)
                {
                    return Json(new { success = false, message = "Subscription not found. Please refresh the page and try again." });
                }


                // Verify the subscription belongs to the manager
                if (subscription.ManagerId != request.ManagerId)
                {
                    return Json(new { success = false, message = "Subscription does not belong to this manager" });
                }
            

                // Check if payment already exists for this subscription (only for existing subscriptions)
                if (request.SubscriptionId > 0)
                {
                    var existingPayment = await _context.Payments
                        .FirstOrDefaultAsync(p => p.SubscriptionId == request.SubscriptionId && 
                                                p.PaymentStatus == "Pending");
                    
                    if (existingPayment != null)
                    {
                        return Json(new { success = false, message = "Payment request already exists for this subscription" });
                    }
                }



                // Create payment record
                var generatedTransactionId = request.PaymentMethod switch
                {
                    "Visa" => $"VISA-{Guid.NewGuid().ToString("N").Substring(0, 16).ToUpper()}",
                    "VodafoneCash" => $"VFC-{Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper()}",
                    "Fawry" => $"FWR-{Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper()}",
                    _ => Guid.NewGuid().ToString("N").ToUpper()
                };

                var payment = new Payment
                {
                    ManagerId = request.ManagerId,
                    SubscriptionId = request.SubscriptionId,
                    Amount = request.Amount,
                    PaymentMethod = request.PaymentMethod,
                    PaymentStatus = "Pending",
                    PaymentDetails = request.PaymentDetails ?? "",
                    TransactionId = generatedTransactionId,
                    CreatedAt = DateTime.UtcNow
                };
                
                // Log payment creation
                _logger.LogInformation("Creating payment: ManagerId={ManagerId}, SubscriptionId={SubscriptionId}, Amount={Amount}, Method={Method}", 
                    request.ManagerId, request.SubscriptionId, request.Amount, request.PaymentMethod);

                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                // Create admin notification
                var managerName = !string.IsNullOrEmpty(manager.name) ? manager.name : $"Manager {manager.ID}";
                var subscriptionText = request.SubscriptionId == 0 ? "new subscription" : $"subscription #{request.SubscriptionId}";
                var notificationMessage = $"Payment submitted by {managerName} via {request.PaymentMethod} for ${request.Amount:F2}. Please verify and process {subscriptionText}.";
                var notification = new Notification
                {
                    Message = notificationMessage,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = null,
                    NotificationType = "Manager"
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Payment processed successfully: ManagerId={ManagerId}, SubscriptionId={SubscriptionId}, Amount={Amount}", 
                    request.ManagerId, request.SubscriptionId, request.Amount);

                var successMessage = request.SubscriptionId == 0 
                    ? "Payment request submitted successfully. Admin will verify and create your subscription." 
                    : "Payment request submitted successfully. Admin will verify and process it.";
                    
                return Json(new { success = true, message = successMessage });
            }
            catch (DbUpdateException dbEx)
            {
                _logger.LogError(dbEx, "Database error while processing payment for manager {ManagerId}: {Message}", request.ManagerId, dbEx.Message);
                var errorMessage = dbEx.InnerException?.Message ?? dbEx.Message;
                return Json(new { success = false, message = $"Database error while saving payment. Please try again. Details: {errorMessage}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing payment request for manager {ManagerId}: {Message}", request.ManagerId, ex.Message);
                return Json(new { success = false, message = $"Error processing payment. Please try again. Details: {ex.Message}" });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestSubscriptionRenewal()
        {
            if (RequireManagerLogin()) return Json(new { success = false, message = "Not authorized" });
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                if (!managerId.HasValue)
                {
                    return Json(new { success = false, message = "Manager not authenticated" });
                }

                var manager = await _context.Managers.FindAsync(managerId.Value);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found" });
                }

                var subscription = await _context.ManagerSubscriptions
                    .FirstOrDefaultAsync(s => s.ManagerId == managerId.Value);

                // Check if renewal request already exists
                var managerName = !string.IsNullOrEmpty(manager.name) ? manager.name : $"Manager {manager.ID}";
                var existingRequest = await _context.Notifications
                    .FirstOrDefaultAsync(n => n.NotificationType == "Manager" && 
                                           n.Message.Contains("requested subscription renewal") &&
                                           n.Message.Contains(managerName) &&
                                           n.CreatedAt > DateTime.UtcNow.AddHours(-24)); // Within last 24 hours

                if (existingRequest != null)
                {
                    return Json(new { success = false, message = "Renewal request already submitted recently. Please wait for admin response." });
                }

                var message = $"Manager {managerName} requested subscription renewal" +
                              (subscription != null ? $" (Subscription ID: {subscription.ID})" : string.Empty);

                var notification = new Notification
                {
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = null,
                    NotificationType = "Manager"
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Renewal request sent to admin successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending subscription renewal request");
                return Json(new { success = false, message = "Error sending renewal request. Please try again." });
            }
        }



        [HttpPost]
        public async Task<IActionResult> AddPaymentMethod([FromForm] PaymentMethodCreateViewModel model)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var managerId = HttpContext.Session.GetInt32("UserId");
                    if (managerId == null)
                    {
                        return Json(new { success = false, message = "Manager not authenticated" });
                    }

                    var paymentMethod = new PaymentMethod
                    {
                        ManagerId = managerId.Value,
                        PaymentType = model.PaymentType,
                        CardType = model.CardType,
                        LastFourDigits = model.LastFourDigits,
                        ExpiryMonth = model.ExpiryMonth,
                        ExpiryYear = model.ExpiryYear,
                        CardholderName = model.CardholderName,
                        VodafoneCashNumber = model.VodafoneCashNumber,
                        FawryNumber = model.FawryNumber,
                        FawryEmail = model.FawryEmail,
                        IsDefault = model.IsDefault,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    // If this is set as default, unset all other payment methods
                    if (model.IsDefault)
                    {
                        var existingMethods = await _context.PaymentMethods
                            .Where(p => p.ManagerId == managerId.Value)
                            .ToListAsync();
                        
                        foreach (var method in existingMethods)
                        {
                            method.IsDefault = false;
                        }
                    }

                    _context.PaymentMethods.Add(paymentMethod);
                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Payment method added successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding payment method");
                    return Json(new { success = false, message = "Error adding payment method" });
                }
            }

            return Json(new { success = false, message = "Invalid data" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePaymentMethod([FromForm] PaymentMethodUpdateViewModel model)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var managerId = HttpContext.Session.GetInt32("UserId");
                    if (managerId == null)
                    {
                        return Json(new { success = false, message = "Manager not authenticated" });
                    }

                    var paymentMethod = await _context.PaymentMethods
                        .Where(p => p.ID == model.Id && p.ManagerId == managerId.Value)
                        .FirstOrDefaultAsync();

                    if (paymentMethod == null)
                    {
                        return Json(new { success = false, message = "Payment method not found" });
                    }

                    paymentMethod.PaymentType = model.PaymentType;
                    paymentMethod.CardType = model.CardType;
                    paymentMethod.LastFourDigits = model.LastFourDigits;
                    paymentMethod.ExpiryMonth = model.ExpiryMonth;
                    paymentMethod.ExpiryYear = model.ExpiryYear;
                    paymentMethod.CardholderName = model.CardholderName;
                    paymentMethod.VodafoneCashNumber = model.VodafoneCashNumber;
                    paymentMethod.FawryNumber = model.FawryNumber;
                    paymentMethod.FawryEmail = model.FawryEmail;
                    paymentMethod.UpdatedAt = DateTime.UtcNow;

                    // If this is set as default, unset all other payment methods
                    if (model.IsDefault)
                    {
                        var existingMethods = await _context.PaymentMethods
                            .Where(p => p.ManagerId == managerId.Value && p.ID != model.Id)
                            .ToListAsync();
                        
                        foreach (var method in existingMethods)
                        {
                            method.IsDefault = false;
                        }
                        paymentMethod.IsDefault = true;
                    }

                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Payment method updated successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating payment method");
                    return Json(new { success = false, message = "Error updating payment method" });
                }
            }

            return Json(new { success = false, message = "Invalid data" });
        }

        [HttpPost]
        public async Task<IActionResult> DeletePaymentMethod([FromBody] DeletePaymentMethodRequest request)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                if (managerId == null)
                {
                    return Json(new { success = false, message = "Manager not authenticated" });
                }

                var paymentMethod = await _context.PaymentMethods
                    .Where(p => p.ID == request.Id && p.ManagerId == managerId.Value)
                    .FirstOrDefaultAsync();

                if (paymentMethod == null)
                {
                    return Json(new { success = false, message = "Payment method not found" });
                }

                _context.PaymentMethods.Remove(paymentMethod);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Payment method deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting payment method");
                return Json(new { success = false, message = "Error deleting payment method" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetWorkers()
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation($"GetWorkers called for manager ID: {managerId}");
                
                // Get the selected store from session, or fallback to first store
                var selectedStoreId = HttpContext.Session.GetString("SelectedStoreId");
                _logger.LogInformation($"Selected Store ID from session: {selectedStoreId}");
                
                Store store = null;
                
                if (!string.IsNullOrEmpty(selectedStoreId))
                {
                    // Try to get the selected store
                    store = await _context.Stores
                        .Where(s => s.StoreId.ToString() == selectedStoreId && s.ManagerId == managerId)
                        .FirstOrDefaultAsync();
                    _logger.LogInformation($"Found selected store: {store?.StoreId}");
                }
                
                // If no selected store or selected store not found, get the first store
                if (store == null)
                {
                    _logger.LogInformation("No selected store found, looking for any store for this manager");
                    store = await _context.Stores
                        .Where(s => s.ManagerId == managerId)
                        .FirstOrDefaultAsync();
                    _logger.LogInformation($"Found fallback store: {store?.StoreId}");
                }

                if (store == null)
                {
                    _logger.LogWarning($"No store found for manager {managerId}");
                    
                    // Let's check what stores exist in the database
                    var allStores = await _context.Stores.ToListAsync();
                    _logger.LogInformation($"Total stores in database: {allStores.Count}");
                    
                    foreach (var s in allStores)
                    {
                        _logger.LogInformation($"Store {s.StoreId}: ManagerId={s.ManagerId}, Name={s.StoreName}");
                    }
                    
                    return Json(new List<object>());
                }
                
                _logger.LogInformation($"Using store: {store.StoreId} - {store.StoreName}");
                
                // Get workers associated with the specific store
                var workers = await _context.Workers
                    .Where(w => w.StoreId == store.StoreId)
                    .Select(w => new
                    {
                        id = w.ID,
                        name = w.name,
                        email = w.email,
                        phone = w.Phone_number,
                        department = w.Department,
                        address = w.Address,
                        role = w.role
                    })
                    .ToListAsync();

                _logger.LogInformation($"Found {workers.Count} workers for store {store.StoreId}");
                return Json(workers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting workers for manager");
                return Json(new List<object>());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddWorker([FromForm] WorkerCreateViewModel model)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            _logger.LogInformation($"AddWorker called with model: Name={model.Name}, Email={model.Email}, Phone={model.Phone}, Department={model.Department}, Address={model.Address}, Password={!string.IsNullOrEmpty(model.Password)}");

            // Log ModelState errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogWarning($"ModelState errors: {string.Join(", ", errors)}");
                return Json(new { success = false, message = "Invalid data", errors = errors.ToList() });
            }

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                _logger.LogInformation($"Manager ID from session: {managerId}");
                
                if (managerId == null)
                {
                    _logger.LogWarning("Manager ID is null in session");
                    return Json(new { success = false, message = "Manager not authenticated" });
                }
                
                // Get the selected store from session, or fallback to first store
                var selectedStoreId = HttpContext.Session.GetString("SelectedStoreId");
                _logger.LogInformation($"Selected Store ID from session: {selectedStoreId}");
                
                Store store = null;
                
                if (!string.IsNullOrEmpty(selectedStoreId))
                {
                    // Try to get the selected store
                    store = await _context.Stores
                        .Where(s => s.StoreId.ToString() == selectedStoreId && s.ManagerId == managerId)
                        .FirstOrDefaultAsync();
                    _logger.LogInformation($"Found selected store: {store?.StoreId}");
                }
                
                // If no selected store or selected store not found, get the first store
                if (store == null)
                {
                    _logger.LogInformation("No selected store found, looking for any store for this manager");
                    store = await _context.Stores
                        .Where(s => s.ManagerId == managerId)
                        .FirstOrDefaultAsync();
                    _logger.LogInformation($"Found fallback store: {store?.StoreId}");
                }

                if (store == null)
                {
                    _logger.LogWarning($"No store found for manager {managerId}");
                    
                    // Let's check what stores exist in the database
                    var allStores = await _context.Stores.ToListAsync();
                    _logger.LogInformation($"Total stores in database: {allStores.Count}");
                    
                    foreach (var s in allStores)
                    {
                        _logger.LogInformation($"Store {s.StoreId}: ManagerId={s.ManagerId}, Name={s.StoreName}");
                    }
                    
                    return Json(new { success = false, message = "No store found for this manager" });
                }

                // Check if email already exists
                if (await _context.Workers.AnyAsync(w => w.email == model.Email))
                {
                    _logger.LogWarning($"Email already exists: {model.Email}");
                    return Json(new { success = false, message = "Email already exists" });
                }

                var worker = new Worker
                {
                    name = model.Name,
                    email = model.Email,
                    password = HashPassword(model.Password),
                    Phone_number = model.Phone,
                    Address = model.Address ?? "",
                    Department = model.Department,
                    role = "worker",
                    StoreId = store.StoreId,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _logger.LogInformation($"Creating worker with StoreId: {worker.StoreId}");

                _context.Workers.Add(worker);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Worker added successfully with ID: {worker.ID}");
                return Json(new { success = true, message = "Worker added successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding worker");
                return Json(new { success = false, message = "Error adding worker: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateWorker([FromForm] WorkerUpdateViewModel model)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var managerId = HttpContext.Session.GetInt32("UserId");
                    
                                    // Get the selected store from session, or fallback to first store
                var selectedStoreId = HttpContext.Session.GetString("SelectedStoreId");
                Store store = null;
                    
                    if (!string.IsNullOrEmpty(selectedStoreId))
                    {
                        // Try to get the selected store
                        store = await _context.Stores
                            .Where(s => s.StoreId.ToString() == selectedStoreId && s.ManagerId == managerId)
                            .FirstOrDefaultAsync();
                    }
                    
                    // If no selected store or selected store not found, get the first store
                    if (store == null)
                    {
                        store = await _context.Stores
                            .Where(s => s.ManagerId == managerId)
                            .FirstOrDefaultAsync();
                    }

                    if (store == null)
                    {
                        return Json(new { success = false, message = "No store found for this manager" });
                    }
                    
                    // Verify the worker belongs to the manager's store
                    var worker = await _context.Workers
                        .Where(w => w.ID == model.Id && w.StoreId == store.StoreId)
                        .FirstOrDefaultAsync();

                    if (worker == null)
                    {
                        return Json(new { success = false, message = "Worker not found" });
                    }

                    // Unique email check excluding current worker
                    if (!string.IsNullOrWhiteSpace(model.Email))
                    {
                        var emailExists = await _context.Workers
                            .AnyAsync(w => w.email == model.Email && w.ID != worker.ID);
                        if (emailExists)
                        {
                            return Json(new { success = false, message = "Email already exists for another worker" });
                        }
                    }

                    worker.name = model.Name ?? worker.name;
                    worker.email = model.Email ?? worker.email;
                    worker.Phone_number = model.Phone ?? worker.Phone_number;
                    worker.Address = model.Address ?? worker.Address;
                    worker.Department = model.Department ?? worker.Department;

                    await _context.SaveChangesAsync();

                    return Json(new { success = true, message = "Worker updated successfully" });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error updating worker");
                    return Json(new { success = false, message = ex.Message });
                }
            }

            return Json(new { success = false, message = "Invalid data" });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteWorker([FromBody] DeleteWorkerRequest request)
        {
            if (RequireManagerLogin()) return new EmptyResult();
            
            // Check subscription for non-subscription related pages
            if (!await RequireActiveSubscription())
            {
                return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
            }

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                
                // Get the selected store from session, or fallback to first store
                var selectedStoreId = HttpContext.Session.GetString("SelectedStoreId");
                Store store = null;
                
                if (!string.IsNullOrEmpty(selectedStoreId))
                {
                    // Try to get the selected store
                    store = await _context.Stores
                        .Where(s => s.StoreId.ToString() == selectedStoreId && s.ManagerId == managerId)
                        .FirstOrDefaultAsync();
                }
                
                // If no selected store or selected store not found, get the first store
                if (store == null)
                {
                    store = await _context.Stores
                        .Where(s => s.ManagerId == managerId)
                        .FirstOrDefaultAsync();
                }

                if (store == null)
                {
                    return Json(new { success = false, message = "No store found for this manager" });
                }
                
                // Verify the worker belongs to the manager's store
                var worker = await _context.Workers
                    .Where(w => w.ID == request.Id && w.StoreId == store.StoreId)
                    .FirstOrDefaultAsync();

                if (worker == null)
                {
                    return Json(new { success = false, message = "Worker not found" });
                }

                _context.Workers.Remove(worker);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Worker deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting worker");
                return Json(new { success = false, message = "Error deleting worker" });
            }
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }
    }

    public class PaymentRequest
    {
        public int ManagerId { get; set; }
        public int SubscriptionId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; }
        public string PaymentDetails { get; set; }
    }

    public class SubscriptionRenewalRequest
    {
        public int Id { get; set; }
        public int ManagerId { get; set; }
        public int SubscriptionId { get; set; }
        public DateTime RequestedAt { get; set; }
        public string Status { get; set; }
    }

    public class WorkerCreateViewModel
    {
        [Required(ErrorMessage = "Name is required")]
        public string Name { get; set; }
        
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; }
        
        [Required(ErrorMessage = "Phone is required")]
        public string Phone { get; set; }
        
        public string Address { get; set; }
        
        [Required(ErrorMessage = "Department is required")]
        public string Department { get; set; }
        
        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public string Password { get; set; }
    }

    public class WorkerUpdateViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Department { get; set; }
    }

    public class DeleteWorkerRequest
    {
        public int Id { get; set; }
    }

    public class PaymentMethodCreateViewModel
    {
        [Required(ErrorMessage = "Payment type is required")]
        public string PaymentType { get; set; }
        
        public string CardType { get; set; }
        public string LastFourDigits { get; set; }
        public string ExpiryMonth { get; set; }
        public string ExpiryYear { get; set; }
        public string CardholderName { get; set; }
        public string VodafoneCashNumber { get; set; }
        public string FawryNumber { get; set; }
        public string FawryEmail { get; set; }
        public bool IsDefault { get; set; }
    }

    public class PaymentMethodUpdateViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Payment type is required")]
        public string PaymentType { get; set; }
        
        public string CardType { get; set; }
        public string LastFourDigits { get; set; }
        public string ExpiryMonth { get; set; }
        public string ExpiryYear { get; set; }
        public string CardholderName { get; set; }
        public string VodafoneCashNumber { get; set; }
        public string FawryNumber { get; set; }
        public string FawryEmail { get; set; }
        public bool IsDefault { get; set; }
    }

    public class DeletePaymentMethodRequest
    {
        public int Id { get; set; }
    }
}
