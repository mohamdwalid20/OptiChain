using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CapstoneOptichain.Models;
using CapstoneOptichain.Data;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace CapstoneOptichain.Controllers
{
    public class SettingsController : Controller
    {
        private readonly ProjectContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SettingsController(ProjectContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private bool RequireLogin()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                HttpContext.Response.Redirect("/Dashboard/Index");
                return true;
            }
            return false;
        }

        private string HashPassword(string password)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public async Task<IActionResult> Index()
        {
            if (RequireLogin()) return new EmptyResult();
            
            var userId = HttpContext.Session.GetInt32("UserId");
            var userRole = HttpContext.Session.GetString("UserType");

                                        // Get user data based on role
                            object userData = null;
                            switch (userRole?.ToLower())
                            {
                                case "admin":
                                    userData = await _context.Admins.FirstOrDefaultAsync(a => a.ID == userId);
                                    break;
                                case "manager":
                                    userData = await _context.Managers.FirstOrDefaultAsync(m => m.ID == userId);
                                    break;
                                case "supplier":
                                    userData = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == userId);
                                    break;
                                case "worker":
                                    userData = await _context.Workers.FirstOrDefaultAsync(w => w.ID == userId);
                                    break;
                            }

            ViewBag.UserRole = userRole;
            ViewBag.UserData = userData;
            ViewBag.UserId = userId;

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfile(IFormFile profileImage, string fullName, string email, string phone)
        {
            try
            {
                if (RequireLogin()) return Json(new { success = false, message = "User not authenticated" });
                
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserType");

                // Handle profile image upload
                string profileImagePath = null;
                if (profileImage != null && profileImage.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "profile-images");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + profileImage.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await profileImage.CopyToAsync(fileStream);
                    }

                    profileImagePath = "/profile-images/" + uniqueFileName;
                }

                                            // Update user data based on role
                            bool updateSuccess = false;
                            switch (userRole?.ToLower())
                            {
                                case "admin":
                                    var admin = await _context.Admins.FirstOrDefaultAsync(a => a.ID == userId);
                                    if (admin != null)
                                    {
                                        if (!string.IsNullOrEmpty(fullName)) admin.name = fullName;
                                        if (!string.IsNullOrEmpty(email)) admin.email = email;
                                        if (!string.IsNullOrEmpty(profileImagePath)) admin.ProfileImageUrl = profileImagePath;
                                        updateSuccess = true;
                                    }
                                    break;

                                case "manager":
                                    var manager = await _context.Managers.FirstOrDefaultAsync(m => m.ID == userId);
                                    if (manager != null)
                                    {
                                        if (!string.IsNullOrEmpty(fullName)) manager.name = fullName;
                                        if (!string.IsNullOrEmpty(email)) manager.email = email;
                                        if (!string.IsNullOrEmpty(phone)) manager.phone = phone;
                                        if (!string.IsNullOrEmpty(profileImagePath)) manager.ProfileImageUrl = profileImagePath;
                                        updateSuccess = true;
                                    }
                                    break;

                                case "supplier":
                                    var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == userId);
                                    if (supplier != null)
                                    {
                                        if (!string.IsNullOrEmpty(fullName)) supplier.name = fullName;
                                        if (!string.IsNullOrEmpty(email)) supplier.email = email;
                                        if (!string.IsNullOrEmpty(profileImagePath)) supplier.ProfileImageUrl = profileImagePath;
                                        updateSuccess = true;
                                    }
                                    break;

                                case "worker":
                                    var worker = await _context.Workers.FirstOrDefaultAsync(w => w.ID == userId);
                                    if (worker != null)
                                    {
                                        if (!string.IsNullOrEmpty(fullName)) worker.name = fullName;
                                        if (!string.IsNullOrEmpty(email)) worker.email = email;
                                        if (!string.IsNullOrEmpty(phone)) worker.Phone_number = phone;
                                        if (!string.IsNullOrEmpty(profileImagePath)) worker.ProfileImageUrl = profileImagePath;
                                        updateSuccess = true;
                                    }
                                    break;
                            }

                if (updateSuccess)
                {
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Profile updated successfully", profileImage = profileImagePath });
                }

                return Json(new { success = false, message = "Failed to update profile" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error updating profile: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordModel model)
        {
            try
            {
                if (RequireLogin()) return Json(new { success = false, message = "User not authenticated" });
                
                // Log the received data for debugging
                System.Diagnostics.Debug.WriteLine($"Received model: currentPassword={model?.currentPassword}, newPassword={model?.newPassword}, confirmPassword={model?.confirmPassword}");
                
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserType");

                                if (model == null)
                {
                    return Json(new { success = false, message = "No data received" });
                }

                if (string.IsNullOrEmpty(model.currentPassword) || string.IsNullOrEmpty(model.newPassword) || string.IsNullOrEmpty(model.confirmPassword))
                {
                    return Json(new { success = false, message = "All password fields are required" });
                }

                if (model.newPassword != model.confirmPassword)
                {
                    return Json(new { success = false, message = "New password and confirmation do not match" });
                }

                if (model.newPassword.Length < 6)
                {
                    return Json(new { success = false, message = "New password must be at least 6 characters long" });
                }

                // Verify current password and update based on role
                bool passwordChanged = false;
                var hashedCurrentPassword = HashPassword(model.currentPassword);
                var hashedNewPassword = HashPassword(model.newPassword);
                            
                            switch (userRole?.ToLower())
                            {
                                case "admin":
                                    var admin = await _context.Admins.FirstOrDefaultAsync(a => a.ID == userId);
                                    if (admin != null && admin.password == hashedCurrentPassword)
                                    {
                                        admin.password = hashedNewPassword;
                                        passwordChanged = true;
                                    }
                                    break;

                                case "manager":
                                    var manager = await _context.Managers.FirstOrDefaultAsync(m => m.ID == userId);
                                    if (manager != null && manager.password == hashedCurrentPassword)
                                    {
                                        manager.password = hashedNewPassword;
                                        passwordChanged = true;
                                    }
                                    break;

                                case "supplier":
                                    var supplier = await _context.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == userId);
                                    if (supplier != null && supplier.password == hashedCurrentPassword)
                                    {
                                        supplier.password = hashedNewPassword;
                                        passwordChanged = true;
                                    }
                                    break;

                                case "worker":
                                    var worker = await _context.Workers.FirstOrDefaultAsync(w => w.ID == userId);
                                    if (worker != null && worker.password == hashedCurrentPassword)
                                    {
                                        worker.password = hashedNewPassword;
                                        passwordChanged = true;
                                    }
                                    break;
                            }

                if (passwordChanged)
                {
                    await _context.SaveChangesAsync();
                    return Json(new { success = true, message = "Password changed successfully" });
                }

                return Json(new { success = false, message = "Current password is incorrect" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error changing password: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUserData()
        {
            try
            {
                if (RequireLogin()) return Json(new { success = false, message = "User not authenticated" });
                
                var userId = HttpContext.Session.GetInt32("UserId");
                var userRole = HttpContext.Session.GetString("UserType");

                                            // Get user data based on role
                            object userData = null;
                            switch (userRole?.ToLower())
                            {
                                case "admin":
                                    userData = await _context.Admins
                                        .Where(a => a.ID == userId)
                                        .Select(a => new { a.name, a.email, a.ProfileImageUrl })
                                        .FirstOrDefaultAsync();
                                    break;

                                case "manager":
                                    userData = await _context.Managers
                                        .Where(m => m.ID == userId)
                                        .Select(m => new { m.name, m.email, m.ProfileImageUrl })
                                        .FirstOrDefaultAsync();
                                    break;

                                case "supplier":
                                    userData = await _context.Suppliers
                                        .Where(s => s.SupplierId == userId)
                                        .Select(s => new { s.name, s.email, s.ProfileImageUrl })
                                        .FirstOrDefaultAsync();
                                    break;

                                case "worker":
                                    userData = await _context.Workers
                                        .Where(w => w.ID == userId)
                                        .Select(w => new { w.name, w.email, w.ProfileImageUrl })
                                        .FirstOrDefaultAsync();
                                    break;
                            }

                return Json(new { success = true, data = userData });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error getting user data: " + ex.Message });
            }
        }
    }
}
