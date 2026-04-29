using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class SupplierEditDto
	{
		public int SupplierId { get; set; }

		[Required]
		public string SupplierName { get; set; }

		[Required]
		public string ContactNumber { get; set; }

		[Required]
		[EmailAddress]
		public string Email { get; set; }
		public string Type { get; set; }

	}
}
