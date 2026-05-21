namespace MOAClover.Models.ViewModels
{
    public class AdminOrderAlertViewModel
    {
        public int IssueCount { get; set; }

        public int CancelRequestedCount { get; set; }

        public int ReturnRequestedCount { get; set; }

        public int ExchangeRequestedCount { get; set; }

        public int PendingPaymentCount { get; set; }

        public int PreparingCount { get; set; }

        public int ReturnExchangeRequestedCount
        {
            get
            {
                return ReturnRequestedCount + ExchangeRequestedCount;
            }
        }
    }
}