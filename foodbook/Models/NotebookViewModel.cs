namespace foodbook.Models
{
    public class NotebookViewModel
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public string ThumbnailImg { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string UserAvatarUrl { get; set; } = string.Empty;
        public int LikesCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
