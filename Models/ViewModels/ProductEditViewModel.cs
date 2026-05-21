using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MOAClover.Models.ViewModels
{
    public class ProductEditViewModel
    {
        public int ProductId { get; set; }

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

        public int CategoryId { get; set; }

        public bool IsVisible { get; set; } = true;

        // 외부몰 상품 URL
        [Url(ErrorMessage = "스마트스토어 URL 형식이 올바르지 않습니다.")]
        public string? SmartStoreUrl { get; set; }

        [Url(ErrorMessage = "쿠팡 URL 형식이 올바르지 않습니다.")]
        public string? CoupangUrl { get; set; }

        // 기존 미디어
        public List<MediaEditItemVm> ExistingMedia { get; set; } = new();

        // 새 업로드
        public IFormFile? NewThumbnail { get; set; }
        public List<IFormFile> NewImages { get; set; } = new();
        public List<IFormFile> NewVideos { get; set; } = new();
        public List<IFormFile> NewDetailImages { get; set; } = new();

        // 기존 미디어 중 “이걸 썸네일로 지정”
        public int? SelectedThumbnailMediaId { get; set; }
    }

    public class MediaEditItemVm
    {
        public int MediaId { get; set; }
        public string MediaType { get; set; } = "";
        public string FileUrl { get; set; } = "";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }

        // 화면에서 삭제 체크
        public bool Delete { get; set; }
    }
}

// 상품 수정 전용