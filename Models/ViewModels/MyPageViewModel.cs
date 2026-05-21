using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class MyPageViewModel
    {
        public string UserName { get; set; } = "";

        [Required]
        public string Name { get; set; } = "";

        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Required]
        public string Phone { get; set; } = "";

        [EmailAddress]
        public string? Email { get; set; }

        [DataType(DataType.Password)]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [MinLength(6)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        public string? ConfirmNewPassword { get; set; }

        public List<AddressItemVm> Addresses { get; set; } = new();
    }

    public class AddressItemVm
    {
        public int Id { get; set; }

        public string ReceiverName { get; set; } = "";
        public string ReceiverPhone { get; set; } = "";

        public string ZipCode { get; set; } = "";
        public string Address { get; set; } = "";
        public string? AddressDetail { get; set; }

        public bool IsDefault { get; set; }
    }

}

// 마이페이지