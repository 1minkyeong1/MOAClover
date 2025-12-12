using System;

namespace MOAClover.Models
{
    public class Media
    {
        public int MediaId { get; set; }  // PK

        public int ProductId { get; set; }  // FK     상품아이디
        public string MediaType { get; set; }      // 미디어 종류 (thumbnail, image, video 등)
        public string FileUrl { get; set; }        // 파일 저장 경로
        public int SortOrder { get; set; } = 0;    // 노출 순서

        public DateTime CreatedAt { get; set; } = DateTime.Now;     // 등록일
        public bool IsActive { get; set; } = true;                  // 활성여부
        public DateTime? DeletedAt { get; set; }                    // 삭제일 (NULL 또는 삭제 시 날짜)
    }
}
