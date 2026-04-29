using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class Manager
	{
		public int ID { get; set; }
		
		[Required]
		public string name { get; set; }
		
		[Required]
		[EmailAddress]
		public string email { get; set; }
		
		[Required]
		public string password { get; set; }
		
		public string? phone { get; set; }
		
		public string? ProfileImageUrl { get; set; }
		
		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
		
		public bool IsActive { get; set; } = true;
		
		// Navigation properties
		public ManagerSubscription? ManagerSubscription { get; set; }
		public List<Store> Stores { get; set; } = new List<Store>();
	}
}
