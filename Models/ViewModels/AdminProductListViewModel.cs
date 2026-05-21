namespace MOAClover.Models.ViewModels
{
    public class AdminProductListViewModel
    {
        public List<AdminProductListItemViewModel> Products { get; set; } = new();

        public string? Keyword { get; set; }

        public string Filter { get; set; } = "all";

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; } = 1;

        public int TotalCount { get; set; }

        public int VisibleCount { get; set; }

        public int HiddenCount { get; set; }

        public int SoldoutCount { get; set; }

        public int DeletedCount { get; set; }
    }

    public class AdminProductListItemViewModel
    {
        public int ProductId { get; set; }

        public string Name { get; set; } = "";

        public int Price { get; set; }

        public int? DiscountRate { get; set; }

        public int FinalPrice
        {
            get
            {
                if (DiscountRate.HasValue && DiscountRate.Value > 0)
                    return Price - (Price * DiscountRate.Value / 100);

                return Price;
            }
        }

        public bool UseStock { get; set; }

        public int StockQuantity { get; set; }

        public bool IsSoldout => UseStock && StockQuantity <= 0;

        public bool IsVisible { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? MainImageUrl { get; set; }

        public DateTime? DeletedAt { get; set; }

        public bool IsDeleted => DeletedAt != null;
    }
}