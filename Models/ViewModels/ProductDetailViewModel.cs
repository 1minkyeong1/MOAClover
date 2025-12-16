

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

        public List<ProductQnA> QnAs { get; set; } = new();

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
