using System;
using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class Notification
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Message { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? SupplierId { get; set; }
        public int? StoreId { get; set; }
        public string NotificationType { get; set; } = "Manager"; // Manager or Supplier
    }
} 