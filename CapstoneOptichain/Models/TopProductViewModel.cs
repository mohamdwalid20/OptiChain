namespace CapstoneOptichain.Models
{
	public class TopProductViewModel
	{
		public string ProductName { get; set; }
		public int ProductId { get; set; }
		public string Category { get; set; }
		public int RemainingQuantity { get; set; }
		public decimal TurnOver { get; set; }
		public decimal PercentageIncrease { get; set; }
        public int QuantitySold { get; set; }
        public decimal TotalValue { get; set; }


    }
}
