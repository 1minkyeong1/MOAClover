using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class AddressEditVm
    {
        public int Id { get; set; }

        public string ReceiverName { get; set; } = "";

        public string ReceiverPhone { get; set; } = "";

        [Required(ErrorMessage = "우편번호를 입력해주세요.")]
        public string ZipCode { get; set; } = "";

        [Required(ErrorMessage = "주소를 입력해주세요.")]
        public string Address { get; set; } = "";

        public string? AddressDetail { get; set; }

        public bool IsDefault { get; set; }
    }
}

// 수정 창 주소용