using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class Supplier
	{
		[Key]
		public int SupplierId { get; set; }
		[Required]
		public string name { get; set; }
		public string ContactNumber { get; set; }
		[EmailAddress]
		public string email { get; set; }

		[Required]
		public string password { get; set; } 

		[Required]
		public string Role { get; set; } = "supplier"; 
		public string Address { get; set; }

		public int? StoreId { get; set; }
		public Store Store { get; set; }

		public ICollection<SupplierProduct> SupplierProducts { get; set; }

		public string? ProfileImageUrl { get; set; }
	}
}
