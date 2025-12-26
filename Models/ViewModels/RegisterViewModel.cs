using System;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class RegisterViewModel
    {
        // 계정 정보
        [Required(ErrorMessage = "아이디를 입력해주세요.")]
        [StringLength(20, MinimumLength = 4, ErrorMessage = "아이디는 4~20자로 입력해주세요.")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "아이디는 영문/숫자/_(언더바)만 사용할 수 있습니다.")]
        [Display(Name = "아이디")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "이메일을 입력해주세요.")]
        [EmailAddress(ErrorMessage = "올바른 이메일 형식이 아닙니다.")]
        [Display(Name = "이메일")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "비밀번호를 입력해주세요.")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "비밀번호는 최소 6자 이상 입력해주세요.")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "비밀번호는 영문과 숫자를 최소 1개 이상 포함해야 합니다.")]
        [Display(Name = "비밀번호")]
        public string? Password { get; set; }

        // ✅ 확인 비밀번호 (실시간으로 “불일치” 표시 가능)
        [Required(ErrorMessage = "비밀번호 확인을 입력해주세요.")]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "비밀번호가 일치하지 않습니다.")]
        [Display(Name = "비밀번호 확인")]
        public string? ConfirmPassword { get; set; }

        // 사용자 기본 정보
        [Required(ErrorMessage = "이름을 입력해주세요.")]
        [StringLength(30, ErrorMessage = "이름은 최대 30자까지 입력할 수 있습니다.")]
        [Display(Name = "이름")]
        public string? Name { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "생년월일")]
        public DateTime? BirthDate { get; set; }

        [Phone(ErrorMessage = "올바른 전화번호 형식이 아닙니다.")]
        [StringLength(20, ErrorMessage = "전화번호는 최대 20자까지 입력할 수 있습니다.")]
        [Display(Name = "전화번호")]
        public string? Phone { get; set; }

        // 주소
        [StringLength(10, ErrorMessage = "우편번호가 너무 깁니다.")]
        [Display(Name = "우편번호")]
        public string? ZipCode { get; set; }

        [StringLength(200, ErrorMessage = "주소가 너무 깁니다.")]
        [Display(Name = "주소")]
        public string? Address { get; set; }

        [StringLength(200, ErrorMessage = "상세주소가 너무 깁니다.")]
        [Display(Name = "상세주소")]
        public string? AddressDetail { get; set; }
    }
}

//회원가입용