namespace CapstoneOptichain.Models
{
    public class AdminDashboardViewModel
    {
        public int TotalManagers { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int ExpiredSubscriptions { get; set; }
        public decimal TotalRevenue { get; set; }
        public List<Manager> RecentManagers { get; set; } = new List<Manager>();
        public List<ManagerSubscription> ExpiringSubscriptions { get; set; } = new List<ManagerSubscription>();
    }
}
