using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class ProductCreateDto
	{

		public int ProductId { get; set; }


		[Required]
			public string ProductName { get; set; }

			[Required]
			public int CategoryId { get; set; }

			[Required]
			[Range(0.01, double.MaxValue)]
			public decimal BuyingPrice { get; set; }


			[Required]
			[Range(0, int.MaxValue)]
			public int Quantity { get; set; }

			public DateTime? ExpiryDate { get; set; }



			public IFormFile ProductImage { get; set; }
			public int? WorkerId { get; set; }
			public int StoreId { get; set; }



	}
}
