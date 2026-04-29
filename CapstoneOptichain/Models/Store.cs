using System.ComponentModel.DataAnnotations;

namespace CapstoneOptichain.Models
{
	public class Store
	{

        [Key]
        public int StoreId { get; set; }

        [Required]
        public string StoreName { get; set; }

        [Required]
        public string BranchName { get; set; }
        
        [Required]
        public string Address { get; set; }
        
        [Required]
        public string ContactNumber { get; set; }

        // Foreign key for Manager
        public int? ManagerId { get; set; }
        public Manager Manager { get; set; }

        public virtual ICollection<Order> Orders { get; set; }
        public virtual ICollection<Product> Products { get; set; }
        public virtual ICollection<Inventory> Inventories { get; set; }
        public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
        public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();

    }
}
