using Microsoft.AspNetCore.Mvc;
using MOAClover.Data;
using Microsoft.EntityFrameworkCore;

namespace MOAClover.ViewComponents
{
    public class AdminQnaAlertViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AdminQnaAlertViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            // 미답변: Answer가 null/빈문자 인 것
            int count = await _context.ProductQnA
                .AsNoTracking()
                .CountAsync(q => !q.IsDeleted && (q.Answer == null || q.Answer == ""));

            return View(count);
        }
    }
}
