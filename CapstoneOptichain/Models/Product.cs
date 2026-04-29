using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CapstoneOptichain.Models
{
	public class Product
	{
        [Key]
        public int ProductId { get; set; }

        [Required]
        public string ProductName { get; set; }

        [ForeignKey("Category")]
        public int CategoryId { get; set; }
        public virtual Category Category { get; set; } 

        public decimal BuyingPrice { get; set; }
        public int Quantity { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string ImagePath { get; set; }

        [Required]
        public int StoreId { get; set; }
        public virtual Store Store { get; set; }


        public virtual ICollection<Inventory> Inventories { get; set; }

    }
}
