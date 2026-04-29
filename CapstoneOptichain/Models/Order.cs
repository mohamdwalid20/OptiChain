using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class Order
	{
        [Key]
        public int OrderId { get; set; }

        [Required]
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpectedDeliveryDate { get; set; }

        [Required]
        public string Status { get; set; } // Pending, PendingPrice, PriceProposed, AcceptedBySupplier, RejectedBySupplier, AwaitingManagerApproval

        [Required]
        public string OrderType { get; set; }

        public int StoreId { get; set; }
        public virtual Store Store { get; set; } 

        public int? SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; } 

        public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        
        // New field to track if all suppliers have proposed prices
        public bool AllSuppliersProposed { get; set; } = false;
        
        // Deadline for suppliers to propose prices
        public DateTime? PriceProposalDeadline { get; set; }
    }
}
