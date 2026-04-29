using CapstoneOptichain.Data;
using CapstoneOptichain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CapstoneOptichain.Controllers
{
    public class OrderworkerController : Controller
    {
        private readonly ProjectContext _context;

        public OrderworkerController(ProjectContext context)
        {
            _context = context;
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
                // For workers, redirect to dashboard instead of store selection
                HttpContext.Response.Redirect("/Dashboardworker/Index");
                return true;
            }
            return false;
        }

        public async Task<IActionResult> Index(string searchString, int page = 1, int pageSize = 10)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var selectedStoreIdStr = HttpContext.Session.GetString("SelectedStoreId");
            if (string.IsNullOrEmpty(selectedStoreIdStr))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction("Index2", "Orderworker");
            }

            if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction("Index2", "Orderworker");
            }


            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var query = _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .Where(o => o.StoreId == selectedStoreId)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(o =>
                    o.OrderId.ToString().Contains(searchString) ||
                    o.Status.Contains(searchString) ||
                    o.OrderItems.Any(oi => oi.Product.ProductName.Contains(searchString)));
            }

            var totalOrders = await query.CountAsync();
            var orders = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();


            var recentOrders = await _context.Orders
                .Where(o => o.OrderDate >= sevenDaysAgo && o.StoreId == selectedStoreId)
                .ToListAsync();

            ViewBag.TotalOrders = recentOrders.Count;
            ViewBag.TotalReceived = recentOrders.Count(o => o.Status == "AcceptedBySupplier");
            ViewBag.TotalReturned = recentOrders.Count(o => o.Status == "Pending");
            ViewBag.OnTheWay = recentOrders.Count(o => o.Status == "On the way");
            ViewBag.Cost = recentOrders.Sum(o => o.OrderItems.Sum(oi => oi.OrderValue)).ToString("C");
            ViewBag.LowStockInfo = await GetLowStockInfo(selectedStoreId);

            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            int storeId = 0;
            int.TryParse(storeIdStr, out storeId);
            var notificationCount = await _context.Notifications
                .Where(n => n.StoreId == storeId && n.NotificationType == "Supplier" && !n.IsRead)
                .CountAsync();
            ViewBag.NotificationCount = notificationCount;

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalOrders / (double)pageSize);
            ViewBag.SearchString = searchString;


            var products = await _context.Products
                .Where(p => p.StoreId == selectedStoreId)
                .Select(p => new {
                    id = p.ProductId,
                    name = p.ProductName,
                    quantity = p.Quantity
                })
                .ToListAsync();

            ViewBag.ProductsJson = JsonConvert.SerializeObject(products);

            // Get existing approval requests for the current worker
            var workerId = HttpContext.Session.GetInt32("UserId");
            if (workerId.HasValue)
            {
                var existingRequests = await _context.OrderRequests
                    .Where(or => or.WorkerId == workerId.Value && 
                                 (or.Status == "Pending" || or.Status == "Approved" || or.Status == "Rejected"))
                    .Select(or => or.OrderId)
                    .ToListAsync();
                ViewBag.ExistingRequests = existingRequests;
            }

            return View(orders);
        }

        private async Task<string> GetLowStockInfo(int storeId)
        {
            var lowStockCount = await _context.Products
                .Where(p => p.StoreId == storeId && p.Quantity <= 0)
                .CountAsync();

            return lowStockCount > 0 ? $"{lowStockCount} items need restock" : "All items in stock";
        }



        		[HttpPost]
		public async Task<IActionResult> Create([FromForm] Order order, [FromForm] int[] productIds,
		[FromForm] int[] quantities)
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            try
            {
                var selectedStoreIdStr = HttpContext.Session.GetString("SelectedStoreId");
                if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
                {
                    return Json(new { success = false, message = "Please select a store first" });
                }

                if (productIds == null || productIds.Length == 0)
                {
                    return Json(new { success = false, message = "Please add at least one product" });
                }

                order.OrderDate = DateTime.UtcNow;
                order.StoreId = selectedStoreId;
                order.Status = "Pending"; // Start with Pending status, waiting for manager approval
                order.OrderItems = new List<OrderItem>();

                for (int i = 0; i < productIds.Length; i++)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.ProductId == productIds[i] && p.StoreId == selectedStoreId);

                    if (product == null)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Product with ID {productIds[i]} not found in selected store"
                        });
                    }

                    // Validate quantity for Sale orders
                    if (order.OrderType == "Sale" && quantities[i] > product.Quantity)
                    {
                        return Json(new
                        {
                            success = false,
                            message = $"Insufficient quantity for {product.ProductName}. Requested: {quantities[i]}, Available: {product.Quantity}"
                        });
                    }

                    // For now, set OrderValue to 0 until supplier proposes price
                    order.OrderItems.Add(new OrderItem
                    {
                        ProductId = productIds[i],
                        Quantity = quantities[i],
                        OrderValue = 0, // Will be updated when supplier proposes price
                        OrderDate = DateTime.UtcNow
                    });


                }


                _context.Orders.Add(order);
                await _context.SaveChangesAsync();


                var notification = new Notification
                {
                    Message = $"A new {order.OrderType} order (ID: {order.OrderId}) was created for store {order.StoreId}.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId
                };
                _context.Notifications.Add(notification);


                var supplierNotification = new Notification
                {
                    Message = $"New {order.OrderType} order (ID: {order.OrderId}) available for processing in your store.",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Supplier"
                };
                _context.Notifications.Add(supplierNotification);

                await _context.SaveChangesAsync();

                return Json(new
                {
                    success = true,
                    redirect = Url.Action("Index")
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving order: {ex.Message}");
                return Json(new
                {
                    success = false,
                    message = "Error saving order. Please try again.",
                    error = ex.Message
                });
            }
        }


        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var selectedStoreIdStr = HttpContext.Session.GetString("SelectedStoreId");
                if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
                {
                    return Json(new { success = false, message = "Please select a store first" });
                }

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == id && o.StoreId == selectedStoreId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                // Check if order is accepted by supplier or rejected by manager
                if (order.Status != "AcceptedBySupplier" && order.Status != "RejectedByManager")
                {
                    return Json(new { success = false, message = "Only accepted orders or rejected orders can be deleted" });
                }

                // Delete related OrderRequests first
                var orderRequests = await _context.OrderRequests
                    .Where(or => or.OrderId == id)
                    .ToListAsync();
                
                if (orderRequests.Any())
                {
                    _context.OrderRequests.RemoveRange(orderRequests);
                }

                // Update product quantities
                foreach (var item in order.OrderItems)
                {
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.ProductId == item.ProductId && p.StoreId == selectedStoreId);

                    if (product != null)
                    {
                        product.Quantity -= item.Quantity;
                        _context.Products.Update(product);
                    }
                }

                _context.OrderItems.RemoveRange(order.OrderItems);
                _context.Orders.Remove(order);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Order deleted successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error deleting order: " + ex.Message });
            }
        }





        private bool OrderExists(int id)
        {
            return _context.Orders.Any(e => e.OrderId == id);
        }

        [HttpGet]
        public async Task<IActionResult> OrderDetails(int orderId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .ThenInclude(p => p.Category)
                .Include(o => o.Store)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction("Index");
            }

            return View(order);
        }



        [HttpPost]
        public async Task<IActionResult> RequestOrderApproval(int orderId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            try
            {
                var workerId = HttpContext.Session.GetInt32("UserId");
                if (!workerId.HasValue)
                {
                    return Json(new { success = false, message = "Worker not authenticated" });
                }

                // Check if request already exists
                var existingRequest = await _context.OrderRequests
                    .FirstOrDefaultAsync(or => or.OrderId == orderId && or.WorkerId == workerId.Value);

                if (existingRequest != null)
                {
                    return Json(new { success = false, message = "Request already exists for this order" });
                }

                var order = await _context.Orders
                    .Include(o => o.Store)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found" });
                }

                var orderRequest = new OrderRequest
                {
                    WorkerId = workerId.Value,
                    OrderId = orderId,
                    RequestDate = DateTime.UtcNow,
                    Status = "Pending"
                };

                _context.OrderRequests.Add(orderRequest);
                await _context.SaveChangesAsync();

                // Create notification for manager
                var notification = new Notification
                {
                    Message = $"Worker {workerId.Value} has requested approval for order {orderId}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Manager"
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Order approval request sent successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error sending request: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SetCurrentStore([FromBody] StoreSelectionModel model)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(model.storeName) || string.IsNullOrWhiteSpace(model.storeId))
                {
                    return Json(new { success = false, message = "Invalid store data" });
                }

                HttpContext.Session.SetString("SelectedStoreId", model.storeId);
                HttpContext.Session.SetString("SelectedStore", model.storeName);

                TempData["SelectedStore"] = model.storeName;

                return Json(new { success = true, storeName = model.storeName });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        public IActionResult Index2()
        {
            var stores = _context.Stores.ToList();

            var selectedStore = HttpContext.Session.GetString("SelectedStore");
            if (!string.IsNullOrEmpty(selectedStore))
            {
                TempData["SelectedStore"] = selectedStore;
            }

            return View(stores);
        }

        [HttpGet]
        public IActionResult AddStore()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStore(Store store)
        {
            if (!string.IsNullOrWhiteSpace(store.StoreName))
            {
                _context.Stores.Add(store);
                await _context.SaveChangesAsync();
                TempData["StoreAdded"] = true;
                return RedirectToAction("Index2");
            }
            TempData["StoreAdded"] = false;
            return RedirectToAction("Index2");
        }


        [HttpGet]
        public IActionResult CheckStoreSelection()
        {
            var storeId = HttpContext.Session.GetString("SelectedStoreId");
            if (string.IsNullOrEmpty(storeId))
            {
                return Json(new { hasStore = false });
            }

            return Json(new
            {
                hasStore = true,
                storeId,
                storeName = HttpContext.Session.GetString("SelectedStore")
            });
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