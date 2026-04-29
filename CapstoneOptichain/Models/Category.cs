using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class Category
	{
		[Key]
		public int CategoryId { get; set; }
		[Required]
		public string CategoryName { get; set; }
		public int? StoreId { get; set; } // Make categories store-specific
		public Store Store { get; set; }
		public ICollection<Product> Products { get; set; }
	}
}
