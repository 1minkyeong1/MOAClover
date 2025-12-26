using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Models.ViewModels;
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

        // =========================
        // 로그인
        // =========================
        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ModelState.AddModelError("", "아이디와 비밀번호를 입력해주세요.");
                return View();
            }

            var user = await _userManager.FindByNameAsync(username);

            if (user == null || !user.IsActive || user.DeletedAt != null)
            {
                ModelState.AddModelError("", "아이디 또는 비밀번호가 올바르지 않습니다.");
                return View();
            }

            var result = await _signInManager.PasswordSignInAsync(user, password, false, lockoutOnFailure: false);

            if (!result.Succeeded)
            {
                ModelState.AddModelError("", "아이디 또는 비밀번호가 올바르지 않습니다.");
                return View();
            }

            return RedirectToAction("Index", "Home");
        }

        // 로그아웃
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        // =========================
        // 회원가입
        // =========================
        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 중복 체크
            if (!string.IsNullOrWhiteSpace(model.UserName))
            {
                var existsByName = await _userManager.FindByNameAsync(model.UserName);
                if (existsByName != null)
                {
                    ModelState.AddModelError("", "이미 사용 중인 아이디입니다.");
                    return View(model);
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var existsByEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existsByEmail != null)
                {
                    ModelState.AddModelError("", "이미 가입된 이메일입니다.");
                    return View(model);
                }
            }

            var user = new User
            {
                UserName = model.UserName ?? "",
                Email = model.Email ?? "",
                Name = model.Name ?? "",
                BirthDate = model.BirthDate,
                Phone = model.Phone ?? "",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password ?? "");

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                return View(model);
            }

            // 주소 저장 (입력된 경우에만)
            if (!string.IsNullOrWhiteSpace(model.Address) || !string.IsNullOrWhiteSpace(model.ZipCode))
            {
                var address = new UserAddress
                {
                    UserId = user.Id,
                    Address = model.Address ?? "",
                    AddressDetail = model.AddressDetail,
                    ZipCode = model.ZipCode ?? "",
                    IsDefault = true
                };

                _context.UserAddresses.Add(address);
                await _context.SaveChangesAsync();
            }

            return RedirectToAction("RegisterSuccess");
        }

        [HttpGet]
        public IActionResult RegisterSuccess() => View();

        // =========================
        // 아이디 찾기 (이름 + 이메일)
        // =========================
        [HttpGet]
        public IActionResult FindId() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindId(string name, string email)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "이름과 이메일을 입력해주세요.");
                return View();
            }

            var user = await _userManager.Users.FirstOrDefaultAsync(u =>
                u.Name == name &&
                u.Email == email &&
                u.IsActive &&
                u.DeletedAt == null);

            if (user == null)
            {
                ViewBag.Message = "입력하신 정보로 가입된 계정을 찾을 수 없습니다.";
                return View();
            }

            // 화면에 아이디 일부 마스킹
            ViewBag.Message = $"가입된 아이디: &nbsp;&nbsp; {MaskUserName(user.UserName ?? "")}";
            return View();
        }

        // =========================
        // 비밀번호 찾기 (이메일로 재설정 링크 발송)
        // =========================
        [HttpGet]
        public IActionResult FindPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FindPassword(string username, string email)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(email))
            {
                ModelState.AddModelError("", "아이디와 이메일을 입력해주세요.");
                return View();
            }

            var user = await _userManager.FindByNameAsync(username);

            // 보안상 동일 메시지
            if (user == null ||
                !string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase) ||
                !user.IsActive || user.DeletedAt != null)
            {
                ViewBag.Message = "입력하신 정보가 맞다면 비밀번호 재설정 이메일을 발송했습니다.";
                return View();
            }

            var identityToken = await _userManager.GeneratePasswordResetTokenAsync(user);

            var tokenEntity = new PasswordResetToken
            {
                UserId = user.Id,
                Token = identityToken,
                ExpireAt = DateTime.UtcNow.AddMinutes(30),
                IsUsed = false
            };

            _context.PasswordResetTokens.Add(tokenEntity);
            await _context.SaveChangesAsync();

            var resetLink = Url.Action(
                "ResetPassword",
                "Account",
                new { tokenId = tokenEntity.Id, token = Uri.EscapeDataString(identityToken) },
                protocol: Request.Scheme
            )!;

            var body = $@"
                    <h2>비밀번호 재설정 안내</h2>
                    <p>아래 링크를 클릭하면 비밀번호 재설정 페이지로 이동합니다.</p>
                    <p><a href='{resetLink}'>비밀번호 재설정하기</a></p>
                    <p>30분간 유효합니다.</p>";

            await _emailService.SendEmailAsync(user.Email!, "비밀번호 재설정", body);

            ViewBag.Message = "비밀번호 재설정 이메일을 발송했습니다. 입력하신 이메일을 확인해 주세요.";
            return View();
        }


        // =========================
        // 비밀번호 재설정 페이지
        // =========================
        [HttpGet]
        public async Task<IActionResult> ResetPassword(int tokenId, string token)
        {
            if (tokenId <= 0 || string.IsNullOrWhiteSpace(token))
                return BadRequest("잘못된 요청입니다.");

            var row = await _context.PasswordResetTokens.FirstOrDefaultAsync(x => x.Id == tokenId);

            if (row == null || row.IsUsed || row.ExpireAt < DateTime.UtcNow)
                return View("ResetPasswordExpired"); // 만료 뷰 추천

            // 토큰 변조 방지: URL token == DB token 확인
            var decoded = Uri.UnescapeDataString(token);
            if (!string.Equals(row.Token, decoded, StringComparison.Ordinal))
                return BadRequest("잘못된 토큰입니다.");

            ViewBag.TokenId = tokenId;
            ViewBag.Token = decoded;
            return View();
        }

        // 새 비밀번호 저장처리
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int tokenId, string token, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "비밀번호가 일치하지 않습니다.");
                ViewBag.TokenId = tokenId;
                ViewBag.Token = token;
                return View();
            }

            var row = await _context.PasswordResetTokens.FirstOrDefaultAsync(x => x.Id == tokenId);

            if (row == null || row.IsUsed)
            {
                ModelState.AddModelError("", "유효하지 않은 토큰입니다.");
                ViewBag.TokenId = tokenId;
                ViewBag.Token = token;
                return View();
            }

            if (row.ExpireAt < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "토큰이 만료되었습니다.");
                ViewBag.TokenId = tokenId;
                ViewBag.Token = token;
                return View();
            }

            var decodedToken = Uri.UnescapeDataString(token ?? "");

            if (!string.Equals(row.Token, decodedToken, StringComparison.Ordinal))
            {
                ModelState.AddModelError("", "토큰이 올바르지 않습니다.");
                ViewBag.TokenId = tokenId;
                ViewBag.Token = token;
                return View();
            }

            var user = await _userManager.FindByIdAsync(row.UserId);
            if (user == null)
            {
                ModelState.AddModelError("", "사용자를 찾을 수 없습니다.");
                ViewBag.TokenId = tokenId;
                ViewBag.Token = token;
                return View();
            }

            // ✅ Identity 정석 방식
            var resetResult = await _userManager.ResetPasswordAsync(user, decodedToken, newPassword);

            if (!resetResult.Succeeded)
            {
                foreach (var e in resetResult.Errors)
                    ModelState.AddModelError("", e.Description);

                ViewBag.TokenId = tokenId;
                ViewBag.Token = token;
                return View();
            }

            row.IsUsed = true;
            await _context.SaveChangesAsync();

            return RedirectToAction("ResetPasswordSuccess");
        }

        [HttpGet]
        public IActionResult ResetPasswordSuccess() => View();

        private static string MaskUserName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName)) return "";
            if (userName.Length <= 2) return userName[0] + "*";
            return userName.Substring(0, 2) + new string('*', Math.Max(1, userName.Length - 2));
        }



        // ✅ 마이페이지 조회
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> MyPage()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var addresses = await _context.UserAddresses
                .Where(a => a.UserId == user.Id)
                .OrderByDescending(a => a.IsDefault)
                .ThenByDescending(a => a.Id)
                .Select(a => new AddressItemVm
                {
                    Id = a.Id,
                    ZipCode = a.ZipCode,
                    Address = a.Address,
                    AddressDetail = a.AddressDetail,
                    IsDefault = a.IsDefault
                })
                .ToListAsync();

            var vm = new MyPageViewModel
            {
                UserName = user.UserName ?? "",
                Name = user.Name,
                BirthDate = user.BirthDate,
                Phone = user.Phone,         // ✅ User.Phone
                Email = user.Email,
                Addresses = addresses
            };

            return View(vm);
        }

        // ✅ 기본정보 수정 (아이디 제외)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateMyPage(MyPageViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            // 아이디(UserName)는 수정 금지 → 건드리지 않음

            user.Name = vm.Name?.Trim() ?? "";
            user.BirthDate = vm.BirthDate;
            user.Phone = vm.Phone?.Trim() ?? "";
            user.UpdatedAt = DateTime.UtcNow;

            // 이메일은 Identity 메서드로 변경 (정석)
            if (!string.Equals(user.Email, vm.Email, StringComparison.OrdinalIgnoreCase))
            {
                var r = await _userManager.SetEmailAsync(user, vm.Email ?? "");
                if (!r.Succeeded)
                {
                    TempData["Err"] = string.Join(" / ", r.Errors.Select(e => e.Description));
                    return RedirectToAction(nameof(MyPage));
                }
            }

            var update = await _userManager.UpdateAsync(user);
            if (!update.Succeeded)
            {
                TempData["Err"] = string.Join(" / ", update.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(MyPage));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Msg"] = "회원정보가 수정되었습니다.";
            return RedirectToAction(nameof(MyPage));
        }

        // ✅ 비밀번호 변경
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(MyPageViewModel vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(vm.CurrentPassword) ||
                string.IsNullOrWhiteSpace(vm.NewPassword) ||
                string.IsNullOrWhiteSpace(vm.ConfirmNewPassword))
            {
                TempData["Err"] = "현재/새 비밀번호/확인을 모두 입력해주세요.";
                return RedirectToAction(nameof(MyPage));
            }

            if (vm.NewPassword != vm.ConfirmNewPassword)
            {
                TempData["Err"] = "새 비밀번호가 서로 다릅니다.";
                return RedirectToAction(nameof(MyPage));
            }

            var result = await _userManager.ChangePasswordAsync(user, vm.CurrentPassword, vm.NewPassword);
            if (!result.Succeeded)
            {
                TempData["Err"] = string.Join(" / ", result.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(MyPage));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["Msg"] = "비밀번호가 변경되었습니다.";
            return RedirectToAction(nameof(MyPage));
        }

        // ✅ 배송지 추가
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddAddress(AddressEditVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (!ModelState.IsValid)
            {
                TempData["Err"] = "배송지 입력값을 확인해주세요.";
                return RedirectToAction(nameof(MyPage));
            }

            // 대표로 지정하면 기존 대표 해제
            if (vm.IsDefault)
            {
                var olds = await _context.UserAddresses.Where(a => a.UserId == user.Id && a.IsDefault).ToListAsync();
                foreach (var o in olds) o.IsDefault = false;
            }

            var addr = new UserAddress
            {
                UserId = user.Id,
                ZipCode = vm.ZipCode.Trim(),
                Address = vm.Address.Trim(),
                AddressDetail = vm.AddressDetail?.Trim(),
                IsDefault = vm.IsDefault
            };

            // 최초 주소면 무조건 대표로
            var hasAny = await _context.UserAddresses.AnyAsync(a => a.UserId == user.Id);
            if (!hasAny) addr.IsDefault = true;

            _context.UserAddresses.Add(addr);
            await _context.SaveChangesAsync();

            TempData["Msg"] = "배송지가 추가되었습니다.";
            return RedirectToAction(nameof(MyPage));
        }

        // ✅ 배송지 수정
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateAddress(AddressEditVm vm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var addr = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == vm.Id && a.UserId == user.Id);
            if (addr == null) return NotFound();

            if (!ModelState.IsValid)
            {
                TempData["Err"] = "배송지 입력값을 확인해주세요.";
                return RedirectToAction(nameof(MyPage));
            }

            if (vm.IsDefault)
            {
                var olds = await _context.UserAddresses.Where(a => a.UserId == user.Id && a.IsDefault).ToListAsync();
                foreach (var o in olds) o.IsDefault = false;
            }

            addr.ZipCode = vm.ZipCode.Trim();
            addr.Address = vm.Address.Trim();
            addr.AddressDetail = vm.AddressDetail?.Trim();
            addr.IsDefault = vm.IsDefault;

            await _context.SaveChangesAsync();

            TempData["Msg"] = "배송지가 수정되었습니다.";
            return RedirectToAction(nameof(MyPage));
        }

        // ✅ 배송지 삭제 (대표 배송지 삭제 방지)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAddress(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var addr = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);
            if (addr == null) return NotFound();

            if (addr.IsDefault)
            {
                TempData["Err"] = "대표 배송지는 삭제할 수 없습니다. 다른 배송지를 대표로 지정 후 삭제해주세요.";
                return RedirectToAction(nameof(MyPage));
            }

            _context.UserAddresses.Remove(addr);
            await _context.SaveChangesAsync();

            TempData["Msg"] = "배송지가 삭제되었습니다.";
            return RedirectToAction(nameof(MyPage));
        }

        // ✅ 대표 배송지 지정
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetDefaultAddress(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            var target = await _context.UserAddresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == user.Id);
            if (target == null) return NotFound();

            var olds = await _context.UserAddresses.Where(a => a.UserId == user.Id && a.IsDefault).ToListAsync();
            foreach (var o in olds) o.IsDefault = false;

            target.IsDefault = true;
            await _context.SaveChangesAsync();

            TempData["Msg"] = "대표 배송지가 변경되었습니다.";
            return RedirectToAction(nameof(MyPage));
        }

        // ✅ 탈퇴(소프트 삭제 + 로그인 해제)
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(string password)
        {
            if (User.IsInRole("admin")) return Forbid();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login");

            if (string.IsNullOrWhiteSpace(password) || !await _userManager.CheckPasswordAsync(user, password))
            {
                TempData["Err"] = "비밀번호가 올바르지 않습니다.";
                return RedirectToAction(nameof(MyPage));
            }

            // ✅ 소프트 삭제로 처리 (IsActive=false, DeletedAt 기록)
            user.IsActive = false;
            user.DeletedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _userManager.UpdateAsync(user);
            await _signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home");
        }
    }

}

