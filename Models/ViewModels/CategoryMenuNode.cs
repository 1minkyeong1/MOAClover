namespace MOAClover.Models.ViewModels
{
    public class CategoryMenuNode
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<CategoryMenuNode> Children { get; set; } = new();
    }
}

//카테고리 메뉴 구성용