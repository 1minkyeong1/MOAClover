namespace MOAClover.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public int TodayOrderCount { get; set; }

        public int TodayPaidAmount { get; set; }

        public int PendingPaymentCount { get; set; }

        public int PreparingCount { get; set; }

        public int CancelRequestedCount { get; set; }

        public int ReturnExchangeRequestedCount { get; set; }

        public int WaitingQnaCount { get; set; }

        public int SoldoutProductCount { get; set; }

        public int LowStockProductCount { get; set; }

        public int HiddenProductCount { get; set; }

        public List<AdminDashboardOrderItemViewModel> RecentOrders { get; set; } = new();

        public List<AdminDashboardQnaItemViewModel> WaitingQnas { get; set; } = new();

        public List<AdminDashboardProductItemViewModel> StockIssues { get; set; } = new();

        public List<AdminDashboardIssueOrderViewModel> IssueOrders { get; set; } = new();
    }

    public class AdminDashboardOrderItemViewModel
    {
        public string OrderNo { get; set; } = "";

        public string BuyerName { get; set; } = "";

        public int TotalPayAmount { get; set; }

        public string OrderStatus { get; set; } = "";

        public string PaymentStatus { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }

    public class AdminDashboardQnaItemViewModel
    {
        public int ProductId { get; set; }

        public string ProductName { get; set; } = "";

        public string UserName { get; set; } = "";

        public string Question { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public bool IsSecret { get; set; }
    }

    public class AdminDashboardProductItemViewModel
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = "";

        public int StockQuantity { get; set; }

        public bool IsSoldout => StockQuantity <= 0;
    }

    public class AdminDashboardIssueOrderViewModel
    {
        public string OrderNo { get; set; } = "";

        public string BuyerName { get; set; } = "";

        public int TotalPayAmount { get; set; }

        public string OrderStatus { get; set; } = "";

        public string PaymentStatus { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}