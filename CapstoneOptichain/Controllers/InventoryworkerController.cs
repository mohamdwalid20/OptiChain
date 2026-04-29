using CapstoneOptichain.Data;
using CapstoneOptichain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace CapstoneOptichain.Controllers
{
    public class InventoryworkerController : Controller
    {
        private readonly ProjectContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<InventoryworkerController> _logger;


        public InventoryworkerController(ProjectContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
        }

        private bool RequireLoginAndStore()
        {
            var action = this.RouteData.Values["action"].ToString().ToLower();
            var controller = this.RouteData.Values["controller"].ToString().ToLower();
            if (controller == "dashboard" && (action == "index"))
                return false;
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                HttpContext.Response.Redirect("/Dashboardworker/Index");
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

        public async Task<IActionResult> Index2(string searchString = "", int page = 1)
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            const int pageSize = 10;

            var selectedStoreIdStr = HttpContext.Session.GetString("SelectedStoreId");

            if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction("Index", "Dashboardworker");
            }

            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            int storeId = 0;
            int.TryParse(storeIdStr, out storeId);
            var notificationCount = await _context.Notifications
                .Where(n => n.StoreId == storeId && n.NotificationType == "Supplier" && !n.IsRead)
                .CountAsync();
            ViewBag.NotificationCount = notificationCount;


            var productsQuery = _context.Products
                .Include(p => p.Category)
                .Where(p => p.StoreId == selectedStoreId)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                productsQuery = productsQuery.Where(p =>
                    p.ProductName.Contains(searchString) ||
                    p.Category.CategoryName.Contains(searchString));
            }

            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            var revenue = await _context.OrderItems
    .Where(oi => oi.Order.Status == "AcceptedBySupplier" &&
          oi.OrderDate >= sevenDaysAgo &&
          oi.Order.StoreId == selectedStoreId)
    .SumAsync(oi => oi.OrderValue * oi.Quantity);

            var cost = await productsQuery
                .SumAsync(p => p.BuyingPrice * p.Quantity);

            var topCategories = await _context.OrderItems
                .Where(oi => oi.OrderDate >= sevenDaysAgo && oi.Order.StoreId == selectedStoreId && oi.Order.Status == "AcceptedBySupplier")
                .GroupBy(oi => oi.Product.Category.CategoryName)
                .Select(g => new TopCategoryViewModel
                {
                    CategoryName = g.Key,
                    TurnOver = g.Sum(oi => oi.OrderValue * oi.Quantity),
                    PercentageIncrease = 0
                })
                .OrderByDescending(c => c.TurnOver)
                .Take(3)
                .ToListAsync();

            var topProducts = await _context.OrderItems
                .Where(oi => oi.OrderDate >= sevenDaysAgo && oi.Order.StoreId == selectedStoreId && oi.Order.Status == "AcceptedBySupplier")
                .GroupBy(oi => new { oi.Product.ProductName, oi.Product.ProductId, oi.Product.Category.CategoryName })
                .Select(g => new TopProductViewModel
                {
                    ProductName = g.Key.ProductName,
                    ProductId = g.Key.ProductId,
                    Category = g.Key.CategoryName,
                    RemainingQuantity = _context.Products
                        .Where(p => p.ProductId == g.Key.ProductId && p.StoreId == selectedStoreId)
                        .Select(p => p.Quantity)
                        .FirstOrDefault(),
                    TurnOver = g.Sum(oi => oi.OrderValue * oi.Quantity),
                    PercentageIncrease = 0
                })
                .OrderByDescending(p => p.TurnOver)
                .Take(4)
                .ToListAsync();

            var viewModel = new InventoryDashboardViewModel
            {
                Products = await productsQuery.ToListAsync(),
                Categories = await _context.Categories
                    .Where(c => c.StoreId == selectedStoreId)
                    .ToListAsync(),
                InventorySummary = new InventorySummary
                {
                    TotalCategories = await _context.Categories
                        .Where(c => c.StoreId == selectedStoreId)
                        .CountAsync(),
                    TotalProducts = await productsQuery.CountAsync(),
                    LowStockItems = await productsQuery
                        .CountAsync(p => p.Quantity <= 0)
                },
                ProfitRevenue = new ProfitRevenueSummary
                {
                    TotalRevenue = revenue,
                    TotalCost = cost,
                    GrossProfit = revenue - cost,
                    ProfitMargin = revenue > 0 ? (revenue - cost) / revenue * 100 : 0
                },
                TopCategories = topCategories,
                TopProducts = topProducts
            };

            var overviewData = new OverviewViewModel
            {
                TotalProfit = revenue - cost,
                Revenue = revenue,
                Sales = await _context.OrderItems
                    .Where(oi => oi.OrderDate >= sevenDaysAgo && oi.Order.StoreId == selectedStoreId && oi.Order.Status == "AcceptedBySupplier")
                    .CountAsync(),
                NetPurchaseValue = await productsQuery
                    .SumAsync(p => p.BuyingPrice * p.Quantity),
                NetSalesValue = revenue
            };

            viewModel.Overview = overviewData;
            return View(viewModel);
        }



        public async Task<IActionResult> Index(string searchString = "", int page = 1, int? categoryId = null)
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            var storeId = HttpContext.Session.GetString("SelectedStoreId");
            if (string.IsNullOrEmpty(storeId))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction("Index", "Dashboardworker");
            }

            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            int storeIdInt = 0;
            int.TryParse(storeIdStr, out storeIdInt);
            var notificationCount = await _context.Notifications
                .Where(n => n.StoreId == storeIdInt && n.NotificationType == "Supplier" && !n.IsRead)
                .CountAsync();
            ViewBag.NotificationCount = notificationCount;


            const int pageSize = 10;

            var productsQuery = _context.Products
          .Include(p => p.Category)
          .Where(p => p.StoreId.ToString() == storeId)
          .AsQueryable();

            if (categoryId.HasValue && categoryId.Value > 0)
            {
                productsQuery = productsQuery.Where(p => p.CategoryId == categoryId.Value);
            }
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                productsQuery = productsQuery.Where(p =>
                    p.ProductName.Contains(searchString) ||
                    p.Category.CategoryName.Contains(searchString));
            }

            var totalProducts = await productsQuery.CountAsync();
            var products = await productsQuery
                .OrderBy(p => p.ProductName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var categories = await _context.Categories
                .Where(c => c.StoreId == storeIdInt)
                .ToListAsync();

            var sevenDaysAgo = DateTime.Now.AddDays(-7);

            var viewModel = new InventoryDashboardViewModel
            {
                Products = products,
                Categories = categories,
                InventorySummary = new InventorySummary
                {
                    TotalCategories = await _context.Categories
                        .Where(c => c.StoreId == storeIdInt)
                        .CountAsync(),
                    TotalProducts = totalProducts,
                    TopSelling = await _context.OrderItems
                        .Where(oi => oi.OrderDate >= sevenDaysAgo && oi.Order.StoreId == storeIdInt && oi.Order.Status == "AcceptedBySupplier")
                        .GroupBy(oi => oi.ProductId)
                        .CountAsync(),
                    Revenue = await _context.OrderItems
                        .Where(oi => oi.OrderDate >= sevenDaysAgo && oi.Order.StoreId == storeIdInt && oi.Order.Status == "AcceptedBySupplier")
                        .SumAsync(oi => oi.OrderValue),
                    Cost = await productsQuery
                        .SumAsync(p => p.BuyingPrice * p.Quantity),
                    LowStockItems = await productsQuery
                        .CountAsync(p => p.Quantity <= 0)
                }
            };

            
            ViewBag.SelectedCategoryId = categoryId;
            ViewBag.Categories = categories;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalProducts / (double)pageSize);
            ViewBag.SearchString = searchString;

            return View(viewModel);
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
                    var manager = await _context.Managers.FirstOrDefaultAsync(m => m.ID == userId);
                    if (manager != null)
                    {
                        userName = manager.name ?? "Manager";
                        profileImageUrl = !string.IsNullOrEmpty(manager.ProfileImageUrl) ? manager.ProfileImageUrl : "/images/default-avatar.png";
                    }
                }
                else if (userType == "worker")
                {
                    var worker = await _context.Workers.FirstOrDefaultAsync(w => w.ID == userId);
                    if (worker != null)
                    {
                        userName = worker.name ?? "Worker";
                        profileImageUrl = !string.IsNullOrEmpty(worker.ProfileImageUrl) ? worker.ProfileImageUrl : "/images/default-avatar.png";
                    }
                }
                else if (userType == "supplier")
                {
                    var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == userId);
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