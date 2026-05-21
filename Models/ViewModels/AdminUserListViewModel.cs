namespace MOAClover.Models.ViewModels
{
    public class AdminUserListViewModel
    {
        public List<AdminUserListItemViewModel> Users { get; set; } = new();

        public string? Keyword { get; set; }

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; } = 1;

        public int TotalCount { get; set; }
    }

    public class AdminUserListItemViewModel
    {
        public string Id { get; set; } = "";

        public string UserName { get; set; } = "";

        public string Name { get; set; } = "";

        public string Email { get; set; } = "";

        public string Phone { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public bool IsAdmin { get; set; }

        public int OrderCount { get; set; }

        public int PaidOrderCount { get; set; }

        public int TotalPaidAmount { get; set; }
    }

    public class AdminUserDetailViewModel
    {
        public string Id { get; set; } = "";

        public string UserName { get; set; } = "";

        public string Name { get; set; } = "";

        public string Email { get; set; } = "";

        public string Phone { get; set; } = "";

        public DateTime CreatedAt { get; set; }

        public bool IsActive { get; set; }

        public bool IsAdmin { get; set; }

        public List<AdminUserAddressViewModel> Addresses { get; set; } = new();

        public List<Order> Orders { get; set; } = new();

        public int OrderCount { get; set; }

        public int PaidOrderCount { get; set; }

        public int TotalPaidAmount { get; set; }

        public int CurrentPage { get; set; } = 1;

        public int TotalPages { get; set; } = 1;
    }

    public class AdminUserAddressViewModel
    {
        public int Id { get; set; }

        public string ReceiverName { get; set; } = "";

        public string ReceiverPhone { get; set; } = "";

        public string ZipCode { get; set; } = "";

        public string Address { get; set; } = "";

        public string? AddressDetail { get; set; }

        public bool IsDefault { get; set; }
    }
}