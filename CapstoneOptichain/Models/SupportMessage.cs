using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class SupportMessage
	{
		[Key]
		public int Id { get; set; }

		// Conversation is per manager with admin
		[Required]
		public int ManagerId { get; set; }
		public Manager Manager { get; set; }

		[Required]
		[MaxLength(20)]
		public string SenderType { get; set; } = "manager"; // "manager" or "admin"

		[Required]
		[MaxLength(2000)]
		public string Content { get; set; }

		public bool IsRead { get; set; } = false;
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
	}
}


