using CapstoneOptichain.Data;
using CapstoneOptichain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Net.Mail;
using System.Net;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CapstoneOptichain.Controllers.Dashboard
{
    public class DashboardController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ProjectContext _projectContext;
        private readonly ILogger<DashboardController> _logger;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public DashboardController(IConfiguration config, ProjectContext projectContext, ILogger<DashboardController> logger, IWebHostEnvironment webHostEnvironment)
        {
            _config = config;
            _projectContext = projectContext;
            _logger = logger;
            _webHostEnvironment = webHostEnvironment;
            _logger.LogInformation("DashboardController initialized.");
        }

        private bool RequireLoginAndStore()
        {
            var action = this.RouteData.Values["action"].ToString().ToLower();
            var controller = this.RouteData.Values["controller"].ToString().ToLower();

            if (controller == "dashboard" && (action == "index" || action == "login" || action == "index2" || action == "index4" || action == "index5" || action == "land"))
                return false;
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                HttpContext.Response.Redirect("/Dashboard/Index");
                return true;
            }
            var storeId = HttpContext.Session.GetString("SelectedStoreId");
            if (string.IsNullOrEmpty(storeId))
            {
                HttpContext.Response.Redirect("/Order/Index2");
                return true;
            }
            return false;
        }

        public ActionResult Index()
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            TempData["SelectedStore"] = HttpContext.Session.GetString("SelectedStore");
            ViewBag.SelectedStore = HttpContext.Session.GetString("SelectedStore");
            return View();
        }
        public ActionResult Index2() => View("Index2");
        public ActionResult Land() { if (RequireLoginAndStore()) return new EmptyResult(); TempData["SelectedStore"] = HttpContext.Session.GetString("SelectedStore"); ViewBag.SelectedStore = HttpContext.Session.GetString("SelectedStore"); return View("Land"); }
        public ActionResult Index4() { if (RequireLoginAndStore()) return new EmptyResult(); TempData["SelectedStore"] = HttpContext.Session.GetString("SelectedStore"); ViewBag.SelectedStore = HttpContext.Session.GetString("SelectedStore"); return View("Index4"); }

        public async Task<IActionResult> Index3()
        {
            _logger.LogInformation($"Store ID in session: {HttpContext.Session.GetString("SelectedStoreId")}"); // Temporary log statement
            {
                _logger.LogInformation("Index3 method started."); 
                {
                    _logger.LogInformation("Index3 action called.");
                    if (RequireLoginAndStore()) return new EmptyResult();
                    var userId = HttpContext.Session.GetInt32("UserId");
                    if (userId == null)
                    {
                        _logger.LogWarning("No user ID found. Redirecting to login.");
                        return RedirectToAction("Index", "Dashboard");
                    }
                    var userType = HttpContext.Session.GetString("UserType");
                    if (userType == "supplier")
                    {
                        _logger.LogInformation("User is a supplier. Redirecting to SupplierDashboard.");
                        return RedirectToAction("Index", "SupplierDashboard");
                    }
                    else if (userType == "worker")
                    {
                        _logger.LogInformation("User is a worker. Redirecting to workerDashboard.");
                        return RedirectToAction("Index", "Dashboardworker");
                    }
                    else if (userType == "manager")
                    {
                        // Check manager's subscription status
                        var subscription = await _projectContext.ManagerSubscriptions
                            .Where(s => s.ManagerId == userId.Value)
                            .OrderByDescending(s => s.CreatedAt)
                            .FirstOrDefaultAsync();
                        
                        // If no subscription or subscription is cancelled/expired, redirect to subscription page
                        if (subscription == null || 
                            subscription.Status == "Cancelled" || 
                            subscription.Status == "Expired" ||
                            subscription.SubscriptionEndDate <= DateTime.UtcNow)
                        {
                            return RedirectToAction("Subscription", "Manager");
                        }
                    }
                    var storeId = HttpContext.Session.GetString("SelectedStoreId");
                    _logger.LogInformation($"Store ID i+n Index3: {storeId}"); 
                    if (string.IsNullOrEmpty(storeId))
                    {
                        _logger.LogWarning("No store selected. Redirecting to store selection page.");
                        return RedirectToAction("Index2", "Order");
                    }
                    TempData["SelectedStore"] = HttpContext.Session.GetString("SelectedStore");
                    ViewBag.SelectedStore = HttpContext.Session.GetString("SelectedStore");

                    // Fetch sales data
                    var salesData = await GetSalesData(storeId);
                    ViewBag.SalesData = salesData;

                    return View();
                }
            }
        }
        public ActionResult Index5(string email = null)
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            var sessionEmail = HttpContext.Session.GetString("GoogleEmail");
            if (!string.IsNullOrEmpty(email))
            {
                HttpContext.Session.SetString("GoogleEmail", email);
                sessionEmail = email;
            }
            if (string.IsNullOrEmpty(sessionEmail))
            {
                return RedirectToAction("Index");
            }
            ViewBag.GoogleEmail = sessionEmail;
            return View();
        }

        [HttpPost]
        public IActionResult Login(LoginViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check for admin login first
                if (model.Email == "admin@optichain.com" && model.Password == "Admin@123")
                {
                    HttpContext.Session.SetInt32("UserId", 1);
                    HttpContext.Session.SetString("UserType", "admin");
                    HttpContext.Session.SetString("UserName", "System Admin");
                    return RedirectToAction("Index", "Admin");
                }

                // Hash the input password for comparison
                var hashedPassword = HashPassword(model.Password);
                
                var manager = _projectContext.Managers.FirstOrDefault(m => m.email == model.Email && m.password == hashedPassword);
                if (manager != null)
                {
                    HttpContext.Session.SetInt32("UserId", manager.ID);
                    HttpContext.Session.SetString("UserType", "manager");
                    
                    // Check manager's subscription status
                    var subscription = _projectContext.ManagerSubscriptions
                        .Where(s => s.ManagerId == manager.ID)
                        .OrderByDescending(s => s.CreatedAt)
                        .FirstOrDefault();
                    
                    // If no subscription or subscription is cancelled/expired, redirect to subscription page
                    if (subscription == null || 
                        subscription.Status == "Cancelled" || 
                        subscription.Status == "Expired" ||
                        subscription.SubscriptionEndDate <= DateTime.UtcNow)
                    {
                        return RedirectToAction("Subscription", "Manager");
                    }
                    
                    return RedirectToAction("Index2", "Order");
                }

                var worker = _projectContext.Workers.FirstOrDefault(w => w.email == model.Email && w.password == hashedPassword);
                if (worker != null)
                {
                    HttpContext.Session.SetInt32("UserId", worker.ID);
                    HttpContext.Session.SetString("UserType", worker.role ?? "worker");
                    
                    // Set the worker's store ID in session
                    if (worker.StoreId.HasValue)
                    {
                        HttpContext.Session.SetString("SelectedStoreId", worker.StoreId.Value.ToString());
                        
                        // Get store name for display
                        var store = _projectContext.Stores.FirstOrDefault(s => s.StoreId == worker.StoreId.Value);
                        if (store != null)
                        {
                            HttpContext.Session.SetString("SelectedStore", store.StoreName);
                        }
                    }
                    
                    return RedirectToAction("Index", "Orderworker");
                }

                var supplier = _projectContext.Suppliers.FirstOrDefault(s => s.email == model.Email && s.password == hashedPassword);
                if (supplier != null)
                {
                    HttpContext.Session.SetInt32("UserId", supplier.SupplierId);
                    HttpContext.Session.SetString("UserType", supplier.Role ?? "supplier");

                    if (supplier.StoreId == null || supplier.StoreId == 0)
                    {
                        return RedirectToAction("Stores", "SupplierDashboard");
                    }

                    return RedirectToAction("Index", "SupplierDashboard");
                }

                ViewBag.Error = "Invalid email or password.";
            }
            else
            {
                // Add validation errors to ViewBag
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                ViewBag.Error = string.Join(", ", errors);
            }

            return View("Index");
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(SignUpViewModel model)
        {
            _logger.LogInformation("Create Action Called");
            try
            {
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                    ViewBag.Error = string.Join(", ", errors);
                    return View("Index2");
                }

                // Check if passwords match
                if (model.Password != model.ConfirmPassword)
                {
                    ViewBag.Error = "Password and Confirm Password do not match.";
                    return View("Index2");
                }

                string name = model.Name?.Trim();
                string email = model.Email?.Trim().ToLower();
                string password = model.Password;
                string role = model.UserType?.ToLower();

                // Only allow supplier registration
                if (role != "supplier")
                {
                    ViewBag.Error = "Only supplier registration is allowed. Managers and workers are added by admin.";
                    return View("Index2");
                }

                bool exists = await _projectContext.Workers.AnyAsync(w => w.email == email) ||
                             await _projectContext.Managers.AnyAsync(m => m.email == email) ||
                             await _projectContext.Suppliers.AnyAsync(s => s.email == email);

                if (exists)
                {
                    ViewBag.Error = "This email address is already registered.";
                    return View("Index2");
                }

                // Save profile image if provided
                string profileImageUrl = string.Empty;
                if (model.ProfileImage != null && model.ProfileImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "profile-images");
                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfileImage.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImage.CopyToAsync(fileStream);
                    }

                    profileImageUrl = "/profile-images/" + uniqueFileName;
                }

                if (role == "worker")
                {
                    string phoneNumber = model.Phone?.Trim() ?? "";
                    string address = model.Address?.Trim() ?? "";
                    string department = model.Department?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(name) ||
                        string.IsNullOrWhiteSpace(email) ||
                        string.IsNullOrWhiteSpace(password) ||
                        string.IsNullOrWhiteSpace(phoneNumber))
                    {
                        ViewBag.Error = "Please fill all required fields for Worker registration.";
                        return View("Index2");
                    }

                    var worker = new Worker
                    {
                        name = name,
                        email = email,
                        password = HashPassword(password),
                        Phone_number = phoneNumber,
                        Address = address,
                        Department = department,
                        role = role,
                        ProfileImageUrl = profileImageUrl
                    };

                    _projectContext.Workers.Add(worker);
                    await _projectContext.SaveChangesAsync();

                    HttpContext.Session.SetInt32("WorkerId", worker.ID);
                    HttpContext.Session.SetString("Role", worker.role);
                    return RedirectToAction("Index3");
                }
                else if (role == "manager")
                {
                    string phone = model.Phone?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(name) ||
                        string.IsNullOrWhiteSpace(email) ||
                        string.IsNullOrWhiteSpace(password) ||
                        string.IsNullOrWhiteSpace(phone))
                    {
                        ViewBag.Error = "Please fill all required fields for Manager registration.";
                        return View("Index2");
                    }

                    var manager = new Manager
                    {
                        name = name,
                        email = email,
                        password = HashPassword(password),
                        phone = phone,
                        ProfileImageUrl = profileImageUrl
                    };

                    _projectContext.Managers.Add(manager);
                    await _projectContext.SaveChangesAsync();

                    HttpContext.Session.SetInt32("ManagerId", manager.ID);
                    HttpContext.Session.SetString("Role", "Manager");
                    return RedirectToAction("Index3");
                }
                else if (role == "supplier")
                {
                    string supplierName = model.Name?.Trim() ?? "";
                    string supplierEmail = model.Email?.Trim().ToLower() ?? "";
                    string supplierPassword = model.Password ?? "";
                    string supplierContactNumber = model.Phone?.Trim() ?? "";
                    string supplierAddress = model.Address?.Trim() ?? "";

                    if (string.IsNullOrWhiteSpace(supplierName) ||
                        string.IsNullOrWhiteSpace(supplierEmail) ||
                        string.IsNullOrWhiteSpace(supplierPassword))
                    {
                        ViewBag.Error = "Please fill all required fields for Supplier registration.";
                        return View("Index2");
                    }

                    var supplier = new Supplier
                    {
                        name = supplierName,
                        email = supplierEmail,
                        password = HashPassword(supplierPassword),
                        Role = "supplier",
                        ContactNumber = supplierContactNumber,
                        Address = supplierAddress,
                        StoreId = null,
                        ProfileImageUrl = profileImageUrl
                    };

                    _projectContext.Suppliers.Add(supplier);
                    await _projectContext.SaveChangesAsync();

                    HttpContext.Session.SetInt32("UserId", supplier.SupplierId);
                    HttpContext.Session.SetString("UserType", "supplier");

                    return RedirectToAction("Stores", "SupplierDashboard");
                }

                ViewBag.Error = "Please select a valid role.";
                return View("Index2");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Create action");
                ViewBag.Error = "An error occurred during registration. Please try again.";
                return View("Index2");
            }
        }



        [HttpGet("api/v1/dashboard/orders")]
        public async Task<IActionResult> GetOrderData([FromQuery] string storeId)
        {
            try
            {
                _logger.LogInformation($"GetOrderData called for store: {storeId}");

                if (!int.TryParse(storeId, out int storeIdInt))
                    return BadRequest("Invalid Store ID");

                var storeExists = await _projectContext.Stores.AnyAsync(s => s.StoreId == storeIdInt);
                if (!storeExists)
                    return NotFound("Store not found");

                var ordersData = await _projectContext.Orders
                    .Where(o => o.StoreId == storeIdInt)
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month })
                    .Select(g => new {
                        Year = g.Key.Year,
                        Month = g.Key.Month,
                        Ordered = g.Count(),
                        Delivered = g.Count(o => o.Status == "AcceptedBySupplier")
                    })
                    .OrderBy(x => x.Year)
                    .ThenBy(x => x.Month)
                    .ToListAsync();


                var allMonthsData = ordersData.Select(o => new DateTime(o.Year, o.Month, 1)).ToList();
                DateTime lastMonth = allMonthsData.Any() ? allMonthsData.Max() : DateTime.UtcNow;
                DateTime sixMonthsAgo = lastMonth.AddMonths(-5);
                List<DateTime> lastSixMonths = Enumerable.Range(0, 6)
                    .Select(i => sixMonthsAgo.AddMonths(i))
                    .Select(d => new DateTime(d.Year, d.Month, 1))
                    .ToList();

                var labels = lastSixMonths.Select(x => x.ToString("MMM yyyy")).ToArray();
                var orderedData = lastSixMonths.Select(m => {
                    var o = ordersData.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                    return (double)(o?.Ordered ?? 0);
                }).ToArray();
                var deliveredData = lastSixMonths.Select(m => {
                    var o = ordersData.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
                    return (double)(o?.Delivered ?? 0);
                }).ToArray();

                return Ok(new { labels, orderedData, deliveredData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrderData");
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }




        [HttpGet("api/v1/dashboard/top-products")]
        public async Task<IActionResult> GetTopProducts()
        {
            try
            {
                _logger.LogInformation("GetTopProducts called.");

                var topProducts = await _projectContext.OrderItems
                    .Include(oi => oi.Product)
                    .GroupBy(oi => oi.Product.ProductName)
                    .Select(g => new {
                        productName = g.Key,
                        quantitySold = g.Sum(oi => oi.Quantity),
                        totalValue = g.Sum(oi => oi.OrderValue)
                    })
                    .OrderByDescending(x => x.quantitySold)
                    .Take(5)
                    .ToListAsync();

                if (topProducts == null || topProducts.Count == 0)
                {
                    _logger.LogWarning("No top products found.");
                    return Ok(new List<object>());
                }

                return Ok(topProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopProducts");
                return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
            }
        }



        [HttpGet("api/v1/dashboard/top-selling")]
        public async Task<IActionResult> GetTopSellingProducts([FromQuery] string storeId)
        {
            try
            {
                if (!int.TryParse(storeId, out int storeIdInt))
                    return BadRequest("Invalid Store ID");

                var topProducts = await _projectContext.OrderItems
                    .Include(oi => oi.Order)
                    .Include(oi => oi.Product)
                    .Where(oi => oi.Order.StoreId == storeIdInt &&
                                 oi.Order.OrderType == "Sale" &&
                                 oi.Order.Status == "AcceptedBySupplier")
                    .GroupBy(oi => oi.Product.ProductName)
                    .Select(g => new {
                        productName = g.Key,
                        totalSold = g.Sum(oi => oi.Quantity)
                    })
                    .OrderByDescending(x => x.totalSold)
                    .Take(5)
                    .ToListAsync();

                return Ok(topProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetTopSellingProducts");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpGet("api/v1/dashboard/low-stock")]
        public async Task<IActionResult> GetLowStockProducts([FromQuery] string storeId)
        {
            try
            {
                if (!int.TryParse(storeId, out int storeIdInt))
                    return BadRequest("Invalid Store ID");

                var lowStockProducts = await _projectContext.Products
                    .Where(p => p.StoreId == storeIdInt && p.Quantity < 100)
                    .OrderBy(p => p.Quantity)
                    .Take(5)
                    .Select(p => new {
                        productId = p.ProductId,
                        productName = p.ProductName,
                        currentStock = p.Quantity
                    })
                    .ToListAsync();

                return Ok(lowStockProducts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLowStockProducts");
                return StatusCode(500, "Internal server error");
            }
        }



        [HttpGet("api/v1/dashboard/sales")]
        public async Task<IActionResult> GetSalesData([FromQuery] string storeId)
        {
            try
            {
                return Ok(await FetchSalesData(storeId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSalesData error");
                return StatusCode(500, "Internal server error");
            }
        }

        private async Task<object> FetchSalesData(string storeId)
        {

            if (!int.TryParse(storeId, out int storeIdInt))
                throw new ArgumentException("Invalid Store ID");


            var allMonthsData = await _projectContext.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.StoreId == storeIdInt &&
                            oi.Order.OrderDate >= DateTime.UtcNow.AddMonths(-6) && 
                            oi.Order.OrderType.ToLower() == "sale" &&
                            oi.Order.Status == "AcceptedBySupplier")
                .GroupBy(oi => new { oi.Order.OrderDate.Year, oi.Order.OrderDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalRevenue = g.Sum(oi => oi.OrderValue * oi.Quantity) 
                })
                .ToListAsync();
            var salesData = allMonthsData;

            var purchaseData = await _projectContext.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.Order.StoreId == storeIdInt &&
                            oi.Order.OrderDate >= DateTime.UtcNow.AddMonths(-6) && 
                            oi.Order.OrderType.ToLower() == "purchase" &&
                            oi.Order.Status == "AcceptedBySupplier")
                .GroupBy(oi => new { oi.Order.OrderDate.Year, oi.Order.OrderDate.Month })
                .Select(g => new {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    TotalPurchase = g.Sum(oi => oi.OrderValue * oi.Quantity) 
                })
                .ToListAsync();


            var allMonthsDataPurchase = purchaseData.Select(p => new DateTime(p.Year, p.Month, 1))
                .Concat(salesData.Select(s => new DateTime(s.Year, s.Month, 1)))
                .ToList();
            DateTime lastMonth = allMonthsDataPurchase.Any() ? allMonthsDataPurchase.Max() : DateTime.UtcNow;
            DateTime sixMonthsAgo = lastMonth.AddMonths(-5); 
            List<DateTime> lastSixMonths = Enumerable.Range(0, 6)
                .Select(i => sixMonthsAgo.AddMonths(i))
                .Select(d => new DateTime(d.Year, d.Month, 1))
                .ToList();

            string[] labels = lastSixMonths.Select(m => m.ToString("MMM yyyy")).ToArray();
            decimal[] salesValues = new decimal[6];
            decimal[] purchaseValues = new decimal[6];

            for (int i = 0; i < 6; i++)
            {
                DateTime month = lastSixMonths[i];
                var sale = salesData.FirstOrDefault(s => s.Year == month.Year && s.Month == month.Month);
                var purchase = purchaseData.FirstOrDefault(p => p.Year == month.Year && p.Month == month.Month);

                salesValues[i] = sale?.TotalRevenue ?? 0;
                purchaseValues[i] = purchase?.TotalPurchase ?? 0;
            }
            _logger.LogInformation($"[FetchSalesData] salesValues: {string.Join(", ", salesValues)}");
            _logger.LogInformation($"[FetchSalesData] purchaseValues: {string.Join(", ", purchaseValues)}");

            double totalRevenue = (double)salesValues.Sum();
            double totalPurchase = (double)purchaseValues.Sum();

            return new
            {
                labels,
                salesData = salesValues,
                purchaseData = purchaseValues,
                summary = new
                {
                    totalSales = salesData.Sum(s => s.TotalRevenue),
                    totalRevenue,
                    totalProfit = totalRevenue - totalPurchase,
                    totalCost = totalPurchase
                }
            };
        }





        [HttpGet("api/v1/dashboard/summary")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] string storeId)
        {
            try
            {

              
                if (!int.TryParse(storeId, out int storeIdInt))
                    return BadRequest("Invalid Store ID");


                var salesData = await _projectContext.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.StoreId == storeIdInt &&
                                oi.Order.OrderType == "Sale" &&
                                oi.Order.Status == "AcceptedBySupplier") 
                    .Select(oi => new { oi.Quantity, oi.OrderValue })
                    .ToListAsync();

                int totalSales = salesData.Count;
                decimal totalRevenue = salesData.Sum(x => x.OrderValue * x.Quantity) ;


                var purchases = await _projectContext.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.StoreId == storeIdInt &&
                                oi.Order.OrderType == "Purchase" &&
                                oi.Order.Status == "AcceptedBySupplier") 
                    .ToListAsync();

                int totalPurchases = purchases.Count;
                decimal totalPurchaseCost = purchases.Sum(p => p.OrderValue * p.Quantity) ;


                var purchaseData = await _projectContext.Orders
                    .Where(o => o.StoreId == storeIdInt && o.OrderType == "Purchase" && o.Status == "AcceptedBySupplier")
                    .CountAsync();


                var inventorySummary = new
                {
                    totalInventory = await _projectContext.Products
                        .Where(p => p.StoreId == storeIdInt)
                        .SumAsync(p => p.Quantity),
                    toBeReceived = await _projectContext.Inventories
                        .Where(i => i.StoreId == storeIdInt)
                        .SumAsync(i => i.OnTheWay),
                    lowStockItems = await _projectContext.Products
                        .Where(p => p.StoreId == storeIdInt && p.Quantity <= 10)
                        .CountAsync()
                };

                int rejectedBySupplierCount = await _projectContext.Orders
          .CountAsync(o => o.StoreId == storeIdInt &&
                           o.OrderType == "Purchase" &&
                           o.Status == "RejectedBySupplier");

                return Ok(new
                {
                    salesOverview = new
                    {
                        totalSales,
                        totalRevenue,
                        totalProfit = (totalRevenue - totalPurchaseCost) , 
                        totalCost = totalPurchaseCost
                    },
                    purchaseOverview = new
                    {
                        totalPurchases,
                        totalPurchaseCost,
                        rejectedBySupplierCount
                    },
                    inventorySummary,
                    productSummary = new
                    {
                        supplierCount = await _projectContext.Suppliers
                            .CountAsync(s => s.StoreId == storeIdInt),
                        categoryCount = await _projectContext.Categories.CountAsync()
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetDashboardSummary error");
                return StatusCode(500, "Internal server error");
            }
        }








        [HttpPost]
        public async Task<IActionResult> SendVerificationCode([FromForm] string email)
        {
            _logger.LogInformation($"Request received for email: {email}");

            if (string.IsNullOrEmpty(email))
            {
                return Json(new { success = false, message = "Email is required" });
            }

            try
            {
                var code = new Random().Next(100000, 999999).ToString();
                HttpContext.Session.SetString("ResetEmail", email);
                HttpContext.Session.SetString("ResetCode", code);

                var sender = new EmailSender(_config);
                var success = await sender.SendCodeAsync(email, code);

                return Json(new
                {
                    success,
                    message = success ? "Code sent" : "Failed to send code"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical Error in SendVerificationCode");
                return Json(new { success = false, message = ex.Message });
            }
        }




        [HttpPost]
        public IActionResult VerifyCode(string code)
        {
            try
            {
                var sessionCode = HttpContext.Session.GetString("ResetCode");
                var sessionEmail = HttpContext.Session.GetString("ResetEmail");

                _logger.LogInformation($"The entered code: {code}");
                _logger.LogInformation($"Stored code: {sessionCode}");
                _logger.LogInformation($"Associated Email: {sessionEmail}");

                if (string.IsNullOrEmpty(sessionCode))
                {
                    return Json(new { success = false, message = "The code has expired, please request a new code." });
                }

                if (sessionCode.Trim().Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    HttpContext.Session.SetString("CodeVerified", "true");
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "Invalid verification code" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Validation error in VerifyCode");
                return Json(new { success = false, message = "An error occurred during verification." });
            }
        }

        [HttpPost]
        public IActionResult ResetPassword(string newPassword)
        {
            var email = HttpContext.Session.GetString("ResetEmail");
            var codeVerified = HttpContext.Session.GetString("CodeVerified");

            if (codeVerified == "true" && !string.IsNullOrEmpty(email))
            {
                var manager = _projectContext.Managers.FirstOrDefault(m => m.email == email);
                if (manager != null)
                {
                    manager.password = HashPassword(newPassword);
                    _projectContext.SaveChanges();
                    return Json(new { success = true });
                }

                var worker = _projectContext.Workers.FirstOrDefault(w => w.email == email);
                if (worker != null)
                {
                    worker.password = HashPassword(newPassword);
                    _projectContext.SaveChanges();
                    return Json(new { success = true });
                }

                return Json(new { success = false, message = "User not found." });
            }

            return Json(new { success = false, message = "Code not verified." });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            if (!int.TryParse(storeIdStr, out int storeId))
                return Content("<div>Please Choose Store</div>");
            var notifications = await _projectContext.Notifications
                .Where(n => n.StoreId == storeId && n.NotificationType == "Manager" && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return PartialView("_NotificationListPartial", notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationsRead()
        {
            var unread = await _projectContext.Notifications
                .Where(n => !n.IsRead)
                .ToListAsync();
            foreach (var n in unread)
                n.IsRead = true;
            await _projectContext.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpGet]
        public async Task<IActionResult> OrderRequests()
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            if (!int.TryParse(storeIdStr, out int storeId))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction("Index2", "Order");
            }

            var orderRequests = await _projectContext.OrderRequests
                .Include(or => or.Worker)
                .Include(or => or.Order)
                .ThenInclude(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(or => or.Order.StoreId == storeId && 
                             (or.Status == "Pending" || or.Status == "Approved"))
                .OrderByDescending(or => or.RequestDate)
                .ToListAsync();

            return View(orderRequests);
        }

        [HttpPost]
        public async Task<IActionResult> ApproveOrderRequest(int requestId, string notes = "")
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                var orderRequest = await _projectContext.OrderRequests
                    .Include(or => or.Order)
                    .FirstOrDefaultAsync(or => or.RequestId == requestId);

                if (orderRequest == null)
                {
                    return Json(new { success = false, message = "Order request not found" });
                }

                orderRequest.Status = "Approved";
                orderRequest.ManagerId = managerId;
                orderRequest.ManagerNotes = notes;
                orderRequest.ResponseDate = DateTime.UtcNow;

                // Change order status to "PendingPrice" to send to supplier
                orderRequest.Order.Status = "PendingPrice";
                
                await _projectContext.SaveChangesAsync();

                // Create notification for worker
                var notification = new Notification
                {
                    Message = $"Your order request for order {orderRequest.OrderId} has been approved by manager",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = orderRequest.Order.StoreId,
                    NotificationType = "Worker"
                };

                _projectContext.Notifications.Add(notification);

                // Notify supplier about new order
                var supplierNotification = new Notification
                {
                    Message = $"New {orderRequest.Order.OrderType} order (ID: {orderRequest.OrderId}) has been approved and is ready for pricing.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = orderRequest.Order.StoreId,
                    NotificationType = "Supplier"
                };

                _projectContext.Notifications.Add(supplierNotification);
                await _projectContext.SaveChangesAsync();

                return Json(new { success = true, message = "Order request approved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error approving request: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectOrderRequest(int requestId, string notes = "")
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var managerId = HttpContext.Session.GetInt32("UserId");
                var orderRequest = await _projectContext.OrderRequests
                    .Include(or => or.Order)
                    .FirstOrDefaultAsync(or => or.RequestId == requestId);

                if (orderRequest == null)
                {
                    return Json(new { success = false, message = "Order request not found" });
                }

                orderRequest.Status = "Rejected";
                orderRequest.ManagerId = managerId;
                orderRequest.ManagerNotes = notes;
                orderRequest.ResponseDate = DateTime.UtcNow;

                // Change order status to "RejectedByManager" to prevent re-sending
                orderRequest.Order.Status = "RejectedByManager";

                await _projectContext.SaveChangesAsync();

                // Create notification for worker
                var notification = new Notification
                {
                    Message = $"Your order request for order {orderRequest.OrderId} has been rejected by manager",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = orderRequest.Order.StoreId,
                    NotificationType = "Worker"
                };

                _projectContext.Notifications.Add(notification);
                await _projectContext.SaveChangesAsync();

                return Json(new { success = true, message = "Order request rejected successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error rejecting request: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderDetails(int orderId)
        {
            _logger.LogInformation($"GetOrderDetails called with orderId: {orderId}");
            
            if (RequireLoginAndStore()) 
            {
                _logger.LogWarning("GetOrderDetails: RequireLoginAndStore returned true");
                return new EmptyResult();
            }

            try
            {
                _logger.LogInformation($"Searching for order with ID: {orderId}");
                
                var order = await _projectContext.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Category)
                    .Include(o => o.Store)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    _logger.LogWarning($"Order with ID {orderId} not found");
                    return Json(new { success = false, message = "Order not found" });
                }

                _logger.LogInformation($"Order found: {order.OrderId}, Items count: {order.OrderItems?.Count ?? 0}");

                var orderDetails = new
                {
                    orderId = order.OrderId,
                    orderDate = order.OrderDate.ToString("dd/MM/yyyy HH:mm"),
                    orderType = order.OrderType,
                    status = order.Status,
                    storeName = order.Store?.StoreName,
                    items = order.OrderItems.Select(oi => new
                    {
                        productName = oi.Product?.ProductName ?? "Unknown Product",
                        categoryName = oi.Product?.Category?.CategoryName ?? "No Category",
                        quantity = oi.Quantity,
                        unitPrice = oi.Quantity > 0 ? oi.OrderValue / oi.Quantity : 0,
                        totalValue = oi.OrderValue,
                        imagePath = oi.Product?.ImagePath ?? ""
                    }).ToList(),
                    totalOrderValue = order.OrderItems.Sum(oi => oi.OrderValue)
                };

                _logger.LogInformation($"Returning order details with {orderDetails.items.Count} items");
                return Json(new { success = true, data = orderDetails });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in GetOrderDetails: {ex.Message}");
                return Json(new { success = false, message = "Error loading order details: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult LogOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Dashboard");
        }

        [HttpPost]
        public async Task<IActionResult> ApprovePrice(int orderId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var order = await _projectContext.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                if (order.Status != "PriceProposed")
                {
                    return Json(new { success = false, message = "Order is not waiting for price approval." });
                }

                // Update order items with proposed prices as final prices
                foreach (var item in order.OrderItems)
                {
                    if (item.ProposedPrice.HasValue)
                    {
                        item.OrderValue = item.ProposedPrice.Value * item.Quantity;
                    }
                }

                order.Status = "AcceptedBySupplier";
                await _projectContext.SaveChangesAsync();

                // Notify supplier about price approval
                var notification = new Notification
                {
                    Message = $"Your proposed prices for order {order.OrderId} have been approved by manager.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Supplier"
                };
                _projectContext.Notifications.Add(notification);
                await _projectContext.SaveChangesAsync();

                return Json(new { success = true, message = "Prices approved successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error approving prices: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectPrice(int orderId, string reason)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var order = await _projectContext.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                if (order.Status != "PriceProposed")
                {
                    return Json(new { success = false, message = "Order is not waiting for price approval." });
                }

                // Change status to "RejectedByManager" to prevent re-sending to supplier
                order.Status = "RejectedByManager";
                await _projectContext.SaveChangesAsync();

                // Notify supplier about price rejection
                var notification = new Notification
                {
                    Message = $"Your proposed prices for order {order.OrderId} have been rejected. Reason: {reason}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Supplier"
                };
                _projectContext.Notifications.Add(notification);
                await _projectContext.SaveChangesAsync();

                return Json(new { success = true, message = "Prices rejected successfully. Order will not be sent to supplier again." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error rejecting prices: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPriceApprovalRequests(int storeId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var orders = await _projectContext.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Where(o => o.StoreId == storeId && o.Status == "PriceProposed")
                    .ToListAsync();

                var orderData = orders.Select(o => new
                {
                    orderId = o.OrderId,
                    orderType = o.OrderType,
                    orderDate = o.OrderDate.ToString("dd/MM/yyyy"),
                    items = o.OrderItems.Select(oi => new
                    {
                        productName = oi.Product?.ProductName ?? "Unknown Product",
                        quantity = oi.Quantity,
                        proposedPrice = oi.ProposedPrice ?? 0
                    }).ToList(),
                    proposedTotal = o.OrderItems.Sum(oi => (oi.ProposedPrice ?? 0) * oi.Quantity)
                }).ToList();

                return Json(new { success = true, data = orderData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading price approval requests: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetOrderApprovalRequests(int storeId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var orderRequests = await _projectContext.OrderRequests
                    .Include(or => or.Order)
                    .ThenInclude(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .Where(or => or.Order.StoreId == storeId && or.Status == "Pending")
                    .ToListAsync();

                var requestData = orderRequests.Select(or => new
                {
                    requestId = or.RequestId,
                    orderId = or.Order.OrderId,
                    orderType = or.Order.OrderType,
                    orderDate = or.Order.OrderDate.ToString("dd/MM/yyyy"),
                    workerId = or.WorkerId,
                    items = or.Order.OrderItems.Select(oi => new
                    {
                        productName = oi.Product?.ProductName ?? "Unknown Product",
                        quantity = oi.Quantity
                    }).ToList()
                }).ToList();

                return Json(new { success = true, data = requestData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading order approval requests: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ApproveOrder(int orderId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var order = await _projectContext.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                if (order.Status != "Pending")
                {
                    return Json(new { success = false, message = "Order is not waiting for approval." });
                }

                // Change status to "PendingPrice" to send to supplier
                order.Status = "PendingPrice";
                await _projectContext.SaveChangesAsync();

                // Update OrderRequest status to "Approved"
                var orderRequest = await _projectContext.OrderRequests
                    .FirstOrDefaultAsync(or => or.OrderId == orderId && or.Status == "Pending");
                
                if (orderRequest != null)
                {
                    orderRequest.Status = "Approved";
                    await _projectContext.SaveChangesAsync();
                }

                // Notify supplier about new order
                var notification = new Notification
                {
                    Message = $"New {order.OrderType} order (ID: {order.OrderId}) has been approved and is ready for pricing.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Supplier"
                };
                _projectContext.Notifications.Add(notification);
                await _projectContext.SaveChangesAsync();

                return Json(new { success = true, message = "Order approved successfully and sent to supplier for pricing." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error approving order: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> RejectOrder(int orderId, string reason)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var order = await _projectContext.Orders
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                if (order.Status != "Pending")
                {
                    return Json(new { success = false, message = "Order is not waiting for approval." });
                }

                // Change status to "RejectedByManager" to prevent re-sending
                order.Status = "RejectedByManager";
                await _projectContext.SaveChangesAsync();

                // Update OrderRequest status to "Rejected"
                var orderRequest = await _projectContext.OrderRequests
                    .FirstOrDefaultAsync(or => or.OrderId == orderId && or.Status == "Pending");
                
                if (orderRequest != null)
                {
                    orderRequest.Status = "Rejected";
                    await _projectContext.SaveChangesAsync();
                }

                // Notify worker about order rejection
                var notification = new Notification
                {
                    Message = $"Your order (ID: {order.OrderId}) has been rejected by manager. Reason: {reason}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Worker"
                };
                _projectContext.Notifications.Add(notification);
                await _projectContext.SaveChangesAsync();

                return Json(new { success = true, message = "Order rejected successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error rejecting order: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserProfileData()
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                var userType = HttpContext.Session.GetString("UserType");

                if (userId == null)
                {
                    return Json(new { success = false, message = "User not logged in" });
                }

                string userName = "My Profile";
                string profileImageUrl = "/images/default-avatar.png";

                if (userType == "admin")
                {
                    userName = "System Admin";
                    profileImageUrl = "/images/default-avatar.png";
                }
                else if (userType == "manager")
                {
                    var manager = await _projectContext.Managers.FirstOrDefaultAsync(m => m.ID == userId);
                    if (manager != null)
                    {
                        userName = manager.name ?? "Manager";
                        profileImageUrl = !string.IsNullOrEmpty(manager.ProfileImageUrl) ? manager.ProfileImageUrl : "/images/default-avatar.png";
                    }
                }
                else if (userType == "worker")
                {
                    var worker = await _projectContext.Workers.FirstOrDefaultAsync(w => w.ID == userId);
                    if (worker != null)
                    {
                        userName = worker.name ?? "Worker";
                        profileImageUrl = !string.IsNullOrEmpty(worker.ProfileImageUrl) ? worker.ProfileImageUrl : "/images/default-avatar.png";
                    }
                }
                else if (userType == "supplier")
                {
                    var supplier = await _projectContext.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == userId);
                    if (supplier != null)
                    {
                        userName = supplier.name ?? "Supplier";
                        profileImageUrl = !string.IsNullOrEmpty(supplier.ProfileImageUrl) ? supplier.ProfileImageUrl : "/images/default-avatar.png";
                    }
                }

                return Json(new { 
                    success = true, 
                    userName = userName, 
                    profileImageUrl = profileImageUrl,
                    userType = userType
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error getting user profile data: " + ex.Message });
            }
        }
    }
}
