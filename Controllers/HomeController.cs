using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Models.ViewModels;
using MOAClover.ViewComponents;
using System.Diagnostics;
using System.Linq.Expressions;

namespace MOAClover.Controllers
{
    public class HomeController : Controller
    {

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;

        // 로그기록 (실행 중 발생하는 메시지를 콘솔이나 파일로 남겨줌)
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, IWebHostEnvironment env, IMemoryCache cache)
        {
            _logger = logger;
            _context = context;
            _env = env;
            _cache = cache;
        }


        // Index화면
        public IActionResult Index(int page = 1, int? categoryId = null, string? q = null, bool showHidden = false)
        {
            const int pageSize = 20;
            page = Math.Max(1, page);

            bool isAdmin = User.IsInRole("admin");

            // 1) 기본 상품 쿼리 (삭제X)
            IQueryable<Product> query = _context.Products
                .AsNoTracking()
                .Where(p => p.DeletedAt == null);

            // 2) 노출/숨김
            if (!isAdmin || !showHidden)
                query = query.Where(p => p.IsVisible);

            if (isAdmin)
            {
                ViewBag.HiddenCount = _context.Products
                    .AsNoTracking()
                    .Count(p => p.DeletedAt == null && !p.IsVisible);
            }

            // 3) 카테고리 트리 로드(검색/하위포함 위해)
            var categories = _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new { c.CategoryId, c.Name, c.ParentCategoryId })
                .ToList();

            var childrenLookup = categories.ToLookup(c => c.ParentCategoryId); // ✅ CS8714 해결

            // 하위 카테고리 id 전부 구하기 (root 포함)
            List<int> GetDescendantsInclusive(int rootId)
            {
                var result = new List<int>();
                var stack = new Stack<int>();
                stack.Push(rootId);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    if (result.Contains(cur)) continue; // 카테고리 수 적으면 OK (원하면 HashSet로 변경)
                    result.Add(cur);

                    foreach (var ch in childrenLookup[cur])
                        stack.Push(ch.CategoryId);
                }
                return result;
            }

            // ✅ SQL2012용: Contains(List) 대신 OR 조건 생성
            IQueryable<T> WhereInSql2012<T>(IQueryable<T> src, Expression<Func<T, int>> selector, List<int> values)
            {
                values = values.Distinct().ToList();
                if (values.Count == 0) return src.Where(_ => false);

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

            // 4) 카테고리 클릭 필터 (선택 카테고리 + 하위 포함)
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                var catIds = GetDescendantsInclusive(categoryId.Value);
                query = WhereInSql2012(query, p => p.CategoryId, catIds);
            }

            // 5) 검색(q): 상품명 + 카테고리명 검색
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();

                // (A) 상품명에 q 포함
                var nameQuery = query.Where(p => p.Name.Contains(q));

                // (B) 카테고리명에 q 포함 -> 해당 카테고리(들) + 하위 카테고리 상품
                var matchedCatRoots = categories
                    .Where(c => c.Name != null && c.Name.Contains(q))
                    .Select(c => c.CategoryId)
                    .Distinct()
                    .ToList();

                IQueryable<Product> catQuery = query.Where(_ => false);
                if (matchedCatRoots.Count > 0)
                {
                    var allCatIds = new HashSet<int>();
                    foreach (var root in matchedCatRoots)
                        foreach (var id in GetDescendantsInclusive(root))
                            allCatIds.Add(id);

                    catQuery = WhereInSql2012(query, p => p.CategoryId, allCatIds.ToList());
                }

                // 상품명 OR 카테고리명
                query = nameQuery.Union(catQuery);
            }

            // 6) 페이징
            int totalCount = query.Count();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var pageProducts = query
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductListItemViewModel
                {
                    ProductId = p.ProductId,
                    Name = p.Name,
                    Price = p.Price,
                    DiscountRate = p.DiscountRate,
                    IsVisible = p.IsVisible,
                    ImageUrls = new List<string>() // 아래에서 채움
                })
                .ToList();

            // 7) 페이지 상품들의 썸네일/이미지 로드 (SQL2012: OR 조건)
            var productIds = pageProducts.Select(x => x.ProductId).ToList();

            List<dynamic> mediaRows = new();
            if (productIds.Count > 0)
            {
                var mediaQuery = _context.Media
                    .AsNoTracking()
                    .Where(m => m.DeletedAt == null
                             && m.IsActive
                             && m.FileUrl != null && m.FileUrl != ""
                             && (m.MediaType == "thumb" || m.MediaType == "image"));

                mediaQuery = WhereInSql2012(mediaQuery, m => m.ProductId, productIds);

                mediaRows = mediaQuery
                    .Select(m => new { m.ProductId, m.MediaType, m.SortOrder, m.FileUrl })
                    .ToList<dynamic>();
            }

            var mediaByProduct = mediaRows
                .OrderBy(m => (string)m.MediaType == "thumb" ? 0 : 1) // thumb 먼저
                .ThenBy(m => (int)m.SortOrder)
                .GroupBy(m => (int)m.ProductId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => (string)x.FileUrl).Take(8).ToList()
                );

            foreach (var p in pageProducts)
                p.ImageUrls = mediaByProduct.TryGetValue(p.ProductId, out var urls) ? urls : new List<string>();

            var model = new ProductListViewModel
            {
                Products = pageProducts,
                CurrentPage = page,
                TotalPages = totalPages
            };

            return View(model);
        }




        // 상품등록 (관리자만)
        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            //  4단 카테고리: 대분류(Parent null)만 내려줌
            ViewBag.Level1 = await _context.Categories
                .Where(c => c.IsActive && c.ParentCategoryId == null)
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.Level2 = new List<Category>();
            ViewBag.Level3 = new List<Category>();
            ViewBag.Level4 = new List<Category>();

            return View(new ProductCreateViewModel());
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductCreateViewModel vm)
        {
            if (vm.CategoryId <= 0)
                ModelState.AddModelError(nameof(vm.CategoryId), "카테고리를 선택해주세요.");

            //  썸네일 8장 제한
            if (vm.ThumbImages != null && vm.ThumbImages.Count > 8)
                ModelState.AddModelError(nameof(vm.ThumbImages), "상단 썸네일은 최대 8장까지 가능합니다.");

            if (!ModelState.IsValid)
            {
                ViewBag.Level1 = await _context.Categories
                    .Where(c => c.IsActive && c.ParentCategoryId == null)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                ViewBag.Level2 = new List<Category>();
                ViewBag.Level3 = new List<Category>();
                ViewBag.Level4 = new List<Category>();
                return View(vm);
            }

            var product = new Product
            {
                Name = vm.Name.Trim(),
                Description = vm.Description,
                Price = vm.Price,
                DiscountRate = vm.DiscountRate,
                CategoryId = vm.CategoryId,
                IsVisible = vm.IsVisible,
                CreatedAt = DateTime.Now
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            string SaveFile(IFormFile file, string folderRel)
            {
                var root = _env.WebRootPath;
                var folderAbs = Path.Combine(root, folderRel.Replace("/", Path.DirectorySeparatorChar.ToString()));
                Directory.CreateDirectory(folderAbs);

                var ext = Path.GetExtension(file.FileName);
                var safeName = $"{Guid.NewGuid():N}{ext}";
                var absPath = Path.Combine(folderAbs, safeName);

                using (var fs = new FileStream(absPath, FileMode.Create))
                    file.CopyTo(fs);

                return "/" + folderRel.Trim('/').Replace("\\", "/") + "/" + safeName;
            }

            var now = DateTime.Now;
            var baseFolder = $"uploads/products/{product.ProductId}";

            //  1) 상단 썸네일 저장 (thumb)
            int tOrder = 0;
            foreach (var f in (vm.ThumbImages ?? new()).Where(x => x != null && x.Length > 0))
            {
                var url = SaveFile(f, baseFolder);
                _context.Media.Add(new Media
                {
                    ProductId = product.ProductId,
                    MediaType = "thumb",
                    FileUrl = url,
                    SortOrder = tOrder++,
                    IsActive = true,
                    CreatedAt = now
                });
            }

            //  2) 상세페이지 이미지 저장 (detail)
            int dOrder = 0;
            foreach (var f in (vm.DetailImages ?? new()).Where(x => x != null && x.Length > 0))
            {
                var url = SaveFile(f, baseFolder);
                _context.Media.Add(new Media
                {
                    ProductId = product.ProductId,
                    MediaType = "detail",
                    FileUrl = url,
                    SortOrder = dOrder++,
                    IsActive = true,
                    CreatedAt = now
                });
            }

            //  3) 동영상 저장 (video)
            int vOrder = 0;
            foreach (var v in (vm.Videos ?? new()).Where(x => x != null && x.Length > 0))
            {
                var url = SaveFile(v, baseFolder);
                _context.Media.Add(new Media
                {
                    ProductId = product.ProductId,
                    MediaType = "video",
                    FileUrl = url,
                    SortOrder = vOrder++,
                    IsActive = true,
                    CreatedAt = now
                });
            }

            await _context.SaveChangesAsync();

            _cache.Remove(CategoryMenuViewComponent.GetCacheKey());
            TempData["SuccessMessage"] = "상품이 성공적으로 등록되었습니다.";
            return RedirectToAction("Detail", new { id = product.ProductId });
        }

        // 상품 수정 (관리자만)
        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null) return NotFound();

            // ✅ thumb → detail → video 순서로 정렬해서 내려주기
            var medias = await _context.Media
                .Where(m => m.ProductId == id && m.DeletedAt == null)
                .OrderBy(m =>
                    m.MediaType == "thumb" ? 0 :
                    m.MediaType == "detail" ? 1 :
                    m.MediaType == "video" ? 2 : 9)
                .ThenBy(m => m.SortOrder)
                .ThenBy(m => m.MediaId)
                .ToListAsync();

            var vm = new ProductEditViewModel
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountRate = product.DiscountRate,
                CategoryId = product.CategoryId,
                IsVisible = product.IsVisible,

                ExistingMedia = medias.Select(m => new MediaEditItemVm
                {
                    MediaId = m.MediaId,
                    MediaType = m.MediaType ?? "",
                    FileUrl = m.FileUrl,
                    SortOrder = m.SortOrder,
                    IsActive = m.IsActive
                }).ToList()
            };

            // 4단 카테고리 ViewBag 구성
            await Build4LevelCategoryViewBag(product.CategoryId);

            return View(vm);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(ProductEditViewModel vm)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == vm.ProductId);
            if (product == null) return NotFound();

            if (!ModelState.IsValid)
            {
                await Build4LevelCategoryViewBag(vm.CategoryId);
                return View(vm);
            }

            // 1) 상품 기본정보 업데이트
            product.Name = vm.Name.Trim();
            product.Description = vm.Description;
            product.Price = vm.Price;
            product.DiscountRate = vm.DiscountRate;
            product.CategoryId = vm.CategoryId;
            product.IsVisible = vm.IsVisible;
            product.UpdatedAt = DateTime.Now;

            // 2) 기존 미디어 로드
            var medias = await _context.Media
                .Where(m => m.ProductId == vm.ProductId && m.DeletedAt == null)
                .ToListAsync();

            var now = DateTime.Now;

            // 3) 기존 미디어 수정(정렬/삭제/활성)
            if (vm.ExistingMedia != null)
            {
                foreach (var item in vm.ExistingMedia)
                {
                    var m = medias.FirstOrDefault(x => x.MediaId == item.MediaId);
                    if (m == null) continue;

                    m.SortOrder = item.SortOrder;

                    if (item.Delete)
                    {
                        m.IsActive = false;     //  삭제면 활성 강제 OFF
                        m.DeletedAt = now;      //  소프트삭제
                    }
                    else
                    {
                        m.IsActive = item.IsActive;
                    }
                }
            }

            //  4) 썸네일 지정/승격 기능 제거 (thumb를 image로 내리는 로직도 제거)
            // if (vm.SelectedThumbnailMediaId.HasValue) { ... }  <-- 삭제

            // 5) 새 파일 업로드 처리
            string SaveFile(IFormFile file, string folderRel)
            {
                var root = _env.WebRootPath;
                var folderAbs = Path.Combine(root, folderRel.Replace("/", Path.DirectorySeparatorChar.ToString()));
                Directory.CreateDirectory(folderAbs);

                var ext = Path.GetExtension(file.FileName);
                var safeName = $"{Guid.NewGuid():N}{ext}";
                var absPath = Path.Combine(folderAbs, safeName);

                using (var fs = new FileStream(absPath, FileMode.Create))
                    file.CopyTo(fs);

                return "/" + folderRel.Trim('/').Replace("\\", "/") + "/" + safeName;
            }

            var baseFolder = $"uploads/products/{vm.ProductId}";

            //  (A) 새 썸네일 1장 업로드 기능 제거
            // if (vm.NewThumbnail != null ...) { ... }  <-- 삭제

            //  (B) NewImages = “상단 썸네일 thumb”로만 저장
            if (vm.NewImages != null && vm.NewImages.Count > 0)
            {
                var maxSort = medias
                    .Where(x => x.DeletedAt == null && x.MediaType == "thumb")
                    .Select(x => x.SortOrder)
                    .DefaultIfEmpty(-1)
                    .Max();

                foreach (var f in vm.NewImages.Where(x => x != null && x.Length > 0))
                {
                    var url = SaveFile(f, baseFolder);
                    _context.Media.Add(new Media
                    {
                        ProductId = vm.ProductId,
                        MediaType = "thumb",   
                        FileUrl = url,
                        SortOrder = ++maxSort,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
            }

            // (C) 새 “상세페이지 이미지(detail)”
            if (vm.NewDetailImages != null && vm.NewDetailImages.Count > 0)
            {
                var maxSort = medias
                    .Where(x => x.DeletedAt == null && x.MediaType == "detail")
                    .Select(x => x.SortOrder)
                    .DefaultIfEmpty(-1)
                    .Max();

                foreach (var f in vm.NewDetailImages.Where(x => x != null && x.Length > 0))
                {
                    var url = SaveFile(f, baseFolder);
                    _context.Media.Add(new Media
                    {
                        ProductId = vm.ProductId,
                        MediaType = "detail",
                        FileUrl = url,
                        SortOrder = ++maxSort,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
            }

            // (D) 새 영상(video)
            if (vm.NewVideos != null && vm.NewVideos.Count > 0)
            {
                var maxSort = medias
                    .Where(x => x.DeletedAt == null && x.MediaType == "video")
                    .Select(x => x.SortOrder)
                    .DefaultIfEmpty(-1)
                    .Max();

                foreach (var f in vm.NewVideos.Where(x => x != null && x.Length > 0))
                {
                    var url = SaveFile(f, baseFolder);
                    _context.Media.Add(new Media
                    {
                        ProductId = vm.ProductId,
                        MediaType = "video",
                        FileUrl = url,
                        SortOrder = ++maxSort,
                        IsActive = true,
                        CreatedAt = now
                    });
                }
            }

            await _context.SaveChangesAsync();

            _cache.Remove(CategoryMenuViewComponent.GetCacheKey());

            return RedirectToAction("Detail", new { id = vm.ProductId });
        }


       
        // 상품 삭제 (관리자만)
        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null) return NotFound();
            return View(product);
        }

        [Authorize(Roles = "admin")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null) return NotFound();

            //  상품 소프트삭제 권장(데이터 유지)
            product.IsVisible = false;
            product.DeletedAt = DateTime.Now;
            product.UpdatedAt = DateTime.Now;

            // 관련 미디어도 비활성화/삭제 처리
            var medias = await _context.Media.Where(m => m.ProductId == id && m.DeletedAt == null).ToListAsync();
            foreach (var m in medias)
            {
                m.IsActive = false;
                m.DeletedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();

            _cache.Remove(CategoryMenuViewComponent.GetCacheKey());

            return RedirectToAction("Index");
        }

        // 상품상세페이지
        [HttpGet]
        public IActionResult Detail(int id, int qnaPage = 1)
        {
            const int qnaPageSize = 5;

            var product = _context.Products
                .FirstOrDefault(p => p.ProductId == id);

            if (product == null)
                return NotFound();

            // ✅ 미디어 로드 (활성 + 삭제아님)
            var medias = _context.Media
                .Where(m => m.ProductId == id && m.IsActive && m.DeletedAt == null)
                .OrderBy(m => m.SortOrder)
                .ThenBy(m => m.MediaId)
                .ToList();

            // ✅ 새 규칙: thumb / detail / video
            // (기존 데이터 호환: thumbnail, image도 섞어서 처리)
            var thumbUrls = medias
                .Where(m => m.MediaType == "thumb" || m.MediaType == "thumbnail")
                .Select(m => m.FileUrl)
                .Take(8)
                .ToList();

            var detailImages = medias
                .Where(m => m.MediaType == "detail")
                .Select(m => m.FileUrl)
                .ToList();

            var videos = medias
                .Where(m => m.MediaType == "video")
                .Select(m => m.FileUrl)
                .ToList();

            // ✅ fallback: 아직 thumb/detail 안쓰는 기존 상품(=image만 있음) 지원
            var legacyImages = medias
                .Where(m => m.MediaType == "image")
                .Select(m => m.FileUrl)
                .ToList();

            if (thumbUrls.Count == 0 && legacyImages.Count > 0)
                thumbUrls = legacyImages.Take(8).ToList();

            if (detailImages.Count == 0 && legacyImages.Count > 8)
                detailImages = legacyImages.Skip(8).ToList();

            // ✅ QnA 페이징
            var qnaQuery = _context.ProductQnA
                .Where(q => q.ProductId == id && !q.IsDeleted)
                .OrderByDescending(q => q.CreatedAt);

            int totalQnA = qnaQuery.Count();
            int totalPages = (int)Math.Ceiling(totalQnA / (double)qnaPageSize);

            var qnaPageItems = qnaQuery
                .Skip((qnaPage - 1) * qnaPageSize)
                .Take(qnaPageSize)
                .ToList();

            ViewBag.QnACurrentPage = qnaPage;
            ViewBag.QnATotalPages = totalPages;

            // ✅ 카테고리 경로
            string categoryPath = "";
            var current = _context.Categories.FirstOrDefault(c => c.CategoryId == product.CategoryId);

            while (current != null)
            {
                categoryPath = current.Name + (categoryPath == "" ? "" : " > " + categoryPath);
                current = current.ParentCategoryId == null
                    ? null
                    : _context.Categories.FirstOrDefault(c => c.CategoryId == current.ParentCategoryId);
            }

            // ✅ ViewModel 생성
            var model = new ProductDetailViewModel
            {
                ProductId = product.ProductId,
                Name = product.Name,
                Price = product.Price,
                DiscountRate = product.DiscountRate,
                Description = product.Description,
                CategoryPath = categoryPath,

                // ✅ 상단 썸네일 영역(최대 8)
                ImageUrls = thumbUrls,

                // ✅ 상세 탭 이미지
                DetailImageUrls = detailImages,

                // ✅ 동영상
                VideoUrls = videos,

                QnAs = qnaPageItems
            };

            return View(model);
        }

        // QnA
        // Qna Ajax 저장 – AddQnAAjax
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddQnAAjax(ProductQnA qna)
        {
            if (!User.Identity!.IsAuthenticated && string.IsNullOrEmpty(qna.UserName))
            {
                return BadRequest("작성자를 입력해주세요.");
            }

            // 로그인 유저면 이름 덮어쓰기
            if (User.Identity!.IsAuthenticated)
            {
                qna.UserName = User.Identity!.Name!;
            }

            qna.CreatedAt = DateTime.Now;
            qna.IsDeleted = false;

            _context.ProductQnA.Add(qna);
            _context.SaveChanges();

            // 다시 해당 상품의 첫 페이지 Q&A 목록 재조회
            const int qnaPageSize = 5;
            var qnaQuery = _context.ProductQnA
                .Where(q => q.ProductId == qna.ProductId && !q.IsDeleted)
                .OrderByDescending(q => q.CreatedAt);

            int totalQnA = qnaQuery.Count();
            int totalPages = (int)Math.Ceiling(totalQnA / (double)qnaPageSize);

            var qnaPageItems = qnaQuery
                .Take(qnaPageSize)
                .ToList();

            var detailModel = new ProductDetailViewModel
            {
                ProductId = qna.ProductId,
                QnAs = qnaPageItems
            };

            ViewBag.QnACurrentPage = 1;
            ViewBag.QnATotalPages = totalPages;

            return PartialView("QnA", detailModel);
        }

        // QnA 페이징 Ajax – QnAPage
        [HttpGet]
        public IActionResult QnAPage(int productId, int page = 1)
        {
            const int qnaPageSize = 5;

            var qnaQuery = _context.ProductQnA
                .Where(q => q.ProductId == productId && !q.IsDeleted)
                .OrderByDescending(q => q.CreatedAt);

            int totalQnA = qnaQuery.Count();
            int totalPages = (int)Math.Ceiling(totalQnA / (double)qnaPageSize);

            var qnaPageItems = qnaQuery
                .Skip((page - 1) * qnaPageSize)
                .Take(qnaPageSize)
                .ToList();

            var model = new ProductDetailViewModel
            {
                ProductId = productId,
                QnAs = qnaPageItems
            };

            ViewBag.QnACurrentPage = page;
            ViewBag.QnATotalPages = totalPages;

            return PartialView("QnA", model);
        }

        // QnA 수정,저장
        public class QnAEditDto
        {
            public int QnaId { get; set; }
            public string? Question { get; set; }
            public bool IsSecret { get; set; }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditQnA([FromBody] QnAEditDto dto)
        {
            var qna = _context.ProductQnA.FirstOrDefault(q => q.QnAId == dto.QnaId);
            if (qna == null) return NotFound();

            bool isAdmin = User.IsInRole("admin");
            string? me = User.Identity?.Name;

            if (!isAdmin && (me == null || me != qna.UserName))
                return Forbid();

            var text = (dto.Question ?? "").Trim();
            if (string.IsNullOrEmpty(text))
                return BadRequest("내용을 입력하세요.");

            qna.Question = text;
            qna.IsSecret = dto.IsSecret;
            _context.SaveChanges();

            // 수정 후 1페이지로 갱신 (원하면 page 유지도 가능)
            return QnAPage(qna.ProductId, 1);
        }

        // QnA 삭제기능 - DeleteQnA
        public class QnAIdDto { public int QnaId { get; set; } }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteQnA([FromBody] QnAIdDto dto)
        {
            var qna = _context.ProductQnA.FirstOrDefault(q => q.QnAId == dto.QnaId);
            if (qna == null) return NotFound();

            bool isAdmin = User.IsInRole("admin");
            string? me = User.Identity?.Name;

            if (!isAdmin && (me == null || me != qna.UserName))
                return Forbid();

            qna.IsDeleted = true;
            _context.SaveChanges();

            // 삭제 후 목록 새로 그리기
            return QnAPage(qna.ProductId, 1);
        }

        // QnA 관리자 답변기능
        public class QnAAnswerDto
        {
            public int QnaId { get; set; }
            public string? Answer { get; set; }
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public IActionResult AnswerQnA([FromBody] QnAAnswerDto dto)
        {
            var qna = _context.ProductQnA.FirstOrDefault(q => q.QnAId == dto.QnaId);
            if (qna == null) return NotFound();

            qna.Answer = dto.Answer ?? "";
            qna.AnsweredAt = DateTime.Now;
            _context.SaveChanges();

            return QnAPage(qna.ProductId, 1);
        }

        // 답변 수정 DTO
        public class AnswerEditDto
        {
            public int QnaId { get; set; }
            public string? Answer { get; set; }
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public IActionResult EditAnswer([FromBody] AnswerEditDto dto)
        {
            var qna = _context.ProductQnA.FirstOrDefault(q => q.QnAId == dto.QnaId);
            if (qna == null) return NotFound();

            var text = (dto.Answer ?? "").Trim();
            if (string.IsNullOrEmpty(text))
                return BadRequest("답변을 입력하세요.");

            qna.Answer = text;
            qna.AnsweredAt = DateTime.Now;
            _context.SaveChanges();

            return QnAPage(qna.ProductId, 1);
        }

        // 답변 삭제 DTO 
        public class AnswerDeleteDto
        {
            public int QnaId { get; set; }
        }

        [HttpPost]
        [Authorize(Roles = "admin")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAnswer([FromBody] AnswerDeleteDto dto)
        {
            var qna = _context.ProductQnA.FirstOrDefault(q => q.QnAId == dto.QnaId);
            if (qna == null) return NotFound();

            qna.Answer = null;

            // AnsweredAt이 nullable이면 null 가능
            // nullable이 아니면 아래 줄은 지우거나 DateTime.MinValue로 처리
            qna.AnsweredAt = null;

            _context.SaveChanges();

            return QnAPage(qna.ProductId, 1);
        }

        // 관리자 Q&A 관리(미답변 목록)
        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> QnAAdmin(int page = 1, bool showAnswered = false)
        {
            const int pageSize = 20;
            page = Math.Max(1, page);

            var q = _context.ProductQnA
                .AsNoTracking()
                .Where(x => !x.IsDeleted);

            // 기본은 미답변만, showAnswered=true면 전체(답변포함)
            if (!showAnswered)
            {
                q = q.Where(x => x.Answer == null || x.Answer == "");
            }

            int totalCount = await q.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            if (page > totalPages) page = totalPages;

            var items = await q
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.ShowAnswered = showAnswered;

            return View(items);
        }



        //  4단 카테고리 ViewBag 구성 메서드(중복 제거용)
        private async Task Build4LevelCategoryViewBag(int leafCategoryId)
        {
            var categories = await _context.Categories
                .Where(c => c.IsActive)
                .ToListAsync();

            var byId = categories.ToDictionary(x => x.CategoryId, x => x);
            var chain = new List<int>();

            if (byId.TryGetValue(leafCategoryId, out var leaf))
            {
                var cur = leaf;
                while (true)
                {
                    chain.Add(cur.CategoryId);
                    if (!cur.ParentCategoryId.HasValue) break;
                    if (!byId.TryGetValue(cur.ParentCategoryId.Value, out cur)) break;
                }
                chain.Reverse(); // root -> leaf
            }

            int? lv1 = chain.Count > 0 ? chain[0] : null;
            int? lv2 = chain.Count > 1 ? chain[1] : null;
            int? lv3 = chain.Count > 2 ? chain[2] : null;
            int? lv4 = chain.Count > 3 ? chain[3] : null;

            ViewBag.Level1 = categories
                .Where(c => c.ParentCategoryId == null)
                .OrderBy(c => c.Name)
                .ToList();

            ViewBag.Level2 = lv1.HasValue
                ? categories.Where(c => c.ParentCategoryId == lv1.Value).OrderBy(c => c.Name).ToList()
                : new List<Category>();

            ViewBag.Level3 = lv2.HasValue
                ? categories.Where(c => c.ParentCategoryId == lv2.Value).OrderBy(c => c.Name).ToList()
                : new List<Category>();

            ViewBag.Level4 = lv3.HasValue
                ? categories.Where(c => c.ParentCategoryId == lv3.Value).OrderBy(c => c.Name).ToList()
                : new List<Category>();

            ViewBag.SelectedLv1 = lv1;
            ViewBag.SelectedLv2 = lv2;
            ViewBag.SelectedLv3 = lv3;
            ViewBag.SelectedLv4 = lv4;
        }

        // 자식 카테고리 가져오기
        [Authorize(Roles = "admin")]
        [HttpGet]
        public async Task<IActionResult> GetChildCategories(int parentId)
        {
            var list = await _context.Categories
                .Where(c => c.ParentCategoryId == parentId && c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.CategoryId, name = c.Name })
                .ToListAsync();

            return Json(list);
        }

        // 하위 카테고리 펼치는 함수
        private List<int> GetDescendantCategoryIds(int rootId)
        {
            var categories = _context.Categories
                .AsNoTracking()
                .Where(c => c.IsActive)
                .Select(c => new { c.CategoryId, c.ParentCategoryId })
                .ToList();

            var childrenMap = categories
                .GroupBy(x => x.ParentCategoryId ?? 0)
                .ToDictionary(g => g.Key, g => g.Select(x => x.CategoryId).ToList());

            var result = new List<int>();
            var visited = new HashSet<int>();
            var stack = new Stack<int>();
            stack.Push(rootId);

            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (!visited.Add(cur)) continue;

                result.Add(cur);

                if (childrenMap.TryGetValue(cur, out var kids))
                {
                    foreach (var k in kids)
                        stack.Push(k);
                }
            }

            return result;
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
