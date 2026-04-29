using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class Worker
	{
		public int ID { get; set; }

		[Required]
		public string name { get; set; } = string.Empty;

		[Required]
		[EmailAddress]
		public string email { get; set; } = string.Empty;

		public string? password { get; set; }

		public string? Phone_number { get; set; }

		public string? Address { get; set; }

		public string? Department { get; set; }

		[Required]
		public string role { get; set; } = "worker";

		public string? ProfileImageUrl { get; set; }

		// Foreign key for Store
		public int? StoreId { get; set; }
		public Store Store { get; set; }

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		public bool IsActive { get; set; } = true;
	}
}
