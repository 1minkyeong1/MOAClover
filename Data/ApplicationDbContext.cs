using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using MOAClover.Models;

namespace MOAClover.Data
{
    public class ApplicationDbContext : IdentityDbContext<User>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Product> Products { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Media> Media { get; set; }
        public DbSet<UserAddress> UserAddresses { get; set; }
        public DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
        public DbSet<ProductQnA> ProductQnA { get; set; }
       
       

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Identity 기본 설정 (항상 먼저 호출)
            base.OnModelCreating(modelBuilder);

            // ❗ 카테고리 부모 관계
            modelBuilder.Entity<Category>()
                .HasOne<Category>()
                .WithMany()
                .HasForeignKey(c => c.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            // ❗ 상품 - 카테고리 관계
            modelBuilder.Entity<Product>()
                 .HasOne<Category>()
                 .WithMany()
                 .HasForeignKey(p => p.CategoryId)
                 .OnDelete(DeleteBehavior.Restrict);

            // ❗ 상품 - 미디어 관계
            modelBuilder.Entity<Media>()
                 .HasOne<Product>()
                 .WithMany()
                 .HasForeignKey(m => m.ProductId)
                 .OnDelete(DeleteBehavior.Cascade);

            // 🔥 사용자 - 주소 관계 (필수)
            modelBuilder.Entity<UserAddress>()
                .HasOne(ua => ua.User)
                .WithMany(u => u.Addresses)
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<ProductQnA>()
                .HasKey(q => q.QnAId);

            // Products 테이블과 ProductId 컬럼이 FK 로 연결
            modelBuilder.Entity<ProductQnA>()
                .HasOne<Product>()
                .WithMany()
                .HasForeignKey(q => q.ProductId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
