using System;

namespace MOAClover.Models
{
    public class Product
    {
        public int ProductId { get; set; } // PK

        public string Name { get; set; } = string.Empty; // 상품명
        public string? Description { get; set; } // 상품설명
        public int Price { get; set; } // 가격
        public int? DiscountRate { get; set; } // 할인율 %

        // 재고 수량
        public int StockQuantity { get; set; } = 0;

        // 재고 관리 여부
        public bool UseStock { get; set; } = false;

        // 배송비 설정
        public string ShippingType { get; set; } = "Fixed";
        public int ShippingFee { get; set; } = 4000;
        public int? FreeShippingMinAmount { get; set; }
        public int JejuExtraFee { get; set; } = 0;
        public int RemoteAreaExtraFee { get; set; } = 0;

        public int CategoryId { get; set; } // FK 카테고리아이디

        // 외부몰 상품 연결 URL
        public string? SmartStoreUrl { get; set; } // 스마트스토어 상품 URL
        public string? CoupangUrl { get; set; } // 쿠팡 상품 URL

        public DateTime CreatedAt { get; set; } = DateTime.Now; // 등록일
        public DateTime? UpdatedAt { get; set; } // 수정일
        public bool IsVisible { get; set; } = true; // 노출여부
        public DateTime? DeletedAt { get; set; } // 삭제일
    }
}