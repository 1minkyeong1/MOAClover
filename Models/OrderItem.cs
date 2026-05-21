using System;

namespace MOAClover.Models
{
    public class OrderItem
    {
        public int OrderItemId { get; set; }

        public int OrderId { get; set; }

        public int ProductId { get; set; }

        // 주문 당시 상품명/가격을 복사해서 저장
        public string ProductName { get; set; } = "";

        public string? MainImageUrl { get; set; }

        public int UnitPrice { get; set; }

        public int? DiscountRate { get; set; }

        public int FinalUnitPrice { get; set; }

        public int Quantity { get; set; }

        public int LineTotal { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public Order? Order { get; set; }

        // 배송비
        public string ShippingType { get; set; } = "Included";

        public int ShippingFee { get; set; }

        public int? FreeShippingMinAmount { get; set; }

        public int JejuExtraFee { get; set; }

        public int RemoteAreaExtraFee { get; set; }
    }
}