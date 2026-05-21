namespace MOAClover.Models.ViewModels
{
    public class CheckoutItemViewModel
    {
        public int CartItemId { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = "";

        public string? MainImageUrl { get; set; }

        public int Price { get; set; }

        public int? DiscountRate { get; set; }

        public int Quantity { get; set; }

        // 배송비
        public string ShippingType { get; set; } = "Fixed";

        public int ShippingFee { get; set; } = 4000;

        public int? FreeShippingMinAmount { get; set; }

        public int JejuExtraFee { get; set; } = 0;

        public int RemoteAreaExtraFee { get; set; } = 0;

        public int FinalPrice
        {
            get
            {
                if (DiscountRate.HasValue && DiscountRate.Value > 0)
                    return Price - (Price * DiscountRate.Value / 100);

                return Price;
            }
        }

        public int LineTotal
        {
            get
            {
                return FinalPrice * Quantity;
            }
        }
    }
}