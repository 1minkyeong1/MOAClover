namespace MOAClover.Models.ViewModels
{
    public class CartViewModel
    {
        public List<CartItemViewModel> Items { get; set; } = new();

        public int ProductTotal => Items.Sum(x => x.LineTotal);

        public bool HasCollectShipping => Items.Any(x => x.ShippingType == "Collect");

        public bool HasIncludedShipping =>
            Items.Any(x => x.ShippingType == "Included");

        public int ShippingFee
        {
            get
            {
                if (HasCollectShipping)
                    return 0;

                var total = 0;

                foreach (var item in Items)
                {
                    if (item.ShippingType == "Included")
                        continue;

                    if (item.ShippingType == "Free")
                        continue;

                    if (item.ShippingType == "Collect")
                        continue;

                    if (item.ShippingType == "ConditionalFree")
                    {
                        if (item.FreeShippingMinAmount.HasValue
                            && item.FreeShippingMinAmount.Value > 0
                            && item.LineTotal >= item.FreeShippingMinAmount.Value)
                        {
                            continue;
                        }

                        total += item.ShippingFee;
                        continue;
                    }

                    if (item.ShippingType == "Fixed")
                    {
                        total += item.ShippingFee;
                    }
                }

                return total;
            }
        }

        public int TotalPayAmount => ProductTotal + ShippingFee;
    }
}