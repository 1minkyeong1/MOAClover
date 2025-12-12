using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MOAClover.Models;

namespace MOAClover.Controllers
{
    public class HomeController : Controller
    {
        // 로그기록 (실행 중 발생하는 메시지를 콘솔이나 파일로 남겨줌)
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        //  /Home/Index 로 접속하면 Index화면 보여줌
        public IActionResult Index()
        {
            return View();
        }
        
        //  /Home/Create 로 접속하면 새상품등록(Create) 화면을 보여줌
        public IActionResult Create()
        {
            return View();
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

        //  /Home/Detail  상품상세페이지 이동
        public IActionResult Detail()
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
