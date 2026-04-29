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
	public class OrderController : Controller
	{
		private readonly ProjectContext _context;

		public OrderController(ProjectContext context)
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
				HttpContext.Response.Redirect("/Order/Index2");
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
				return RedirectToAction("Index2", "Order");
			}

			if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
			{
				TempData["ErrorMessage"] = "Please select a store first";
				return RedirectToAction("Index2", "Order");
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
					name = p.ProductName
				})
				.ToListAsync();

			ViewBag.ProductsJson = JsonConvert.SerializeObject(products);

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
				order.Status = "PendingPrice";
				order.OrderItems = new List<OrderItem>();
				order.PriceProposalDeadline = DateTime.UtcNow.AddDays(3); // 3 days deadline for suppliers
				order.AllSuppliersProposed = false;

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

					// Validate available stock for Sale orders
					if (string.Equals(order.OrderType, "Sale", StringComparison.OrdinalIgnoreCase))
					{
						var requestedQty = quantities[i];
						if (requestedQty <= 0)
						{
							return Json(new { success = false, message = $"Quantity for {product.ProductName} must be greater than 0." });
						}
						if (product.Quantity < requestedQty)
						{
							return Json(new { success = false, message = $"Insufficient stock for {product.ProductName}. Available: {product.Quantity}. Requested: {requestedQty}." });
						}
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

				// Get all suppliers for this store
				var suppliers = await _context.Suppliers
					.Where(s => s.StoreId == selectedStoreId)
					.ToListAsync();

				// Notify all suppliers about the new order
				foreach (var supplier in suppliers)
				{
					var supplierNotification = new Notification
					{
						Message = $"New {order.OrderType} order (ID: {order.OrderId}) available for pricing. Please propose your prices within 3 days.",
						IsRead = false,
						CreatedAt = DateTime.UtcNow,
						StoreId = order.StoreId,
						NotificationType = "Supplier"
					};
					_context.Notifications.Add(supplierNotification);
				}

				var notification = new Notification
				{
					Message = $"A new {order.OrderType} order (ID: {order.OrderId}) was created for store {order.StoreId}. Waiting for supplier price proposals.",
					IsRead = false,
					CreatedAt = DateTime.UtcNow,
					StoreId = order.StoreId
				};
				_context.Notifications.Add(notification);

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

			var selectedStoreIdStr = HttpContext.Session.GetString("SelectedStoreId");
			if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
			{
				TempData["ErrorMessage"] = "Please select a store first";
				return RedirectToAction("Index2", "Order");
			}

			var order = await _context.Orders
				.Include(o => o.OrderItems)
				.FirstOrDefaultAsync(o => o.OrderId == id && o.StoreId == selectedStoreId);

			if (order != null)
			{
				// Delete related OrderRequests first (due to DeleteBehavior.Restrict)
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
			}
			return RedirectToAction(nameof(Index));
		}





		private bool OrderExists(int id)
		{
			return _context.Orders.Any(e => e.OrderId == id);
		}



		[HttpPost]
		[ValidateAntiForgeryToken]
		public async Task<IActionResult> SetCurrentStore([FromBody] StoreSelectionModel model)
		{
			try
			{
				// Check subscription status for managers
				var userId = HttpContext.Session.GetInt32("UserId");
				var userType = HttpContext.Session.GetString("UserType");
				
				if (userType == "manager" && userId.HasValue)
				{
					var subscription = await _context.ManagerSubscriptions
						.Where(s => s.ManagerId == userId.Value)
						.OrderByDescending(s => s.CreatedAt)
						.FirstOrDefaultAsync();
					
					// If no subscription or subscription is cancelled/expired, redirect to subscription page
					if (subscription == null || 
						subscription.Status == "Cancelled" || 
						subscription.Status == "Expired" ||
						subscription.SubscriptionEndDate <= DateTime.UtcNow)
					{
						return Json(new { success = false, message = "Subscription required", redirect = "/Manager/Subscription" });
					}
				}
				
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

		public async Task<IActionResult> Index2()
		{
			var userId = HttpContext.Session.GetInt32("UserId");
			var userType = HttpContext.Session.GetString("UserType");
			
			// Check subscription status for managers
			if (userType == "manager" && userId.HasValue)
			{
				var subscription = await _context.ManagerSubscriptions
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
			
			List<Store> stores;
			
			if (userType == "manager" && userId.HasValue)
			{
				// For managers, show only their stores
				stores = await _context.Stores
					.Where(s => s.ManagerId == userId)
					.ToListAsync();
			}
			else if (userType == "admin")
			{
				// For admins, show all stores
				stores = await _context.Stores.ToListAsync();
			}
			else
			{
				// For other users, show empty list
				stores = new List<Store>();
			}

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
				// Set the ManagerId for the store
				var userId = HttpContext.Session.GetInt32("UserId");
				var userType = HttpContext.Session.GetString("UserType");
				
				if (userType == "manager" && userId.HasValue)
				{
					store.ManagerId = userId.Value;
				}
				
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

		[HttpGet]
		public async Task<IActionResult> OrderDetails(int orderId)
		{
			if (RequireLoginAndStore()) return new EmptyResult();

			var selectedStoreIdStr = HttpContext.Session.GetString("SelectedStoreId");
			if (!int.TryParse(selectedStoreIdStr, out int selectedStoreId))
			{
				TempData["ErrorMessage"] = "Please select a store first";
				return RedirectToAction("Index2", "Order");
			}

			var order = await _context.Orders
				.Include(o => o.OrderItems)
				.ThenInclude(oi => oi.Product)
				.ThenInclude(p => p.Category)
				.Include(o => o.Store)
				.FirstOrDefaultAsync(o => o.OrderId == orderId && o.StoreId == selectedStoreId);

			if (order == null)
			{
				TempData["ErrorMessage"] = "Order not found";
				return RedirectToAction("Index");
			}

			// Get all supplier proposals for this order
			var proposals = await _context.SupplierProposals
				.Include(sp => sp.Supplier)
				.Include(sp => sp.ProposalItems)
				.ThenInclude(pi => pi.Product)
				.Where(sp => sp.OrderId == orderId)
				.OrderBy(sp => sp.TotalAmount)
				.ToListAsync();

			ViewBag.Order = order;
			ViewBag.Proposals = proposals;
			ViewBag.HasProposals = proposals.Any();

			return View();
		}

		[HttpPost]
		public async Task<IActionResult> AcceptProposal(int proposalId)
		{
			if (RequireLoginAndStore()) return Json(new { success = false, message = "Not authorized" });

			try
			{
				var proposal = await _context.SupplierProposals
					.Include(sp => sp.Order)
					.ThenInclude(o => o.OrderItems)
					.Include(sp => sp.ProposalItems)
					.Include(sp => sp.Supplier)
					.FirstOrDefaultAsync(sp => sp.ProposalId == proposalId);

				if (proposal == null)
				{
					return Json(new { success = false, message = "Proposal not found. Please refresh the page and try again." });
				}

				if (proposal.Status != "Pending")
				{
					return Json(new { success = false, message = "Proposal has already been processed" });
				}

				// Accept the proposal
				proposal.Status = "Accepted";
				proposal.ResponseDate = DateTime.UtcNow;
				proposal.ManagerResponse = "Accepted by manager";

				// Update order status
				proposal.Order.Status = "AcceptedBySupplier";
				proposal.Order.SupplierId = proposal.SupplierId;

				// Update order items with accepted prices
				foreach (var proposalItem in proposal.ProposalItems)
				{
					var orderItem = proposal.Order.OrderItems
						.FirstOrDefault(oi => oi.ProductId == proposalItem.ProductId);
					
					if (orderItem != null)
					{
						orderItem.OrderValue = proposalItem.UnitPrice; // Storing unit price as used across the app
					}
				}

				// Adjust inventory based on order type (Purchase increases, Sale decreases)
				foreach (var item in proposal.Order.OrderItems)
				{
					var product = await _context.Products
						.FirstOrDefaultAsync(p => p.ProductId == item.ProductId && p.StoreId == proposal.Order.StoreId);
					if (product != null)
					{
						if (string.Equals(proposal.Order.OrderType, "Purchase", StringComparison.OrdinalIgnoreCase))
						{
							product.Quantity += item.Quantity;
						}
						else if (string.Equals(proposal.Order.OrderType, "Sale", StringComparison.OrdinalIgnoreCase))
						{
							product.Quantity -= item.Quantity;
						}
						_context.Products.Update(product);
					}
				}

				// Reject all other proposals for this order
				var otherProposals = await _context.SupplierProposals
					.Where(sp => sp.OrderId == proposal.OrderId && sp.ProposalId != proposalId)
					.ToListAsync();

				foreach (var otherProposal in otherProposals)
				{
					otherProposal.Status = "Rejected";
					otherProposal.ResponseDate = DateTime.UtcNow;
					otherProposal.ManagerResponse = "Rejected - Another proposal was accepted";
				}

				// Notify the accepted supplier
				var notification = new Notification
				{
					Message = $"Your proposal for order {proposal.OrderId} has been accepted!",
					IsRead = false,
					CreatedAt = DateTime.UtcNow,
					StoreId = proposal.Order.StoreId,
					NotificationType = "Supplier"
				};
				_context.Notifications.Add(notification);

				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "Proposal accepted successfully" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "Error accepting proposal: " + ex.Message });
			}
		}

		[HttpPost]
		public async Task<IActionResult> RejectProposal(int proposalId, string reason = "")
		{
			if (RequireLoginAndStore()) return Json(new { success = false, message = "Not authorized" });

			try
			{
				var proposal = await _context.SupplierProposals
					.Include(sp => sp.Order)
					.FirstOrDefaultAsync(sp => sp.ProposalId == proposalId);

				if (proposal == null)
				{
					return Json(new { success = false, message = "Proposal not found" });
				}

				if (proposal.Status != "Pending")
				{
					return Json(new { success = false, message = "Proposal has already been processed" });
				}

				// Reject the proposal
				proposal.Status = "Rejected";
				proposal.ResponseDate = DateTime.UtcNow;
				proposal.ManagerResponse = reason;

				await _context.SaveChangesAsync();

				// Notify the rejected supplier
				var notification = new Notification
				{
					Message = $"Your proposal for order {proposal.OrderId} has been rejected. Reason: {reason}",
					IsRead = false,
					CreatedAt = DateTime.UtcNow,
					StoreId = proposal.Order.StoreId,
					NotificationType = "Supplier"
				};
				_context.Notifications.Add(notification);

				await _context.SaveChangesAsync();

				return Json(new { success = true, message = "Proposal rejected successfully" });
			}
			catch (Exception ex)
			{
				return Json(new { success = false, message = "Error rejecting proposal: " + ex.Message });
			}
		}

	}
}