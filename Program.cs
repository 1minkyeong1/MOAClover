using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MOAClover.Data;
using MOAClover.Models;
using MOAClover.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.  MVC
builder.Services.AddControllersWithViews();

//  ASP.NET Core 애플리케이션에서 Entity Framework Core를 사용하여 SQL Server  DB 연결하기 위해 DbContext를 서비스로 등록하는 부분
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

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";    // 쿠키 로그인 옵션
});

// Email 설정 (비밀번호 찾기)
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddTransient<IEmailService, EmailService>();

// MVC
builder.Services.AddControllersWithViews();



var app = builder.Build();

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

