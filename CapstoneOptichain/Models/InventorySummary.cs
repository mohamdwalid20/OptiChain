namespace CapstoneOptichain.Models
{
	public class InventorySummary
	{
		public int? TotalCategories { get; set; }
		public int? TotalProducts { get; set; }
		public int? TopSelling { get; set; }
		public decimal? Revenue { get; set; }
		public decimal? Cost { get; set; }
		public int? LowStockItems { get; set; }

        public int TotalInventory { get; set; }
        public int ToBeReceived { get; set; }
    }
}
