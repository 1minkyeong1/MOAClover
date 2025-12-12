using System;

namespace MOAClover.Models
{
    public class Product
    {
        public int ProductId { get; set; } // PK

        public string Name { get; set; }           // 상품명
        public string? Description { get; set; }   // 상품설명
        public int Price { get; set; }             // 가격

        public int CategoryId { get; set; }  // FK  카테고리아이디

        public DateTime CreatedAt { get; set; } = DateTime.Now;    // 등록일
        public DateTime? UpdatedAt { get; set; }                   // 수정일
        public bool IsVisible { get; set; } = true;                // 노출여부
        public DateTime? DeletedAt { get; set; }                   // 삭제일
    }
}
