using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Models.ViewModels;
using System.Diagnostics;

namespace MOAClover.Controllers
{
    public class HomeController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        // 로그기록 (실행 중 발생하는 메시지를 콘솔이나 파일로 남겨줌)
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IWebHostEnvironment env)
        {
            _logger = logger;
            _context = context;
            _env = env;
        }

        // Index화면 보여줌
        public IActionResult Index(int page = 1)
        {
            const int pageSize = 20;

            var query = _context.Products
                .Where(p => p.IsVisible && p.DeletedAt == null);

            int totalCount = query.Count();
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            var products = query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListItemViewModel
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    Price = p.Price,
                    DiscountRate = p.DiscountRate,

                    ImageUrls = _context.Media
                        .Where(m => m.ProductId == p.ProductId && m.MediaType == "image")
                        .OrderBy(m => m.SortOrder)
                        .Select(m => m.FileUrl)
                        .ToList()
                })
                .ToList();

            var model = new ProductListViewModel
            {
                Products = products,
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(model);
        }

        // 상품등록 창 이동
        // 관리자만 접근가능
        [Authorize(Roles = "admin")]
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Categories = _context.Categories
                .Where(c => c.IsActive)
                .ToList();

            return View();
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        public async Task<IActionResult> Create(Product product, List<IFormFile> Images, IFormFile? Video)
        {
            if (product.CategoryId == 0)
            {
                ModelState.AddModelError("CategoryId", "카테고리를 선택해주세요.");

                ViewBag.Categories = _context.Categories
                   .Where(c => c.IsActive)
                   .ToList();

                return View(product);
            }

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            string uploadPath = Path.Combine(_env.WebRootPath, "uploads");
            if (!Directory.Exists(uploadPath))
                Directory.CreateDirectory(uploadPath);

            int order = 0;

            foreach (var file in Images)
            {
                string fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
                string filePath = Path.Combine(uploadPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await file.CopyToAsync(stream);

                _context.Media.Add(new Media
                {
                    ProductId = product.ProductId,
                    MediaType = "image",
                    FileUrl = "/uploads/" + fileName,
                    SortOrder = order++
                });
            }

            if (Video != null)
            {
                string fileName = Guid.NewGuid() + Path.GetExtension(Video.FileName);
                string filePath = Path.Combine(uploadPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await Video.CopyToAsync(stream);

                _context.Media.Add(new Media
                {
                    ProductId = product.ProductId,
                    MediaType = "video",
                    FileUrl = "/uploads/" + fileName
                });
            }

            await _context.SaveChangesAsync();

            // ✅ 등록 완료 메시지
            TempData["SuccessMessage"] = "상품이 성공적으로 등록되었습니다.";

            return RedirectToAction("Detail", new { id = product.ProductId });
        }

        // 상품상세페이지 이동
        [HttpGet]
        public IActionResult Detail(int id)
        {
            var product = _context.Products
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            var medias = _context.Media
                .Where(m => m.ProductId == id && m.IsActive)
                .OrderBy(m => m.SortOrder)
                .ToList();

            // 카테고리 경로 생성
            string categoryPath = "";
            var current = _context.Categories.FirstOrDefault(c => c.CategoryId == product.CategoryId);

            while (current != null)
            {
                categoryPath = current.Name + (categoryPath == "" ? "" : " > " + categoryPath);
                current = current.ParentCategoryId == null
                    ? null
                    : _context.Categories.FirstOrDefault(c => c.CategoryId == current.ParentCategoryId);
            }

            var model = new ProductDetailViewModel
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Price = product.Price,
                DiscountRate = product.DiscountRate,
                Description = product.Description,
                CategoryPath = categoryPath,
                ImageUrls = medias
                    .Where(m => m.MediaType == "image")
                    .Select(m => m.FileUrl)
                    .ToList(),
                VideoUrls = medias
                    .Where(m => m.MediaType == "video")
                    .Select(m => m.FileUrl)
                    .ToList()
            };

            return View(model);
        }

        // 수정창 이동
        public IActionResult Edit()
        {
            return View();
        }

        // 삭제 창 이동
        public IActionResult Delete()
        {
            return View();
        }

      


        //  에러 발생 시 보여주는 기본 오류 페이지 (Error.cshtml 에 RequestId 를 보내서 화면에서 에러 추적 가능)
        // Duration = 0 → 캐시저장 안함.  NoStore = true → 브라우저 캐시에 저장 X
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
