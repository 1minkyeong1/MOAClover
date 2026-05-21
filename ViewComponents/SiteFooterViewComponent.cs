using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;

namespace MOAClover.ViewComponents
{
    public class SiteFooterViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public SiteFooterViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var setting = await _context.SiteSettings
                .AsNoTracking()
                .OrderBy(x => x.SiteSettingId)
                .FirstOrDefaultAsync();

            if (setting == null)
            {
                setting = new SiteSetting
                {
                    ShopName = "모아클로버",
                    OwnerName = "홍길동",
                    BusinessNumber = "000-00-00000",
                    CustomerEmail = "contact@myshop.com",
                    CustomerPhone = "010-0000-0000",
                    Address = "서울특별시 어디구 어디동 123-45"
                };
            }

            return View(setting);
        }
    }
}