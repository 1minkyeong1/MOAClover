namespace MOAClover.Models.ViewModels
{
    public class ProductListItemViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public int Price { get; set; }
        public int? DiscountRate { get; set; }
        public bool IsVisible { get; set; }

        // 재고
        public bool UseStock { get; set; }
        public int StockQuantity { get; set; }

        // 배송비
        public string ShippingType { get; set; } = "Fixed";
        public int ShippingFee { get; set; } = 4000;
        public int? FreeShippingMinAmount { get; set; }
        public int JejuExtraFee { get; set; }
        public int RemoteAreaExtraFee { get; set; }

        public List<string> ImageUrls { get; set; } = new();

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



//“상품 목록(리스트) 화면 전용”