using Microsoft.AspNetCore.Identity;
using MOAClover.Models;
using System;

public class User : IdentityUser
{
    // 사용자 기본 정보
    public string Name { get; set; } = string.Empty;                 // 이름
    public DateTime? BirthDate { get; set; }         // 생년월일
    public string Phone { get; set; } = string.Empty;                // 전화번호

    // 계정 상태
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;     // 등록일
    public DateTime? UpdatedAt { get; set; }                       // 수정일
    public DateTime? DeletedAt { get; set; }                       // 삭제일
    public bool IsActive { get; set; } = true;                     // 계정 활성화여부

    // 주소 연결
    public ICollection<UserAddress> Addresses { get; set; }
    = new List<UserAddress>();

}
