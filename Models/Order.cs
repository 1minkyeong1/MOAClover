using System;
using System.Collections.Generic;

namespace MOAClover.Models
{
    public class Order
    {
        public int OrderId { get; set; }

        public string OrderNo { get; set; } = "";

        public string? UserId { get; set; }

        public string? GuestCartId { get; set; }

        // 회원/비회원 구분
        public bool IsGuestOrder { get; set; }

        // 비회원 주문조회용 비밀번호 Hash
        public string? GuestOrderPasswordHash { get; set; }

        // 주문자 정보
        public string BuyerName { get; set; } = "";

        public string BuyerPhone { get; set; } = "";

        public string? BuyerEmail { get; set; }

        // 배송지 정보
        public string ReceiverName { get; set; } = "";

        public string ReceiverPhone { get; set; } = "";

        public string ZipCode { get; set; } = "";

        public string Address1 { get; set; } = "";

        public string? Address2 { get; set; }

        public string? DeliveryMemo { get; set; }

        // 금액 정보
        public int ProductTotal { get; set; }

        // 기본 배송비 + 지역 추가배송비가 합쳐진 최종 배송비
        public int ShippingFee { get; set; }

        // 제주/도서산간 등 지역 추가배송비
        public int ExtraShippingFee { get; set; }

        public int TotalPayAmount { get; set; }

        // 배송비 상태
        public bool IsCollectShipping { get; set; }

        public bool IsIncludedShipping { get; set; }

        // PendingPayment, Paid, Preparing, Shipped, Completed, Cancelled
        public string OrderStatus { get; set; } = "PendingPayment";

        // Ready, Paid, Failed, Cancelled, Refunded
        public string PaymentStatus { get; set; } = "Ready";

        // 결제수단
        public string PaymentMethod { get; set; } = "BankTransfer";

        // 재고 차감 여부
        public bool StockDeducted { get; set; } = false;

        // 배송조회용
        public string? TrackingCompany { get; set; }

        public string? TrackingNumber { get; set; }

        public DateTime? ShippedAt { get; set; }

        // 이메일 알림 기록
        public DateTime? OrderEmailSentAt { get; set; }

        // 관리자 전용 메모
        public string? AdminMemo { get; set; }

        public string CustomerMemo { get; set; } = "";

        public DateTime? AdminMemoUpdatedAt { get; set; }

        public string? AdminMemoUpdatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? PaidAt { get; set; }

        public DateTime? CancelledAt { get; set; }

        // 취소 사유
        public string? CancelReason { get; set; }

        public List<OrderItem> Items { get; set; } = new();
    }
}