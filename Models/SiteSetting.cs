using System;

namespace MOAClover.Models
{
    public class SiteSetting
    {
        public int SiteSettingId { get; set; }

        public string ShopName { get; set; } = "모아클로버";

        public string OwnerName { get; set; } = "";

        public string BusinessNumber { get; set; } = "";

        public string MailOrderSalesNumber { get; set; } = "";

        public string CustomerPhone { get; set; } = "";

        public string CustomerEmail { get; set; } = "";

        public string Address { get; set; } = "";

        public string BankName { get; set; } = "";

        public string BankAccountNumber { get; set; } = "";

        public string BankAccountHolder { get; set; } = "";

        public string BankTransferNotice { get; set; } = "";

        public string CustomerServiceHours { get; set; } = "";

        public string DefaultDeliveryCompany { get; set; } = "";

        public string ShippingNotice { get; set; } = "";

        public string FreeShippingNotice { get; set; } = "";

        public string ReturnAddress { get; set; } = "";

        public string ReturnShippingFeeNotice { get; set; } = "";

        public string ReturnExchangeNotice { get; set; } = "";

        // =========================
        // 반품/교환 기본 정책 설정
        // =========================

        public int ReturnShippingFee { get; set; } = 3000;

        public int ExchangeShippingFee { get; set; } = 6000;

        public int ReturnExchangeAvailableDays { get; set; } = 7;

        public string? JejuExtraShippingFeeNotice { get; set; }

        public string? RemoteAreaExtraShippingFeeNotice { get; set; }


        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // =========================
        // 사이트 디자인 설정
        // =========================

        public string? HeaderBackgroundColor { get; set; }

        public string? HeaderTextColor { get; set; }

        public string? HeaderBackgroundImageUrl { get; set; }

        public bool UseHeaderBackgroundImage { get; set; } = false;

        public string? LogoImageUrl { get; set; }

        public string? CategoryBackgroundColor { get; set; }

        public string? CategoryBackgroundImageUrl { get; set; }

        public bool UseCategoryBackgroundImage { get; set; } = false;

        public string? CategoryTextColor { get; set; }

        public string? CategoryActiveTextColor { get; set; }

        public string? CategoryActiveBackgroundColor { get; set; }

        public string? FooterBackgroundColor { get; set; }

        public string? FooterTextColor { get; set; }

        public string? FooterBackgroundImageUrl { get; set; }

        public bool UseFooterBackgroundImage { get; set; } = false;

        // =========================
        // 메인 광고 / 배너 설정
        // =========================

        public bool UseMainHeroBanner { get; set; } = false;

        public string? MainHeroTitle { get; set; }

        public string? MainHeroSubtitle { get; set; }

        public string? MainHeroImageUrl { get; set; }

        public string? MainHeroVideoUrl { get; set; }

        public string? MainHeroLinkUrl { get; set; }

        public string? MainHeroButtonText { get; set; }

        public string? MainHeroBackgroundColor { get; set; }

        public bool UseMainHeroSlider { get; set; } = false;

        public string? MainHeroSliderImageUrls { get; set; }

        public string? MainHeroSliderLinkUrls { get; set; }


        // =========================
        // 고객 안내 페이지 디자인 설정
        // =========================

        public string? PolicyPageBackgroundColor { get; set; }

        public string? PolicyPageTextColor { get; set; }

        public string? PolicyPageBoxBackgroundColor { get; set; }

        public string? PolicyPageBackgroundImageUrl { get; set; }

        public bool UsePolicyPageBackgroundImage { get; set; } = false;
    }
}