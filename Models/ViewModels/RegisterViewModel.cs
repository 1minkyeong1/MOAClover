using System;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models
{
    public class RegisterViewModel
    {
        // 계정 정보
        [Required]
        [Display(Name = "아이디")]
        public string UserName { get; set; }

        [Required]
        [EmailAddress]
        [Display(Name = "이메일")]
        public string Email { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Display(Name = "비밀번호")]
        public string Password { get; set; }

        // 사용자 기본 정보
        [Required]
        [Display(Name = "이름")]
        public string Name { get; set; }

        [Display(Name = "생년월일")]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "전화번호")]
        public string Phone { get; set; }

        // 주소
        [Display(Name = "우편번호")]
        public string ZipCode { get; set; }

        [Display(Name = "주소")]
        public string Address { get; set; }

        [Display(Name = "상세주소")]
        public string AddressDetail { get; set; }
    }
}
