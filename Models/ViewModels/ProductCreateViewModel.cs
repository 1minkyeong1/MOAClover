using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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

        //  최종 선택된 카테고리(세부/소분류/중분류/대분류 중 가장 깊은 값)
        [Range(1, int.MaxValue, ErrorMessage = "카테고리를 선택해주세요.")]
        public int CategoryId { get; set; }

        public bool IsVisible { get; set; } = true;

        //  상단 썸네일 (최대 8장)
        public List<IFormFile> ThumbImages { get; set; } = new();

        //  상세페이지 이미지 (여러장)
        public List<IFormFile> DetailImages { get; set; } = new();

        //  동영상 (여러개)
        public List<IFormFile> Videos { get; set; } = new();
    }
}

//“상품 등록(Create) 화면 전용”