using System;

namespace foodbook.Models
{
    public class RecipeViewModel
    {
        public int RecipeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Level { get; set; }
        public int CookTime { get; set; }
        public int Likes { get; set; }
        public int CommentsCount { get; set; }
        public int SharesCount { get; set; }
        public DateTime? CreatedAt { get; set; }

        // Dữ liệu ảnh (có thể là base64 hoặc byte[])
        public string? ThumbnailImg { get; set; }
        
    }
}