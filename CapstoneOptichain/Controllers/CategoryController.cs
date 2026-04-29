using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CapstoneOptichain.Models;
using System.Linq;
using System.Threading.Tasks;
using CapstoneOptichain.Data;
using System;

namespace CapstoneOptichain.Controllers
{
    public class CategoryController : Controller
    {
        private readonly ProjectContext _context;

        public CategoryController(ProjectContext context)
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

        public async Task<IActionResult> Index(string searchString = "")
        {
            if (RequireLoginAndStore()) return new EmptyResult();
            var storeId = HttpContext.Session.GetString("SelectedStoreId");
            if (string.IsNullOrEmpty(storeId))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction("Index2", "Order");
            }
            
            if (!int.TryParse(storeId, out int selectedStoreId))
            {
                TempData["ErrorMessage"] = "Invalid store ID";
                return RedirectToAction("Index2", "Order");
            }
            
            // Get categories for the selected store only
            var categoriesQuery = _context.Categories
                .Where(c => c.StoreId == selectedStoreId)
                .Include(c => c.Products)
                .AsQueryable();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                categoriesQuery = categoriesQuery.Where(c => 
                    c.CategoryName.Contains(searchString));
            }

            var categories = await categoriesQuery.ToListAsync();
            
            ViewBag.SearchString = searchString;
            return View(categories);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string categoryName)
        {
            if (ModelState.IsValid)
            {
                var storeId = HttpContext.Session.GetString("SelectedStoreId");
                if (string.IsNullOrEmpty(storeId) || !int.TryParse(storeId, out int selectedStoreId))
                {
                    TempData["ErrorMessage"] = "Please select a store first";
                    return RedirectToAction(nameof(Index));
                }
                
                var category = new Category { 
                    CategoryName = categoryName,
                    StoreId = selectedStoreId
                };
                _context.Categories.Add(category);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index)); 
            }

            TempData["ErrorMessage"] = "Please enter a valid category name";
            return RedirectToAction(nameof(Index));
        }


        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var storeId = HttpContext.Session.GetString("SelectedStoreId");
            if (string.IsNullOrEmpty(storeId) || !int.TryParse(storeId, out int selectedStoreId))
            {
                TempData["ErrorMessage"] = "Please select a store first";
                return RedirectToAction(nameof(Index));
            }
            
            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.CategoryId == id && c.StoreId == selectedStoreId);
                
            if (category == null)
            {
                TempData["ErrorMessage"] = "Category not found or not accessible";
                return RedirectToAction(nameof(Index));
            }

            var hasProducts = await _context.Products.AnyAsync(p => p.CategoryId == id);
            if (hasProducts)
            {
                TempData["ErrorMessage"] = "Cannot delete category because it contains products. Please remove all products first.";
                return RedirectToAction(nameof(Index));
            }

            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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