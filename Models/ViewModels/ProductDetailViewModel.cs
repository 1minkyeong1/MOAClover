

namespace MOAClover.Models.ViewModels
{
    public class ProductDetailViewModel
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }
        public int? DiscountRate { get; set; }

        public string? Description { get; set; }
        public string CategoryPath { get; set; } = string.Empty;

        public List<string> ImageUrls { get; set; } = new();
        public List<string> VideoUrls { get; set; } = new();

        public string? MainImageUrl => ImageUrls.FirstOrDefault();

        public List<ProductQnA> QnAs { get; set; } = new List<ProductQnA>();

        public List<string> ThumbUrls { get; set; } = new();       //  상단 썸네일(최대 8장)
        public List<string> DetailImageUrls { get; set; } = new(); //  상세 탭에서만 보여줄 이미지

        public int FinalPrice
        {
            get
            {
                if (DiscountRate.HasValue)
                    return Price - (Price * DiscountRate.Value / 100);
                return Price;
            }
        }
    }
}

//상세페이지 표시용