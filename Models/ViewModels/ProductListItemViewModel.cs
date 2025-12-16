namespace MOAClover.Models.ViewModels
{
    public class ProductListItemViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public int Price { get; set; }
        public int? DiscountRate { get; set; }

        public List<string> ImageUrls { get; set; } = new();

        public List<ProductListItemViewModel> Products { get; set; } = new();

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }

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
