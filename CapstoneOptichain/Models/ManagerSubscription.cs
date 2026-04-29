using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class ManagerSubscription
    {
        [Key]
        public int ID { get; set; }
        
        [Required]
        public int ManagerId { get; set; }
        
        public Manager Manager { get; set; }
        
        [Required]
        public DateTime SubscriptionStartDate { get; set; } = DateTime.UtcNow;
        
        [Required]
        public DateTime SubscriptionEndDate { get; set; } = DateTime.UtcNow.AddMonths(1);
        
        [Required]
        public decimal Amount { get; set; } = 99.99m;
        
        [Required]
        public string Status { get; set; } = "Active"; // Active, Expired, Cancelled
        
        public DateTime? LastPaymentDate { get; set; } = null;
        
        public DateTime? NextPaymentDate { get; set; } = null;
        
        public string? PaymentMethod { get; set; } = null;
        
        public string? TransactionId { get; set; } = null;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; } = null;
        
        // Subscription plan details
        public string PlanType { get; set; } = "Monthly"; // Monthly, Quarterly, Yearly
        
        public int MaxStores { get; set; } = 50;
        
        public int MaxWorkersPerStore { get; set; } = 20;
        
        public bool IsActive => Status == "Active" && SubscriptionEndDate > DateTime.UtcNow;
    }
}
