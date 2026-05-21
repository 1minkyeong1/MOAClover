using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class FindGuestOrderNoViewModel
    {
        [Required(ErrorMessage = "주문자 이름을 입력해주세요.")]
        public string BuyerName { get; set; } = "";

        [Required(ErrorMessage = "주문자 연락처를 입력해주세요.")]
        public string BuyerPhone { get; set; } = "";

        [Required(ErrorMessage = "이메일을 입력해주세요.")]
        [EmailAddress(ErrorMessage = "이메일 형식이 올바르지 않습니다.")]
        public string BuyerEmail { get; set; } = "";

        public bool Searched { get; set; }

        public List<GuestOrderNoResultItem> Results { get; set; } = new();
    }

    public class GuestOrderNoResultItem
    {
        public string OrderNo { get; set; } = "";

        public string MaskedOrderNo
        {
            get
            {
                if (string.IsNullOrWhiteSpace(OrderNo) || OrderNo.Length < 8)
                    return OrderNo;

                return OrderNo.Substring(0, 8) + "****" + OrderNo.Substring(OrderNo.Length - 4);
            }
        }

        public DateTime CreatedAt { get; set; }

        public int TotalPayAmount { get; set; }

        public string OrderStatus { get; set; } = "";

        public string PaymentStatus { get; set; } = "";
    }
}