using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models.ViewModels;

namespace MOAClover.ViewComponents
{
    public class AdminOrderAlertViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AdminOrderAlertViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (!(User?.Identity?.IsAuthenticated ?? false) || !User.IsInRole("admin"))
            {
                return View(new AdminOrderAlertViewModel());
            }

            var cancelRequestedCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "CancelRequested");

            var returnRequestedCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "ReturnRequested");

            var exchangeRequestedCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "ExchangeRequested");

            var pendingPaymentCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "PendingPayment"
                                 && o.PaymentStatus == "Ready");

            var preparingCount = await _context.Orders
                .AsNoTracking()
                .CountAsync(o => o.OrderStatus == "Preparing");

            var issueCount =
                cancelRequestedCount
                + returnRequestedCount
                + exchangeRequestedCount
                + pendingPaymentCount
                + preparingCount;

            var model = new AdminOrderAlertViewModel
            {
                IssueCount = issueCount,
                CancelRequestedCount = cancelRequestedCount,
                ReturnRequestedCount = returnRequestedCount,
                ExchangeRequestedCount = exchangeRequestedCount,
                PendingPaymentCount = pendingPaymentCount,
                PreparingCount = preparingCount
            };

            return View(model);
        }
    }
}