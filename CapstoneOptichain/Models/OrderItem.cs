using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class OrderItem
	{
		[Key]
		public int OrderItemId { get; set; }

		[ForeignKey("Order")]
		public int OrderId { get; set; }
		public Order Order { get; set; }

		[ForeignKey("Product")]
		public int ProductId { get; set; }
		public Product Product { get; set; }

		[Required]
		[Range(1, int.MaxValue)]
		public int Quantity { get; set; }

			[Required]
	[Range(0.01, double.MaxValue)]
	public decimal OrderValue { get; set; }

	// Supplier proposed price (nullable until supplier sets it)
	public decimal? ProposedPrice { get; set; }

	[Required]
	public DateTime OrderDate { get; set; } = DateTime.UtcNow;

	}
}
