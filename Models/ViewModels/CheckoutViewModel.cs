using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace MOAClover.Models.ViewModels
{
    public class CheckoutViewModel
    {
        public List<CheckoutItemViewModel> Items { get; set; } = new();

        public bool IsGuestOrder { get; set; }

        public List<CheckoutSavedAddressViewModel> SavedAddresses { get; set; } = new();

        public int? SelectedAddressId { get; set; }

        public bool SaveAddressToMyPage { get; set; }

        public bool SetAsDefaultAddress { get; set; }

        [Required(ErrorMessage = "주문자 이름을 입력해주세요.")]
        public string BuyerName { get; set; } = "";

        [Required(ErrorMessage = "주문자 연락처를 입력해주세요.")]
        public string BuyerPhone { get; set; } = "";

        [EmailAddress(ErrorMessage = "이메일 형식이 올바르지 않습니다.")]
        public string? BuyerEmail { get; set; }

        [Required(ErrorMessage = "받는 사람 이름을 입력해주세요.")]
        public string ReceiverName { get; set; } = "";

        [Required(ErrorMessage = "받는 사람 연락처를 입력해주세요.")]
        public string ReceiverPhone { get; set; } = "";

        [Required(ErrorMessage = "우편번호를 입력해주세요.")]
        public string ZipCode { get; set; } = "";

        [Required(ErrorMessage = "주소를 입력해주세요.")]
        public string Address1 { get; set; } = "";

        public string? Address2 { get; set; }

        public string? DeliveryMemo { get; set; }

        public string? GuestOrderPassword { get; set; }

        public string? GuestOrderPasswordConfirm { get; set; }

        public int ProductTotal => Items.Sum(x => x.LineTotal);

        // 배송비관련 (도서산간은 아직 추가안함)
        public bool HasCollectShipping
        {
            get
            {
                return Items.Any(x => x.ShippingType == "Collect");
            }
        }

        public bool HasIncludedShipping
        {
            get
            {
                return Items.Any() && Items.All(x => x.ShippingType == "Included");
            }
        }

        public int BaseShippingFee
        {
            get
            {
                if (!Items.Any())
                    return 0;

                if (HasCollectShipping)
                    return 0;

                var fees = Items.Select(item =>
                {
                    if (item.ShippingType == "Included")
                        return 0;

                    if (item.ShippingType == "Free")
                        return 0;

                    if (item.ShippingType == "ConditionalFree")
                    {
                        var min = item.FreeShippingMinAmount ?? 0;

                        if (min > 0 && item.LineTotal >= min)
                            return 0;

                        return item.ShippingFee;
                    }

                    return item.ShippingFee;
                });

                return fees.DefaultIfEmpty(0).Max();
            }
        }

        public int ExtraShippingFee
        {
            get
            {
                if (!Items.Any())
                    return 0;

                if (HasCollectShipping)
                    return 0;

                if (string.IsNullOrWhiteSpace(Address1))
                    return 0;

                if (Address1.Contains("제주"))
                {
                    return Items
                        .Where(x => x.ShippingType == "Fixed" || x.ShippingType == "ConditionalFree")
                        .Select(x => x.JejuExtraFee)
                        .DefaultIfEmpty(0)
                        .Max();
                }

                return 0;
            }
        }

        public int ShippingFee
        {
            get
            {
                if (HasCollectShipping)
                    return 0;

                return BaseShippingFee + ExtraShippingFee;
            }
        }

        public int TotalPayAmount => ProductTotal + ShippingFee;

        // 결제수단
        public string PaymentMethod { get; set; } = "BankTransfer";

    }

    public class CheckoutSavedAddressViewModel
    {
        public int Id { get; set; }

        public string ReceiverName { get; set; } = "";
        public string ReceiverPhone { get; set; } = "";

        public string ZipCode { get; set; } = "";

        public string Address { get; set; } = "";

        public string? AddressDetail { get; set; }

        public bool IsDefault { get; set; }

        public string FullAddress
        {
            get
            {
                if (string.IsNullOrWhiteSpace(AddressDetail))
                    return $"({ZipCode}) {Address}";

                return $"({ZipCode}) {Address} {AddressDetail}";
            }
        }
    }
}