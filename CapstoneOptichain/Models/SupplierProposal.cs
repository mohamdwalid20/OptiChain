using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
    public class SupplierProposal
    {
        [Key]
        public int ProposalId { get; set; }

        [Required]
        public int OrderId { get; set; }
        public virtual Order Order { get; set; }

        [Required]
        public int SupplierId { get; set; }
        public virtual Supplier Supplier { get; set; }

        [Required]
        public DateTime ProposedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public decimal TotalAmount { get; set; }

        [Required]
        public string Status { get; set; } = "Pending"; // Pending, Accepted, Rejected

        public string? Notes { get; set; }

        public DateTime? ResponseDate { get; set; }

        public string? ManagerResponse { get; set; }

        // Collection of individual item proposals
        public virtual ICollection<ProposalItem> ProposalItems { get; set; } = new List<ProposalItem>();
    }

    public class ProposalItem
    {
        [Key]
        public int ItemId { get; set; }

        [Required]
        public int ProposalId { get; set; }
        public virtual SupplierProposal Proposal { get; set; }

        [Required]
        public int ProductId { get; set; }
        public virtual Product Product { get; set; }

        [Required]
        public int Quantity { get; set; }

        [Required]
        public decimal UnitPrice { get; set; }

        [Required]
        public decimal TotalPrice { get; set; }

        public string? Notes { get; set; }
    }
}
