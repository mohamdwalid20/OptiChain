using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class SignUpViewModel
	{
		[Required(ErrorMessage = "Name is required")]
		[StringLength(100, ErrorMessage = "Name must be less than 100 characters")]
		[Display(Name = "Name")]
		public string Name { get; set; }

		[Required(ErrorMessage = "Email is required")]
		[EmailAddress(ErrorMessage = "Email is not valid")]
		[Display(Name = "Email")]
		public string Email { get; set; }

		[Required(ErrorMessage = "Password is required")]
		[MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
		[Display(Name = "Password")]
		[DataType(DataType.Password)]
		public string Password { get; set; }

		[Required(ErrorMessage = "Confirm Password is required")]
		[Compare("Password", ErrorMessage = "Password and Confirm Password do not match")]
		[Display(Name = "Confirm Password")]
		[DataType(DataType.Password)]
		public string ConfirmPassword { get; set; }

		[Required(ErrorMessage = "User Type is required")]
		[Display(Name = "User Type")]
		public string UserType { get; set; }

		[Display(Name = "Phone")]
		[Phone(ErrorMessage = "Phone is not valid")]
		public string Phone { get; set; }

		[Display(Name = "Address")]
		public string? Address { get; set; }

		[Display(Name = "Department")]
		public string? Department { get; set; }

		[Display(Name = "Profile Image")]
		public IFormFile? ProfileImage { get; set; }
	}
}
