using System;

namespace MOAClover.Models
{
    public class CartItem
    {
        public int CartItemId { get; set; }

        // 로그인 사용자는 UserName 기준
        public string? UserId { get; set; }

        // 비회원 장바구니용 쿠키 ID
        public string? GuestCartId { get; set; }

        public int ProductId { get; set; }

        public int Quantity { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // 추후 옵션 연결 대비
        // 지금은 사용 안 하지만 CartItemId 기준으로 옵션 테이블을 나중에 붙일 수 있음
    }
}