using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Services;

var builder = WebApplication.CreateBuilder(args);

// DB (ASP.NET Core 애플리케이션에서 Entity Framework Core를 사용하여 SQL Server  DB 연결하기 위해 DbContext를 서비스로 등록하는 부분)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Model Identity 등록
builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// MVC
builder.Services.AddControllersWithViews();

// Identity 쿠키 인증 설정 “로그인 안 한 사용자를 어디로 보낼지 정하는 설정”
// 비로그인 사용자가 접근하면 자동으로 / Account / Login으로 보내라
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";    // 쿠키 로그인 옵션
});

// Email 설정 (비밀번호 찾기)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();


var app = builder.Build();


// 관리자 계정 Seed
using (var scope = app.Services.CreateScope())
{
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    //  admin Role 생성
    if (!await roleManager.RoleExistsAsync("admin"))
    {
        await roleManager.CreateAsync(new IdentityRole("admin"));
    }

    //  admin 유저 조회
    var admin = await userManager.FindByNameAsync("admin");

    if (admin == null)
    {
        admin = new User
        {
            UserName = "admin",
            Email = "moaclover@naver.com",
            Name = "관리자",
            Phone = "01040330394",
            IsActive = true
        };

        await userManager.CreateAsync(admin, "Admin@1234");
    }

    //  Role 연결 (이미 있어도 안전)
    if (!await userManager.IsInRoleAsync(admin, "admin"))
    {
        await userManager.AddToRoleAsync(admin, "admin");
    }
}


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();   // <—— 누락되면 로그인/Identity 전부 작동 안함
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

