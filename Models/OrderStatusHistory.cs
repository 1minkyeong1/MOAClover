using System;

namespace MOAClover.Models
{
    public class OrderStatusHistory
    {
        public int Id { get; set; }

        public int OrderId { get; set; }
        public Order? Order { get; set; }

        public string OrderNo { get; set; } = "";

        public string? BeforeOrderStatus { get; set; }
        public string? AfterOrderStatus { get; set; }

        public string? BeforePaymentStatus { get; set; }
        public string? AfterPaymentStatus { get; set; }

        public string? TrackingCompany { get; set; }
        public string? TrackingNumber { get; set; }

        public string? AdminMemo { get; set; }
        public string? CancelReason { get; set; }

        public string? ChangedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}