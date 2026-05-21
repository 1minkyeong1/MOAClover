using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Models.ViewModels;
using System.Linq.Expressions;

namespace MOAClover.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CartController(ApplicationDbContext context)
        {
            _context = context;
        }

        private string? GetUserId()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return User.Identity.Name;

            return null;
        }

        private string GetOrCreateGuestCartId()
        {
            const string cookieName = "MOA_GUEST_CART_SESSION_ID";

            if (Request.Cookies.TryGetValue(cookieName, out var existing)
                && !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var newId = Guid.NewGuid().ToString("N");

            Response.Cookies.Append(cookieName, newId, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps
            });

            return newId;
        }

        private IQueryable<CartItem> MyCartQuery()
        {
            var userId = GetUserId();

            if (!string.IsNullOrWhiteSpace(userId))
            {
                return _context.CartItems.Where(x => x.UserId == userId);
            }

            var guestCartId = GetOrCreateGuestCartId();
            return _context.CartItems.Where(x => x.GuestCartId == guestCartId);
        }

        // SQL Server 2012 호환용
        // List.Contains(...)가 WITH/OPENJSON 형태로 번역되어 오류나는 문제 방지
        private IQueryable<T> WhereInSql2012<T>(
            IQueryable<T> src,
            Expression<Func<T, int>> selector,
            List<int> values)
        {
            values = values.Distinct().ToList();

            if (values.Count == 0)
                return src.Where(_ => false);

            var param = selector.Parameters[0];
            Expression? body = null;

            foreach (var v in values)
            {
                var eq = Expression.Equal(selector.Body, Expression.Constant(v));
                body = body == null ? eq : Expression.OrElse(body, eq);
            }

            var lambda = Expression.Lambda<Func<T, bool>>(body!, param);
            return src.Where(lambda);
        }

        [HttpGet]
        public IActionResult Index()
        {
            var cartItems = MyCartQuery()
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var productIds = cartItems
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var model = new CartViewModel();

            if (productIds.Count == 0)
                return View("Cart", model);

            var productQuery = _context.Products
                .AsNoTracking()
                .Where(p => p.DeletedAt == null && p.IsVisible);

            productQuery = WhereInSql2012(productQuery, p => p.ProductId, productIds);

            var products = productQuery.ToList();

            var mediaQuery = _context.Media
                .AsNoTracking()
                .Where(m => m.DeletedAt == null
                            && m.IsActive
                            && m.FileUrl != null
                            && m.FileUrl != ""
                            && (m.MediaType == "thumb"
                                || m.MediaType == "thumbnail"
                                || m.MediaType == "image"));

            mediaQuery = WhereInSql2012(mediaQuery, m => m.ProductId, productIds);

            var mediaRows = mediaQuery
                .OrderBy(m => m.MediaType == "thumb" ? 0 :
                              m.MediaType == "thumbnail" ? 1 : 2)
                .ThenBy(m => m.SortOrder)
                .ThenBy(m => m.MediaId)
                .ToList();

            var firstImageByProduct = mediaRows
                .GroupBy(m => m.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().FileUrl
                );

            foreach (var item in cartItems)
            {
                var product = products.FirstOrDefault(p => p.ProductId == item.ProductId);

                if (product == null)
                    continue;

                model.Items.Add(new CartItemViewModel
                {
                    CartItemId = item.CartItemId,
                    ProductId = product.ProductId,
                    ProductName = product.Name,
                    Price = product.Price,
                    DiscountRate = product.DiscountRate,
                    Quantity = item.Quantity,
                    MainImageUrl = firstImageByProduct.TryGetValue(product.ProductId, out var img)
                        ? img
                        : null,

                    // 배송비 설정
                    ShippingType = product.ShippingType,
                    ShippingFee = product.ShippingFee,
                    FreeShippingMinAmount = product.FreeShippingMinAmount,
                    JejuExtraFee = product.JejuExtraFee,
                    RemoteAreaExtraFee = product.RemoteAreaExtraFee
                });
            }

            return View("Cart", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(
            int productId,
            int quantity = 1,
            string? returnUrl = null,
            bool directBuy = false)
        {
            if (quantity <= 0)
                quantity = 1;

            var product = _context.Products
                .FirstOrDefault(p => p.ProductId == productId
                                     && p.DeletedAt == null
                                     && p.IsVisible);

            if (product == null)
                return NotFound();

            // 재고 관리 상품이면 장바구니 담기 전에 재고 확인
            var cappedByStock = false;

            if (product.UseStock)
            {
                if (product.StockQuantity <= 0)
                {
                    TempData.Remove("CartAdded");
                    TempData["CartMessage"] = "품절된 상품입니다.";

                    if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Detail", "Home", new { id = product.ProductId });
                }

                if (quantity > product.StockQuantity)
                {
                    quantity = product.StockQuantity;
                    cappedByStock = true;
                }
            }

            var userId = GetUserId();

            string? guestCartId = null;

            if (string.IsNullOrWhiteSpace(userId))
                guestCartId = GetOrCreateGuestCartId();

            CartItem? existing;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                existing = _context.CartItems
                    .FirstOrDefault(x => x.UserId == userId && x.ProductId == productId);
            }
            else
            {
                existing = _context.CartItems
                    .FirstOrDefault(x => x.GuestCartId == guestCartId && x.ProductId == productId);
            }

            if (existing != null)
            {
                var nextQuantity = existing.Quantity + quantity;

                if (product.UseStock && nextQuantity > product.StockQuantity)
                {
                    existing.Quantity = product.StockQuantity;
                    existing.UpdatedAt = DateTime.Now;
                    cappedByStock = true;
                }
                else
                {
                    existing.Quantity = nextQuantity;
                    existing.UpdatedAt = DateTime.Now;
                }
            }
            else
            {
                _context.CartItems.Add(new CartItem
                {
                    UserId = userId,
                    GuestCartId = guestCartId,
                    ProductId = productId,
                    Quantity = quantity,
                    CreatedAt = DateTime.Now
                });
            }

            _context.SaveChanges();

            // 바로구매는 장바구니 팝업을 띄우지 않고 주문 흐름으로 이동
            if (directBuy)
            {
                TempData.Remove("CartAdded");

                if (cappedByStock)
                {
                    TempData["CartMessage"] = $"재고가 {product.StockQuantity}개라 장바구니 수량을 {product.StockQuantity}개로 맞췄습니다.";
                }
                else
                {
                    TempData.Remove("CartMessage");
                }

                return RedirectToAction("Start", "Order");
            }

            // 일반 장바구니 담기일 때만 팝업 표시
            TempData["CartAdded"] = "true";

            if (cappedByStock)
            {
                TempData["CartMessage"] = $"재고가 {product.StockQuantity}개라 장바구니 수량을 {product.StockQuantity}개로 맞췄습니다.";
            }
            else
            {
                TempData["CartMessage"] = "장바구니에 상품을 담았습니다.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Cart");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity(int cartItemId, int quantity)
        {
            if (quantity <= 0)
                quantity = 1;

            var item = MyCartQuery()
                .FirstOrDefault(x => x.CartItemId == cartItemId);

            if (item == null)
                return NotFound();

            var product = _context.Products
                .AsNoTracking()
                .FirstOrDefault(p => p.ProductId == item.ProductId
                                     && p.DeletedAt == null
                                     && p.IsVisible);

            if (product == null)
            {
                TempData["CartMessage"] = "상품 정보를 찾을 수 없습니다.";
                return RedirectToAction("Index");
            }

            if (product.UseStock)
            {
                if (product.StockQuantity <= 0)
                {
                    TempData["CartMessage"] = "품절된 상품입니다.";
                    return RedirectToAction("Index");
                }

                if (quantity > product.StockQuantity)
                {
                    TempData["CartMessage"] = $"재고가 부족합니다. 현재 재고: {product.StockQuantity}개";
                    return RedirectToAction("Index");
                }
            }

            item.Quantity = quantity;
            item.UpdatedAt = DateTime.Now;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Remove(int cartItemId, string? returnUrl = null)
        {
            var item = MyCartQuery()
                .FirstOrDefault(x => x.CartItemId == cartItemId);

            if (item == null)
                return NotFound();

            _context.CartItems.Remove(item);
            _context.SaveChanges();

            TempData["CartMessage"] = "상품을 삭제했습니다.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            var items = MyCartQuery().ToList();

            if (items.Any())
            {
                _context.CartItems.RemoveRange(items);
                _context.SaveChanges();
            }

            return RedirectToAction("Index");
        }
    }
}