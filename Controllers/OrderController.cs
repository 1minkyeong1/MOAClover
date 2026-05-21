using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Models.ViewModels;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using MOAClover.Services;
using System.Security.Claims;

namespace MOAClover.Controllers
{
    public class OrderController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly OrderStockService _orderStockService; 
        private readonly IEmailService _emailService;

        public OrderController(
            ApplicationDbContext context,
            IConfiguration configuration,
            OrderStockService orderStockService,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _orderStockService = orderStockService;
            _emailService = emailService;
        }

        private string? GetUserId()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return User.Identity.Name;

            return null;
        }

        private string? GetGuestCartId()
        {
            const string cookieName = "MOA_GUEST_CART_SESSION_ID";

            if (Request.Cookies.TryGetValue(cookieName, out var existing)
                && !string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            return null;
        }

        private IQueryable<CartItem> MyCartQuery()
        {
            var userId = GetUserId();

            if (!string.IsNullOrWhiteSpace(userId))
                return _context.CartItems.Where(x => x.UserId == userId);

            var guestCartId = GetGuestCartId();

            if (string.IsNullOrWhiteSpace(guestCartId))
                return _context.CartItems.Where(_ => false);

            return _context.CartItems.Where(x => x.GuestCartId == guestCartId);
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

        private CheckoutViewModel BuildCheckoutViewModel()
        {
            var cartItems = MyCartQuery()
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            var productIds = cartItems
                .Select(x => x.ProductId)
                .Distinct()
                .ToList();

            var model = new CheckoutViewModel();

            if (productIds.Count == 0)
                return model;

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

                model.Items.Add(new CheckoutItemViewModel
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

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var loginId = User.Identity.Name ?? "";

                var user = _context.Users
                    .AsNoTracking()
                    .FirstOrDefault(u => u.UserName == loginId);

                if (user != null)
                {
                    model.BuyerName = user.Name ?? "";
                    model.BuyerPhone = user.Phone ?? "";
                    model.BuyerEmail = user.Email ?? "";

                    var addresses = _context.UserAddresses
                        .AsNoTracking()
                        .Where(a => a.UserId == user.Id)
                        .OrderByDescending(a => a.IsDefault)
                        .ThenByDescending(a => a.Id)
                        .ToList();

                    model.SavedAddresses = addresses.Select(a => new CheckoutSavedAddressViewModel
                    {
                        Id = a.Id,
                        ReceiverName = a.ReceiverName,
                        ReceiverPhone = a.ReceiverPhone,
                        ZipCode = a.ZipCode,
                        Address = a.Address,
                        AddressDetail = a.AddressDetail,
                        IsDefault = a.IsDefault
                    }).ToList();

                    var defaultAddress = addresses.FirstOrDefault(a => a.IsDefault)
                        ?? addresses.FirstOrDefault();

                    if (defaultAddress != null)
                    {
                        model.SelectedAddressId = defaultAddress.Id;
                        model.ReceiverName = string.IsNullOrWhiteSpace(defaultAddress.ReceiverName)
                            ? model.BuyerName
                            : defaultAddress.ReceiverName;
                        model.ReceiverPhone = string.IsNullOrWhiteSpace(defaultAddress.ReceiverPhone)
                            ? model.BuyerPhone
                            : defaultAddress.ReceiverPhone;
                        model.ZipCode = defaultAddress.ZipCode;
                        model.Address1 = defaultAddress.Address;
                        model.Address2 = defaultAddress.AddressDetail;
                    }
                }
                else
                {
                    model.BuyerName = loginId;
                }
            }

            return model;
        }

        private BankTransferViewModel GetBankTransferInfo()
        {
            var setting = _context.SiteSettings
                .AsNoTracking()
                .OrderBy(x => x.SiteSettingId)
                .FirstOrDefault();

            if (setting != null)
            {
                return new BankTransferViewModel
                {
                    BankName = setting.BankName ?? "",
                    AccountHolder = setting.BankAccountHolder ?? "",
                    AccountNumber = setting.BankAccountNumber ?? "",
                    Notice = setting.BankTransferNotice ?? ""
                };
            }

            var section = _configuration.GetSection("BankTransfer");

            return new BankTransferViewModel
            {
                BankName = section["BankName"] ?? "",
                AccountHolder = section["AccountHolder"] ?? "",
                AccountNumber = section["AccountNumber"] ?? "",
                Notice = section["Notice"] ?? ""
            };
        }

        private string? ValidateCheckoutStock(CheckoutViewModel model)
        {
            foreach (var item in model.Items)
            {
                var product = _context.Products
                    .AsNoTracking()
                    .FirstOrDefault(p => p.ProductId == item.ProductId
                                         && p.DeletedAt == null
                                         && p.IsVisible);

                if (product == null)
                {
                    return $"{item.ProductName} 상품 정보를 찾을 수 없습니다.";
                }

                if (!product.UseStock)
                    continue;

                if (product.StockQuantity <= 0)
                {
                    return $"{product.Name} 상품은 품절되었습니다.";
                }

                if (product.StockQuantity < item.Quantity)
                {
                    return $"{product.Name} 재고가 부족합니다. 현재 재고: {product.StockQuantity}개";
                }
            }

            return null;
        }

        private string CreateOrderNo()
        {
            return "MOA" + DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(1000, 9999);
        }

        private string BuildOrderCreatedEmailBody(Order order)
        {
            var orderTypeText = order.IsGuestOrder ? "비회원 주문" : "회원 주문";

            var itemsHtml = string.Join("", order.Items.Select(item => $@"
                <tr>
                    <td style='padding:10px;border-bottom:1px solid #eee;'>{item.ProductName}</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:center;'>{item.Quantity}개</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:right;'>{item.LineTotal:N0}원</td>
                </tr>
            "));

            var shippingText =
                order.IsCollectShipping ? "착불/별도" :
                order.IsIncludedShipping ? "배송비 포함" :
                $"{order.ShippingFee:N0}원";

            var guestNotice = order.IsGuestOrder
                ? @"
                    <div style='margin-top:16px;padding:14px;background:#fff7ed;border:1px solid #fed7aa;border-radius:10px;color:#9a3412;'>
                        <strong>비회원 주문조회 안내</strong><br/>
                        비회원 주문은 주문번호와 주문조회 비밀번호로 조회할 수 있습니다.<br/>
                        주문번호를 꼭 보관해주세요.
                    </div>
                "
                : "";

             return $@"
                <div style='font-family:Arial, sans-serif;max-width:680px;margin:0 auto;color:#111827;'>
                    <h2 style='margin:0 0 10px;'>[모아클로버] 주문이 접수되었습니다.</h2>
                    <p style='margin:0 0 20px;color:#6b7280;'>주문 내용을 확인해주세요.</p>

                    <div style='padding:16px;background:#f9fafb;border-radius:12px;margin-bottom:18px;'>
                        <div><strong>주문번호:</strong> {order.OrderNo}</div>
                        <div><strong>주문구분:</strong> {orderTypeText}</div>
                        <div><strong>주문일시:</strong> {order.CreatedAt:yyyy-MM-dd HH:mm}</div>
                        <div><strong>주문자:</strong> {order.BuyerName}</div>
                        <div><strong>연락처:</strong> {order.BuyerPhone}</div>
                    </div>

                    <h3 style='margin:20px 0 10px;'>주문 상품</h3>
                    <table style='width:100%;border-collapse:collapse;border-top:2px solid #111827;'>
                        <thead>
                            <tr>
                                <th style='padding:10px;text-align:left;border-bottom:1px solid #ddd;'>상품명</th>
                                <th style='padding:10px;text-align:center;border-bottom:1px solid #ddd;'>수량</th>
                                <th style='padding:10px;text-align:right;border-bottom:1px solid #ddd;'>금액</th>
                            </tr>
                        </thead>
                        <tbody>
                            {itemsHtml}
                        </tbody>
                    </table>

                    <h3 style='margin:20px 0 10px;'>결제 금액</h3>
                    <div style='padding:16px;background:#f9fafb;border-radius:12px;'>
                        <div><strong>상품금액:</strong> {order.ProductTotal:N0}원</div>
                        <div><strong>배송비:</strong> {shippingText}</div>
                        {(order.ExtraShippingFee > 0 ? $"<div><strong>지역 추가배송비:</strong> {order.ExtraShippingFee:N0}원</div>" : "")}
                        <div style='margin-top:8px;font-size:18px;'><strong>총 결제금액:</strong> {order.TotalPayAmount:N0}원</div>
                    </div>

                    <h3 style='margin:20px 0 10px;'>배송지</h3>
                    <div style='padding:16px;background:#f9fafb;border-radius:12px;'>
                        <div><strong>받는 사람:</strong> {order.ReceiverName}</div>
                        <div><strong>연락처:</strong> {order.ReceiverPhone}</div>
                        <div><strong>주소:</strong> ({order.ZipCode}) {order.Address1} {order.Address2}</div>
                        {(string.IsNullOrWhiteSpace(order.DeliveryMemo) ? "" : $"<div><strong>배송 요청사항:</strong> {order.DeliveryMemo}</div>")}
                    </div>

                    {guestNotice}

                    <p style='margin-top:24px;color:#6b7280;font-size:13px;line-height:1.6;'>
                        무통장입금 주문은 입금 확인 후 상품 준비가 시작됩니다.<br/>
                        카드결제 주문은 결제 완료 후 상품 준비가 시작됩니다.
                    </p>
                </div>
            ";
        }

        private string BuildBankPaymentCompletedEmailBody(Order order)
        {
            var itemsHtml = string.Join("", order.Items.Select(item => $@"
                <tr>
                    <td style='padding:10px;border-bottom:1px solid #eee;'>{item.ProductName}</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:center;'>{item.Quantity}개</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:right;'>{item.LineTotal:N0}원</td>
                </tr>
            "));

            return $@"
                <div style='font-family:Arial, sans-serif;max-width:680px;margin:0 auto;color:#111827;'>
                    <h2 style='margin:0 0 10px;'>[모아클로버] 입금확인이 완료되었습니다.</h2>
                    <p style='margin:0 0 20px;color:#6b7280;'>상품 준비가 시작됩니다.</p>

                    <div style='padding:16px;background:#f9fafb;border-radius:12px;margin-bottom:18px;'>
                        <div><strong>주문번호:</strong> {order.OrderNo}</div>
                        <div><strong>입금확인일시:</strong> {(order.PaidAt.HasValue ? order.PaidAt.Value.ToString("yyyy-MM-dd HH:mm") : DateTime.Now.ToString("yyyy-MM-dd HH:mm"))}</div>
                        <div><strong>주문자:</strong> {order.BuyerName}</div>
                        <div><strong>결제금액:</strong> {order.TotalPayAmount:N0}원</div>
                    </div>

                    <h3 style='margin:20px 0 10px;'>주문 상품</h3>
                    <table style='width:100%;border-collapse:collapse;border-top:2px solid #111827;'>
                        <thead>
                            <tr>
                                <th style='padding:10px;text-align:left;border-bottom:1px solid #ddd;'>상품명</th>
                                <th style='padding:10px;text-align:center;border-bottom:1px solid #ddd;'>수량</th>
                                <th style='padding:10px;text-align:right;border-bottom:1px solid #ddd;'>금액</th>
                            </tr>
                        </thead>
                        <tbody>
                            {itemsHtml}
                        </tbody>
                    </table>

                    <p style='margin-top:24px;color:#6b7280;font-size:13px;line-height:1.6;'>
                        배송이 시작되면 배송정보가 주문상세에 표시됩니다.
                    </p>
                </div>
            ";
        }

        private string BuildShippingStartedEmailBody(Order order)
        {
            var itemsHtml = string.Join("", order.Items.Select(item => $@"
                <tr>
                    <td style='padding:10px;border-bottom:1px solid #eee;'>{item.ProductName}</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:center;'>{item.Quantity}개</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:right;'>{item.LineTotal:N0}원</td>
                </tr>
            "));

            var trackingCompany = string.IsNullOrWhiteSpace(order.TrackingCompany)
                ? "-"
                : order.TrackingCompany;

            var trackingNumber = string.IsNullOrWhiteSpace(order.TrackingNumber)
                ? "-"
                : order.TrackingNumber;

            return $@"
                <div style='font-family:Arial, sans-serif;max-width:680px;margin:0 auto;color:#111827;'>
                    <h2 style='margin:0 0 10px;'>[모아클로버] 상품이 발송되었습니다.</h2>
                    <p style='margin:0 0 20px;color:#6b7280;'>
                        주문하신 상품의 배송이 시작되었습니다.
                    </p>

                    <div style='padding:16px;background:#f9fafb;border-radius:12px;margin-bottom:18px;'>
                        <div><strong>주문번호:</strong> {order.OrderNo}</div>
                        <div><strong>발송일시:</strong> {(order.ShippedAt.HasValue ? order.ShippedAt.Value.ToString("yyyy-MM-dd HH:mm") : DateTime.Now.ToString("yyyy-MM-dd HH:mm"))}</div>
                        <div><strong>받는 사람:</strong> {order.ReceiverName}</div>
                        <div><strong>배송지:</strong> ({order.ZipCode}) {order.Address1} {order.Address2}</div>
                    </div>

                    <h3 style='margin:20px 0 10px;'>배송 정보</h3>
                    <div style='padding:16px;background:#fff7ed;border:1px solid #fed7aa;border-radius:12px;color:#9a3412;margin-bottom:18px;'>
                        <div><strong>택배사:</strong> {trackingCompany}</div>
                        <div><strong>송장번호:</strong> {trackingNumber}</div>
                    </div>

                    <h3 style='margin:20px 0 10px;'>배송 상품</h3>
                    <table style='width:100%;border-collapse:collapse;border-top:2px solid #111827;'>
                        <thead>
                            <tr>
                                <th style='padding:10px;text-align:left;border-bottom:1px solid #ddd;'>상품명</th>
                                <th style='padding:10px;text-align:center;border-bottom:1px solid #ddd;'>수량</th>
                                <th style='padding:10px;text-align:right;border-bottom:1px solid #ddd;'>금액</th>
                            </tr>
                        </thead>
                        <tbody>
                            {itemsHtml}
                        </tbody>
                    </table>

                    <p style='margin-top:24px;color:#6b7280;font-size:13px;line-height:1.6;'>
                        택배사 배송조회 페이지에서 송장번호로 배송 상태를 확인할 수 있습니다.<br/>
                        배송 상황은 택배사 사정에 따라 반영 시간이 지연될 수 있습니다.
                    </p>
                </div>
            ";
        }

        private string BuildCancelRequestedEmailBody(Order order)
        {
            return $@"
                <div style='font-family:Arial, sans-serif; line-height:1.6; color:#111827;'>
                    <h2>취소/환불 요청이 접수되었습니다.</h2>

                    <p>
                        고객님의 주문 취소/환불 요청이 접수되었습니다.<br />
                        관리자 확인 후 결제수단에 따라 취소 또는 환불 처리가 진행됩니다.
                    </p>

                    <div style='margin:20px 0; padding:16px; background:#f9fafb; border-radius:12px;'>
                        <p><strong>주문번호</strong> : {order.OrderNo}</p>
                        <p><strong>주문자</strong> : {order.BuyerName}</p>
                        <p><strong>결제금액</strong> : {order.TotalPayAmount:N0}원</p>
                        <p><strong>현재 상태</strong> : 취소/환불 요청 접수</p>
                    </div>

                    <p style='color:#6b7280; font-size:13px;'>
                        카드결제 주문은 결제취소 승인 후 환불이 완료되며,
                        무통장입금 주문은 환불계좌 확인 후 수동 환불 처리됩니다.
                    </p>
                </div>";
        }

        private string BuildRefundCompletedEmailBody(Order order)
        {
            return $@"
                <div style='font-family:Arial, sans-serif; line-height:1.6; color:#111827;'>
                    <h2>취소/환불 처리가 완료되었습니다.</h2>

                    <p>
                        고객님의 주문 취소/환불 처리가 완료되었습니다.
                    </p>

                    <div style='margin:20px 0; padding:16px; background:#f9fafb; border-radius:12px;'>
                        <p><strong>주문번호</strong> : {order.OrderNo}</p>
                        <p><strong>주문자</strong> : {order.BuyerName}</p>
                        <p><strong>환불금액</strong> : {order.TotalPayAmount:N0}원</p>
                        <p><strong>처리상태</strong> : 환불완료</p>
                    </div>

                    <p style='color:#6b7280; font-size:13px;'>
                        카드결제 환불은 카드사 사정에 따라 실제 승인취소 반영까지 시간이 걸릴 수 있습니다.
                    </p>
                </div>";
        }

        private string BuildReturnExchangeRequestedEmailBody(Order order, string requestType)
        {
            var title = requestType == "exchange"
                ? "교환 요청이 접수되었습니다."
                : "반품 요청이 접수되었습니다.";

            var requestText = requestType == "exchange"
                ? "교환 요청"
                : "반품 요청";

            return $@"
                <div style='font-family:Arial, sans-serif; line-height:1.6; color:#111827;'>
                    <h2>{title}</h2>

                    <p>
                        고객님의 {requestText}이 접수되었습니다.<br />
                        관리자 확인 후 처리 절차가 진행됩니다.
                    </p>

                    <div style='margin:20px 0; padding:16px; background:#f9fafb; border-radius:12px;'>
                        <p><strong>주문번호</strong> : {order.OrderNo}</p>
                        <p><strong>주문자</strong> : {order.BuyerName}</p>
                        <p><strong>결제금액</strong> : {order.TotalPayAmount:N0}원</p>
                        <p><strong>요청상태</strong> : {requestText} 접수</p>
                    </div>

                    <p style='color:#6b7280; font-size:13px;'>
                        상품 상태 확인 및 회수/교환 절차가 필요한 경우 별도 안내드릴 수 있습니다.
                    </p>
                </div>";
        }

        private string BuildReturnExchangeProcessedEmailBody(Order order, string processType, string? adminMemo)
        {
            var title = processType switch
            {
                "returned" => "반품 처리가 완료되었습니다.",
                "exchanged" => "교환 처리가 완료되었습니다.",
                "rejected" => "반품/교환 요청이 거절되었습니다.",
                _ => "반품/교환 요청 처리 결과 안내"
            };

            var statusText = processType switch
            {
                "returned" => "반품완료",
                "exchanged" => "교환완료",
                "rejected" => "요청거절",
                _ => "처리완료"
            };

            return $@"
                <div style='font-family:Arial, sans-serif; line-height:1.6; color:#111827;'>
                    <h2>{title}</h2>

                    <p>
                        고객님의 주문에 대한 반품/교환 요청 처리 결과를 안내드립니다.
                    </p>

                    <div style='margin:20px 0; padding:16px; background:#f9fafb; border-radius:12px;'>
                        <p><strong>주문번호</strong> : {order.OrderNo}</p>
                        <p><strong>주문자</strong> : {order.BuyerName}</p>
                        <p><strong>처리상태</strong> : {statusText}</p>
                        <p><strong>결제금액</strong> : {order.TotalPayAmount:N0}원</p>
                    </div>

                    {(string.IsNullOrWhiteSpace(adminMemo) ? "" : $@"
                        <div style='margin:20px 0; padding:14px; background:#fff7ed; border-radius:12px; color:#9a3412;'>
                            <strong>처리 메모</strong><br />
                            {adminMemo}
                        </div>
                    ")}

                    <p style='color:#6b7280; font-size:13px;'>
                        자세한 내용은 주문상세 또는 고객센터를 통해 확인해주세요.
                    </p>
                </div>";
        }

        private string BuildOrderCancelledEmailBody(Order order)
        {
            return $@"
                <div style='font-family:Arial, sans-serif; line-height:1.6; color:#111827;'>
                    <h2>주문이 취소되었습니다.</h2>

                    <p>
                        고객님의 주문이 정상적으로 취소되었습니다.
                    </p>

                    <div style='margin:20px 0; padding:16px; background:#f9fafb; border-radius:12px;'>
                        <p><strong>주문번호</strong> : {order.OrderNo}</p>
                        <p><strong>주문자</strong> : {order.BuyerName}</p>
                        <p><strong>주문금액</strong> : {order.TotalPayAmount:N0}원</p>
                        <p><strong>처리상태</strong> : 주문취소 완료</p>
                    </div>

                    <p style='color:#6b7280; font-size:13px;'>
                        결제 전 주문이므로 별도 환불 절차는 진행되지 않습니다.
                    </p>
                </div>";
        }

        private string BuildCancelRequestWithdrawnEmailBody(Order order)
        {
            return $@"
                <div style='font-family:Arial, sans-serif; line-height:1.6; color:#111827;'>
                    <h2>취소/환불 요청이 철회되었습니다.</h2>

                    <p>
                        고객님의 요청에 따라 취소/환불 요청이 철회 처리되었습니다.<br />
                        주문은 다시 정상 처리 상태로 진행됩니다.
                    </p>

                    <div style='margin:20px 0; padding:16px; background:#f9fafb; border-radius:12px;'>
                        <p><strong>주문번호</strong> : {order.OrderNo}</p>
                        <p><strong>주문자</strong> : {order.BuyerName}</p>
                        <p><strong>결제금액</strong> : {order.TotalPayAmount:N0}원</p>
                        <p><strong>현재 상태</strong> : 배송준비중</p>
                    </div>

                    <p style='color:#6b7280; font-size:13px;'>
                        상품 준비 및 배송 절차가 계속 진행됩니다.
                    </p>
                </div>";
        }

        private void AddEmailFailedHistory(Order order, string emailType, Exception ex)
        {
            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = order.OrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = order.PaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                AdminMemo = $"{emailType} 이메일 발송 실패: {ex.Message}",
                ChangedBy = "EmailService",
                CreatedAt = DateTime.Now
            });
        }

        [HttpGet]
        public IActionResult Start()
        {
            var hasItems = MyCartQuery().Any();

            if (!hasItems)
            {
                TempData["CartMessage"] = "주문할 상품이 없습니다.";
                return RedirectToAction("Index", "Cart");
            }

            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Checkout");
            }

            return View();
        }

        [HttpGet]
        public IActionResult Checkout(bool guest = false)
        {
            var model = BuildCheckoutViewModel();

            if (model.Items.Count == 0)
            {
                TempData["CartMessage"] = "주문할 상품이 없습니다.";
                return RedirectToAction("Index", "Cart");
            }

            var stockError = ValidateCheckoutStock(model);

            if (!string.IsNullOrWhiteSpace(stockError))
            {
                TempData["CartMessage"] = stockError;
                return RedirectToAction("Index", "Cart");
            }

            model.IsGuestOrder = guest || !(User.Identity?.IsAuthenticated ?? false);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel vm)
        {
            var model = BuildCheckoutViewModel();

            model.IsGuestOrder = vm.IsGuestOrder || !(User.Identity?.IsAuthenticated ?? false);

            model.BuyerName = vm.BuyerName;
            model.BuyerPhone = vm.BuyerPhone;
            model.BuyerEmail = vm.BuyerEmail;
            model.ReceiverName = vm.ReceiverName;
            model.ReceiverPhone = vm.ReceiverPhone;
            model.ZipCode = vm.ZipCode;
            model.Address1 = vm.Address1;
            model.Address2 = vm.Address2;
            model.DeliveryMemo = vm.DeliveryMemo;

            model.SelectedAddressId = vm.SelectedAddressId;
            model.SaveAddressToMyPage = vm.SaveAddressToMyPage;
            model.SetAsDefaultAddress = vm.SetAsDefaultAddress;
            model.PaymentMethod = string.IsNullOrWhiteSpace(vm.PaymentMethod)
                ? "BankTransfer"
                : vm.PaymentMethod;

            if (model.Items.Count == 0)
            {
                TempData["CartMessage"] = "주문할 상품이 없습니다.";
                return RedirectToAction("Index", "Cart");
            }

            var stockError = ValidateCheckoutStock(model);

            if (!string.IsNullOrWhiteSpace(stockError))
            {
                ModelState.AddModelError("", stockError);
            }

            // 비회원 주문일 때만 주문조회 비밀번호 검사
            if (model.IsGuestOrder)
            {
                if (string.IsNullOrWhiteSpace(vm.GuestOrderPassword) ||
                    string.IsNullOrWhiteSpace(vm.GuestOrderPasswordConfirm))
                {
                    ModelState.AddModelError("", "비회원 주문조회 비밀번호를 입력해주세요.");
                }
                else if (vm.GuestOrderPassword != vm.GuestOrderPasswordConfirm)
                {
                    ModelState.AddModelError("", "비회원 주문조회 비밀번호가 일치하지 않습니다.");
                }
                else if (vm.GuestOrderPassword.Length < 4)
                {
                    ModelState.AddModelError("", "비회원 주문조회 비밀번호는 4자 이상 입력해주세요.");
                }
            }

            if (!ModelState.IsValid)
                return View(model);

            var orderNo = CreateOrderNo();

            var order = new Order
            {
                OrderNo = orderNo,
                UserId = GetUserId(),
                GuestCartId = GetGuestCartId(),

                IsGuestOrder = model.IsGuestOrder,

                BuyerName = model.BuyerName.Trim(),
                BuyerPhone = model.BuyerPhone.Trim(),
                BuyerEmail = model.BuyerEmail,

                ReceiverName = model.ReceiverName.Trim(),
                ReceiverPhone = model.ReceiverPhone.Trim(),
                ZipCode = model.ZipCode.Trim(),
                Address1 = model.Address1.Trim(),
                Address2 = model.Address2,
                DeliveryMemo = model.DeliveryMemo,

                ProductTotal = model.ProductTotal,
                ShippingFee = model.ShippingFee,
                ExtraShippingFee = model.ExtraShippingFee,
                TotalPayAmount = model.TotalPayAmount,

                IsCollectShipping = model.HasCollectShipping,
                IsIncludedShipping = model.HasIncludedShipping,

                OrderStatus = "PendingPayment",
                PaymentStatus = "Ready",
                PaymentMethod = model.PaymentMethod,
                CreatedAt = DateTime.Now
            };

            if (model.IsGuestOrder)
            {
                var passwordHasher = new PasswordHasher<Order>();
                order.GuestOrderPasswordHash =
                    passwordHasher.HashPassword(order, vm.GuestOrderPassword!);
            }

            foreach (var item in model.Items)
            {
                order.Items.Add(new OrderItem
                {
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    MainImageUrl = item.MainImageUrl,
                    UnitPrice = item.Price,
                    DiscountRate = item.DiscountRate,
                    FinalUnitPrice = item.FinalPrice,
                    Quantity = item.Quantity,
                    LineTotal = item.LineTotal,
                    CreatedAt = DateTime.Now
                });
            }

            _context.Orders.Add(order);
            _context.SaveChanges();

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = $"[모아클로버] 주문이 접수되었습니다. ({order.OrderNo})";
                    var body = BuildOrderCreatedEmailBody(order);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);

                    order.OrderEmailSentAt = DateTime.Now;
                }
                catch (Exception ex)
                {
                    AddEmailFailedHistory(order, "주문접수", ex);
                }
            }

            _context.Payments.Add(new Payment
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                Provider = model.PaymentMethod == "BankTransfer"
                     ? "BankTransfer"
                     : model.PaymentMethod == "TossPayments"
                         ? "TossPayments"
                         : "Manual",

                PaymentStatus = "Ready",
                Amount = order.TotalPayAmount,
                CreatedAt = DateTime.Now
            });

            if (!model.IsGuestOrder && (model.SaveAddressToMyPage || model.SetAsDefaultAddress))
            {
                var loginId = User.Identity?.Name ?? "";

                var user = _context.Users
                    .FirstOrDefault(u => u.UserName == loginId);

                if (user != null)
                {
                    UserAddress? targetAddress = null;

                    // 1) 저장된 배송지를 선택한 경우, 그 배송지를 우선 사용
                    if (model.SelectedAddressId.HasValue && model.SelectedAddressId.Value > 0)
                    {
                        targetAddress = _context.UserAddresses
                            .FirstOrDefault(a => a.Id == model.SelectedAddressId.Value
                                                 && a.UserId == user.Id);
                    }

                    // 2) 선택된 배송지가 없으면 같은 주소가 있는지 찾기
                    if (targetAddress == null)
                    {
                        targetAddress = _context.UserAddresses.FirstOrDefault(a =>
                            a.UserId == user.Id
                            && a.ZipCode == model.ZipCode
                            && a.Address == model.Address1
                            && a.AddressDetail == model.Address2);
                    }

                    // 3) 없으면 새로 추가
                    if (targetAddress == null)
                    {
                        targetAddress = new UserAddress
                        {
                            UserId = user.Id,
                            ReceiverName = model.ReceiverName.Trim(),
                            ReceiverPhone = model.ReceiverPhone.Trim(),
                            ZipCode = model.ZipCode.Trim(),
                            Address = model.Address1.Trim(),
                            AddressDetail = model.Address2?.Trim(),
                            IsDefault = false
                        };

                        _context.UserAddresses.Add(targetAddress);
                    }
                    else
                    {
                        // 기존 배송지를 선택했거나 같은 주소가 있으면 정보 갱신
                        targetAddress.ReceiverName = model.ReceiverName.Trim();
                        targetAddress.ReceiverPhone = model.ReceiverPhone.Trim();
                        targetAddress.ZipCode = model.ZipCode.Trim();
                        targetAddress.Address = model.Address1.Trim();
                        targetAddress.AddressDetail = model.Address2?.Trim();
                    }

                    // 4) 대표 배송지로 지정한 경우 기존 대표 해제 후 현재 배송지를 대표로
                    if (model.SetAsDefaultAddress)
                    {
                        var oldDefaults = _context.UserAddresses
                            .Where(a => a.UserId == user.Id && a.IsDefault)
                            .ToList();

                        foreach (var old in oldDefaults)
                        {
                            old.IsDefault = false;
                        }

                        targetAddress.IsDefault = true;
                    }
                }
            }

            var cartItems = MyCartQuery().ToList();
            if (cartItems.Any())
                _context.CartItems.RemoveRange(cartItems);

            _context.SaveChanges();

            if (model.PaymentMethod == "TossPayments")
            {
                return RedirectToAction("Pay", "Payment", new { orderNo = order.OrderNo });
            }

            return RedirectToAction("Complete", new { orderNo = order.OrderNo });
        }

        [HttpGet]
        public IActionResult Complete(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var order = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            ViewBag.BankTransfer = GetBankTransferInfo();

            return View(order);
        }

        // 비회원 주문조회
        [HttpGet]
        public IActionResult GuestLookup()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GuestLookup(string orderNo, string guestOrderPassword)
        {
            if (string.IsNullOrWhiteSpace(orderNo) || string.IsNullOrWhiteSpace(guestOrderPassword))
            {
                ModelState.AddModelError("", "주문번호와 비회원 주문 비밀번호를 입력해주세요.");
                return View();
            }

            orderNo = orderNo.Trim();

            var order = _context.Orders
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo && o.IsGuestOrder);

            if (order == null || string.IsNullOrWhiteSpace(order.GuestOrderPasswordHash))
            {
                ModelState.AddModelError("", "입력하신 정보와 일치하는 비회원 주문을 찾을 수 없습니다.");
                return View();
            }

            var passwordHasher = new PasswordHasher<Order>();

            var verifyResult = passwordHasher.VerifyHashedPassword(
                order,
                order.GuestOrderPasswordHash,
                guestOrderPassword
            );

            if (verifyResult == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "입력하신 정보와 일치하는 비회원 주문을 찾을 수 없습니다.");
                return View();
            }

            ViewBag.BankTransfer = GetBankTransferInfo();

            return View("GuestDetail", order);
        }

        [HttpGet]
        public IActionResult FindGuestOrderNo()
        {
            return View(new FindGuestOrderNoViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendGuestOrderNoEmail(
    string buyerName,
    string buyerPhone,
    string buyerEmail)
        {
            buyerName = buyerName?.Trim() ?? "";
            buyerPhone = buyerPhone?.Trim() ?? "";
            buyerEmail = buyerEmail?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(buyerName)
                || string.IsNullOrWhiteSpace(buyerPhone)
                || string.IsNullOrWhiteSpace(buyerEmail))
            {
                TempData["GuestFindMessage"] = "주문자명, 연락처, 이메일을 모두 입력해주세요.";
                return RedirectToAction("FindGuestOrderNo");
            }

            var orders = _context.Orders
                .AsNoTracking()
                .Where(o => o.IsGuestOrder
                            && o.BuyerName == buyerName
                            && o.BuyerPhone == buyerPhone
                            && o.BuyerEmail == buyerEmail)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToList();

            if (!orders.Any())
            {
                TempData["GuestFindMessage"] = "입력하신 정보와 일치하는 비회원 주문을 찾을 수 없습니다.";
                return RedirectToAction("FindGuestOrderNo");
            }

            var orderRows = string.Join("", orders.Select(o => $@"
                <tr>
                    <td style='padding:10px;border-bottom:1px solid #eee;'>{o.OrderNo}</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;'>{o.CreatedAt:yyyy-MM-dd HH:mm}</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:right;'>{o.TotalPayAmount:N0}원</td>
                </tr>
            "));

            var body = $@"
                <div style='font-family:Arial, sans-serif;max-width:680px;margin:0 auto;color:#111827;'>
                    <h2 style='margin:0 0 10px;'>[모아클로버] 비회원 주문번호 안내</h2>
                    <p style='margin:0 0 20px;color:#6b7280;'>
                        요청하신 비회원 주문번호입니다.
                    </p>

                    <div style='padding:16px;background:#f9fafb;border-radius:12px;margin-bottom:18px;'>
                        <div><strong>주문자명:</strong> {buyerName}</div>
                        <div><strong>연락처:</strong> {buyerPhone}</div>
                        <div><strong>이메일:</strong> {buyerEmail}</div>
                    </div>

                    <table style='width:100%;border-collapse:collapse;border-top:2px solid #111827;'>
                        <thead>
                            <tr>
                                <th style='padding:10px;text-align:left;border-bottom:1px solid #ddd;'>주문번호</th>
                                <th style='padding:10px;text-align:left;border-bottom:1px solid #ddd;'>주문일시</th>
                                <th style='padding:10px;text-align:right;border-bottom:1px solid #ddd;'>결제금액</th>
                            </tr>
                        </thead>
                        <tbody>
                            {orderRows}
                        </tbody>
                    </table>

                    <p style='margin-top:24px;color:#6b7280;font-size:13px;line-height:1.6;'>
                        비회원 주문조회 페이지에서 주문번호와 주문조회 비밀번호를 입력하면 주문상세를 확인할 수 있습니다.
                    </p>
                </div>
            ";

            try
            {
                await _emailService.SendEmailAsync(
                    buyerEmail,
                    "[모아클로버] 비회원 주문번호 안내",
                    body
                );

                TempData["GuestFindMessage"] = "입력하신 이메일로 주문번호를 발송했습니다.";
            }
            catch
            {
                TempData["GuestFindMessage"] = "이메일 발송에 실패했습니다. 잠시 후 다시 시도해주세요.";
            }

            return RedirectToAction("FindGuestOrderNo");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult FindGuestOrderNo(FindGuestOrderNoViewModel vm)
        {
            vm.Searched = true;

            if (!ModelState.IsValid)
                return View(vm);

            var name = vm.BuyerName.Trim();
            var phone = vm.BuyerPhone.Trim();
            var email = vm.BuyerEmail.Trim();

            var orders = _context.Orders
                .AsNoTracking()
                .Where(o => o.IsGuestOrder
                            && o.BuyerName == name
                            && o.BuyerPhone == phone
                            && o.BuyerEmail == email)
                .OrderByDescending(o => o.CreatedAt)
                .Take(10)
                .ToList();

            vm.Results = orders.Select(o => new GuestOrderNoResultItem
            {
                OrderNo = o.OrderNo,
                CreatedAt = o.CreatedAt,
                TotalPayAmount = o.TotalPayAmount,
                OrderStatus = o.OrderStatus,
                PaymentStatus = o.PaymentStatus
            }).ToList();

            return View(vm);
        }

        [Authorize]
        [HttpGet]
        public IActionResult MyOrders(int page = 1)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/MyOrders" });

            const int pageSize = 10;

            if (page < 1)
                page = 1;

            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .Where(o => o.UserId == userId && !o.IsGuestOrder)
                .OrderByDescending(o => o.CreatedAt);

            var totalCount = query.Count();

            var orders = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return View(orders);
        }

        [Authorize]
        [HttpGet]
        public IActionResult MyOrderDetail(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/MyOrders" });

            var order = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo
                                     && o.UserId == userId
                                     && !o.IsGuestOrder);

            if (order == null)
                return NotFound();

            ViewBag.BankTransfer = GetBankTransferInfo();

            var payment = _context.Payments
                .AsNoTracking()
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            ViewBag.Payment = payment;

            return View(order);
        }

        // 구매내역 영수증 보기
        [Authorize]
        [HttpGet]
        public IActionResult Receipt(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/MyOrders" });

            var order = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo
                                     && o.UserId == userId
                                     && !o.IsGuestOrder);

            if (order == null)
                return NotFound();

            var payment = _context.Payments
                .AsNoTracking()
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            ViewBag.Payment = payment;

            return View(order);
        }

        // 주문 취소 / 취소요청
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMyOrder(string orderNo, string? cancelReason)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/MyOrders" });

            var order = _context.Orders
                .FirstOrDefault(o => o.OrderNo == orderNo
                                     && o.UserId == userId
                                     && !o.IsGuestOrder);

            if (order == null)
                return NotFound();

            // 이미 취소/취소요청 상태면 중복 처리 방지
            if (order.OrderStatus == "Cancelled")
            {
                TempData["MyOrderMessage"] = "이미 취소된 주문입니다.";
                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            if (order.OrderStatus == "CancelRequested")
            {
                TempData["MyOrderMessage"] = "이미 취소/환불 요청이 접수된 주문입니다.";
                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            // 배송 시작 이후는 고객 직접 취소요청 불가
            if (order.OrderStatus == "Shipped" || order.OrderStatus == "Completed")
            {
                TempData["MyOrderMessage"] = "배송중 또는 배송완료 주문은 직접 취소할 수 없습니다. 고객센터로 문의해주세요.";
                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            cancelReason = cancelReason?.Trim();

            // 1) 결제 전 주문: 바로 취소 가능
            if (order.PaymentStatus == "Ready" || order.OrderStatus == "PendingPayment")
            {
                order.OrderStatus = "Cancelled";
                order.PaymentStatus = "Cancelled";
                order.CancelReason = string.IsNullOrWhiteSpace(cancelReason)
                    ? "구매자 직접 취소"
                    : cancelReason;

                if (order.CancelledAt == null)
                    order.CancelledAt = DateTime.Now;

                await _orderStockService.RestoreStockAsync(order);

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,

                    BeforeOrderStatus = beforeOrderStatus,
                    AfterOrderStatus = order.OrderStatus,

                    BeforePaymentStatus = beforePaymentStatus,
                    AfterPaymentStatus = order.PaymentStatus,

                    CancelReason = order.CancelReason,
                    AdminMemo = "구매자 직접 주문취소",
                    ChangedBy = userId,
                    CreatedAt = DateTime.Now
                });

                if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
                {
                    try
                    {
                        var subject = $"[모아클로버] 주문이 취소되었습니다. ({order.OrderNo})";
                        var body = BuildOrderCancelledEmailBody(order);

                        await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                    }
                    catch (Exception ex)
                    {
                        AddEmailFailedHistory(order, "주문취소", ex);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["MyOrderMessage"] = "주문이 취소되었습니다.";
                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            // 2) 결제 완료 + 배송 전 주문: 환불 완료가 아니라 취소요청만 접수
            if (order.PaymentStatus == "Paid"
                && (order.OrderStatus == "Paid" || order.OrderStatus == "Preparing"))
            {
                order.OrderStatus = "CancelRequested";
                order.PaymentStatus = "Paid";
                order.CancelReason = string.IsNullOrWhiteSpace(cancelReason)
                    ? "구매자 취소/환불 요청"
                    : cancelReason;

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,

                    BeforeOrderStatus = beforeOrderStatus,
                    AfterOrderStatus = order.OrderStatus,

                    BeforePaymentStatus = beforePaymentStatus,
                    AfterPaymentStatus = order.PaymentStatus,

                    CancelReason = order.CancelReason,
                    AdminMemo = "구매자 취소/환불 요청 접수",
                    ChangedBy = userId,
                    CreatedAt = DateTime.Now
                });

                // 우선 취소요청 상태를 먼저 저장
                await _context.SaveChangesAsync();

                var cancelRequestEmailFailed = false;

                if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
                {
                    try
                    {
                        var subject = $"[모아클로버] 취소/환불 요청이 접수되었습니다. ({order.OrderNo})";
                        var body = BuildCancelRequestedEmailBody(order);

                        await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                    }
                    catch (Exception ex)
                    {
                        cancelRequestEmailFailed = true;

                        AddEmailFailedHistory(order, "취소요청 접수", ex);

                        await _context.SaveChangesAsync();
                    }
                }

                TempData["MyOrderMessage"] = cancelRequestEmailFailed
                    ? "취소/환불 요청은 접수되었지만, 안내 이메일 발송에 실패했습니다."
                    : "취소/환불 요청이 접수되었습니다. 관리자 확인 후 처리됩니다.";

                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            TempData["MyOrderMessage"] = "현재 상태에서는 직접 취소할 수 없습니다. 고객센터로 문의해주세요.";
            return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
        }

        // 반품 / 교환 요청
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestReturnExchange(
            string orderNo,
            string requestType,
            string? requestReason)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            orderNo = orderNo.Trim();
            requestType = requestType?.Trim() ?? "";

            if (requestType != "return" && requestType != "exchange")
            {
                TempData["MyOrderMessage"] = "요청 구분이 올바르지 않습니다.";
                return RedirectToAction("MyOrders");
            }

            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(userId))
            {
                return RedirectToAction("Login", "Account", new { returnUrl = "/Order/MyOrders" });
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo
                                          && o.UserId == userId
                                          && !o.IsGuestOrder);

            if (order == null)
            {
                TempData["MyOrderMessage"] = "주문 정보를 찾을 수 없습니다.";
                return RedirectToAction("MyOrders");
            }

            if (order.PaymentStatus != "Paid")
            {
                TempData["MyOrderMessage"] = "결제완료 주문만 반품/교환 요청할 수 있습니다.";
                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            if (order.OrderStatus != "Shipped" && order.OrderStatus != "Completed")
            {
                TempData["MyOrderMessage"] = "배송중 또는 배송완료 주문만 반품/교환 요청할 수 있습니다.";
                return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            order.OrderStatus = requestType == "exchange"
                ? "ExchangeRequested"
                : "ReturnRequested";

            order.CancelReason = string.IsNullOrWhiteSpace(requestReason)
                ? (requestType == "exchange" ? "고객 교환 요청" : "고객 반품 요청")
                : requestReason.Trim();

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                CancelReason = order.CancelReason,
                AdminMemo = requestType == "exchange"
                    ? "고객 교환 요청 접수"
                    : "고객 반품 요청 접수",

                ChangedBy = userId ?? "Customer",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var emailFailed = false;

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = requestType == "exchange"
                        ? $"[모아클로버] 교환 요청이 접수되었습니다. ({order.OrderNo})"
                        : $"[모아클로버] 반품 요청이 접수되었습니다. ({order.OrderNo})";

                    var body = BuildReturnExchangeRequestedEmailBody(order, requestType);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    emailFailed = true;

                    AddEmailFailedHistory(
                        order,
                        requestType == "exchange" ? "교환요청 접수" : "반품요청 접수",
                        ex
                    );

                    await _context.SaveChangesAsync();
                }
            }

            TempData["MyOrderMessage"] = emailFailed
                ? "요청은 접수되었지만, 안내 이메일 발송에 실패했습니다."
                : (requestType == "exchange"
                    ? "교환 요청이 접수되었습니다. 관리자 확인 후 처리됩니다."
                    : "반품 요청이 접수되었습니다. 관리자 확인 후 처리됩니다.");

            return RedirectToAction("MyOrderDetail", new { orderNo = order.OrderNo });
        }

        // 비회원 주문 취소 / 취소요청
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelGuestOrder(
            string orderNo,
            string guestOrderPassword,
            string? cancelReason)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            if (string.IsNullOrWhiteSpace(guestOrderPassword))
            {
                TempData["GuestOrderMessage"] = "비회원 주문조회 비밀번호를 입력해주세요.";
                return RedirectToAction("GuestLookup");
            }

            orderNo = orderNo.Trim();

            var order = _context.Orders
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo && o.IsGuestOrder);

            if (order == null || string.IsNullOrWhiteSpace(order.GuestOrderPasswordHash))
                return NotFound();

            var passwordHasher = new PasswordHasher<Order>();

            var verifyResult = passwordHasher.VerifyHashedPassword(
                order,
                order.GuestOrderPasswordHash,
                guestOrderPassword
            );

            if (verifyResult == PasswordVerificationResult.Failed)
            {
                TempData["GuestOrderMessage"] = "비회원 주문조회 비밀번호가 일치하지 않습니다.";
                return RedirectToAction("GuestLookup");
            }

            if (order.OrderStatus == "Cancelled")
            {
                TempData["GuestOrderMessage"] = "이미 취소된 주문입니다.";
                return RedirectToAction("GuestLookup");
            }

            if (order.OrderStatus == "CancelRequested")
            {
                TempData["GuestOrderMessage"] = "이미 취소/환불 요청이 접수된 주문입니다.";
                return RedirectToAction("GuestLookup");
            }

            if (order.OrderStatus == "Shipped" || order.OrderStatus == "Completed")
            {
                TempData["GuestOrderMessage"] = "배송중 또는 배송완료 주문은 직접 취소할 수 없습니다. 고객센터로 문의해주세요.";
                return RedirectToAction("GuestLookup");
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            cancelReason = cancelReason?.Trim();

            // 1) 결제 전 주문: 바로 취소
            if (order.PaymentStatus == "Ready" || order.OrderStatus == "PendingPayment")
            {
                order.OrderStatus = "Cancelled";
                order.PaymentStatus = "Cancelled";
                order.CancelReason = string.IsNullOrWhiteSpace(cancelReason)
                    ? "비회원 구매자 직접 취소"
                    : cancelReason;

                if (order.CancelledAt == null)
                    order.CancelledAt = DateTime.Now;

                await _orderStockService.RestoreStockAsync(order);

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,

                    BeforeOrderStatus = beforeOrderStatus,
                    AfterOrderStatus = order.OrderStatus,

                    BeforePaymentStatus = beforePaymentStatus,
                    AfterPaymentStatus = order.PaymentStatus,

                    CancelReason = order.CancelReason,
                    AdminMemo = "비회원 구매자 직접 주문취소",
                    ChangedBy = "Guest",
                    CreatedAt = DateTime.Now
                });

                if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
                {
                    try
                    {
                        var subject = $"[모아클로버] 주문이 취소되었습니다. ({order.OrderNo})";
                        var body = BuildOrderCancelledEmailBody(order);

                        await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                    }
                    catch (Exception ex)
                    {
                        AddEmailFailedHistory(order, "비회원 주문취소", ex);
                    }
                }

                await _context.SaveChangesAsync();

                TempData["GuestOrderMessage"] = "주문이 취소되었습니다.";
                return RedirectToAction("GuestLookup");
            }

            // 2) 결제 완료 + 배송 전 주문: 취소/환불 요청
            if (order.PaymentStatus == "Paid"
                && (order.OrderStatus == "Paid" || order.OrderStatus == "Preparing"))
            {
                order.OrderStatus = "CancelRequested";
                order.PaymentStatus = "Paid";
                order.CancelReason = string.IsNullOrWhiteSpace(cancelReason)
                    ? "비회원 구매자 취소/환불 요청"
                    : cancelReason;

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,

                    BeforeOrderStatus = beforeOrderStatus,
                    AfterOrderStatus = order.OrderStatus,

                    BeforePaymentStatus = beforePaymentStatus,
                    AfterPaymentStatus = order.PaymentStatus,

                    CancelReason = order.CancelReason,
                    AdminMemo = "비회원 구매자 취소/환불 요청 접수",
                    ChangedBy = "Guest",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                var cancelRequestEmailFailed = false;

                if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
                {
                    try
                    {
                        var subject = $"[모아클로버] 취소/환불 요청이 접수되었습니다. ({order.OrderNo})";
                        var body = BuildCancelRequestedEmailBody(order);

                        await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                    }
                    catch (Exception ex)
                    {
                        cancelRequestEmailFailed = true;

                        AddEmailFailedHistory(order, "비회원 취소요청 접수", ex);

                        await _context.SaveChangesAsync();
                    }
                }

                TempData["GuestOrderMessage"] = cancelRequestEmailFailed
                    ? "취소/환불 요청은 접수되었지만, 안내 이메일 발송에 실패했습니다."
                    : "취소/환불 요청이 접수되었습니다. 관리자 확인 후 처리됩니다.";

                return RedirectToAction("GuestLookup");
            }

            TempData["GuestOrderMessage"] = "현재 상태에서는 직접 취소할 수 없습니다. 고객센터로 문의해주세요.";
            return RedirectToAction("GuestLookup");
        }

        // 비회원 반품/교환 요청
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestGuestReturnExchange(
            string orderNo,
            string guestOrderPassword,
            string requestType,
            string? requestReason)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            if (string.IsNullOrWhiteSpace(guestOrderPassword))
            {
                TempData["GuestOrderMessage"] = "비회원 주문조회 비밀번호를 입력해주세요.";
                return RedirectToAction("GuestLookup");
            }

            orderNo = orderNo.Trim();
            requestType = requestType?.Trim() ?? "";

            if (requestType != "return" && requestType != "exchange")
            {
                TempData["GuestOrderMessage"] = "요청 구분이 올바르지 않습니다.";
                return RedirectToAction("GuestLookup");
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo && o.IsGuestOrder);

            if (order == null || string.IsNullOrWhiteSpace(order.GuestOrderPasswordHash))
                return NotFound();

            var passwordHasher = new PasswordHasher<Order>();

            var verifyResult = passwordHasher.VerifyHashedPassword(
                order,
                order.GuestOrderPasswordHash,
                guestOrderPassword
            );

            if (verifyResult == PasswordVerificationResult.Failed)
            {
                TempData["GuestOrderMessage"] = "비회원 주문조회 비밀번호가 일치하지 않습니다.";
                return RedirectToAction("GuestLookup");
            }

            if (order.PaymentStatus != "Paid")
            {
                TempData["GuestOrderMessage"] = "결제완료 주문만 반품/교환 요청할 수 있습니다.";
                return RedirectToAction("GuestLookup");
            }

            if (order.OrderStatus != "Shipped" && order.OrderStatus != "Completed")
            {
                TempData["GuestOrderMessage"] = "배송중 또는 배송완료 주문만 반품/교환 요청할 수 있습니다.";
                return RedirectToAction("GuestLookup");
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            order.OrderStatus = requestType == "exchange"
                ? "ExchangeRequested"
                : "ReturnRequested";

            order.CancelReason = string.IsNullOrWhiteSpace(requestReason)
                ? (requestType == "exchange" ? "비회원 교환 요청" : "비회원 반품 요청")
                : requestReason.Trim();

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                CancelReason = order.CancelReason,
                AdminMemo = requestType == "exchange"
                    ? "비회원 교환 요청 접수"
                    : "비회원 반품 요청 접수",

                ChangedBy = "Guest",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var emailFailed = false;

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = requestType == "exchange"
                        ? $"[모아클로버] 교환 요청이 접수되었습니다. ({order.OrderNo})"
                        : $"[모아클로버] 반품 요청이 접수되었습니다. ({order.OrderNo})";

                    var body = BuildReturnExchangeRequestedEmailBody(order, requestType);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    emailFailed = true;

                    AddEmailFailedHistory(
                        order,
                        requestType == "exchange" ? "비회원 교환요청 접수" : "비회원 반품요청 접수",
                        ex
                    );

                    await _context.SaveChangesAsync();
                }
            }

            TempData["GuestOrderMessage"] = emailFailed
                ? "요청은 접수되었지만, 안내 이메일 발송에 실패했습니다."
                : (requestType == "exchange"
                    ? "교환 요청이 접수되었습니다. 관리자 확인 후 처리됩니다."
                    : "반품 요청이 접수되었습니다. 관리자 확인 후 처리됩니다.");

            return RedirectToAction("GuestLookup");
        }


        // 관리자 주문관리
        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult AdminOrders(
            string? status,
            string? keyword,
            string? specialFilter,
            string? dateFilter,
            string? paymentMethod,
            string? orderType,
            DateTime? startDate,
            DateTime? endDate,
            int page = 1)
        {
            const int pageSize = 10;

            if (page < 1)
                page = 1;

            status = status?.Trim();
            keyword = keyword?.Trim();
            specialFilter = specialFilter?.Trim();
            dateFilter = dateFilter?.Trim();
            paymentMethod = paymentMethod?.Trim();
            orderType = orderType?.Trim();

            // =========================
            // 탭 숫자 카운트
            // =========================
            var countQuery = _context.Orders.AsNoTracking();

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            ViewBag.TodayOrderCount = countQuery
                .Count(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);

            ViewBag.IssueCount = countQuery
                .Count(o =>
                    o.OrderStatus == "CancelRequested"
                    || o.OrderStatus == "ReturnRequested"
                    || o.OrderStatus == "ExchangeRequested"
                    || (o.OrderStatus == "PendingPayment" && o.PaymentStatus == "Ready")
                    || o.OrderStatus == "Preparing");

            ViewBag.PendingPaymentCount = countQuery
                .Count(o => o.OrderStatus == "PendingPayment");

            ViewBag.PaidCount = countQuery
                .Count(o => o.OrderStatus == "Paid");

            ViewBag.PreparingCount = countQuery
                .Count(o => o.OrderStatus == "Preparing");

            ViewBag.ShippedCount = countQuery
                .Count(o => o.OrderStatus == "Shipped");

            ViewBag.CompletedCount = countQuery
                .Count(o => o.OrderStatus == "Completed");

            ViewBag.DoneCount = countQuery
                .Count(o =>
                    o.OrderStatus == "Completed"
                    || o.OrderStatus == "Cancelled"
                    || o.OrderStatus == "Returned"
                    || o.OrderStatus == "Exchanged"
                    || o.OrderStatus == "RequestRejected");

            ViewBag.CancelRequestedCount = countQuery
                .Count(o => o.OrderStatus == "CancelRequested");

            ViewBag.ReturnExchangeRequestedCount = countQuery
                .Count(o =>
                    o.OrderStatus == "ReturnRequested"
                    || o.OrderStatus == "ExchangeRequested");

            ViewBag.CancelledCount = countQuery
                .Count(o => o.OrderStatus == "Cancelled");

            ViewBag.MemoCount = countQuery
                .Count(o => o.AdminMemo != null && o.AdminMemo != "");

            ViewBag.CollectCount = countQuery
                .Count(o => o.IsCollectShipping);

            // =========================
            // 실제 목록 조회
            // =========================
            var query = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .AsQueryable();

            // =========================
            // 날짜 필터
            // =========================
            if (dateFilter == "today")
            {
                query = query.Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow);
            }
            else
            {
                if (startDate.HasValue)
                {
                    var start = startDate.Value.Date;
                    query = query.Where(o => o.CreatedAt >= start);
                }

                if (endDate.HasValue)
                {
                    var end = endDate.Value.Date.AddDays(1);
                    query = query.Where(o => o.CreatedAt < end);
                }
            }

            // =========================
            // 특수 필터
            // =========================
            if (!string.IsNullOrWhiteSpace(specialFilter))
            {
                if (specialFilter == "issue")
                {
                    query = query.Where(o =>
                        o.OrderStatus == "CancelRequested"
                        || o.OrderStatus == "ReturnRequested"
                        || o.OrderStatus == "ExchangeRequested"
                        || (o.OrderStatus == "PendingPayment" && o.PaymentStatus == "Ready")
                        || o.OrderStatus == "Preparing");
                }
                else if (specialFilter == "done")
                {
                    query = query.Where(o =>
                        o.OrderStatus == "Completed"
                        || o.OrderStatus == "Cancelled"
                        || o.OrderStatus == "Returned"
                        || o.OrderStatus == "Exchanged"
                        || o.OrderStatus == "RequestRejected");
                }
                else if (specialFilter == "cancelRequested")
                {
                    query = query.Where(o => o.OrderStatus == "CancelRequested");
                }
                else if (specialFilter == "returnExchange")
                {
                    query = query.Where(o =>
                        o.OrderStatus == "ReturnRequested"
                        || o.OrderStatus == "ExchangeRequested");
                }
                else if (specialFilter == "memo")
                {
                    query = query.Where(o => o.AdminMemo != null && o.AdminMemo != "");
                }
                else if (specialFilter == "collect")
                {
                    query = query.Where(o => o.IsCollectShipping);
                }
            }

            // =========================
            // 주문상태 필터
            // =========================
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(o => o.OrderStatus == status);
            }

            // =========================
            // 결제수단 필터
            // =========================
            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                query = query.Where(o => o.PaymentMethod == paymentMethod);
            }

            // =========================
            // 회원 / 비회원 필터
            // =========================
            if (!string.IsNullOrWhiteSpace(orderType))
            {
                if (orderType == "member")
                {
                    query = query.Where(o => !o.IsGuestOrder);
                }
                else if (orderType == "guest")
                {
                    query = query.Where(o => o.IsGuestOrder);
                }
            }

            // =========================
            // 통합 검색
            // 주문번호 / 상품명 / 주문자 / 연락처 / 송장번호 / 택배사
            // =========================
            if (!string.IsNullOrWhiteSpace(keyword))
            {
                query = query.Where(o =>
                    o.OrderNo.Contains(keyword)
                    || o.BuyerName.Contains(keyword)
                    || o.BuyerPhone.Contains(keyword)
                    || o.ReceiverName.Contains(keyword)
                    || o.ReceiverPhone.Contains(keyword)
                    || (o.BuyerEmail != null && o.BuyerEmail.Contains(keyword))
                    || (o.TrackingNumber != null && o.TrackingNumber.Contains(keyword))
                    || (o.TrackingCompany != null && o.TrackingCompany.Contains(keyword))
                    || o.Items.Any(i => i.ProductName.Contains(keyword))
                );
            }

            var totalCount = query.Count();

            var orders = query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Status = status;
            ViewBag.Keyword = keyword;
            ViewBag.SpecialFilter = specialFilter;
            ViewBag.DateFilter = dateFilter;
            ViewBag.PaymentMethod = paymentMethod;
            ViewBag.OrderType = orderType;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            return View(orders);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmBankPayment(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var order = _context.Orders
                .FirstOrDefault(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            if (order.OrderStatus == "Cancelled")
            {
                TempData["OrderAdminMessage"] = "취소된 주문은 입금확인 처리할 수 없습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            if (order.PaymentStatus == "Paid")
            {
                TempData["OrderAdminMessage"] = "이미 결제완료 처리된 주문입니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            // 입금확인 처리 전에 재고 차감
            var stockResult = await _orderStockService.DeductStockAsync(order);

            if (!stockResult.Ok)
            {
                TempData["OrderAdminMessage"] = stockResult.Message;
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            order.PaymentStatus = "Paid";
            order.OrderStatus = "Preparing";

            if (order.PaidAt == null)
            {
                order.PaidAt = DateTime.Now;
            }

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                AdminMemo = "관리자 입금확인 처리",
                ChangedBy = User.Identity?.Name ?? "admin",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var emailFailed = false;

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = $"[모아클로버] 입금확인이 완료되었습니다. ({order.OrderNo})";
                    var body = BuildBankPaymentCompletedEmailBody(order);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    emailFailed = true;

                    AddEmailFailedHistory(order, "입금확인", ex);

                    await _context.SaveChangesAsync();
                }
            }

            TempData["OrderAdminMessage"] = emailFailed
                ? "입금확인은 처리되었지만, 이메일 발송에 실패했습니다. 처리이력에서 내용을 확인해주세요."
                : "입금확인 처리되었습니다. 주문상태가 배송준비중으로 변경되었습니다.";

            return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
        }

        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult AdminOrderDetail(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var order = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            var histories = _context.OrderStatusHistories
                .AsNoTracking()
                .Where(h => h.OrderId == order.OrderId)
                .OrderByDescending(h => h.CreatedAt)
                .ToList();

            ViewBag.OrderHistories = histories;

            var payment = _context.Payments
                .AsNoTracking()
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            ViewBag.Payment = payment;

            return View(order);
        }

        // 무통장 입금 환불
        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteManualRefund(string orderNo, string refundMemo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            refundMemo = refundMemo?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(refundMemo))
            {
                TempData["OrderAdminMessage"] = "수동 환불완료 처리 사유를 입력해주세요.";
                return RedirectToAction("AdminOrderDetail", new { orderNo });
            }

            var order = _context.Orders
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            if (order.PaymentMethod != "BankTransfer")
            {
                TempData["OrderAdminMessage"] = "무통장입금 주문만 수동 환불완료 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            if (order.PaymentStatus != "Paid")
            {
                TempData["OrderAdminMessage"] = "결제완료 상태의 무통장입금 주문만 환불완료 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            var canRefund =
                order.OrderStatus == "Paid"
                || order.OrderStatus == "Preparing"
                || order.OrderStatus == "CancelRequested"
                || order.OrderStatus == "ReturnRequested"
                || order.OrderStatus == "Returned";

            if (!canRefund)
            {
                TempData["OrderAdminMessage"] = "현재 주문상태에서는 환불완료 처리할 수 없습니다. 취소요청 또는 반품요청 상태에서 처리해주세요.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            var isReturnRefund =
                beforeOrderStatus == "ReturnRequested"
                || beforeOrderStatus == "Returned";

            order.PaymentStatus = "Refunded";

            // 배송 전 취소/환불이면 취소, 배송 후 반품 환불이면 반품완료 유지
            order.OrderStatus = isReturnRefund
                ? "Returned"
                : "Cancelled";

            order.CancelReason = string.IsNullOrWhiteSpace(order.CancelReason)
                ? refundMemo
                : order.CancelReason;

            order.AdminMemo = string.IsNullOrWhiteSpace(order.AdminMemo)
                ? $"수동 환불완료: {refundMemo}"
                : order.AdminMemo + Environment.NewLine + $"수동 환불완료: {refundMemo}";

            if (!isReturnRefund && order.CancelledAt == null)
                order.CancelledAt = DateTime.Now;

            // 환불 성공 시 재고 복구
            await _orderStockService.RestoreStockAsync(order);

            var payment = _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (payment != null)
            {
                payment.PaymentStatus = "Refunded";
                payment.FailureCode = null;
                payment.FailureMessage = null;
            }

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                CancelReason = order.CancelReason,
                AdminMemo = isReturnRefund
                    ? $"무통장입금 반품 환불완료 처리: {refundMemo}"
                    : $"무통장입금 수동 환불완료 처리: {refundMemo}",

                ChangedBy = User.Identity?.Name ?? "admin",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var refundEmailFailed = false;

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = isReturnRefund
                        ? $"[모아클로버] 반품 환불 처리가 완료되었습니다. ({order.OrderNo})"
                        : $"[모아클로버] 환불 처리가 완료되었습니다. ({order.OrderNo})";

                    var body = BuildRefundCompletedEmailBody(order);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    refundEmailFailed = true;

                    AddEmailFailedHistory(
                        order,
                        isReturnRefund ? "무통장 반품환불완료" : "무통장 환불완료",
                        ex
                    );

                    await _context.SaveChangesAsync();
                }
            }

            TempData["OrderAdminMessage"] = refundEmailFailed
                ? "환불완료 처리는 되었지만, 이메일 발송에 실패했습니다. 처리이력에서 확인해주세요."
                : isReturnRefund
                    ? "무통장입금 반품 환불완료 처리가 완료되었습니다."
                    : "무통장입금 수동 환불완료 처리가 완료되었습니다.";

            return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
        }

        // 환불, 취소 철회
        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> WithdrawCancelRequest(string orderNo, string? adminMemo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            if (order.OrderStatus != "CancelRequested")
            {
                TempData["OrderAdminMessage"] = "취소/환불 요청 상태의 주문만 철회 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            if (order.PaymentStatus != "Paid")
            {
                TempData["OrderAdminMessage"] = "결제완료 상태의 취소요청 주문만 철회 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            /*
                취소요청 전 상태를 정확히 저장해두는 컬럼이 아직 없기 때문에,
                현재 구조에서는 배송 전 결제완료 주문의 기본 진행 상태인 Preparing으로 되돌립니다.

                나중에 BeforeCancelRequestStatus 같은 컬럼을 만들면
                Paid / Preparing 중 원래 상태로 정확히 복구할 수 있습니다.
            */
            order.OrderStatus = "Preparing";
            order.PaymentStatus = "Paid";
            order.CancelReason = null;

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                CancelReason = null,
                AdminMemo = string.IsNullOrWhiteSpace(adminMemo)
                    ? "고객 요청으로 취소/환불 요청 철회 처리"
                    : $"고객 요청으로 취소/환불 요청 철회 처리: {adminMemo}",

                ChangedBy = User.Identity?.Name ?? "admin",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var withdrawEmailFailed = false;

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = $"[모아클로버] 취소/환불 요청이 철회되었습니다. ({order.OrderNo})";
                    var body = BuildCancelRequestWithdrawnEmailBody(order);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    withdrawEmailFailed = true;

                    AddEmailFailedHistory(order, "취소요청 철회", ex);

                    await _context.SaveChangesAsync();
                }
            }

            TempData["OrderAdminMessage"] = withdrawEmailFailed
                ? "취소/환불 요청은 철회 처리되었지만, 안내 이메일 발송에 실패했습니다. 처리이력에서 확인해주세요."
                : "취소/환불 요청이 철회 처리되었습니다. 주문은 배송준비중 상태로 돌아갔습니다.";

            return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CompleteReturnExchangeRequest(
            string orderNo,
            string actionType,
            string? adminMemo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            orderNo = orderNo.Trim();
            actionType = actionType?.Trim() ?? "";

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            if (order.OrderStatus != "ReturnRequested"
                && order.OrderStatus != "ExchangeRequested")
            {
                TempData["OrderAdminMessage"] = "반품/교환 요청 상태의 주문만 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            if (actionType != "complete" && actionType != "reject")
            {
                TempData["OrderAdminMessage"] = "처리 구분이 올바르지 않습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            if (actionType == "reject")
            {
                order.OrderStatus = "RequestRejected";
            }
            else
            {
                order.OrderStatus = beforeOrderStatus == "ReturnRequested"
                    ? "Returned"
                    : "Exchanged";
            }

            var processText = actionType == "reject"
                ? "요청거절"
                : beforeOrderStatus == "ReturnRequested"
                    ? "반품완료"
                    : "교환완료";

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                CancelReason = order.CancelReason,
                AdminMemo = string.IsNullOrWhiteSpace(adminMemo)
                    ? $"{processText} 처리"
                    : $"{processText} 처리: {adminMemo}",

                ChangedBy = User.Identity?.Name ?? "admin",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            var processEmailFailed = false;

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var processType = order.OrderStatus == "Returned"
                        ? "returned"
                        : order.OrderStatus == "Exchanged"
                            ? "exchanged"
                            : "rejected";

                    var subject = processType switch
                    {
                        "returned" => $"[모아클로버] 반품 처리가 완료되었습니다. ({order.OrderNo})",
                        "exchanged" => $"[모아클로버] 교환 처리가 완료되었습니다. ({order.OrderNo})",
                        "rejected" => $"[모아클로버] 반품/교환 요청 처리 결과 안내 ({order.OrderNo})",
                        _ => $"[모아클로버] 반품/교환 요청 처리 결과 안내 ({order.OrderNo})"
                    };

                    var body = BuildReturnExchangeProcessedEmailBody(order, processType, adminMemo);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    processEmailFailed = true;

                    AddEmailFailedHistory(order, $"{processText} 안내", ex);

                    await _context.SaveChangesAsync();
                }
            }

            TempData["OrderAdminMessage"] = processEmailFailed
                ? $"{processText} 처리는 완료되었지만, 안내 이메일 발송에 실패했습니다. 처리이력에서 확인해주세요."
                : $"{processText} 처리되었습니다.";

            return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateOrderStatus(
            string orderNo,
            string orderStatus,
            string paymentStatus,
            string? trackingCompany,
            string? trackingNumber,
            string? adminMemo,
            string? customerMemo,
            string? cancelReason)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var order = _context.Orders
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            // 변경 전 상태 기록용
            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;
            var beforeTrackingCompany = order.TrackingCompany;
            var beforeTrackingNumber = order.TrackingNumber;
            var beforeAdminMemo = order.AdminMemo;
            var beforeCustomerMemo = order.CustomerMemo;
            var beforeCancelReason = order.CancelReason;

            // 값 trim
            orderStatus = orderStatus?.Trim() ?? "";
            paymentStatus = paymentStatus?.Trim() ?? "";
            trackingCompany = trackingCompany?.Trim();
            trackingNumber = trackingNumber?.Trim();
            adminMemo = adminMemo?.Trim();
            customerMemo = customerMemo?.Trim();
            cancelReason = cancelReason?.Trim();

            /*
                관리자 수동 저장에서 허용할 주문상태

                주의:
                - Cancelled는 환불/취소 버튼으로 처리
                - Returned는 반품완료 처리 버튼 또는 환불 처리 흐름으로 처리
                - Exchanged는 교환완료 처리 버튼으로 처리
            */
            var allowedOrderStatuses = new[]
            {
                "PendingPayment",
                "Paid",
                "Preparing",
                "Shipped",
                "Completed",
                "CancelRequested",
                "ReturnRequested",
                "ExchangeRequested",
                "RequestRejected"
            };

            /*
                관리자 수동 저장에서 허용할 결제상태

                주의:
                - Refunded는 결제취소/환불 버튼 또는 수동 환불완료 버튼으로만 처리
                - Cancelled도 실제 환불/취소 흐름 없이 직접 저장하지 않도록 막음
            */
            var allowedPaymentStatuses = new[]
            {
                "Ready",
                "Paid",
                "Failed"
            };

            if (!allowedOrderStatuses.Contains(orderStatus))
            {
                TempData["OrderAdminMessage"] =
                    "이 주문상태는 직접 저장할 수 없습니다. 취소/환불, 반품완료, 교환완료는 전용 처리 버튼을 사용해주세요.";

                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            if (!allowedPaymentStatuses.Contains(paymentStatus))
            {
                TempData["OrderAdminMessage"] =
                    "이 결제상태는 직접 저장할 수 없습니다. 환불완료는 결제취소/환불 또는 수동 환불완료 버튼을 사용해주세요.";

                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 배송중/배송완료 송장번호 검사
            if ((orderStatus == "Shipped" || orderStatus == "Completed")
                && (string.IsNullOrWhiteSpace(trackingCompany)
                    || string.IsNullOrWhiteSpace(trackingNumber)))
            {
                TempData["OrderAdminMessage"] = "배송중 또는 배송완료 상태로 변경하려면 택배사와 송장번호를 입력해주세요.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 결제 전 주문을 배송 단계로 넘기지 않기
            if ((orderStatus == "Preparing" || orderStatus == "Shipped" || orderStatus == "Completed")
                && paymentStatus != "Paid")
            {
                TempData["OrderAdminMessage"] = "배송준비중/배송중/배송완료 상태는 결제상태가 결제완료일 때만 저장할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 반품/교환 요청은 배송중/배송완료 이후 주문에서만 권장
            if ((orderStatus == "ReturnRequested" || orderStatus == "ExchangeRequested")
                && beforeOrderStatus != "Shipped"
                && beforeOrderStatus != "Completed"
                && beforeOrderStatus != "ReturnRequested"
                && beforeOrderStatus != "ExchangeRequested")
            {
                TempData["OrderAdminMessage"] = "반품/교환 요청 상태는 배송중 또는 배송완료 주문에서만 변경하는 것이 안전합니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 반품/교환/요청거절은 결제완료 상태 유지
            if ((orderStatus == "ReturnRequested"
                 || orderStatus == "ExchangeRequested"
                 || orderStatus == "RequestRejected")
                && paymentStatus != "Paid")
            {
                TempData["OrderAdminMessage"] = "반품/교환 요청 또는 요청거절 상태는 결제상태를 결제완료로 유지해주세요.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 요청거절은 반품/교환 요청 상태에서만 처리
            if (orderStatus == "RequestRejected"
                && beforeOrderStatus != "ReturnRequested"
                && beforeOrderStatus != "ExchangeRequested"
                && beforeOrderStatus != "RequestRejected")
            {
                TempData["OrderAdminMessage"] = "요청거절은 반품요청 또는 교환요청 상태의 주문에서만 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 취소/환불/반품완료/교환완료는 전용 버튼 사용 안내
            if (orderStatus == "Cancelled"
                || orderStatus == "Returned"
                || orderStatus == "Exchanged"
                || paymentStatus == "Refunded"
                || paymentStatus == "Cancelled")
            {
                TempData["OrderAdminMessage"] = "취소/환불/반품완료/교환완료는 직접 저장하지 말고 전용 처리 버튼을 사용해주세요.";
                return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
            }

            // 실제 주문 값 변경
            order.OrderStatus = orderStatus;
            order.PaymentStatus = paymentStatus;
            order.TrackingCompany = trackingCompany;
            order.TrackingNumber = trackingNumber;
            order.AdminMemo = adminMemo;
            order.CustomerMemo = customerMemo ?? "";

            /*
                CancelReason은 취소/환불/반품/교환 요청 사유로 같이 사용 중입니다.
                빈 값으로 저장해서 기존 사유가 지워지는 것을 막기 위해,
                입력값이 있을 때만 갱신합니다.
            */
            if (!string.IsNullOrWhiteSpace(cancelReason))
            {
                order.CancelReason = cancelReason;
            }

            // 결제완료 처리 시 결제일 자동 입력
            if (paymentStatus == "Paid" && order.PaidAt == null)
            {
                order.PaidAt = DateTime.Now;
            }

            // 배송중/배송완료 처리 시 발송일 자동 입력
            if ((orderStatus == "Shipped" || orderStatus == "Completed") && order.ShippedAt == null)
            {
                order.ShippedAt = DateTime.Now;
            }

            // 송장번호가 없고 배송중/배송완료가 아니면 발송일 제거
            if (orderStatus != "Shipped"
                && orderStatus != "Completed"
                && string.IsNullOrWhiteSpace(trackingNumber))
            {
                order.ShippedAt = null;
            }

            // 변경 이력
            var changedBy = User.Identity?.Name ?? "admin";

            var memoChanged =
                beforeAdminMemo != order.AdminMemo ||
                beforeCustomerMemo != order.CustomerMemo;

            if (memoChanged)
            {
                order.AdminMemoUpdatedAt = DateTime.Now;
                order.AdminMemoUpdatedBy = changedBy;
            }

            var hasChanged =
                beforeOrderStatus != order.OrderStatus ||
                beforePaymentStatus != order.PaymentStatus ||
                beforeTrackingCompany != order.TrackingCompany ||
                beforeTrackingNumber != order.TrackingNumber ||
                beforeAdminMemo != order.AdminMemo ||
                beforeCustomerMemo != order.CustomerMemo ||
                beforeCancelReason != order.CancelReason;

            if (hasChanged)
            {
                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,

                    BeforeOrderStatus = beforeOrderStatus,
                    AfterOrderStatus = order.OrderStatus,

                    BeforePaymentStatus = beforePaymentStatus,
                    AfterPaymentStatus = order.PaymentStatus,

                    TrackingCompany = order.TrackingCompany,
                    TrackingNumber = order.TrackingNumber,

                    AdminMemo = order.AdminMemo,
                    CancelReason = order.CancelReason,

                    ChangedBy = changedBy,
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();

            var shouldSendShippingEmail =
                beforeOrderStatus != "Shipped"
                && order.OrderStatus == "Shipped"
                && !string.IsNullOrWhiteSpace(order.TrackingCompany)
                && !string.IsNullOrWhiteSpace(order.TrackingNumber)
                && !string.IsNullOrWhiteSpace(order.BuyerEmail);

            var shippingEmailFailed = false;

            if (shouldSendShippingEmail)
            {
                try
                {
                    var subject = $"[모아클로버] 상품이 발송되었습니다. ({order.OrderNo})";
                    var body = BuildShippingStartedEmailBody(order);

                    await _emailService.SendEmailAsync(order.BuyerEmail!, subject, body);
                }
                catch (Exception ex)
                {
                    shippingEmailFailed = true;

                    AddEmailFailedHistory(order, "배송안내", ex);

                    await _context.SaveChangesAsync();
                }
            }

            TempData["OrderAdminMessage"] = shippingEmailFailed
                ? "주문 정보는 저장되었지만, 배송안내 이메일 발송에 실패했습니다. 처리이력에서 내용을 확인해주세요."
                : "주문 정보가 저장되었습니다.";

            return RedirectToAction("AdminOrderDetail", new { orderNo = order.OrderNo });
        }

    }
}