namespace MOAClover.Models.ViewModels
{
    public class CartItemViewModel
    {
        public int CartItemId { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = "";

        public string? MainImageUrl { get; set; }

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

        public int Quantity { get; set; }

        // 배송비
        public string ShippingType { get; set; } = "Included";
        public int ShippingFee { get; set; }
        public int? FreeShippingMinAmount { get; set; }
        public int JejuExtraFee { get; set; }
        public int RemoteAreaExtraFee { get; set; }

        public int LineTotal => FinalPrice * Quantity;
    }
}