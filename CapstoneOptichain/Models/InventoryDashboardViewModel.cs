namespace CapstoneOptichain.Models
{
	public class InventoryDashboardViewModel
	{
		public IEnumerable<Product> Products { get; set; }
		public IEnumerable<Category> Categories { get; set; }
		public InventorySummary InventorySummary { get; set; }
		public ProfitRevenueSummary ProfitRevenue { get; set; }
		public List<TopCategoryViewModel> TopCategories { get; set; }
		public List<TopProductViewModel> TopProducts { get; set; }
		public OverviewViewModel Overview { get; set; }
	}
}
