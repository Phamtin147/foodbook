namespace foodbook.Models
{
    public class SearchViewModel
    {
        public List<SearchResultViewModel> SearchResults { get; set; } = new List<SearchResultViewModel>();
        public List<string> Ingredients { get; set; } = new List<string>();
        public List<string> RecipeTypes { get; set; } = new List<string>();
    }

    public class SearchResultViewModel
    {
        public int RecipeId { get; set; }
        public string RecipeName { get; set; } = string.Empty;
        public string ThumbnailImg { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserAvatarUrl { get; set; } = string.Empty;
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int SharesCount { get; set; }
    }
    
    public class UserSearchResultViewModel
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string AvatarUrl { get; set; } = string.Empty;
        public string Bio { get; set; } = string.Empty;
        public int FollowersCount { get; set; }
        public int RecipesCount { get; set; }
    }

    public class SearchRequestModel
    {
        public string? SearchTerm { get; set; }
        public List<string>? SelectedIngredients { get; set; }
        public List<string>? SelectedTypes { get; set; }
        public List<string>? SelectedDifficulties { get; set; }
        public string? SearchType { get; set; }
        public string SortBy { get; set; } = "time_desc";
    }
}