using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MOAClover.Services;

namespace MOAClover.Controllers
{
    public class PaymentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly OrderStockService _orderStockService;
        private readonly IEmailService _emailService;

        public PaymentController(
            ApplicationDbContext context,
            IConfiguration configuration,
            IHttpClientFactory httpClientFactory,
            OrderStockService orderStockService,
            IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _httpClientFactory = httpClientFactory;
            _orderStockService = orderStockService;
            _emailService = emailService;
        }

        [HttpGet]
        public IActionResult Pay(string orderNo)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            var order = _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefault(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            if (order.OrderStatus == "Cancelled")
            {
                TempData["PaymentMessage"] = "취소된 주문은 결제할 수 없습니다.";
                return RedirectToAction("Complete", "Order", new { orderNo = order.OrderNo });
            }

            if (order.PaymentStatus == "Paid")
            {
                TempData["PaymentMessage"] = "이미 결제완료된 주문입니다.";
                return RedirectToAction("Complete", "Order", new { orderNo = order.OrderNo });
            }

            var clientKey = _configuration["TossPayments:ClientKey"];

            if (string.IsNullOrWhiteSpace(clientKey))
            {
                TempData["PaymentMessage"] = "토스페이먼츠 ClientKey가 설정되지 않았습니다.";
                return RedirectToAction("Complete", "Order", new { orderNo = order.OrderNo });
            }

            ViewBag.TossClientKey = clientKey;

            return View(order);
        }

        [HttpGet]
        public async Task<IActionResult> Success(string paymentKey, string orderId, int amount)
        {
            if (string.IsNullOrWhiteSpace(paymentKey)
                || string.IsNullOrWhiteSpace(orderId)
                || amount <= 0)
            {
                return RedirectToAction("Fail", new
                {
                    code = "INVALID_PAYMENT_RESULT",
                    message = "결제 승인 정보가 올바르지 않습니다.",
                    orderId
                });
            }

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderNo == orderId);

            if (order == null)
                return NotFound();

            if (order.PaymentStatus == "Paid")
            {
                return RedirectToAction("Complete", "Order", new { orderNo = order.OrderNo });
            }

            if (order.OrderStatus == "Cancelled")
            {
                return RedirectToAction("Fail", new
                {
                    code = "ORDER_CANCELLED",
                    message = "취소된 주문은 결제 승인할 수 없습니다.",
                    orderId = order.OrderNo
                });
            }

            if (order.TotalPayAmount != amount)
            {
                return RedirectToAction("Fail", new
                {
                    code = "AMOUNT_MISMATCH",
                    message = "주문 금액과 결제 금액이 일치하지 않습니다.",
                    orderId = order.OrderNo
                });
            }

            var secretKey = _configuration["TossPayments:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                return RedirectToAction("Fail", new
                {
                    code = "MISSING_SECRET_KEY",
                    message = "토스페이먼츠 SecretKey가 설정되지 않았습니다.",
                    orderId = order.OrderNo
                });
            }

            var beforeOrderStatus = order.OrderStatus;
            var beforePaymentStatus = order.PaymentStatus;

            // 결제 승인 전에 재고 먼저 확인/차감
            var stockResult = await _orderStockService.DeductStockAsync(order);

            if (!stockResult.Ok)
            {
                return RedirectToAction("Fail", new
                {
                    code = "OUT_OF_STOCK",
                    message = stockResult.Message,
                    orderId = order.OrderNo
                });
            }

            var confirmResult = await ConfirmTossPaymentAsync(
                secretKey,
                paymentKey,
                orderId,
                amount
            );

            if (!confirmResult.Ok)
            {
                await _orderStockService.RestoreStockAsync(order);

                SaveFailedPayment(order, confirmResult.Code, confirmResult.Message);

                return RedirectToAction("Fail", new
                {
                    code = confirmResult.Code,
                    message = confirmResult.Message,
                    orderId = order.OrderNo
                });
            }

            order.PaymentStatus = "Paid";
            order.OrderStatus = "Preparing";
            order.PaymentMethod = "TossPayments";

            if (order.PaidAt == null)
            {
                order.PaidAt = DateTime.Now;
            }

            var payment = _context.Payments
                .Where(p => p.OrderId == order.OrderId)
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefault();

            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,
                    Provider = "TossPayments",
                    PaymentStatus = "Paid",
                    Amount = order.TotalPayAmount,
                    PaymentKey = paymentKey,

                    // 토스 승인 응답 결제 상세
                    PayMethod = confirmResult.PayMethod,
                    CardCompany = confirmResult.CardCompany,
                    CardNumber = confirmResult.CardNumber,
                    ReceiptUrl = confirmResult.ReceiptUrl,

                    FailureCode = null,
                    FailureMessage = null,
                    ApprovedAt = DateTime.Now,
                    CreatedAt = DateTime.Now
                };

                _context.Payments.Add(payment);
            }
            else
            {
                payment.Provider = "TossPayments";
                payment.PaymentStatus = "Paid";
                payment.Amount = order.TotalPayAmount;
                payment.PaymentKey = paymentKey;

                // 토스 승인 응답 결제 상세
                payment.PayMethod = confirmResult.PayMethod;
                payment.CardCompany = confirmResult.CardCompany;
                payment.CardNumber = confirmResult.CardNumber;
                payment.ReceiptUrl = confirmResult.ReceiptUrl;

                payment.FailureCode = null;
                payment.FailureMessage = null;
                payment.ApprovedAt = DateTime.Now;
            }

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                AdminMemo = "토스페이먼츠 카드결제 승인 완료",
                ChangedBy = "TossPayments",
                CreatedAt = DateTime.Now
            });

            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(order.BuyerEmail))
            {
                try
                {
                    var subject = $"[모아클로버] 결제가 완료되었습니다. ({order.OrderNo})";
                    var body = BuildPaymentCompletedEmailBody(order, payment);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    _context.OrderStatusHistories.Add(new OrderStatusHistory
                    {
                        OrderId = order.OrderId,
                        OrderNo = order.OrderNo,

                        BeforeOrderStatus = order.OrderStatus,
                        AfterOrderStatus = order.OrderStatus,

                        BeforePaymentStatus = order.PaymentStatus,
                        AfterPaymentStatus = order.PaymentStatus,

                        AdminMemo = $"카드결제 완료 이메일 발송 실패: {ex.Message}",
                        ChangedBy = "EmailService",
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Complete", "Order", new { orderNo = order.OrderNo });
        }

        [HttpGet]
        public IActionResult Fail(string? code, string? message, string? orderId)
        {
            ViewBag.Code = code;
            ViewBag.Message = message;
            ViewBag.OrderNo = orderId;

            return View();
        }

        // 환불
        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelTossPayment(string orderNo, string cancelReason)
        {
            if (string.IsNullOrWhiteSpace(orderNo))
                return NotFound();

            cancelReason = cancelReason?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(cancelReason))
            {
                TempData["OrderAdminMessage"] = "결제취소/환불 사유를 입력해주세요.";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo });
            }

            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);

            if (order == null)
                return NotFound();

            if (order.PaymentMethod != "TossPayments")
            {
                TempData["OrderAdminMessage"] = "카드결제 주문만 결제취소/환불 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
            }

            if (order.PaymentStatus != "Paid")
            {
                TempData["OrderAdminMessage"] = "결제완료 상태의 주문만 결제취소/환불 처리할 수 있습니다.";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
            }

            var canRefund =
                order.OrderStatus == "Paid"
                || order.OrderStatus == "Preparing"
                || order.OrderStatus == "CancelRequested"
                || order.OrderStatus == "ReturnRequested"
                || order.OrderStatus == "Returned";

            if (!canRefund)
            {
                TempData["OrderAdminMessage"] = "현재 주문상태에서는 결제취소/환불 처리할 수 없습니다. 취소요청 또는 반품요청 상태에서 처리해주세요.";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
            }

            var payment = await _context.Payments
                .Where(p => p.OrderId == order.OrderId && p.Provider == "TossPayments")
                .OrderByDescending(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (payment == null || string.IsNullOrWhiteSpace(payment.PaymentKey))
            {
                TempData["OrderAdminMessage"] = "토스 결제키가 없어 결제취소를 진행할 수 없습니다.";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
            }

            var secretKey = _configuration["TossPayments:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                TempData["OrderAdminMessage"] = "토스페이먼츠 SecretKey가 설정되지 않았습니다.";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
            }

            var cancelResult = await CancelTossPaymentAsync(
                secretKey,
                payment.PaymentKey,
                cancelReason
            );

            if (!cancelResult.Ok)
            {
                payment.FailureCode = cancelResult.Code;
                payment.FailureMessage = cancelResult.Message;

                _context.OrderStatusHistories.Add(new OrderStatusHistory
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,

                    BeforeOrderStatus = order.OrderStatus,
                    AfterOrderStatus = order.OrderStatus,

                    BeforePaymentStatus = order.PaymentStatus,
                    AfterPaymentStatus = order.PaymentStatus,

                    CancelReason = cancelReason,
                    AdminMemo = $"토스페이먼츠 결제취소 실패: {cancelResult.Code} / {cancelResult.Message}",
                    ChangedBy = User.Identity?.Name ?? "admin",
                    CreatedAt = DateTime.Now
                });

                await _context.SaveChangesAsync();

                TempData["OrderAdminMessage"] = $"결제취소에 실패했습니다. {cancelResult.Message}";
                return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
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

            order.CancelReason = cancelReason;

            if (!isReturnRefund && order.CancelledAt == null)
                order.CancelledAt = DateTime.Now;

            // 환불 성공 시 재고 복구
            await _orderStockService.RestoreStockAsync(order);

            payment.PaymentStatus = "Refunded";
            payment.FailureCode = null;
            payment.FailureMessage = null;

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,

                BeforeOrderStatus = beforeOrderStatus,
                AfterOrderStatus = order.OrderStatus,

                BeforePaymentStatus = beforePaymentStatus,
                AfterPaymentStatus = order.PaymentStatus,

                CancelReason = cancelReason,
                AdminMemo = isReturnRefund
                    ? "토스페이먼츠 반품 환불 완료"
                    : "토스페이먼츠 결제취소/환불 완료",

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
                        : $"[모아클로버] 결제취소/환불이 완료되었습니다. ({order.OrderNo})";

                    var body = BuildRefundCompletedEmailBody(order);

                    await _emailService.SendEmailAsync(order.BuyerEmail, subject, body);
                }
                catch (Exception ex)
                {
                    refundEmailFailed = true;

                    _context.OrderStatusHistories.Add(new OrderStatusHistory
                    {
                        OrderId = order.OrderId,
                        OrderNo = order.OrderNo,

                        BeforeOrderStatus = order.OrderStatus,
                        AfterOrderStatus = order.OrderStatus,

                        BeforePaymentStatus = order.PaymentStatus,
                        AfterPaymentStatus = order.PaymentStatus,

                        CancelReason = cancelReason,
                        AdminMemo = isReturnRefund
                            ? $"카드 반품환불 완료 이메일 발송 실패: {ex.Message}"
                            : $"카드환불 완료 이메일 발송 실패: {ex.Message}",

                        ChangedBy = "EmailService",
                        CreatedAt = DateTime.Now
                    });

                    await _context.SaveChangesAsync();
                }
            }

            TempData["OrderAdminMessage"] = refundEmailFailed
                ? "결제취소/환불 처리는 완료되었지만, 이메일 발송에 실패했습니다. 처리이력에서 확인해주세요."
                : isReturnRefund
                    ? "카드결제 반품 환불 처리가 완료되었습니다."
                    : "결제취소/환불 처리가 완료되었습니다.";

            return RedirectToAction("AdminOrderDetail", "Order", new { orderNo = order.OrderNo });
        }

        private string BuildRefundCompletedEmailBody(Order order)
        {
            return $@"
                <div style='font-family:Arial, sans-serif;max-width:680px;margin:0 auto;color:#111827;line-height:1.6;'>
                    <h2 style='margin:0 0 10px;'>[모아클로버] 결제취소/환불이 완료되었습니다.</h2>

                    <p style='margin:0 0 20px;color:#6b7280;'>
                        고객님의 카드결제 취소/환불 처리가 완료되었습니다.
                    </p>

                    <div style='padding:16px;background:#f9fafb;border-radius:12px;margin-bottom:18px;'>
                        <div><strong>주문번호:</strong> {order.OrderNo}</div>
                        <div><strong>주문자:</strong> {order.BuyerName}</div>
                        <div><strong>환불금액:</strong> {order.TotalPayAmount:N0}원</div>
                        <div><strong>처리상태:</strong> 결제취소/환불 완료</div>
                        <div><strong>처리일시:</strong> {DateTime.Now:yyyy-MM-dd HH:mm}</div>
                    </div>

                    <p style='margin-top:24px;color:#6b7280;font-size:13px;line-height:1.6;'>
                        카드사 사정에 따라 실제 승인취소 반영까지 시간이 걸릴 수 있습니다.<br/>
                        자세한 내역은 카드사 또는 결제내역에서 확인해주세요.
                    </p>
                </div>
            ";
        }

        private string BuildPaymentCompletedEmailBody(Order order, Payment? payment)
        {
            var itemsHtml = string.Join("", order.Items.Select(item => $@"
                <tr>
                    <td style='padding:10px;border-bottom:1px solid #eee;'>{item.ProductName}</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:center;'>{item.Quantity}개</td>
                    <td style='padding:10px;border-bottom:1px solid #eee;text-align:right;'>{item.LineTotal:N0}원</td>
                </tr>
            "));

            var cardInfo = "";

            if (payment != null)
            {
                if (!string.IsNullOrWhiteSpace(payment.CardCompany))
                {
                    cardInfo += $"<div><strong>카드사:</strong> {payment.CardCompany}</div>";
                }

                if (!string.IsNullOrWhiteSpace(payment.CardNumber))
                {
                    cardInfo += $"<div><strong>카드번호:</strong> {payment.CardNumber}</div>";
                }

                if (!string.IsNullOrWhiteSpace(payment.ReceiptUrl))
                {
                    cardInfo += $@"
                        <div style='margin-top:12px;'>
                            <a href='{payment.ReceiptUrl}'
                               style='display:inline-block;padding:10px 14px;background:#111827;color:#fff;text-decoration:none;border-radius:8px;font-weight:bold;'>
                                카드 영수증 보기
                            </a>
                        </div>
                    ";
                }
            }

            return $@"
                <div style='font-family:Arial, sans-serif;max-width:680px;margin:0 auto;color:#111827;'>
                    <h2 style='margin:0 0 10px;'>[모아클로버] 결제가 완료되었습니다.</h2>
                    <p style='margin:0 0 20px;color:#6b7280;'>상품 준비가 시작됩니다.</p>

                    <div style='padding:16px;background:#f9fafb;border-radius:12px;margin-bottom:18px;'>
                        <div><strong>주문번호:</strong> {order.OrderNo}</div>
                        <div><strong>결제일시:</strong> {(order.PaidAt.HasValue ? order.PaidAt.Value.ToString("yyyy-MM-dd HH:mm") : DateTime.Now.ToString("yyyy-MM-dd HH:mm"))}</div>
                        <div><strong>주문자:</strong> {order.BuyerName}</div>
                        <div><strong>결제금액:</strong> {order.TotalPayAmount:N0}원</div>
                        {cardInfo}
                    </div>

                    <h3 style='margin:20px 0 10px;'>결제 상품</h3>
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

        private async Task<TossConfirmResult> ConfirmTossPaymentAsync(
            string secretKey,
            string paymentKey,
            string orderId,
            int amount)
        {
            var client = _httpClientFactory.CreateClient();

            var encodedSecretKey = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(secretKey + ":")
            );

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encodedSecretKey);

            var payload = new
            {
                paymentKey,
                orderId,
                amount
            };

            var json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync(
                "https://api.tosspayments.com/v1/payments/confirm",
                content
            );

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = new TossConfirmResult
                {
                    Ok = true,
                    RawResponse = responseText
                };

                try
                {
                    using var doc = JsonDocument.Parse(responseText);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("method", out var methodEl))
                        result.PayMethod = methodEl.GetString();

                    if (root.TryGetProperty("card", out var cardEl))
                    {
                        if (cardEl.TryGetProperty("company", out var companyEl))
                            result.CardCompany = companyEl.GetString();

                        if (cardEl.TryGetProperty("number", out var numberEl))
                            result.CardNumber = numberEl.GetString();
                    }

                    if (root.TryGetProperty("receipt", out var receiptEl))
                    {
                        if (receiptEl.TryGetProperty("url", out var urlEl))
                            result.ReceiptUrl = urlEl.GetString();
                    }
                }
                catch
                {
                    // 카드/영수증 정보 파싱 실패 시 결제 자체는 성공으로 처리
                }

                return result;
            }

            string code = "TOSS_CONFIRM_FAILED";
            string message = "결제 승인에 실패했습니다.";

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (root.TryGetProperty("code", out var codeEl))
                    code = codeEl.GetString() ?? code;

                if (root.TryGetProperty("message", out var messageEl))
                    message = messageEl.GetString() ?? message;
            }
            catch
            {
                // 응답 파싱 실패 시 기본 메시지 사용
            }

            return new TossConfirmResult
            {
                Ok = false,
                Code = code,
                Message = message,
                RawResponse = responseText
            };
        }

        // 취소 API호출
        private async Task<TossConfirmResult> CancelTossPaymentAsync(
            string secretKey,
            string paymentKey,
            string cancelReason)
        {
            var client = _httpClientFactory.CreateClient();

            var encodedSecretKey = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(secretKey + ":")
            );

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", encodedSecretKey);

            var payload = new
            {
                cancelReason
            };

            var json = JsonSerializer.Serialize(payload);

            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync(
                $"https://api.tosspayments.com/v1/payments/{paymentKey}/cancel",
                content
            );

            var responseText = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return new TossConfirmResult
                {
                    Ok = true,
                    RawResponse = responseText
                };
            }

            string code = "TOSS_CANCEL_FAILED";
            string message = "결제취소에 실패했습니다.";

            try
            {
                using var doc = JsonDocument.Parse(responseText);
                var root = doc.RootElement;

                if (root.TryGetProperty("code", out var codeEl))
                    code = codeEl.GetString() ?? code;

                if (root.TryGetProperty("message", out var messageEl))
                    message = messageEl.GetString() ?? message;
            }
            catch
            {
                // 응답 파싱 실패 시 기본 메시지 사용
            }

            return new TossConfirmResult
            {
                Ok = false,
                Code = code,
                Message = message,
                RawResponse = responseText
            };
        }

        private void SaveFailedPayment(Order order, string? code, string? message)
        {
            var payment = _context.Payments
                .FirstOrDefault(p => p.OrderId == order.OrderId);

            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId = order.OrderId,
                    OrderNo = order.OrderNo,
                    Provider = "TossPayments",
                    PaymentStatus = "Failed",
                    Amount = order.TotalPayAmount,

                    PayMethod = null,
                    CardCompany = null,
                    CardNumber = null,
                    ReceiptUrl = null,

                    FailureCode = code,
                    FailureMessage = message,
                    CreatedAt = DateTime.Now
                };

                _context.Payments.Add(payment);
            }
            else
            {
                payment.Provider = "TossPayments";
                payment.PaymentStatus = "Failed";
                payment.Amount = order.TotalPayAmount;

                payment.PayMethod = null;
                payment.CardCompany = null;
                payment.CardNumber = null;
                payment.ReceiptUrl = null;

                payment.FailureCode = code;
                payment.FailureMessage = message;
                payment.ApprovedAt = null;
            }

            _context.OrderStatusHistories.Add(new OrderStatusHistory
            {
                OrderId = order.OrderId,
                OrderNo = order.OrderNo,
                BeforeOrderStatus = order.OrderStatus,
                AfterOrderStatus = order.OrderStatus,
                BeforePaymentStatus = order.PaymentStatus,
                AfterPaymentStatus = "Failed",
                AdminMemo = $"토스페이먼츠 결제 승인 실패: {code} / {message}",
                ChangedBy = "TossPayments",
                CreatedAt = DateTime.Now
            });

            _context.SaveChanges();
        }

        private class TossConfirmResult
        {
            public bool Ok { get; set; }

            public string? Code { get; set; }

            public string? Message { get; set; }

            public string? RawResponse { get; set; }

            public string? PayMethod { get; set; }

            public string? CardCompany { get; set; }

            public string? CardNumber { get; set; }

            public string? ReceiptUrl { get; set; }
        }
    }
}