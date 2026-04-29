using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class SupplierProduct
	{
		[Key]
		public int SupplierProductId { get; set; }

		[ForeignKey("Supplier")]
		public int SupplierId { get; set; }
		public Supplier Supplier { get; set; }

		[ForeignKey("Product")]
		public int ProductId { get; set; }
		public Product Product { get; set; }

		public decimal BuyingPrice { get; set; }
	}
}
