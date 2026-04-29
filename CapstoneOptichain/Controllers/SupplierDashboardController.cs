
using Microsoft.AspNetCore.Mvc;
using CapstoneOptichain.Data;
using CapstoneOptichain.Models;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic; 

namespace CapstoneOptichain.Controllers
{
    public class SupplierDashboardController : Controller
    {
        private readonly ProjectContext _context;
        public SupplierDashboardController(ProjectContext context)
        {
            _context = context;
        }

        private bool RequireLoginAndStore()
        {
            var action = this.RouteData.Values["action"].ToString().ToLower();
            var controller = this.RouteData.Values["controller"].ToString().ToLower();

            if ((controller == "dashboard" && (action == "index" || action == "login" || action == "index2" || action == "index4" || action == "index5" || action == "land"))
                || (controller == "supplierdashboard" && action == "stores"))
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
                HttpContext.Response.Redirect("/SupplierDashboard/Stores");
                return true;
            }
            return false;
        }

        public async Task<IActionResult> Index()
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var userType = HttpContext.Session.GetString("UserType");
            if (userType != "supplier")
            {
                if (userType == "manager")
                    return RedirectToAction("Index3", "Dashboard");
                if (userType == "worker")
                    return RedirectToAction("Index", "Dashboardworker");
                return RedirectToAction("Index", "Dashboard");
            }

            int? supplierId = HttpContext.Session.GetInt32("UserId");
            if (supplierId == null)
                return RedirectToAction("Index", "Dashboard");


            var supplier = await _context.Suppliers
                .Include(s => s.Store)
                .FirstOrDefaultAsync(s => s.SupplierId == supplierId);

            if (supplier == null)
            {
                TempData["ErrorMessage"] = "Supplier not found.";
                return RedirectToAction("Index", "Dashboard");
            }


            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            var storeName = HttpContext.Session.GetString("SelectedStore");
            if (string.IsNullOrEmpty(storeIdStr) || !int.TryParse(storeIdStr, out int storeId))
            {
                TempData["ErrorMessage"] = "Please select a store first to access the dashboard.";
                return RedirectToAction("Stores", "SupplierDashboard");
            }

            TempData["SelectedStore"] = storeName;
            ViewBag.SelectedStore = storeName;


            var orders = await _context.Orders
                .Where(o => o.StoreId == storeId && 
                           o.Status != "AcceptedBySupplier" && 
                           o.Status != "RejectedBySupplier" &&
                           o.Status != "RejectedByManager")
                .ToListAsync();


            var notifications = await _context.Notifications
                .Where(n => n.StoreId == storeId && n.NotificationType == "Supplier" && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            ViewBag.Notifications = notifications;
            ViewBag.NotificationCount = notifications.Count;

            // Determine which orders the current supplier has already proposed prices for
            var orderIds = orders.Select(o => o.OrderId).ToList();
            var proposedOrderIds = await _context.SupplierProposals
                .Where(sp => sp.SupplierId == supplierId && orderIds.Contains(sp.OrderId))
                .Select(sp => sp.OrderId)
                .Distinct()
                .ToListAsync();
            ViewBag.ProposedOrderIds = proposedOrderIds;

            return View(orders);
        }

        [HttpPost]
        public async Task<IActionResult> SelectStore(int storeId)
        {
            var userType = HttpContext.Session.GetString("UserType");
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userType != "supplier" || userId == null)
                return Json(new { success = false, message = "Not authorized" });

            var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == userId);
            if (supplier == null)
                return Json(new { success = false, message = "Supplier not found" });

            supplier.StoreId = storeId;
            await _context.SaveChangesAsync();

            var store = await _context.Stores.FirstOrDefaultAsync(s => s.StoreId == storeId);
            HttpContext.Session.SetString("SelectedStoreId", storeId.ToString());
            HttpContext.Session.SetString("SelectedStore", store?.StoreName ?? "Unknown Store");

            return Json(new { success = true });
        }

        public async Task<IActionResult> Stores()
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var userType = HttpContext.Session.GetString("UserType");
            if (userType != "supplier")
            {
                if (userType == "manager")
                    return RedirectToAction("Index3", "Dashboard");
                if (userType == "worker")
                    return RedirectToAction("Index", "Dashboardworker");
                return RedirectToAction("Index", "Dashboard");
            }

            int? supplierId = HttpContext.Session.GetInt32("UserId");
            if (supplierId == null)
                return RedirectToAction("Index", "Dashboard");

            var stores = await _context.Stores.ToListAsync();
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
            if (ModelState.IsValid)
            {
                _context.Stores.Add(store);
                await _context.SaveChangesAsync();
                return RedirectToAction("Stores");
            }
            return View(store);
        }

        [HttpPost]
        public async Task<IActionResult> AcceptOrder(int orderId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction("Index");
            }

            if (order.Status == "AcceptedBySupplier")
            {
                TempData["InfoMessage"] = "This order has already been accepted.";
                return RedirectToAction("Index");
            }


            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId);
                if (product != null)
                {
                    if (order.OrderType == "Purchase")
                    {
                        product.Quantity += item.Quantity;
                    }
                    else if (order.OrderType == "Sale")
                    {
                        product.Quantity -= item.Quantity;

                    }
                    _context.Products.Update(product);
                }
            }

            order.Status = "AcceptedBySupplier";

            // Adjust inventory when supplier accepts (as a fallback if manager accepted directly elsewhere)
            foreach (var item in order.OrderItems)
            {
                var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == item.ProductId && p.StoreId == order.StoreId);
                if (product != null)
                {
                    if (order.OrderType == "Purchase")
                    {
                        product.Quantity += item.Quantity;
                    }
                    else if (order.OrderType == "Sale")
                    {
                        product.Quantity -= item.Quantity;
                    }
                    _context.Products.Update(product);
                }
            }

            await _context.SaveChangesAsync();


            var notification = new Notification
            {
                Message = $"Order {order.OrderId} has been accepted by supplier for store {order.StoreId}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                StoreId = order.StoreId,
                NotificationType = "Manager"
            };
            _context.Notifications.Add(notification);


            var oldSupplierNotifications = await _context.Notifications
                .Where(n => n.StoreId == order.StoreId && 
                           n.NotificationType == "Supplier" && 
                           n.Message.Contains($"order (ID: {order.OrderId})"))
                .ToListAsync();
            
            if (oldSupplierNotifications.Any())
            {
                _context.Notifications.RemoveRange(oldSupplierNotifications);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order accepted successfully and management notified.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> RejectOrder(int orderId)
        {
            if (RequireLoginAndStore()) return new EmptyResult();

            var order = await _context.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null)
            {
                TempData["ErrorMessage"] = "Order not found.";
                return RedirectToAction("Index");
            }

            if (order.Status == "RejectedBySupplier")
            {
                TempData["InfoMessage"] = "This order has already been rejected.";
                return RedirectToAction("Index");
            }

            order.Status = "RejectedBySupplier";
            await _context.SaveChangesAsync();

            var notification = new Notification
            {
                Message = $"Order {order.OrderId} has been rejected by supplier for store {order.StoreId}.",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                StoreId = order.StoreId,
                NotificationType = "Manager"
            };
            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Order rejected successfully and management notified.";
            return RedirectToAction("Index");
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

            // Ensure OrderType and Status are set if they're null
            if (string.IsNullOrEmpty(order.OrderType))
            {
                order.OrderType = "Purchase"; // Default value
            }
            
            if (string.IsNullOrEmpty(order.Status))
            {
                order.Status = "Pending"; // Default value
            }

            // Note: Manager information is not directly linked to Store in the current system
            // Manager-Store relationship is handled through session management
            ViewBag.Manager = null;
            ViewBag.Order = order;

            return View(order);
        }

        [HttpGet]
        [Route("SupplierDashboard/GetOrderItemsForPricing/{orderId}")]
        public async Task<IActionResult> GetOrderItemsForPricing(int orderId)
        {
            // Add logging to debug
            System.Diagnostics.Debug.WriteLine($"GetOrderItemsForPricing called with orderId: {orderId}");
            
            try
            {
                // Check if user is logged in
                var userId = HttpContext.Session.GetInt32("UserId");
                System.Diagnostics.Debug.WriteLine($"UserId from session: {userId}");
                if (userId == null)
                {
                    return Json(new { success = false, message = "Please log in first." });
                }

                // Check if user is a supplier
                var userType = HttpContext.Session.GetString("UserType");
                if (userType != "supplier")
                {
                    return Json(new { success = false, message = "Access denied. Supplier only." });
                }

                // Check if store is selected
                var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
                if (string.IsNullOrEmpty(storeIdStr))
                {
                    return Json(new { success = false, message = "Please select a store first." });
                }

                System.Diagnostics.Debug.WriteLine($"Looking for order with ID: {orderId}");
                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    System.Diagnostics.Debug.WriteLine($"Order {orderId} not found in database");
                    return Json(new { success = false, message = "Order not found." });
                }
                
                System.Diagnostics.Debug.WriteLine($"Order {orderId} found with {order.OrderItems.Count} items");

                var orderItems = order.OrderItems.Select(oi => new
                {
                    productName = oi.Product?.ProductName ?? "Unknown Product",
                    quantity = oi.Quantity
                }).ToList();

                return Json(new { success = true, data = orderItems });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error loading order items: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Notifications()
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
            if (!int.TryParse(storeIdStr, out int storeId))
                return Content("<div>Please Choose Store</div>");
            var notifications = await _context.Notifications
                .Where(n => n.StoreId == storeId && n.NotificationType == "Supplier" && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();
            return PartialView("_NotificationListPartial", notifications);
        }

        [HttpPost]
        public async Task<IActionResult> MarkNotificationRead(int notificationId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId);
            if (notification == null)
                return Json(new { success = false, message = "Notification not found" });
            notification.IsRead = true;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ProposePrice(int orderId, string proposedPrices)
        {
            try
            {
                // Check if user is logged in
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new { success = false, message = "Please log in first." });
                }

                // Check if user is a supplier
                var userType = HttpContext.Session.GetString("UserType");
                if (userType != "supplier")
                {
                    return Json(new { success = false, message = "Access denied. Supplier only." });
                }

                // Check if store is selected
                var storeIdStr = HttpContext.Session.GetString("SelectedStoreId");
                if (string.IsNullOrEmpty(storeIdStr))
                {
                    return Json(new { success = false, message = "Please select a store first." });
                }

                var order = await _context.Orders
                    .Include(o => o.OrderItems)
                    .FirstOrDefaultAsync(o => o.OrderId == orderId);

                if (order == null)
                {
                    return Json(new { success = false, message = "Order not found." });
                }

                if (order.Status != "PendingPrice")
                {
                    return Json(new { success = false, message = "Order is not waiting for price proposal." });
                }

                // Check if supplier already proposed for this order
                var existingProposal = await _context.SupplierProposals
                    .FirstOrDefaultAsync(sp => sp.OrderId == orderId && sp.SupplierId == userId.Value);

                if (existingProposal != null)
                {
                    return Json(new { success = false, message = "You have already proposed prices for this order." });
                }

                // Parse proposed prices from string
                var priceStrings = proposedPrices.Split(',');
                var prices = new List<decimal>();
                
                foreach (var priceStr in priceStrings)
                {
                    if (decimal.TryParse(priceStr.Trim(), out decimal price))
                    {
                        prices.Add(price);
                    }
                }

                if (prices.Count != order.OrderItems.Count)
                {
                    return Json(new { success = false, message = "Number of prices doesn't match number of order items." });
                }

                // Create supplier proposal
                var proposal = new SupplierProposal
                {
                    OrderId = orderId,
                    SupplierId = userId.Value,
                    ProposedAt = DateTime.UtcNow,
                    Status = "Pending",
                    TotalAmount = 0
                };

                _context.SupplierProposals.Add(proposal);
                await _context.SaveChangesAsync();

                // Create proposal items
                decimal totalAmount = 0;
                for (int i = 0; i < order.OrderItems.Count && i < prices.Count; i++)
                {
                    var orderItem = order.OrderItems.ElementAt(i);
                    var proposedPrice = prices[i];
                    var totalPrice = proposedPrice * orderItem.Quantity;
                    totalAmount += totalPrice;

                    var proposalItem = new ProposalItem
                    {
                        ProposalId = proposal.ProposalId,
                        ProductId = orderItem.ProductId,
                        Quantity = orderItem.Quantity,
                        UnitPrice = proposedPrice,
                        TotalPrice = totalPrice
                    };

                    _context.ProposalItems.Add(proposalItem);
                }

                // Update proposal total amount
                proposal.TotalAmount = totalAmount;
                _context.SupplierProposals.Update(proposal);

                // Keep order in PendingPrice status to allow multiple suppliers to propose
                // Manager will move it to approval/acceptance later

                await _context.SaveChangesAsync();

                // Notify manager about price proposal
                var notification = new Notification
                {
                    Message = $"Supplier has proposed prices for order {order.OrderId}. Total: ${totalAmount:F2}",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    StoreId = order.StoreId,
                    NotificationType = "Manager"
                };
                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Prices proposed successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error proposing prices: " + ex.Message });
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