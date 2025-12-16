

public class ProductQnA
{
    public int QnAId { get; set; }
    public int ProductId { get; set; }
    public string? UserId { get; set; }

    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }

    public bool IsSecret { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
}
