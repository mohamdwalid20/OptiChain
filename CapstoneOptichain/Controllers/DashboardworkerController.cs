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

namespace CapstoneOptichain.Controllers.Dashboardworker
{
    public class DashboardworkerController : Controller
    {
        private readonly IConfiguration _config;
        private readonly ProjectContext _projectContext;
        private readonly ILogger<DashboardworkerController> _logger;

        public DashboardworkerController(IConfiguration config, ProjectContext projectContext, ILogger<DashboardworkerController> logger)
        {
            _config = config;
            _projectContext = projectContext;
            _logger = logger;
            _logger.LogInformation("DashboardworkerController initialized.");
        }

        private bool RequireLoginAndStore()
        {
            var action = this.RouteData.Values["action"].ToString().ToLower();
            var controller = this.RouteData.Values["controller"].ToString().ToLower();

            if (controller == "dashboardworker" && (action == "index"))
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
                HttpContext.Response.Redirect("/Orderworker/Index2");
                return true;
            }
            return false;
        }

  
        public async Task<IActionResult> Index()
        {
            _logger.LogInformation($"Store ID in session: {HttpContext.Session.GetString("SelectedStoreId")}"); // Temporary log statement
            {
                _logger.LogInformation("Index method started."); // Temporary log statement
                {
                    _logger.LogInformation("Index action called.");
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
                    var storeId = HttpContext.Session.GetString("SelectedStoreId");
                    _logger.LogInformation($"Store ID i+n Index: {storeId}"); // Log the storeId
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

   


        [HttpGet("api/v1/dashboardworker/orders")]
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




        [HttpGet("api/v1/dashboardworker/top-products")]
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








        [HttpGet("api/v1/dashboardworker/sales")]
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









        [HttpGet("api/v1/dashboardworker/summary")]
        public async Task<IActionResult> GetDashboardSummary([FromQuery] string storeId)
        {
            try
            {
                if (!int.TryParse(storeId, out int storeIdInt))
                    return BadRequest("Invalid Store ID");

                // Sales overview
                var salesData = await _projectContext.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.StoreId == storeIdInt &&
                                oi.Order.OrderType == "Sale" &&
                                oi.Order.Status == "AcceptedBySupplier")
                    .Select(oi => new { oi.Quantity, oi.OrderValue })
                    .ToListAsync();

                int totalSales = salesData.Count;
                decimal totalRevenue = salesData.Sum(x => x.OrderValue * x.Quantity);

                // Purchase overview
                var purchases = await _projectContext.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Order.StoreId == storeIdInt &&
                                oi.Order.OrderType == "Purchase" &&
                                oi.Order.Status == "AcceptedBySupplier")
                    .ToListAsync();

                int totalPurchases = purchases.Count;
                decimal totalPurchaseCost = purchases.Sum(p => p.OrderValue * p.Quantity);

                // Purchase overview
                var purchaseData = await _projectContext.Orders
                    .Where(o => o.StoreId == storeIdInt && o.OrderType == "Purchase" && o.Status == "AcceptedBySupplier")
                    .CountAsync();

                // Inventory summary
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
                        totalProfit = totalRevenue - totalPurchaseCost,
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




        [HttpGet("api/v1/dashboardworker/top-selling")]
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

        [HttpGet("api/v1/dashboardworker/low-stock")]
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

        [HttpGet]
        public IActionResult LogOut()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Index", "Dashboard");
        }
    }
}
