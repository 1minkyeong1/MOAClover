namespace MOAClover.Helpers
{
    public static class OrderStatusHelper
    {
        public static string OrderStatusText(string? status)
        {
            return status switch
            {
                "PendingPayment" => "결제대기",
                "Paid" => "결제완료",
                "Preparing" => "배송준비중",
                "Shipped" => "배송중",
                "Completed" => "배송완료",

                "CancelRequested" => "취소요청",
                "Cancelled" => "취소",

                "ReturnRequested" => "반품요청",
                "ExchangeRequested" => "교환요청",
                "Returned" => "반품완료",
                "Exchanged" => "교환완료",
                "RequestRejected" => "요청거절",

                _ => status ?? ""
            };
        }

        public static string PaymentStatusText(string? status)
        {
            return status switch
            {
                "Ready" => "결제대기",
                "Paid" => "결제완료",
                "Failed" => "결제실패",
                "Cancelled" => "결제취소",
                "Refunded" => "환불완료",
                _ => status ?? ""
            };
        }

        public static string OrderStatusClass(string? status)
        {
            return status switch
            {
                "PendingPayment" => "pending",
                "Paid" => "paid",
                "Preparing" => "preparing",
                "Shipped" => "shipped",
                "Completed" => "completed",

                "CancelRequested" => "status-cancel-requested",
                "Cancelled" => "cancelled",

                "ReturnRequested" => "status-return-requested",
                "ExchangeRequested" => "status-exchange-requested",
                "Returned" => "status-returned",
                "Exchanged" => "status-exchanged",
                "RequestRejected" => "status-request-rejected",

                _ => "pending"
            };
        }

        public static string PaymentStatusClass(string? status)
        {
            return status switch
            {
                "Ready" => "ready",
                "Paid" => "paid",
                "Failed" => "failed",
                "Cancelled" => "cancelled",
                "Refunded" => "refunded",
                _ => "ready"
            };
        }
    }
}