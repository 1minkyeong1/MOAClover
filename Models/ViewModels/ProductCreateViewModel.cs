using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class ProductCreateViewModel
    {
        [Required]
        public string Name { get; set; } = "";

        public string? Description { get; set; }

        [Range(0, int.MaxValue)]
        public int Price { get; set; }

        [Range(0, 100)]
        public int? DiscountRate { get; set; }

        // 재고 관리
        public bool UseStock { get; set; } = false;

        [Range(0, int.MaxValue, ErrorMessage = "재고 수량은 0개 이상 입력해주세요.")]
        public int StockQuantity { get; set; } = 0;

        // 배송비
        [Display(Name = "배송 방식")]
        public string ShippingType { get; set; } = "Included";

        [Display(Name = "기본 배송비")]
        [Range(0, int.MaxValue, ErrorMessage = "기본 배송비는 0원 이상 입력해주세요.")]
        public int? ShippingFee { get; set; }

        [Display(Name = "무료배송 기준금액")]
        [Range(0, int.MaxValue, ErrorMessage = "무료배송 기준금액은 0원 이상 입력해주세요.")]
        public int? FreeShippingMinAmount { get; set; }

        [Display(Name = "제주 추가배송비")]
        [Range(0, int.MaxValue, ErrorMessage = "제주 추가배송비는 0원 이상 입력해주세요.")]
        public int? JejuExtraFee { get; set; }

        [Display(Name = "도서산간 추가배송비")]
        [Range(0, int.MaxValue, ErrorMessage = "도서산간 추가배송비는 0원 이상 입력해주세요.")]
        public int? RemoteAreaExtraFee { get; set; }

        // 최종 선택된 카테고리(세부/소분류/중분류/대분류 중 가장 깊은 값)
        [Range(1, int.MaxValue, ErrorMessage = "카테고리를 선택해주세요.")]
        public int CategoryId { get; set; }

        public bool IsVisible { get; set; } = true;

        // 외부몰 상품 URL
        [Url(ErrorMessage = "스마트스토어 URL 형식이 올바르지 않습니다.")]
        public string? SmartStoreUrl { get; set; }

        [Url(ErrorMessage = "쿠팡 URL 형식이 올바르지 않습니다.")]
        public string? CoupangUrl { get; set; }

        // 상단 썸네일 (최대 8장)
        public List<IFormFile> ThumbImages { get; set; } = new();

        // 상세페이지 이미지 (여러장)
        public List<IFormFile> DetailImages { get; set; } = new();

        // 동영상 (여러개)
        public List<IFormFile> Videos { get; set; } = new();
    }
}

// 상품 등록(Create) 화면 전용