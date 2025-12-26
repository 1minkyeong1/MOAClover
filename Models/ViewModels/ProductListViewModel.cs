namespace MOAClover.Models.ViewModels
{
    public class ProductListViewModel
    {
        public List<ProductListItemViewModel> Products { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
    }
}

// 페이징 표시용