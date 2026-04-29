using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class OrderRequest
	{
		[Key]
		public int RequestId { get; set; }

		[Required]
		public int WorkerId { get; set; }
		public virtual Worker Worker { get; set; }

		[Required]
		public int OrderId { get; set; }
		public virtual Order Order { get; set; }

		[Required]
		public DateTime RequestDate { get; set; } = DateTime.UtcNow;

		[Required]
		public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected

		public string? ManagerNotes { get; set; }

		public DateTime? ResponseDate { get; set; }

		public int? ManagerId { get; set; }
		public virtual Manager Manager { get; set; }
	}
}
