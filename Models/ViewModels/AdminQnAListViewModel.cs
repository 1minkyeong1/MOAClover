namespace MOAClover.Models.ViewModels
{
    public class AdminQnAListViewModel
    {
        public List<AdminQnAListItemViewModel> Items { get; set; } = new();

        public string Filter { get; set; } = "waiting";

        public string? Keyword { get; set; }

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; } = 1;

        public int TotalCount { get; set; }

        public int WaitingCount { get; set; }

        public int AnsweredCount { get; set; }
    }

    public class AdminQnAListItemViewModel
    {
        public int QnAId { get; set; }

        public int ProductId { get; set; }

        public string ProductName { get; set; } = "";

        public string UserName { get; set; } = "";

        public string Question { get; set; } = "";

        public string? Answer { get; set; }

        public bool IsSecret { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? AnsweredAt { get; set; }

        public bool HasAnswer => !string.IsNullOrWhiteSpace(Answer);
    }
}