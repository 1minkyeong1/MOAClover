using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class MyPageViewModel
    {
        // ✅ 수정 불가(표시용)
        public string UserName { get; set; } = "";

        // ✅ 수정 가능
        [Required]
        public string Name { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Required]
        public string Phone { get; set; } = "";   // User.Phone 사용

        [EmailAddress]
        public string? Email { get; set; }

        // ✅ 비밀번호 변경
        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [MinLength(6)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        public string? ConfirmNewPassword { get; set; }

        // ✅ 배송지 목록
        public List<AddressItemVm> Addresses { get; set; } = new();
    }

    public class AddressItemVm
    {
        public int Id { get; set; }
        public string ZipCode { get; set; } = "";
        public string Address { get; set; } = "";
        public string? AddressDetail { get; set; }
        public bool IsDefault { get; set; }
    }
}


//마이페이지