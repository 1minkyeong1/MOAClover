using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MOAClover.Data;
using MOAClover.Models.ViewModels;

namespace MOAClover.ViewComponents
{
    public class CategoryMenuViewComponent : ViewComponent
    {
        private const string CacheKey = "CATEGORY_MENU_V1";
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;

        public CategoryMenuViewComponent(ApplicationDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            if (_cache.TryGetValue(CacheKey, out List<CategoryMenuNode>? cached) && cached != null)
                return View(cached);

            // 1) 상품이 달린 카테고리 Id들
            var productCategoryIds = await _context.Products
                .Select(p => p.CategoryId)
                .Distinct()
                .ToListAsync();

            var hasProduct = new HashSet<int>(productCategoryIds);

            // 2) 전체 카테고리(메뉴 구성용: id/name/parent만)
            var categories = await _context.Categories
                .Where(c => c.IsActive) // 컬럼 없으면 이 줄 제거
                .Select(c => new { c.CategoryId, c.Name, c.ParentCategoryId })
                .ToListAsync();

            var parentMap = categories.ToDictionary(x => x.CategoryId, x => x.ParentCategoryId);

            // 3) “상품 있는 카테고리 + 그 조상(부모들)”까지 모두 보이도록 set 확장
            var visible = new HashSet<int>(hasProduct);
            foreach (var cid in hasProduct.ToList())
            {
                var cur = cid;
                while (parentMap.TryGetValue(cur, out int? pid) && pid.HasValue)
                {
                    if (!visible.Add(pid.Value)) break; // 이미 있으면 더 올라가도 중복
                    cur = pid.Value;
                }
            }

            // 4) visible만 노드 생성
            var nodes = new Dictionary<int, CategoryMenuNode>();
            foreach (var c in categories.Where(x => visible.Contains(x.CategoryId)))
            {
                nodes[c.CategoryId] = new CategoryMenuNode { Id = c.CategoryId, Name = c.Name };
            }

            // 5) 트리 연결
            var roots = new List<CategoryMenuNode>();
            foreach (var c in categories.Where(x => visible.Contains(x.CategoryId)))
            {
                if (c.ParentCategoryId.HasValue && nodes.ContainsKey(c.ParentCategoryId.Value))
                    nodes[c.ParentCategoryId.Value].Children.Add(nodes[c.CategoryId]);
                else
                    roots.Add(nodes[c.CategoryId]);
            }

            // (선택) 자식 정렬: 이름순
            void SortTree(List<CategoryMenuNode> list)
            {
                list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
                foreach (var n in list) SortTree(n.Children);
            }
            SortTree(roots);

            // 6) 캐싱(5분)
            _cache.Set(CacheKey, roots, TimeSpan.FromMinutes(5));

            return View(roots);
        }

        // ✅ 다른 곳(상품등록/카테고리수정)에서 캐시 삭제할 때 이 키만 지우면 됩니다.
        public static string GetCacheKey() => CacheKey;
    }
}
