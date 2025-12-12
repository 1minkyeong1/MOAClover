using System;

namespace MOAClover.Models
{
    public class Category
    {
        public int CategoryId { get; set; }  // PK
        public string Name { get; set; } = string.Empty;     // 카테고리명
        public string? Description { get; set; }             // 카테고리설명

        public int? ParentCategoryId { get; set; }          // 상위 카테고리 ID (없으면 NULL)

        public DateTime CreatedAt { get; set; } = DateTime.Now;   // 생성일
        public DateTime? UpdatedAt { get; set; }                  // 수정일
        public bool IsActive { get; set; } = true;                // 활성여부
        public DateTime? DeletedAt { get; set; }                  // 삭제일 (NULL 또는 삭제 시 날짜)
    }
}
