using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Models.ViewModels;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Hosting;

namespace MOAClover.Controllers
{
    [Authorize(Roles = "admin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWebHostEnvironment _env;

        public AdminController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWebHostEnvironment env)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
        }

        private IQueryable<T> WhereStringInSql2012<T>(
            IQueryable<T> src,
            Expression<Func<T, string?>> selector,
            List<string> values)
        {
            values = values
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct()
                .ToList();

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

        private async Task<string?> SaveSiteDesignFileAsync(
            IFormFile? file,
            string folderName,
            string[] allowedExtensions,
            long maxBytes)
        {
            if (file == null || file.Length <= 0)
                return null;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    $"허용되지 않는 파일 형식입니다. 가능 확장자: {string.Join(", ", allowedExtensions)}");
            }

            if (file.Length > maxBytes)
            {
                throw new InvalidOperationException(
                    $"파일 용량이 너무 큽니다. 최대 {maxBytes / 1024 / 1024}MB까지 업로드할 수 있습니다.");
            }

            var uploadRoot = Path.Combine(_env.WebRootPath, "uploads", "site-design", folderName);

            if (!Directory.Exists(uploadRoot))
            {
                Directory.CreateDirectory(uploadRoot);
            }

            var safeFileName = $"{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
            var savePath = Path.Combine(uploadRoot, safeFileName);

            await using (var stream = new FileStream(savePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return $"/uploads/site-design/{folderName}/{safeFileName}";
        }

        [HttpGet]
        public async Task<IActionResult> Users(string? keyword = null, int page = 1)
        {
            const int pageSize = 20;

            if (page < 1)
                page = 1;

            keyword = keyword?.Trim();

            var query = _context.Users
                .AsNoTracking()
                .Where(u => u.DeletedAt == null);

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(u =>
                    u.UserName!.Contains(keyword)
                    || u.Email!.Contains(keyword)
                    || u.Name.Contains(keyword)
                    || u.Phone.Contains(keyword)
                );
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userNames = users
                .Select(u => u.UserName ?? "")
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            var orderQuery = _context.Orders
                 .AsNoTracking()
                 .Where(o => !o.IsGuestOrder);

            orderQuery = WhereStringInSql2012(orderQuery, o => o.UserId, userNames);

            var orderStats = await orderQuery
                .GroupBy(o => o.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    OrderCount = g.Count(),
                    PaidOrderCount = g.Count(o => o.PaymentStatus == "Paid" || o.PaymentStatus == "Refunded"),
                    TotalPaidAmount = g
                        .Where(o => o.PaymentStatus == "Paid" || o.PaymentStatus == "Refunded")
                        .Sum(o => o.TotalPayAmount)
                })
                .ToListAsync();

            var model = new AdminUserListViewModel
            {
                Keyword = keyword,
                CurrentPage = page,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            };

            foreach (var user in users)
            {
                var stat = orderStats.FirstOrDefault(x => x.UserId == user.UserName);
                var isAdmin = await _userManager.IsInRoleAsync(user, "admin");

                model.Users.Add(new AdminUserListItemViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName ?? "",
                    Name = user.Name,
                    Email = user.Email ?? "",
                    Phone = user.Phone,
                    CreatedAt = user.CreatedAt,
                    IsActive = user.IsActive,
                    IsAdmin = isAdmin,
                    OrderCount = stat?.OrderCount ?? 0,
                    PaidOrderCount = stat?.PaidOrderCount ?? 0,
                    TotalPaidAmount = stat?.TotalPaidAmount ?? 0
                });
            }

            if (model.TotalPages < 1)
                model.TotalPages = 1;

            return View(model);
        }

        // 회원 상세보기
        [HttpGet]
        public async Task<IActionResult> UserDetail(string id, int page = 1)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            const int pageSize = 10;

            if (page < 1)
                page = 1;

            var user = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);

            if (user == null)
                return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "admin");

            var addresses = await _context.UserAddresses
                .AsNoTracking()
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.Id)
                .Select(a => new AdminUserAddressViewModel
                {
                    Id = a.Id,
                    ReceiverName = a.ReceiverName,
                    ReceiverPhone = a.ReceiverPhone,
                    ZipCode = a.ZipCode,
                    Address = a.Address,
                    AddressDetail = a.AddressDetail,
                    IsDefault = a.IsDefault
                })
                .ToListAsync();

            var orderQuery = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Where(o => !o.IsGuestOrder && o.UserId == user.UserName)
                .OrderByDescending(o => o.CreatedAt);

            var orderCount = await orderQuery.CountAsync();

            var orders = await orderQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var paidOrderCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => !o.IsGuestOrder
                                 && o.UserId == user.UserName
                                 && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Refunded"));

            var totalPaidAmount = await _context.Orders
                .AsNoTracking()
                .Where(o => !o.IsGuestOrder
                            && o.UserId == user.UserName
                            && (o.PaymentStatus == "Paid" || o.PaymentStatus == "Refunded"))
                .SumAsync(o => (int?)o.TotalPayAmount) ?? 0;

            var model = new AdminUserDetailViewModel
            {
                Id = user.Id,
                UserName = user.UserName ?? "",
                Name = user.Name,
                Email = user.Email ?? "",
                Phone = user.Phone,
                CreatedAt = user.CreatedAt,
                IsActive = user.IsActive,
                IsAdmin = isAdmin,
                Addresses = addresses,
                Orders = orders,
                OrderCount = orderCount,
                PaidOrderCount = paidOrderCount,
                TotalPaidAmount = totalPaidAmount,
                CurrentPage = page,
                TotalPages = (int)Math.Ceiling(orderCount / (double)pageSize)
            };

            if (model.TotalPages < 1)
                model.TotalPages = 1;

            return View(model);
        }

        // 회원상태 변경(활성, 비활성)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUserActive(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return NotFound();

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == id && u.DeletedAt == null);

            if (user == null)
                return NotFound();

            var isAdmin = await _userManager.IsInRoleAsync(user, "admin");

            if (isAdmin)
            {
                TempData["AdminUserMessage"] = "관리자 계정은 비활성화할 수 없습니다.";
                return RedirectToAction("UserDetail", new { id = user.Id });
            }

            user.IsActive = !user.IsActive;
            user.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["AdminUserMessage"] = user.IsActive
                ? "회원이 활성화되었습니다."
                : "회원이 비활성화되었습니다.";

            return RedirectToAction("UserDetail", new { id = user.Id });
        }

        // 관리자 상품관리 전용
        [HttpGet]
        public async Task<IActionResult> Products(string filter = "all", string? keyword = null, int page = 1)
        {
            const int pageSize = 20;

            if (page < 1)
                page = 1;

            filter = string.IsNullOrWhiteSpace(filter) ? "all" : filter.Trim();
            keyword = keyword?.Trim();

            var allProductsQuery = _context.Products
                .AsNoTracking();

            var baseQuery = allProductsQuery
                .Where(p => p.DeletedAt == null);

            var deletedCount = await allProductsQuery.CountAsync(p => p.DeletedAt != null);
            var visibleCount = await baseQuery.CountAsync(p => p.IsVisible);
            var hiddenCount = await baseQuery.CountAsync(p => !p.IsVisible);
            var soldoutCount = await baseQuery.CountAsync(p => p.UseStock && p.StockQuantity <= 0);

            var query = filter == "deleted"
                ? allProductsQuery.Where(p => p.DeletedAt != null)
                : baseQuery;

            if (filter == "visible")
            {
                query = query.Where(p => p.IsVisible);
            }
            else if (filter == "hidden")
            {
                query = query.Where(p => !p.IsVisible);
            }
            else if (filter == "soldout")
            {
                query = query.Where(p => p.UseStock && p.StockQuantity <= 0);
            }
            else if (filter == "lowstock")
            {
                query = query.Where(p => p.UseStock && p.StockQuantity > 0 && p.StockQuantity <= 5);
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(p => p.Name.Contains(keyword));
            }

            var totalCount = await query.CountAsync();

            var products = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var productIds = products.Select(p => p.ProductId).ToList();

            var mediaQuery = _context.Media
                .AsNoTracking()
                .Where(m => m.DeletedAt == null
                            && m.IsActive
                            && m.FileUrl != null
                            && m.FileUrl != ""
                            && (m.MediaType == "thumb"
                                || m.MediaType == "thumbnail"
                                || m.MediaType == "image"));

            if (productIds.Any())
            {
                mediaQuery = WhereInSql2012(mediaQuery, m => m.ProductId, productIds);
            }
            else
            {
                mediaQuery = mediaQuery.Where(_ => false);
            }

            var mediaRows = await mediaQuery
                .OrderBy(m => m.MediaType == "thumb" ? 0 :
                              m.MediaType == "thumbnail" ? 1 : 2)
                .ThenBy(m => m.SortOrder)
                .ThenBy(m => m.MediaId)
                .ToListAsync();

            var firstImageByProduct = mediaRows
                .GroupBy(m => m.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.First().FileUrl
                );

            var model = new AdminProductListViewModel
            {
                Keyword = keyword,
                Filter = filter,
                CurrentPage = page,
                TotalCount = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                VisibleCount = visibleCount,
                HiddenCount = hiddenCount,
                SoldoutCount = soldoutCount,
                DeletedCount = deletedCount,
                Products = products.Select(p => new AdminProductListItemViewModel
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    Price = p.Price,
                    DiscountRate = p.DiscountRate,
                    UseStock = p.UseStock,
                    StockQuantity = p.StockQuantity,
                    IsVisible = p.IsVisible,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    DeletedAt = p.DeletedAt,
                    MainImageUrl = firstImageByProduct.TryGetValue(p.ProductId, out var img)
                        ? img
                        : null
                }).ToList()
            };

            if (model.TotalPages < 1)
                model.TotalPages = 1;

            return View(model);
        }

        // 노출.숨김 전환
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleProductVisible(int id, string? returnUrl = null)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id && p.DeletedAt == null);

            if (product == null)
                return NotFound();

            product.IsVisible = !product.IsVisible;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["AdminProductMessage"] = product.IsVisible
                ? "상품이 노출 처리되었습니다."
                : "상품이 숨김 처리되었습니다.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Products");
        }

        // 상품관리에서 재고수정
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateProductStock(
            int id,
            bool useStock,
            int stockQuantity,
            string? returnUrl = null)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id && p.DeletedAt == null);

            if (product == null)
                return NotFound();

            if (stockQuantity < 0)
                stockQuantity = 0;

            product.UseStock = useStock;
            product.StockQuantity = stockQuantity;
            product.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["AdminProductMessage"] = "재고 정보가 저장되었습니다.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Products");
        }

        // 상품관리 삭제, 복구 액션
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id, string? returnUrl = null)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            if (product.DeletedAt == null)
            {
                product.DeletedAt = DateTime.Now;
                product.IsVisible = false;
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["AdminProductMessage"] = "상품이 삭제 처리되었습니다.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Products");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreProduct(int id, string? returnUrl = null)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            if (product.DeletedAt != null)
            {
                product.DeletedAt = null;
                product.IsVisible = false;
                product.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                TempData["AdminProductMessage"] = "상품이 복구되었습니다. 복구된 상품은 숨김 상태입니다.";
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return LocalRedirect(returnUrl);

            return RedirectToAction("Products", new { filter = "deleted" });
        }

        // 관리자 대시보드
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var todayOrdersQuery = _context.Orders
                .AsNoTracking()
                .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);

            var todayOrderCount = await todayOrdersQuery.CountAsync();

            var todayPaidAmount = await todayOrdersQuery
                .Where(o => o.PaymentStatus == "Paid")
                .SumAsync(o => (int?)o.TotalPayAmount) ?? 0;

            var pendingPaymentCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus != "Cancelled"
                                 && o.PaymentStatus == "Ready");

            var preparingCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "Preparing");

            var cancelRequestedCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "CancelRequested");

            var returnExchangeRequestedCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o =>
                    o.OrderStatus == "ReturnRequested"
                    || o.OrderStatus == "ExchangeRequested");

            var waitingQnaCount = await _context.ProductQnA
                .AsNoTracking()
                .CountAsync(q => !q.IsDeleted && (q.Answer == null || q.Answer == ""));

            var soldoutProductCount = await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.DeletedAt == null
                                 && p.UseStock
                                 && p.StockQuantity <= 0);

            var lowStockProductCount = await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.DeletedAt == null
                                 && p.UseStock
                                 && p.StockQuantity > 0
                                 && p.StockQuantity <= 5);

            var hiddenProductCount = await _context.Products
                .AsNoTracking()
                .CountAsync(p => p.DeletedAt == null && !p.IsVisible);

            var recentOrders = await _context.Orders
                .AsNoTracking()
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new AdminDashboardOrderItemViewModel
                {
                    OrderNo = o.OrderNo,
                    BuyerName = o.BuyerName,
                    TotalPayAmount = o.TotalPayAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            var issueOrders = await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.OrderStatus == "CancelRequested"
                    || o.OrderStatus == "ReturnRequested"
                    || o.OrderStatus == "ExchangeRequested"
                    || (o.OrderStatus == "PendingPayment" && o.PaymentStatus == "Ready")
                    || o.OrderStatus == "Preparing")
                .OrderByDescending(o => o.CreatedAt)
                .Take(5)
                .Select(o => new AdminDashboardIssueOrderViewModel
                {
                    OrderNo = o.OrderNo,
                    BuyerName = o.BuyerName,
                    TotalPayAmount = o.TotalPayAmount,
                    OrderStatus = o.OrderStatus,
                    PaymentStatus = o.PaymentStatus,
                    CreatedAt = o.CreatedAt
                })
                .ToListAsync();

            var waitingQnas = await
                (
                    from q in _context.ProductQnA.AsNoTracking()
                    join p in _context.Products.AsNoTracking()
                        on q.ProductId equals p.ProductId
                    where !q.IsDeleted
                          && (q.Answer == null || q.Answer == "")
                    orderby q.CreatedAt descending
                    select new AdminDashboardQnaItemViewModel
                    {
                        ProductId = q.ProductId,
                        ProductName = p.Name,
                        UserName = q.UserName,
                        Question = q.Question,
                        CreatedAt = q.CreatedAt,
                        IsSecret = q.IsSecret
                    }
                )
                .Take(5)
                .ToListAsync();

            var stockIssues = await _context.Products
                .AsNoTracking()
                .Where(p => p.DeletedAt == null
                            && p.UseStock
                            && p.StockQuantity <= 5)
                .OrderBy(p => p.StockQuantity)
                .ThenByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new AdminDashboardProductItemViewModel
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    StockQuantity = p.StockQuantity
                })
                .ToListAsync();

            var model = new AdminDashboardViewModel
            {
                TodayOrderCount = todayOrderCount,
                TodayPaidAmount = todayPaidAmount,
                PendingPaymentCount = pendingPaymentCount,
                PreparingCount = preparingCount,
                CancelRequestedCount = cancelRequestedCount,
                ReturnExchangeRequestedCount = returnExchangeRequestedCount,
                WaitingQnaCount = waitingQnaCount,
                SoldoutProductCount = soldoutProductCount,
                LowStockProductCount = lowStockProductCount,
                HiddenProductCount = hiddenProductCount,
                RecentOrders = recentOrders,
                IssueOrders = issueOrders,
                WaitingQnas = waitingQnas,
                StockIssues = stockIssues
            };

            return View(model);
        }

        // 관리자 정보 세팅
        [HttpGet]
        public async Task<IActionResult> SiteSetting()
        {
            var setting = await _context.SiteSettings
                .AsNoTracking()
                .OrderBy(x => x.SiteSettingId)
                .FirstOrDefaultAsync();

            var model = new AdminSiteSettingViewModel();

            if (setting != null)
            {
                // 쇼핑몰 정보
                model.ShopName = setting.ShopName;
                model.OwnerName = setting.OwnerName;
                model.BusinessNumber = setting.BusinessNumber;
                model.MailOrderSalesNumber = setting.MailOrderSalesNumber;
                model.CustomerPhone = setting.CustomerPhone;
                model.CustomerEmail = setting.CustomerEmail;
                model.CustomerServiceHours = setting.CustomerServiceHours;
                model.Address = setting.Address;

                // 무통장입금 계좌
                model.BankName = setting.BankName;
                model.BankAccountNumber = setting.BankAccountNumber;
                model.BankAccountHolder = setting.BankAccountHolder;
                model.BankTransferNotice = setting.BankTransferNotice;

                // 배송 안내
                model.DefaultDeliveryCompany = setting.DefaultDeliveryCompany;
                model.ShippingNotice = setting.ShippingNotice;
                model.FreeShippingNotice = setting.FreeShippingNotice;

                // 반품/교환 안내
                model.ReturnAddress = setting.ReturnAddress;
                model.ReturnShippingFeeNotice = setting.ReturnShippingFeeNotice;
                model.ReturnExchangeNotice = setting.ReturnExchangeNotice;

                model.ReturnShippingFee = setting.ReturnShippingFee;
                model.ExchangeShippingFee = setting.ExchangeShippingFee;
                model.ReturnExchangeAvailableDays = setting.ReturnExchangeAvailableDays;
                model.JejuExtraShippingFeeNotice = setting.JejuExtraShippingFeeNotice;
                model.RemoteAreaExtraShippingFeeNotice = setting.RemoteAreaExtraShippingFeeNotice;

            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SiteSetting(AdminSiteSettingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["AdminSiteSettingMessage"] = "입력값에 문제가 있어 저장되지 않았습니다.";
                return View(model);
            }

            var setting = await _context.SiteSettings
                .OrderBy(x => x.SiteSettingId)
                .FirstOrDefaultAsync();

            if (setting == null)
            {
                setting = new SiteSetting
                {
                    CreatedAt = DateTime.Now
                };

                _context.SiteSettings.Add(setting);
            }

            setting.ShopName = model.ShopName?.Trim() ?? "";
            setting.OwnerName = model.OwnerName?.Trim() ?? "";
            setting.BusinessNumber = model.BusinessNumber?.Trim() ?? "";
            setting.MailOrderSalesNumber = model.MailOrderSalesNumber?.Trim() ?? "";
            setting.CustomerPhone = model.CustomerPhone?.Trim() ?? "";
            setting.CustomerEmail = model.CustomerEmail?.Trim() ?? "";
            setting.CustomerServiceHours = model.CustomerServiceHours?.Trim() ?? "";
            setting.Address = model.Address?.Trim() ?? "";

            setting.BankName = model.BankName?.Trim() ?? "";
            setting.BankAccountNumber = model.BankAccountNumber?.Trim() ?? "";
            setting.BankAccountHolder = model.BankAccountHolder?.Trim() ?? "";
            setting.BankTransferNotice = model.BankTransferNotice?.Trim() ?? "";

            setting.DefaultDeliveryCompany = model.DefaultDeliveryCompany?.Trim() ?? "";
            setting.ShippingNotice = model.ShippingNotice?.Trim() ?? "";
            setting.FreeShippingNotice = model.FreeShippingNotice?.Trim() ?? "";
            setting.ReturnAddress = model.ReturnAddress?.Trim() ?? "";
            setting.ReturnShippingFeeNotice = model.ReturnShippingFeeNotice?.Trim() ?? "";
            setting.ReturnExchangeNotice = model.ReturnExchangeNotice?.Trim() ?? "";

            setting.ReturnShippingFee = model.ReturnShippingFee < 0 ? 0 : model.ReturnShippingFee;
            setting.ExchangeShippingFee = model.ExchangeShippingFee < 0 ? 0 : model.ExchangeShippingFee;
            setting.ReturnExchangeAvailableDays = model.ReturnExchangeAvailableDays <= 0
                ? 7
                : model.ReturnExchangeAvailableDays;

            setting.JejuExtraShippingFeeNotice = model.JejuExtraShippingFeeNotice?.Trim() ?? "";
            setting.RemoteAreaExtraShippingFeeNotice = model.RemoteAreaExtraShippingFeeNotice?.Trim() ?? "";

            setting.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["AdminSiteSettingMessage"] = "기본정보가 저장되었습니다.";

            return RedirectToAction("SiteSetting");
        }

        // 사이트 디자인 설정
        [HttpGet]
        public async Task<IActionResult> SiteDesign()
        {
            var setting = await _context.SiteSettings
                .AsNoTracking()
                .OrderBy(x => x.SiteSettingId)
                .FirstOrDefaultAsync();

            var model = new AdminSiteSettingViewModel();

            if (setting != null)
            {
                model.HeaderBackgroundColor = setting.HeaderBackgroundColor;
                model.HeaderTextColor = setting.HeaderTextColor;
                model.HeaderBackgroundImageUrl = setting.HeaderBackgroundImageUrl;
                model.UseHeaderBackgroundImage = setting.UseHeaderBackgroundImage;
                model.LogoImageUrl = setting.LogoImageUrl;
                model.CategoryBackgroundColor = setting.CategoryBackgroundColor;
                model.CategoryBackgroundColor = setting.CategoryBackgroundColor;
                model.CategoryBackgroundImageUrl = setting.CategoryBackgroundImageUrl;
                model.UseCategoryBackgroundImage = setting.UseCategoryBackgroundImage;
                model.CategoryTextColor = setting.CategoryTextColor;
                model.CategoryActiveTextColor = setting.CategoryActiveTextColor;
                model.CategoryActiveBackgroundColor = setting.CategoryActiveBackgroundColor;
                model.FooterBackgroundColor = setting.FooterBackgroundColor;
                model.FooterTextColor = setting.FooterTextColor;
                model.FooterBackgroundImageUrl = setting.FooterBackgroundImageUrl;
                model.UseFooterBackgroundImage = setting.UseFooterBackgroundImage;
                model.PolicyPageBackgroundColor = setting.PolicyPageBackgroundColor;
                model.PolicyPageTextColor = setting.PolicyPageTextColor;
                model.PolicyPageBoxBackgroundColor = setting.PolicyPageBoxBackgroundColor;
                model.PolicyPageBackgroundImageUrl = setting.PolicyPageBackgroundImageUrl;
                model.UsePolicyPageBackgroundImage = setting.UsePolicyPageBackgroundImage;

                model.UseMainHeroBanner = setting.UseMainHeroBanner;
                model.MainHeroTitle = setting.MainHeroTitle;
                model.MainHeroSubtitle = setting.MainHeroSubtitle;
                model.MainHeroImageUrl = setting.MainHeroImageUrl;
                model.MainHeroVideoUrl = setting.MainHeroVideoUrl;
                model.MainHeroLinkUrl = setting.MainHeroLinkUrl;
                model.MainHeroButtonText = setting.MainHeroButtonText;
                model.MainHeroBackgroundColor = setting.MainHeroBackgroundColor;
                model.UseMainHeroSlider = setting.UseMainHeroSlider;
                model.MainHeroSliderImageUrls = setting.MainHeroSliderImageUrls;
                model.MainHeroSliderLinkUrls = setting.MainHeroSliderLinkUrls;
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SiteDesign(AdminSiteSettingViewModel model)
        {
            if (!ModelState.IsValid)
            {
                TempData["AdminSiteDesignMessage"] = "입력값에 문제가 있어 저장되지 않았습니다.";
                return View(model);
            }

            var setting = await _context.SiteSettings
                .OrderBy(x => x.SiteSettingId)
                .FirstOrDefaultAsync();

            if (setting == null)
            {
                setting = new SiteSetting
                {
                    CreatedAt = DateTime.Now
                };

                _context.SiteSettings.Add(setting);
            }

            var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
            var videoExtensions = new[] { ".mp4", ".webm", ".ogg" };

            const long maxImageBytes = 5 * 1024 * 1024;     // 이미지 5MB
            const long maxVideoBytes = 50 * 1024 * 1024;    // 동영상 50MB

            try
            {
                var uploadedHeaderImageUrl = await SaveSiteDesignFileAsync(
                    model.HeaderBackgroundImageFile,
                    "header",
                    imageExtensions,
                    maxImageBytes);

                var uploadedLogoImageUrl = await SaveSiteDesignFileAsync(
                    model.LogoImageFile,
                    "logo",
                    imageExtensions,
                    maxImageBytes);

                var uploadedCategoryImageUrl = await SaveSiteDesignFileAsync(
                    model.CategoryBackgroundImageFile,
                    "category",
                    imageExtensions,
                    maxImageBytes);

                var uploadedFooterImageUrl = await SaveSiteDesignFileAsync(
                    model.FooterBackgroundImageFile,
                    "footer",
                    imageExtensions,
                    maxImageBytes);

                var uploadedPolicyImageUrl = await SaveSiteDesignFileAsync(
                    model.PolicyPageBackgroundImageFile,
                    "policy",
                    imageExtensions,
                    maxImageBytes);

                var uploadedMainHeroImageUrl = await SaveSiteDesignFileAsync(
                    model.MainHeroImageFile,
                    "banner",
                    imageExtensions,
                    maxImageBytes);

                var uploadedMainHeroVideoUrl = await SaveSiteDesignFileAsync(
                    model.MainHeroVideoFile,
                    "video",
                    videoExtensions,
                    maxVideoBytes);

                var uploadedSliderImageUrls = new List<string>();

                if (model.MainHeroSliderImageFiles != null && model.MainHeroSliderImageFiles.Any())
                {
                    foreach (var sliderFile in model.MainHeroSliderImageFiles)
                    {
                        var uploadedUrl = await SaveSiteDesignFileAsync(
                            sliderFile,
                            "slider",
                            imageExtensions,
                            maxImageBytes);

                        if (!string.IsNullOrWhiteSpace(uploadedUrl))
                        {
                            uploadedSliderImageUrls.Add(uploadedUrl);
                        }
                    }
                }

                setting.HeaderBackgroundColor = model.HeaderBackgroundColor?.Trim() ?? "";
                setting.HeaderTextColor = model.HeaderTextColor?.Trim() ?? "";

                setting.HeaderBackgroundImageUrl = !string.IsNullOrWhiteSpace(uploadedHeaderImageUrl)
                    ? uploadedHeaderImageUrl
                    : model.HeaderBackgroundImageUrl?.Trim() ?? "";

                setting.UseHeaderBackgroundImage = model.UseHeaderBackgroundImage;

                setting.LogoImageUrl = !string.IsNullOrWhiteSpace(uploadedLogoImageUrl)
                    ? uploadedLogoImageUrl
                    : model.LogoImageUrl?.Trim() ?? "";

                setting.CategoryBackgroundColor = model.CategoryBackgroundColor?.Trim() ?? "";

                setting.CategoryBackgroundImageUrl = !string.IsNullOrWhiteSpace(uploadedCategoryImageUrl)
                    ? uploadedCategoryImageUrl
                    : model.CategoryBackgroundImageUrl?.Trim() ?? "";

                setting.UseCategoryBackgroundImage = model.UseCategoryBackgroundImage;

                setting.CategoryTextColor = model.CategoryTextColor?.Trim() ?? "";
                setting.CategoryActiveTextColor = model.CategoryActiveTextColor?.Trim() ?? "";
                setting.CategoryActiveBackgroundColor = model.CategoryActiveBackgroundColor?.Trim() ?? "";

                setting.FooterBackgroundColor = model.FooterBackgroundColor?.Trim() ?? "";
                setting.FooterTextColor = model.FooterTextColor?.Trim() ?? "";

                setting.FooterBackgroundImageUrl = !string.IsNullOrWhiteSpace(uploadedFooterImageUrl)
                    ? uploadedFooterImageUrl
                    : model.FooterBackgroundImageUrl?.Trim() ?? "";

                setting.UseFooterBackgroundImage = model.UseFooterBackgroundImage;

                setting.PolicyPageBackgroundColor = model.PolicyPageBackgroundColor?.Trim() ?? "";
                setting.PolicyPageTextColor = model.PolicyPageTextColor?.Trim() ?? "";
                setting.PolicyPageBoxBackgroundColor = model.PolicyPageBoxBackgroundColor?.Trim() ?? "";
                setting.PolicyPageBackgroundImageUrl = !string.IsNullOrWhiteSpace(uploadedPolicyImageUrl)
                    ? uploadedPolicyImageUrl
                    : model.PolicyPageBackgroundImageUrl?.Trim() ?? "";

                setting.UsePolicyPageBackgroundImage = model.UsePolicyPageBackgroundImage;

                setting.UseMainHeroBanner = model.UseMainHeroBanner;
                setting.MainHeroTitle = model.MainHeroTitle?.Trim() ?? "";
                setting.MainHeroSubtitle = model.MainHeroSubtitle?.Trim() ?? "";

                setting.MainHeroImageUrl = !string.IsNullOrWhiteSpace(uploadedMainHeroImageUrl)
                    ? uploadedMainHeroImageUrl
                    : model.MainHeroImageUrl?.Trim() ?? "";

                setting.MainHeroVideoUrl = !string.IsNullOrWhiteSpace(uploadedMainHeroVideoUrl)
                    ? uploadedMainHeroVideoUrl
                    : model.MainHeroVideoUrl?.Trim() ?? "";

                setting.MainHeroLinkUrl = model.MainHeroLinkUrl?.Trim() ?? "";
                setting.MainHeroButtonText = model.MainHeroButtonText?.Trim() ?? "";
                setting.MainHeroBackgroundColor = model.MainHeroBackgroundColor?.Trim() ?? "";

                setting.UseMainHeroSlider = model.UseMainHeroSlider;

                if (uploadedSliderImageUrls.Any())
                {
                    var existingSliderUrls = model.MainHeroSliderImageUrls?.Trim() ?? "";

                    var mergedSliderUrls = string.IsNullOrWhiteSpace(existingSliderUrls)
                        ? string.Join(Environment.NewLine, uploadedSliderImageUrls)
                        : existingSliderUrls + Environment.NewLine + string.Join(Environment.NewLine, uploadedSliderImageUrls);

                    setting.MainHeroSliderImageUrls = mergedSliderUrls;
                }
                else
                {
                    setting.MainHeroSliderImageUrls = model.MainHeroSliderImageUrls?.Trim() ?? "";
                }

                setting.MainHeroSliderLinkUrls = model.MainHeroSliderLinkUrls?.Trim() ?? "";
            }
            catch (InvalidOperationException ex)
            {
                TempData["AdminSiteDesignMessage"] = ex.Message;
                return View(model);
            }

            setting.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData["AdminSiteDesignMessage"] = "디자인 설정이 저장되었습니다.";

            return RedirectToAction("SiteDesign");
        }

    }
}