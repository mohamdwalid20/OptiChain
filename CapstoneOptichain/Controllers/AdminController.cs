using CapstoneOptichain.Data;
using CapstoneOptichain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace CapstoneOptichain.Controllers
{
    public class AdminController : Controller
    {
        private readonly ProjectContext _context;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public AdminController(ProjectContext context, ILogger<AdminController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
        }

        public class DeleteManagerRequest
        {
            public int Id { get; set; }
        }

        private bool RequireAdminLogin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var userType = HttpContext.Session.GetString("UserType");
            
            if (userId == null || userType != "admin")
            {
                HttpContext.Response.Redirect("/Dashboard/Index");
                return true;
            }
            return false;
        }

        public async Task<IActionResult> Index()
        {
            if (RequireAdminLogin()) return new EmptyResult();

            try
            {
                var dashboardData = new AdminDashboardViewModel
                {
                    TotalManagers = await _context.Managers.CountAsync(),
                    ActiveSubscriptions = await _context.ManagerSubscriptions
                        .Where(ms => ms.Status == "Active" && ms.SubscriptionEndDate > DateTime.UtcNow)
                        .CountAsync(),
                    ExpiredSubscriptions = await _context.ManagerSubscriptions
                        .Where(ms => ms.Status == "Expired" || ms.SubscriptionEndDate <= DateTime.UtcNow)
                        .CountAsync(),
                    TotalRevenue = await _context.ManagerSubscriptions
                        .Where(ms => ms.Status == "Active")
                        .SumAsync(ms => ms.Amount),
                    RecentManagers = await _context.Managers
                        .OrderByDescending(m => m.CreatedAt)
                        .Take(5)
                        .ToListAsync(),
                    ExpiringSubscriptions = await _context.ManagerSubscriptions
                        .Include(ms => ms.Manager)
                        .Where(ms => ms.Status == "Active" && 
                               ms.SubscriptionEndDate <= DateTime.UtcNow.AddDays(7) &&
                               ms.SubscriptionEndDate > DateTime.UtcNow)
                        .OrderBy(ms => ms.SubscriptionEndDate)
                        .Take(10)
                        .ToListAsync()
                };

                return View(dashboardData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading admin dashboard");
                return View("Error");
            }
        }

        public async Task<IActionResult> Managers()
        {
            if (RequireAdminLogin()) return new EmptyResult();

            var managers = await _context.Managers
                .Include(m => m.ManagerSubscription)
                .OrderByDescending(m => m.CreatedAt)
                .ToListAsync();

            return View(managers);
        }

        public IActionResult AddManager()
        {
            if (RequireAdminLogin()) return new EmptyResult();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddManager(ManagerCreateViewModel model)
        {
            if (RequireAdminLogin()) return new EmptyResult();

            if (ModelState.IsValid)
            {
                try
                {
                    // Check if email already exists
                    if (await _context.Managers.AnyAsync(m => m.email == model.Email))
                    {
                        ModelState.AddModelError("Email", "Email already exists");
                        return View(model);
                    }

                    // Create manager only
                    var manager = new Manager
                    {
                        name = model.Name,
                        email = model.Email,
                        password = HashPassword(model.Password),
                        phone = model.Phone ?? string.Empty,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };

                    _context.Managers.Add(manager);
                    await _context.SaveChangesAsync();

                    TempData["SuccessMessage"] = "Manager added successfully. Now create subscription.";
                    return RedirectToAction("CreateSubscription", new { managerId = manager.ID });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error adding manager");
                    ModelState.AddModelError("", "Error adding manager. Please try again.");
                }
            }

            return View(model);
        }

        // New action to create subscription
        public IActionResult CreateSubscription(int managerId)
        {
            if (RequireAdminLogin()) return new EmptyResult();
            
            var manager = _context.Managers.Find(managerId);
            if (manager == null)
            {
                TempData["ErrorMessage"] = "Manager not found";
                return RedirectToAction("Managers");
            }

            ViewBag.ManagerId = managerId;
            ViewBag.ManagerName = manager.name;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSubscription(int managerId, string planType, int months)
        {
            if (RequireAdminLogin()) return new EmptyResult();

            // Add logging to debug the issue
            _logger.LogInformation($"CreateSubscription called with: managerId={managerId}, planType={planType}, months={months}");

            try
            {
                var manager = await _context.Managers.FindAsync(managerId);
                if (manager == null)
                {
                    _logger.LogWarning($"Manager not found with ID: {managerId}");
                    TempData["ErrorMessage"] = "Manager not found";
                    return RedirectToAction("Managers");
                }

                // Check if subscription already exists
                if (await _context.ManagerSubscriptions.AnyAsync(ms => ms.ManagerId == managerId))
                {
                    _logger.LogWarning($"Subscription already exists for manager ID: {managerId}");
                    TempData["ErrorMessage"] = "Subscription already exists for this manager";
                    return RedirectToAction("Managers");
                }

                // Calculate amount based on plan type and months
                decimal amount = 0;
                switch (planType?.ToLower())
                {
                    case "monthly":
                        amount = 99.99m * months;
                        break;
                    case "quarterly":
                        amount = 89.99m * months;
                        break;
                    case "yearly":
                        amount = 79.99m * months;
                        break;
                    default:
                        amount = 99.99m * months;
                        break;
                }

                _logger.LogInformation($"Calculated amount: {amount} for plan: {planType}, months: {months}");

                // Create subscription
                var subscription = new ManagerSubscription
                {
                    ManagerId = managerId,
                    SubscriptionStartDate = DateTime.UtcNow,
                    SubscriptionEndDate = DateTime.UtcNow.AddMonths(months),
                    Amount = amount,
                    Status = "Active",
                    PlanType = planType,
                    MaxStores = 50,
                    MaxWorkersPerStore = 20,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ManagerSubscriptions.Add(subscription);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Subscription created successfully for manager {manager.name} (ID: {managerId})");
                TempData["SuccessMessage"] = $"Subscription created successfully for {months} months";
                return RedirectToAction("Managers");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating subscription");
                TempData["ErrorMessage"] = "Error creating subscription. Please try again.";
                return RedirectToAction("CreateSubscription", new { managerId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteManager([FromBody] DeleteManagerRequest request)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                if (request == null || request.Id <= 0)
                {
                    return Json(new { success = false, message = "Invalid request" });
                }

                var manager = await _context.Managers.FindAsync(request.Id);
                if (manager == null)
                {
                    return Json(new { success = false, message = "Manager not found" });
                }

                // Delete related data
                var subscription = await _context.ManagerSubscriptions.FirstOrDefaultAsync(ms => ms.ManagerId == request.Id);
                if (subscription != null)
                {
                    // Delete payments tied to this subscription
                    var subPayments = await _context.Payments.Where(p => p.SubscriptionId == subscription.ID).ToListAsync();
                    if (subPayments.Any()) _context.Payments.RemoveRange(subPayments);
                    _context.ManagerSubscriptions.Remove(subscription);
                }

                // Delete stores and their data (orders, order items, proposals, products, inventories, workers, suppliers, notifications)
                var stores = await _context.Stores.Where(s => s.ManagerId == request.Id).ToListAsync();
                foreach (var store in stores)
                {
                    // Orders and related
                    var orders = await _context.Orders.Where(o => o.StoreId == store.StoreId).ToListAsync();
                    foreach (var order in orders)
                    {
                        var proposals = await _context.SupplierProposals
                            .Where(sp => sp.OrderId == order.OrderId)
                            .Include(sp => sp.ProposalItems)
                            .ToListAsync();
                        foreach (var p in proposals)
                        {
                            if (p.ProposalItems != null && p.ProposalItems.Any())
                                _context.ProposalItems.RemoveRange(p.ProposalItems);
                        }
                        if (proposals.Any()) _context.SupplierProposals.RemoveRange(proposals);

                        var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == order.OrderId).ToListAsync();
                        if (orderItems.Any()) _context.OrderItems.RemoveRange(orderItems);

                        var orderRequests = await _context.OrderRequests.Where(or => or.OrderId == order.OrderId).ToListAsync();
                        if (orderRequests.Any()) _context.OrderRequests.RemoveRange(orderRequests);
                    }
                    if (orders.Any()) _context.Orders.RemoveRange(orders);

                    // Products & inventories
                    var inventories = await _context.Inventories.Where(i => i.StoreId == store.StoreId).ToListAsync();
                    if (inventories.Any()) _context.Inventories.RemoveRange(inventories);

                    var products = await _context.Products.Where(p => p.StoreId == store.StoreId).ToListAsync();
                    if (products.Any()) _context.Products.RemoveRange(products);

                    // Workers & suppliers
                    var workers = await _context.Workers.Where(w => w.StoreId == store.StoreId).ToListAsync();
                    _context.Workers.RemoveRange(workers);
                    
                    var suppliers = await _context.Suppliers.Where(s => s.StoreId == store.StoreId).ToListAsync();
                    _context.Suppliers.RemoveRange(suppliers);

                    // Notifications for this store
                    var notifs = await _context.Notifications.Where(n => n.StoreId == store.StoreId).ToListAsync();
                    if (notifs.Any()) _context.Notifications.RemoveRange(notifs);
                }
                _context.Stores.RemoveRange(stores);

                // Payments linked directly to manager
                var mgrPayments = await _context.Payments.Where(p => p.ManagerId == request.Id).ToListAsync();
                if (mgrPayments.Any()) _context.Payments.RemoveRange(mgrPayments);

                // Support messages
                var msgs = await _context.SupportMessages.Where(m => m.ManagerId == request.Id).ToListAsync();
                if (msgs.Any()) _context.SupportMessages.RemoveRange(msgs);

                _context.Managers.Remove(manager);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Manager deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting manager");
                return Json(new { success = false, message = "Error deleting manager" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ExtendSubscription([FromBody] ExtendSubscriptionRequest request)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                if (request == null || request.ManagerId <= 0 || request.Months <= 0)
                {
                    return Json(new { success = false, message = "Invalid request data" });
                }

                var subscription = await _context.ManagerSubscriptions
                    .FirstOrDefaultAsync(ms => ms.ManagerId == request.ManagerId);

                if (subscription == null)
                {
                    return Json(new { success = false, message = "No subscription found for this manager" });
                }

                // Extend subscription
                subscription.SubscriptionEndDate = subscription.SubscriptionEndDate.AddMonths(request.Months);
                subscription.Status = "Active";
                subscription.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                // Create notification for manager
                var notification = new Notification
                {
                    Message = $"Your subscription has been extended by {request.Months} month(s). New end date: {subscription.SubscriptionEndDate:MMM dd, yyyy}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = null,
                    NotificationType = "Manager"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = $"Subscription extended by {request.Months} month(s)" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extending subscription for manager {ManagerId}", request?.ManagerId);
                return Json(new { success = false, message = "Error extending subscription: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CancelSubscription([FromBody] int managerId)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Unauthorized" });

            try
            {
                _logger.LogInformation($"CancelSubscription called for manager ID: {managerId}");
                
                var subscription = await _context.ManagerSubscriptions
                    .FirstOrDefaultAsync(ms => ms.ManagerId == managerId);

                if (subscription == null)
                {
                    _logger.LogWarning($"No subscription found for manager ID: {managerId}");
                    return Json(new { success = false, message = "Subscription not found" });
                }

                _logger.LogInformation($"Found subscription ID: {subscription.ID}, Status: {subscription.Status}");

                subscription.Status = "Cancelled";
                subscription.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation($"Subscription {subscription.ID} cancelled successfully for manager {managerId}");

                return Json(new { success = true, message = "Subscription cancelled successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling subscription for manager {ManagerId}", managerId);
                return Json(new { success = false, message = "Error cancelling subscription" });
            }
        }

        public async Task<IActionResult> Subscriptions()
        {
            if (RequireAdminLogin()) return new EmptyResult();

            var subscriptions = await _context.ManagerSubscriptions
                .Include(ms => ms.Manager)
                .OrderByDescending(ms => ms.CreatedAt)
                .ToListAsync();

            return View(subscriptions);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserProfileData()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userType = HttpContext.Session.GetString("UserType");

                if (userId == null || userType != "admin")
                {
                    return Json(new { success = false, message = "User not logged in" });
                }

                // Fetch admin from DB
                var admin = await _context.Admins.FirstOrDefaultAsync(a => a.ID == userId);
                if (admin == null)
                {
                    return Json(new { success = false, message = "Admin not found" });
                }

                return Json(new {
                    success = true,
                    userName = admin.name,
                    profileImageUrl = string.IsNullOrEmpty(admin.ProfileImageUrl) ? "/images/default-avatar.png" : admin.ProfileImageUrl,
                    userType = "admin"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error getting user profile data: " + ex.Message });
            }
        }

        public IActionResult LogOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Dashboard");
        }

        // Admin: list conversations
        public async Task<IActionResult> Support()
        {
            if (RequireAdminLogin()) return new EmptyResult();
            var threads = await _context.SupportMessages
                .Include(m => m.Manager)
                .GroupBy(m => m.ManagerId)
                .Select(g => new {
                    ManagerId = g.Key,
                    ManagerName = g.Max(x => x.Manager.name),
                    LastAt = g.Max(x => x.CreatedAt),
                    Unread = g.Count(x => !x.IsRead && x.SenderType == "manager")
                })
                .OrderByDescending(t => t.LastAt)
                .ToListAsync();
            ViewBag.Threads = threads;
            return View();
        }

        // Admin: messages with a specific manager
        public async Task<IActionResult> SupportThread(int managerId)
        {
            if (RequireAdminLogin()) return new EmptyResult();
            var messages = await _context.SupportMessages
                .Where(m => m.ManagerId == managerId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();
            // mark manager messages as read
            foreach (var m in messages.Where(x => x.SenderType == "manager" && !x.IsRead)) m.IsRead = true;
            await _context.SaveChangesAsync();
            ViewBag.Manager = await _context.Managers.FindAsync(managerId);
            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendSupportReply(int managerId, string content)
        {
            if (RequireAdminLogin()) return new EmptyResult();
            if (string.IsNullOrWhiteSpace(content)) return RedirectToAction("SupportThread", new { managerId });
            var msg = new SupportMessage { ManagerId = managerId, SenderType = "admin", Content = content, CreatedAt = DateTime.UtcNow, IsRead = false };
            _context.SupportMessages.Add(msg);
            await _context.SaveChangesAsync();
            return RedirectToAction("SupportThread", new { managerId });
        }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        // Admin: list all payments
        public async Task<IActionResult> Payments()
        {
            if (RequireAdminLogin()) return new EmptyResult();
            
            var payments = await _context.Payments
                .Include(p => p.Manager)
                .Include(p => p.Subscription)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();
            
            ViewBag.Payments = payments;
            return View();
        }

        // Admin: approve payment
        [HttpPost]
        public async Task<IActionResult> ApprovePayment([FromBody] PaymentApprovalRequest request)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Not authorized" });
            
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Subscription)
                    .FirstOrDefaultAsync(p => p.ID == request.PaymentId);
                
                if (payment == null)
                {
                    return Json(new { success = false, message = "Payment not found" });
                }

                // Update payment status
                payment.PaymentStatus = "Completed";
                payment.PaidAt = DateTime.UtcNow;

                // Extend subscription
                if (payment.Subscription != null)
                {
                    // Calculate new end date (add 1 month from current end date or from now if expired)
                    var currentEndDate = payment.Subscription.SubscriptionEndDate;
                    var now = DateTime.UtcNow;
                    
                    if (currentEndDate <= now)
                    {
                        // Subscription is expired, start from now
                        payment.Subscription.SubscriptionStartDate = now;
                        payment.Subscription.SubscriptionEndDate = now.AddMonths(1);
                    }
                    else
                    {
                        // Subscription is still active, extend from current end date
                        payment.Subscription.SubscriptionEndDate = currentEndDate.AddMonths(1);
                    }
                    
                    payment.Subscription.Status = "Active";
                    payment.Subscription.LastPaymentDate = now;
                    payment.Subscription.NextPaymentDate = payment.Subscription.SubscriptionEndDate;
                }

                await _context.SaveChangesAsync();

                // Create notification for manager
                var notification = new Notification
                {
                    Message = $"Your payment of ${payment.Amount:F2} has been approved and subscription extended until {payment.Subscription?.SubscriptionEndDate:MMM dd, yyyy}!",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = null,
                    NotificationType = "Manager"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Payment approved successfully", paymentId = payment.ID });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving payment: {Message}", ex.Message);
                return Json(new { success = false, message = "Error approving payment: " + ex.Message });
            }
        }

        // Admin: reject payment
        [HttpPost]
        public async Task<IActionResult> RejectPayment([FromBody] PaymentApprovalRequest request)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Not authorized" });
            
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Manager)
                    .FirstOrDefaultAsync(p => p.ID == request.PaymentId);
                
                if (payment == null)
                {
                    return Json(new { success = false, message = "Payment not found" });
                }

                // Update payment status
                payment.PaymentStatus = "Failed";

                await _context.SaveChangesAsync();

                // Create notification for manager
                var notification = new Notification
                {
                    Message = $"Your payment of ${payment.Amount:F2} has been rejected. Please contact support.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = null,
                    NotificationType = "Manager"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Payment rejected successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting payment");
                return Json(new { success = false, message = "Error rejecting payment: " + ex.Message });
            }
        }

        // Admin: list renewal requests (notifications)
        public async Task<IActionResult> RenewalRequests()
        {
            if (RequireAdminLogin()) return new EmptyResult();
            
            var renewalRequests = await _context.Notifications
                .Where(n => n.NotificationType == "Manager" && 
                           n.Message.Contains("requested subscription renewal") && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            
            ViewBag.RenewalRequests = renewalRequests;
            return View();
        }

        // Admin: mark notification as read
        [HttpPost]
        public async Task<IActionResult> MarkNotificationAsRead([FromBody] NotificationReadRequest request)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Not authorized" });
            
            try
            {
                var notification = await _context.Notifications.FindAsync(request.NotificationId);
                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                notification.IsRead = true;
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Notification marked as read" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read");
                return Json(new { success = false, message = "Error marking notification as read" });
            }
        }

        // Admin: approve renewal request and extend subscription
        [HttpPost]
        public async Task<IActionResult> ApproveRenewalRequest([FromBody] RenewalApprovalRequest request)
        {
            if (RequireAdminLogin()) return Json(new { success = false, message = "Not authorized" });
            
            try
            {
                var notification = await _context.Notifications.FindAsync(request.NotificationId);
                if (notification == null)
                {
                    return Json(new { success = false, message = "Notification not found" });
                }

                // Extract manager name from notification message
                var managerName = ExtractManagerNameFromMessage(notification.Message);
                _logger.LogInformation($"Extracted manager name: '{managerName}' from message: '{notification.Message}'");
                
                // Try to find manager by name first
                var manager = await _context.Managers.FirstOrDefaultAsync(m => m.name == managerName);
                _logger.LogInformation($"Manager found by name '{managerName}': {(manager != null ? $"ID={manager.ID}" : "null")}");
                
                // If not found by name, try to extract ID from "Manager {ID}" format
                if (manager == null && managerName.StartsWith("Manager "))
                {
                    var idPart = managerName.Replace("Manager ", "");
                    if (int.TryParse(idPart, out int managerId))
                    {
                        _logger.LogInformation($"Trying to find manager by ID: {managerId}");
                        manager = await _context.Managers.FirstOrDefaultAsync(m => m.ID == managerId);
                        _logger.LogInformation($"Manager found by ID {managerId}: {(manager != null ? $"Name={manager.name}" : "null")}");
                    }
                }
                
                // If still not found, try to extract ID from "manager {word}" format
                if (manager == null && managerName.StartsWith("manager "))
                {
                    var words = managerName.Split(' ');
                    if (words.Length >= 2)
                    {
                        var lastWord = words[words.Length - 1];
                        if (int.TryParse(lastWord, out int managerId))
                        {
                            _logger.LogInformation($"Trying to find manager by ID from 'manager {lastWord}': {managerId}");
                            manager = await _context.Managers.FirstOrDefaultAsync(m => m.ID == managerId);
                            _logger.LogInformation($"Manager found by ID {managerId}: {(manager != null ? $"Name={manager.name}" : "null")}");
                        }
                        else
                        {
                            // Try to convert word to number (e.g., "one" -> 1, "three" -> 3)
                            var numberMapping = new Dictionary<string, int>
                            {
                                {"one", 1}, {"two", 2}, {"three", 3}, {"four", 4}, {"five", 5},
                                {"six", 6}, {"seven", 7}, {"eight", 8}, {"nine", 9}, {"ten", 10}
                            };
                            
                            if (numberMapping.TryGetValue(lastWord.ToLower(), out int mappedId))
                            {
                                _logger.LogInformation($"Trying to find manager by mapped ID from 'manager {lastWord}': {mappedId}");
                                manager = await _context.Managers.FirstOrDefaultAsync(m => m.ID == mappedId);
                                _logger.LogInformation($"Manager found by mapped ID {mappedId}: {(manager != null ? $"Name={manager.name}" : "null")}");
                            }
                        }
                    }
                }
                
                if (manager == null)
                {
                    _logger.LogWarning($"Manager not found for name: '{managerName}'");
                    return Json(new { success = false, message = "Manager not found" });
                }

                var subscription = await _context.ManagerSubscriptions
                    .FirstOrDefaultAsync(s => s.ManagerId == manager.ID);

                if (subscription == null)
                {
                    return Json(new { success = false, message = "Subscription not found" });
                }

                // Extend subscription by 1 month
                subscription.SubscriptionEndDate = subscription.SubscriptionEndDate.AddMonths(1);
                subscription.Status = "Active";
                subscription.UpdatedAt = DateTime.UtcNow;

                // Mark notification as read and remove it from queue
                notification.IsRead = true;
                _context.Notifications.Remove(notification);

                await _context.SaveChangesAsync();

                // Create success notification for manager
                var successNotification = new Notification
                {
                    Message = $"Your subscription renewal request has been approved! Subscription extended until {subscription.SubscriptionEndDate:MMM dd, yyyy}.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = null,
                    NotificationType = "Manager"
                };
                _context.Notifications.Add(successNotification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Renewal request approved and subscription extended successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving renewal request");
                return Json(new { success = false, message = "Error approving renewal request: " + ex.Message });
            }
        }

        private string ExtractManagerNameFromMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return "";
            
            // Extract manager name from message like "Manager John requested subscription renewal"
            // or "Manager Manager 1 requested subscription renewal"
            // or "Manager manager three requested subscription renewal"
            if (message.StartsWith("Manager "))
            {
                var parts = message.Split(' ');
                if (parts.Length >= 3)
                {
                    // If the second word is also "Manager", then the name is "Manager {ID}"
                    if (parts[1] == "Manager" && parts.Length >= 3)
                    {
                        return $"{parts[1]} {parts[2]}"; // Return "Manager {ID}"
                    }
                    else if (parts[1] == "manager" && parts.Length >= 3)
                    {
                        // Handle case like "Manager manager three" - extract "manager three"
                        return $"{parts[1]} {parts[2]}"; // Return "manager three"
                    }
                    else
                    {
                        return parts[1]; // Return just the name
                    }
                }
                else if (parts.Length >= 2)
                {
                    return parts[1]; // Return the name part
                }
            }
            
            return "";
        }
    }
}

// Models for payment actions
public class PaymentApprovalRequest
{
    public int PaymentId { get; set; }
}

// Models for notification actions
public class NotificationReadRequest
{
    public int NotificationId { get; set; }
}

public class RenewalApprovalRequest
{
    public int NotificationId { get; set; }
}

// Models for subscription actions
public class ExtendSubscriptionRequest
{
    public int ManagerId { get; set; }
    public int Months { get; set; }
}
