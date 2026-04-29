using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class Payment
    {
        [Key]
        public int ID { get; set; }
        
        [Required]
        public int ManagerId { get; set; }
        
        [Required]
        public int SubscriptionId { get; set; }
        
        [Required]
        public decimal Amount { get; set; }
        
        [Required]
        [StringLength(50)]
        public string PaymentMethod { get; set; } // "VodafoneCash", "Fawry", "Visa"
        
        [Required]
        [StringLength(20)]
        public string PaymentStatus { get; set; } = "Pending"; // "Pending", "Completed", "Failed"
        
        [StringLength(100)]
        [Required]
        public string TransactionId { get; set; }
        
        public string PaymentDetails { get; set; } // JSON string for payment details
        
        public DateTime? PaidAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual Manager Manager { get; set; }
        public virtual ManagerSubscription Subscription { get; set; }
    }
}
