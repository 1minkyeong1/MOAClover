using MOAClover.Data;
using MOAClover.Models;
using Microsoft.EntityFrameworkCore;

namespace MOAClover.Services
{
    public class OrderStockService
    {
        private readonly ApplicationDbContext _context;

        public OrderStockService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(bool Ok, string Message)> DeductStockAsync(Order order)
        {
            if (order.StockDeducted)
            {
                return (true, "이미 재고 차감된 주문입니다.");
            }

            var orderItems = await _context.OrderItems
                .Where(x => x.OrderId == order.OrderId)
                .ToListAsync();

            foreach (var item in orderItems)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                if (product == null)
                    continue;

                if (!product.UseStock)
                    continue;

                if (product.StockQuantity < item.Quantity)
                {
                    return (false, $"{product.Name} 재고가 부족합니다. 현재 재고: {product.StockQuantity}개");
                }
            }

            foreach (var item in orderItems)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                if (product == null)
                    continue;

                if (!product.UseStock)
                    continue;

                product.StockQuantity -= item.Quantity;
                product.UpdatedAt = DateTime.Now;
            }

            order.StockDeducted = true;

            return (true, "재고가 차감되었습니다.");
        }

        public async Task RestoreStockAsync(Order order)
        {
            if (!order.StockDeducted)
            {
                return;
            }

            var orderItems = await _context.OrderItems
                .Where(x => x.OrderId == order.OrderId)
                .ToListAsync();

            foreach (var item in orderItems)
            {
                var product = await _context.Products
                    .FirstOrDefaultAsync(p => p.ProductId == item.ProductId);

                if (product == null)
                    continue;

                if (!product.UseStock)
                    continue;

                product.StockQuantity += item.Quantity;
                product.UpdatedAt = DateTime.Now;
            }

            order.StockDeducted = false;
        }
    }
}

// 재고관리 공통함수