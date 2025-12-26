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

        public int CategoryId { get; set; }

        public bool IsVisible { get; set; } = true;

        // ✅ 기존 미디어
        public List<MediaEditItemVm> ExistingMedia { get; set; } = new();

        // ✅ 새 업로드
        public IFormFile? NewThumbnail { get; set; }          // 썸네일 최대8개
        public List<IFormFile> NewImages { get; set; } = new(); // 이미지 여러장
        public List<IFormFile> NewVideos { get; set; } = new(); // 영상 여러개
        public List<IFormFile> NewDetailImages { get; set; } = new();  // 상세 이미지

        // ✅ 기존 미디어 중 “이걸 썸네일로 지정”
        public int? SelectedThumbnailMediaId { get; set; }
    }

    public class MediaEditItemVm
    {
        public int MediaId { get; set; }
        public string MediaType { get; set; } = ""; // thumbnail/image/video
        public string FileUrl { get; set; } = "";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }

        // 화면에서 삭제 체크
        public bool Delete { get; set; }
    }
}

//상품 수정 전용