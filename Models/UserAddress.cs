using System;

namespace MOAClover.Models
{
    public class UserAddress
    {
        public int Id { get; set; } // PK            

        public string? UserId { get; set; }   // User연결 FK
        public User? User { get; set; }       // Navigation Property

        public string Address { get; set; } = string.Empty;            // 주소 (전체 주소)
        public string ZipCode { get; set; } = string.Empty;            // 우편번호
        public string? AddressDetail { get; set; }      // 상세주소 (선택)

        public bool IsDefault { get; set; } = false;   // 대표 배송지여부
    }
}