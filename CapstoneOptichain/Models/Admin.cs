using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class Admin
    {
        [Key]
        public int ID { get; set; } = 1; // Static admin ID
        
        [Required]
        [StringLength(100)]
        public string name { get; set; } = "System Admin";
        
        [Required]
        [EmailAddress]
        public string email { get; set; } = "admin@optichain.com";
        
        [Required]
        [StringLength(100)]
        public string password { get; set; } = "Admin@123"; // Default password
        
        public string? ProfileImageUrl { get; set; }
        
        public DateTime CreatedAt { get; set; } = new DateTime(2024, 1, 1); // Static date
        
        public bool IsActive { get; set; } = true;
        
        // Admin specific properties
        public decimal MonthlySubscriptionFee { get; set; } = 99.99m;
        
        public int MaxManagersAllowed { get; set; } = 1000;
        
        public string SystemVersion { get; set; } = "1.0.0";
        
        public DateTime? LastSystemUpdate { get; set; }
        
        public string? SystemSettings { get; set; }
    }
}
