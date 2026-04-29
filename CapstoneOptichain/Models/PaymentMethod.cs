using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class PaymentMethod
    {
        [Key]
        public int ID { get; set; }
        
        [Required]
        public int ManagerId { get; set; }
        
        public Manager Manager { get; set; }
        
        [Required]
        public string PaymentType { get; set; } // Visa, MasterCard, VodafoneCash, Fawry
        public string CardType { get; set; } // Visa, MasterCard (for credit cards)
        public string LastFourDigits { get; set; } // Last 4 digits for cards
        public string ExpiryMonth { get; set; } // For cards
        public string ExpiryYear { get; set; } // For cards
        public string CardholderName { get; set; } // For cards
        public string VodafoneCashNumber { get; set; } // For Vodafone Cash
        public string FawryNumber { get; set; } // For Fawry
        public string FawryEmail { get; set; } // For Fawry
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Masked card number for display
        public string DisplayCardNumber => $"**** **** **** {LastFourDigits}";
        
        // Formatted expiry date for cards
        public string DisplayExpiry => $"{ExpiryMonth}/{ExpiryYear}";
        
        // Display for Vodafone Cash
        public string DisplayVodafoneCash => $"Vodafone Cash: {VodafoneCashNumber}";
        
        // Display for Fawry
        public string DisplayFawry => $"Fawry: {FawryNumber}";
    }
}
