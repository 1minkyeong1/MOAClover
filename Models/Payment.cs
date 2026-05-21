using System;

namespace MOAClover.Models
{
    public class Payment
    {
        public int PaymentId { get; set; }

        public int OrderId { get; set; }

        public string OrderNo { get; set; } = "";

        // TossPayments, Manual 등
        public string Provider { get; set; } = "Manual";

        public string PaymentStatus { get; set; } = "Ready";

        public int Amount { get; set; }

        public string? PaymentKey { get; set; }

        public string? PayMethod { get; set; }

        public string? CardCompany { get; set; }

        public string? CardNumber { get; set; }

        public string? ReceiptUrl { get; set; }

        public string? FailureCode { get; set; }

        public string? FailureMessage { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public string? Method { get; set; }

        public string? RawResponse { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Order? Order { get; set; }
    }
}