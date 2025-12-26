using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class AddressEditVm
    {
        public int Id { get; set; } // 0이면 추가

        [Required]
        public string ZipCode { get; set; } = "";

        [Required]
        public string Address { get; set; } = "";

        public string? AddressDetail { get; set; }

        public bool IsDefault { get; set; }
    }
}

// 수정 창 주소용