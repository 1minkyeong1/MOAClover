using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;

namespace MOAClover.ViewComponents
{
    public class CartCountViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public CartCountViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            int count = 0;

            var userId = User.Identity?.IsAuthenticated == true
                ? User.Identity.Name
                : null;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                count = await _context.CartItems
                    .AsNoTracking()
                    .Where(c => c.UserId == userId)
                    .SumAsync(c => (int?)c.Quantity) ?? 0;
            }
            else
            {
                const string cookieName = "MOA_GUEST_CART_SESSION_ID";

                if (Request.Cookies.TryGetValue(cookieName, out var guestCartId)
                    && !string.IsNullOrWhiteSpace(guestCartId))
                {
                    count = await _context.CartItems
                        .AsNoTracking()
                        .Where(c => c.GuestCartId == guestCartId)
                        .SumAsync(c => (int?)c.Quantity) ?? 0;
                }
            }

            return View(count);
        }
    }
}