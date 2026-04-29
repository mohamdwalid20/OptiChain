using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class LoginViewModel
	{
		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Email is not valid")]
		[Display(Name = "Email")]
		public string Email { get; set; }

		[Required(ErrorMessage = "Password is required")]
		[MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
		[Display(Name = "Password")]
		public string Password { get; set; }
	}
}
