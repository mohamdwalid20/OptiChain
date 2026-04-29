using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Net.Http;
using System.Threading.Tasks;
using CapstoneOptichain.Models;
using CapstoneOptichain.Data;
using Microsoft.AspNetCore.Http;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace CapstoneOptichain.Controllers
{
	public class AuthController : Controller
	{
		private readonly IHttpClientFactory _httpClientFactory;
		private readonly string? _clientId;
		private readonly string? _clientSecret;
		private readonly string? _redirectUri;
		private readonly ProjectContext _dbContext;
		private readonly IWebHostEnvironment _webHostEnvironment;

		public AuthController(IHttpClientFactory httpClientFactory, IConfiguration configuration, ProjectContext dbContext, IWebHostEnvironment webHostEnvironment)
		{
			_httpClientFactory = httpClientFactory;
			_clientId = configuration["Authentication:Google:ClientId"];
			_clientSecret = configuration["Authentication:Google:ClientSecret"];
			_redirectUri = configuration["Authentication:Google:RedirectUri"];
			_dbContext = dbContext;
			_webHostEnvironment = webHostEnvironment;
		}

		private async Task<string> SaveProfileImageAsync(IFormFile? imageFile)
		{
			if (imageFile == null || imageFile.Length == 0)
				return string.Empty;

			var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "profile-images");
			if (!Directory.Exists(uploadsFolder))
				Directory.CreateDirectory(uploadsFolder);

			var uniqueFileName = Guid.NewGuid().ToString() + "_" + imageFile.FileName;
			var filePath = Path.Combine(uploadsFolder, uniqueFileName);

			using (var fileStream = new FileStream(filePath, FileMode.Create))
			{
				await imageFile.CopyToAsync(fileStream);
			}

			return "/profile-images/" + uniqueFileName;
		}

		private string HashPassword(string password)
		{
			using (var sha256 = System.Security.Cryptography.SHA256.Create())
			{
				var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
				return Convert.ToBase64String(hashedBytes);
			}
		}

		[HttpPost("Auth/SignUp")]
		public async Task<IActionResult> SignUp(SignUpViewModel model)
		{
			if (ModelState.IsValid)
			{
				try
				{
					var profileImageUrl = await SaveProfileImageAsync(model.ProfileImage);

					switch (model.UserType.ToLower())
					{
						case "manager":
							var manager = new Manager
							{
								name = model.Name,
								email = model.Email,
								password = HashPassword(model.Password),
								phone = model.Phone,
								ProfileImageUrl = profileImageUrl
							};
							_dbContext.Managers.Add(manager);
							break;

						case "worker":
							var worker = new Worker
							{
								name = model.Name,
								email = model.Email,
								password = HashPassword(model.Password),
								Phone_number = model.Phone,
								Address = model.Address,
								Department = model.Department,
								role = "worker",
								ProfileImageUrl = profileImageUrl
							};
							_dbContext.Workers.Add(worker);
							break;

						case "supplier":
							var supplier = new Supplier
							{
								name = model.Name,
								email = model.Email,
								password = HashPassword(model.Password),
								ContactNumber = model.Phone,
								Address = model.Address,
								Role = "supplier",
								ProfileImageUrl = profileImageUrl
							};
							_dbContext.Suppliers.Add(supplier);
							break;

						default:
							ModelState.AddModelError("UserType", "Invalid user type");
							return View(model);
					}

					await _dbContext.SaveChangesAsync();
					TempData["SuccessMessage"] = "Registration successful! Please log in.";
					return RedirectToAction("Index", "Dashboard");
				}
				catch (Exception ex)
				{
					ModelState.AddModelError("", "An error occurred during registration. Please try again.");
				}
			}

			return View(model);
		}


		[HttpGet("Auth/GoogleCallback")]
		public async Task<IActionResult> GoogleCallback(string code)
		{
			try
			{

				if (string.IsNullOrEmpty(code))
					return BadRequest("Code is missing.");

				var tokenResponse = await _httpClientFactory.CreateClient().PostAsync(
					"https://oauth2.googleapis.com/token",
					new FormUrlEncodedContent(new Dictionary<string, string>
					{
				{ "code", code },
				{ "client_id", _clientId! },
				{ "client_secret", _clientSecret! },
				{ "redirect_uri", _redirectUri! },
				{ "grant_type", "authorization_code" }
					}));

				if (!tokenResponse.IsSuccessStatusCode)
					return BadRequest("Failed to exchange code with Google");

				var tokenData = JsonSerializer.Deserialize<JsonElement>(await tokenResponse.Content.ReadAsStringAsync());
				var accessToken = tokenData.GetProperty("access_token").GetString();

				var httpClient = _httpClientFactory.CreateClient();
				httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
				var peopleResponse = await httpClient.GetAsync("https://people.googleapis.com/v1/people/me?personFields=emailAddresses,names");

				if (!peopleResponse.IsSuccessStatusCode)
					return BadRequest("Failed to get user info from Google");

				var userData = JsonSerializer.Deserialize<JsonElement>(await peopleResponse.Content.ReadAsStringAsync());
				var email = userData.GetProperty("emailAddresses")[0].GetProperty("value").GetString();
				var name = userData.GetProperty("names")[0].GetProperty("displayName").GetString();

				if (string.IsNullOrEmpty(email))
					return Problem("Could not retrieve user email from Google.");

				var existingManager = await _dbContext.Managers
					.Where(m => m.email == email)
					.Select(m => new { m.ID, m.password })
					.FirstOrDefaultAsync();

				if (existingManager != null)
				{
					HttpContext.Session.SetInt32("UserId", existingManager.ID);
					HttpContext.Session.SetString("UserType", "manager");
					if (string.IsNullOrEmpty(existingManager.password))
						return RedirectToAction("Index5", "Dashboard", new { email = email });
					
					// Check manager's subscription status before redirecting
					var subscription = await _dbContext.ManagerSubscriptions
						.Where(s => s.ManagerId == existingManager.ID)
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
					
					return RedirectToAction("Index3", "Dashboard");
				}

				var existingWorker = await _dbContext.Workers
					.Where(w => w.email == email)
					.Select(w => new { w.ID, w.role, w.password })
					.FirstOrDefaultAsync();

				if (existingWorker != null)
				{
					HttpContext.Session.SetInt32("UserId", existingWorker.ID);
					HttpContext.Session.SetString("UserType", existingWorker.role ?? "worker");
					if (string.IsNullOrEmpty(existingWorker.password))
						return RedirectToAction("Index5", "Dashboard", new { email = email });
					return RedirectToAction("Index", "Orderworker");
				}

				var existingSupplier = await _dbContext.Suppliers
					.Where(s => s.email == email)
					.Select(s => new { s.SupplierId, s.Role, s.StoreId, s.password })
					.FirstOrDefaultAsync();

				if (existingSupplier != null)
				{
					HttpContext.Session.SetInt32("UserId", existingSupplier.SupplierId);
					HttpContext.Session.SetString("UserType", existingSupplier.Role ?? "supplier");
					if (string.IsNullOrEmpty(existingSupplier.password))
						return RedirectToAction("Index5", "Dashboard", new { email = email });
					if (existingSupplier.StoreId == null || existingSupplier.StoreId == 0)
					{
						return RedirectToAction("Stores", "SupplierDashboard");
					}
					return RedirectToAction("Index", "SupplierDashboard");
				}

				var role = HttpContext.Request.Cookies["GoogleUserRole"] ?? "worker";
				HttpContext.Session.SetString("GoogleEmail", email);

				if (role == "manager")
				{
					var newManager = new Manager
					{
						email = email,
						name = name ?? "Google User",
						phone = "",
						password = ""
					};

					_dbContext.Managers.Add(newManager);
					await _dbContext.SaveChangesAsync();
					return RedirectToAction("Index5", "Dashboard");
				}
                else if (role == "supplier")
                {
                    var newSupplier = new Supplier
                    {
                        email = email,
                        name = name ?? "Google Supplier",
                        password = "",
                        Role = "supplier",
                        ContactNumber = "",  
                        Address = "",
                        StoreId = null
                    };

                    _dbContext.Suppliers.Add(newSupplier);
                    await _dbContext.SaveChangesAsync();


                    HttpContext.Session.SetInt32("UserId", newSupplier.SupplierId);
                    HttpContext.Session.SetString("UserType", "supplier");
                    HttpContext.Session.SetString("GoogleEmail", email);

                    return RedirectToAction("Index5", "Dashboard", new { email = email });
                }


                else
                {
                    var newWorker = new Worker
                    {
                        email = email,
                        name = name ?? "Google User",
                        Phone_number = "",
                        Address = "",
                        Department = "",
                        password = "",
                        role = "worker"
                    };

                    _dbContext.Workers.Add(newWorker);
                    await _dbContext.SaveChangesAsync();

                    HttpContext.Session.SetInt32("UserId", newWorker.ID);
                    HttpContext.Session.SetString("UserType", "worker");
                    HttpContext.Session.SetString("GoogleEmail", email);

                    return RedirectToAction("Index5", "Dashboard", new { email = email });
                }

            }
            catch (Exception ex)
			{
				Console.WriteLine($"Error in GoogleCallback: {ex}");
				return RedirectToAction("Index", "Dashboard");
			}
		}

		[HttpPost("Auth/SetPasswordAfterGoogle")]
		public async Task<IActionResult> SetPasswordAfterGoogle(string email, string password, string confirmPassword)
		{

			try
			{
                if (string.IsNullOrEmpty(email))
                {

                    email = HttpContext.Session.GetString("GoogleEmail");
                }


                if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
				{
					ViewBag.Error = "Email and password are required";
					return RedirectToAction("Index5", "Dashboard");
				}

				if (password != confirmPassword)
				{
					ViewBag.Error = "Passwords do not match";
					ViewBag.GoogleEmail = email;
					return RedirectToAction("Index5", "Dashboard");
				}

				var worker = await _dbContext.Workers.FirstOrDefaultAsync(w => w.email == email);
				if (worker != null)
				{
					worker.password = HashPassword(password); 
					await _dbContext.SaveChangesAsync();

					HttpContext.Session.SetInt32("UserId", worker.ID);
					HttpContext.Session.SetString("UserType", "worker");
					
					// Set the worker's store ID in session
					if (worker.StoreId.HasValue)
					{
						HttpContext.Session.SetString("SelectedStoreId", worker.StoreId.Value.ToString());
						
						// Get store name for display
						var store = await _dbContext.Stores.FirstOrDefaultAsync(s => s.StoreId == worker.StoreId.Value);
						if (store != null)
						{
							HttpContext.Session.SetString("SelectedStore", store.StoreName);
						}
					}
					
					return RedirectToAction("Index", "Orderworker");
				}

				var manager = await _dbContext.Managers.FirstOrDefaultAsync(m => m.email == email);
				if (manager != null)
				{
					manager.password = HashPassword(password);
					await _dbContext.SaveChangesAsync();

					HttpContext.Session.SetInt32("UserId", manager.ID);
					HttpContext.Session.SetString("UserType", "manager");
					
					// Check manager's subscription status before redirecting
					var subscription = await _dbContext.ManagerSubscriptions
						.Where(s => s.ManagerId == manager.ID)
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
					
					return RedirectToAction("Index3", "Dashboard");
				}

				var supplier = await _dbContext.Suppliers.FirstOrDefaultAsync(s => s.email == email);
				if (supplier != null)
				{
					supplier.password = HashPassword(password);
					await _dbContext.SaveChangesAsync();
					HttpContext.Session.SetInt32("UserId", supplier.SupplierId);
					HttpContext.Session.SetString("UserType", "supplier");
					

					if (supplier.StoreId == null || supplier.StoreId == 0)
					{
						return RedirectToAction("Stores", "SupplierDashboard");
					}
					
					return RedirectToAction("Index", "SupplierDashboard");
				}

				ViewBag.Error = "User not found";
				return RedirectToAction("Index", "Dashboard");
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error in SetPasswordAfterGoogle: {ex}");
				ViewBag.Error = "An error occurred while setting password";
				ViewBag.GoogleEmail = email;
				return RedirectToAction("Index5", "Dashboard");
			}
		}


	}
}
