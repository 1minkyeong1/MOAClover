using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Services;

namespace MOAClover.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
             IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _emailService = emailService;
        }

        // 로그인 페이지
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            var user = await _userManager.FindByNameAsync(username);

            if (user == null || !user.IsActive)
            {
                ModelState.AddModelError("", "아이디 또는 비밀번호가 올바르지 않습니다.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(
                user, password, false, false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "아이디 또는 비밀번호가 올바르지 않습니다.");
                return View();
            }

            // 항상 홈으로
            return RedirectToAction("Index", "Home");
        }

        // 로그아웃
        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }



        // 회원가입창 열기
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // 회원가입 POST
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                Name = model.Name,
                BirthDate = model.BirthDate,
                Phone = model.Phone
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // 주소 저장
                var address = new UserAddress
                {
                    UserId = user.Id,
                    Address = model.Address,
                    AddressDetail = model.AddressDetail,
                    ZipCode = model.ZipCode,
                    IsDefault = true
                };

                _context.UserAddresses.Add(address);
                await _context.SaveChangesAsync();

                return RedirectToAction("RegisterSuccess");
            }

            // 에러 출력
            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(model);
        }

        // 회원가입 성공 후 보여줄 페이지
        [HttpGet]
        public IActionResult RegisterSuccess()
        {
            return View();
        }

        // 아이디 찾기 화면 호출
        [HttpGet]
        public IActionResult FindId()
        {
            return View();
        }

        // 비밀번호 찾기 화면 호출
        [HttpGet]
        public IActionResult FindPassword()
        {
            return View();
        }

        // 이메일로 비밀번호 재설정 링크보내기
        [HttpPost]
        public async Task<IActionResult> FindPassword(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                ModelState.AddModelError("", "해당 이메일의 사용자가 존재하지 않습니다.");
                return View();
            }

            // 토큰 생성
            var resetToken = Guid.NewGuid().ToString("N");

            var tokenEntity = new PasswordResetToken
            {
                UserId = user.Id,
                Token = resetToken,
                ExpireAt = DateTime.Now.AddMinutes(30),
                IsUsed = false
            };

            _context.PasswordResetTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            // 이메일 발송
            string resetLink = $"{Request.Scheme}://{Request.Host}/Account/ResetPassword?token={resetToken}";

            string body = $@"
        <h2>비밀번호 재설정 안내</h2>
        <p>아래 링크를 클릭하면 비밀번호 재설정 페이지로 이동합니다.</p>
        <a href='{resetLink}' style='color:#4a6cf7;'>비밀번호 재설정하기</a>
        <p>30분간 유효합니다.</p>";

            //                                           ! = “내가 책임질게, null 아님”
            await _emailService.SendEmailAsync(user.Email!, "비밀번호 재설정", body);

            ViewBag.Message = "비밀번호 재설정 이메일을 발송했습니다.";
            return View();
        }

        // 비밀번호 재설정 페이지
        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (string.IsNullOrEmpty(token))
                return BadRequest("잘못된 요청입니다.");

            return View(model: token);
        }


        // 새 비밀번호 저장처리
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string token, string newPassword)
        {
            var tokenEntity = _context.PasswordResetTokens
                .FirstOrDefault(x => x.Token == token && !x.IsUsed);

            if (tokenEntity == null)
            {
                ModelState.AddModelError("", "유효하지 않은 토큰입니다.");
                return View(token);
            }

            if (tokenEntity.ExpireAt < DateTime.Now)
            {
                ModelState.AddModelError("", "토큰이 만료되었습니다.");
                return View(token);
            }

            var user = await _userManager.FindByIdAsync(tokenEntity.UserId);
            if (user == null)
            {
                ModelState.AddModelError("", "사용자를 찾을 수 없습니다.");
                return View(token);
            }

            // 기존 비밀번호 삭제 후 새 비밀번호 등록
            var resetResult = await _userManager.RemovePasswordAsync(user);
            if (!resetResult.Succeeded)
            {
                ModelState.AddModelError("", "비밀번호 초기화에 실패했습니다.");
                return View(token);
            }

            var addPasswordResult = await _userManager.AddPasswordAsync(user, newPassword);
            if (!addPasswordResult.Succeeded)
            {
                ModelState.AddModelError("", addPasswordResult.Errors.First().Description);
                return View(token);
            }

            // 토큰 사용 처리
            tokenEntity.IsUsed = true;
            await _context.SaveChangesAsync();

            return RedirectToAction("ResetPasswordSuccess");
        }

        // 비밀번호 변경 성공페이지
        [HttpGet]
        public IActionResult ResetPasswordSuccess()
        {
            return View();
        }


       









    }
}
