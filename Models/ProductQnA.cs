

namespace MOAClover.Models
{
    public class ProductQnA
    {
        public int QnAId { get; set; }
        public int ProductId { get; set; }
        public string UserName { get; set; } = "";
        public string Question { get; set; } = "";
        public string? Answer { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? AnsweredAt { get; set; }
        public bool IsSecret { get; set; }
        public bool IsDeleted { get; set; }
    }
}