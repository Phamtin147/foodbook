using System.Diagnostics;
using foodbook.Models;
using Microsoft.AspNetCore.Mvc;
using foodbook.Attributes;
using foodbook.Services;

namespace foodbook.Controllers
{
    [LoginRequired]
    public class SearchController : Controller
    {
        private readonly ILogger<SearchController> _logger;
        private readonly SupabaseService _supabaseService;

        public SearchController(ILogger<SearchController> logger, SupabaseService supabaseService)
        {
            _logger = logger;
            _supabaseService = supabaseService;
        }

        // Search page - GET
        public async Task<IActionResult> Search(string ingredient = null, string type = null, string difficulty = null)
        {
            try
            {
                _logger.LogInformation("=== SEARCH PAGE LOAD START ===");
                _logger.LogInformation("Query params - ingredient: {Ingredient}, type: {Type}, difficulty: {Difficulty}", 
                    ingredient, type, difficulty);

                // 1. Load ingredients from Ingredient_Master
                var ingredientsResult = await _supabaseService.Client
                    .From<IngredientMaster>()
                    .Select("name")
                    .Get();

                var ingredients = ingredientsResult.Models
                    .Where(x => !string.IsNullOrWhiteSpace(x.name))
                    .Select(x => x.name.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                _logger.LogInformation("Loaded {Count} ingredients: {Ingredients}", 
                    ingredients.Count, string.Join(", ", ingredients.Take(5)));

                // 2. Load recipe types from Recipe_type
                var recipeTypesResult = await _supabaseService.Client
                    .From<RecipeType>()
                    .Select("content")
                    .Get();

                var recipeTypes = recipeTypesResult.Models
                    .Where(x => !string.IsNullOrWhiteSpace(x.content))
                    .Select(x => x.content.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                _logger.LogInformation("Loaded {Count} recipe types: {Types}", 
                    recipeTypes.Count, string.Join(", ", recipeTypes.Take(5)));

                // 3. Load all active recipes for display (chỉ lấy recipes có status = 'active')
                var recipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Select("*")
                    .Where(x => x.status == "active")
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                var searchResults = new List<SearchResultViewModel>();
                foreach (var recipe in recipesResult.Models)
                {
                    // Get user info
                    var userResult = await _supabaseService.Client
                        .From<User>()
                        .Select("full_name, avatar_img")
                        .Where(x => x.user_id == recipe.user_id)
                        .Get();

                    var userInfo = userResult.Models.FirstOrDefault();

                    // Get likes count
                    var likesResult = await _supabaseService.Client
                        .From<likeDislike>()
                        .Select("ld_id")
                        .Where(x => x.recipe_id == recipe.recipe_id)
                        .Get();

                    // Get comments count
                    var commentsResult = await _supabaseService.Client
                        .From<Comment>()
                        .Select("comment_id")
                        .Where(x => x.recipe_id == recipe.recipe_id)
                        .Get();

                    // Get shares count
                    var sharesResult = await _supabaseService.Client
                        .From<Share>()
                        .Where(x => x.recipe_id == recipe.recipe_id)
                        .Get();

                    searchResults.Add(new SearchResultViewModel
                    {
                        RecipeId = recipe.recipe_id ?? 0,
                        RecipeName = recipe.name ?? "Chưa có tên",
                        ThumbnailImg = recipe.thumbnail_img ?? string.Empty,
                        Level = recipe.level ?? "dễ",
                        UserId = recipe.user_id,
                        UserName = userInfo?.full_name ?? "User",
                        UserAvatarUrl = userInfo?.avatar_img ?? "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png",
                        LikesCount = likesResult.Models.Count,
                        CommentsCount = commentsResult.Models.Count,
                        SharesCount = sharesResult.Models.Count
                    });
                }

                _logger.LogInformation("Loaded {Count} recipes for display", searchResults.Count);

                // 4. Apply query parameter filters if provided
                if (!string.IsNullOrEmpty(ingredient))
                {
                    // Filter by ingredient
                    var ingredientIdResult = await _supabaseService.Client
                        .From<IngredientMaster>()
                        .Where(x => x.name == ingredient)
                        .Get();
                    
                    if (ingredientIdResult.Models.Any())
                    {
                        var ingredientId = ingredientIdResult.Models.First().ingredient_id;
                        var recipeIdsWithIngredient = await _supabaseService.Client
                            .From<RecipeIngredient>()
                            .Where(x => x.ingredient_id == ingredientId)
                            .Get();
                        
                        var recipeIds = recipeIdsWithIngredient.Models.Select(x => x.recipe_id).ToList();
                        searchResults = searchResults.Where(x => recipeIds.Contains(x.RecipeId)).ToList();
                        _logger.LogInformation("After ingredient filter '{Ingredient}': {Count} recipes", ingredient, searchResults.Count);
                    }
                }
                
                if (!string.IsNullOrEmpty(type))
                {
                    // Filter by recipe type
                    var typeIdResult = await _supabaseService.Client
                        .From<RecipeType>()
                        .Where(x => x.content == type)
                        .Get();
                    
                    if (typeIdResult.Models.Any())
                    {
                        var typeId = typeIdResult.Models.First().recipe_type_id;
                        var recipeIdsWithType = await _supabaseService.Client
                            .From<RecipeRecipeType>()
                            .Where(x => x.recipe_type_id == typeId)
                            .Get();
                        
                        var recipeIds = recipeIdsWithType.Models.Select(x => x.recipe_id).ToList();
                        searchResults = searchResults.Where(x => recipeIds.Contains(x.RecipeId)).ToList();
                        _logger.LogInformation("After type filter '{Type}': {Count} recipes", type, searchResults.Count);
                    }
                }
                
                if (!string.IsNullOrEmpty(difficulty))
                {
                    // Filter by difficulty
                    searchResults = searchResults.Where(x => x.Level.Equals(difficulty, StringComparison.OrdinalIgnoreCase)).ToList();
                    _logger.LogInformation("After difficulty filter '{Difficulty}': {Count} recipes", difficulty, searchResults.Count);
                }

                // 5. Create view model
                var viewModel = new SearchViewModel
                {
                    SearchResults = searchResults,
                    Ingredients = ingredients,
                    RecipeTypes = recipeTypes
                };

                _logger.LogInformation("=== SEARCH PAGE LOAD COMPLETE ===");
                return View("Search", viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading search page");
                TempData["Error"] = "Không thể tải trang tìm kiếm: " + ex.Message;
                return View("Search", new SearchViewModel());
            }
        }

        // Search with filters - POST
        [HttpPost]
        public async Task<IActionResult> Search([FromBody] SearchRequestModel request)
        {
            try
            {
                _logger.LogInformation("=== SEARCH REQUEST START ===");
                _logger.LogInformation("Search term: {Term}", request.SearchTerm);
                _logger.LogInformation("Selected ingredients: {Ingredients}", 
                    request.SelectedIngredients != null ? string.Join(", ", request.SelectedIngredients) : "None");
                _logger.LogInformation("Selected types: {Types}", 
                    request.SelectedTypes != null ? string.Join(", ", request.SelectedTypes) : "None");
                _logger.LogInformation("Selected difficulties: {Difficulties}", 
                    request.SelectedDifficulties != null ? string.Join(", ", request.SelectedDifficulties) : "None");

                // 1. Start with all active recipes (chỉ lấy recipes có status = 'active')
                var allRecipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Select("*")
                    .Where(x => x.status == "active")
                    .Get();

                var filteredRecipes = allRecipesResult.Models.ToList();
                _logger.LogInformation("Starting with {Count} recipes", filteredRecipes.Count);

                // Check if we have any filters
                bool hasFilters = !string.IsNullOrEmpty(request.SearchTerm) ||
                                 (request.SelectedIngredients != null && request.SelectedIngredients.Any()) ||
                                 (request.SelectedTypes != null && request.SelectedTypes.Any()) ||
                                 (request.SelectedDifficulties != null && request.SelectedDifficulties.Any());

                _logger.LogInformation("Has filters: {HasFilters}", hasFilters);

                // 2. Apply text search filter based on search type
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    if (request.SearchType == "user")
                    {
                        // Search by user name
                        var users = await _supabaseService.Client
                            .From<User>()
                            .Select("user_id, full_name, username")
                            .Get();

                        var matchingUserIds = users.Models
                            .Where(u => (u.full_name != null && u.full_name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                                       (u.username != null && u.username.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase)))
                            .Select(u => u.user_id)
                            .ToList();

                        _logger.LogInformation("Found {Count} matching users for '{Term}'", matchingUserIds.Count, request.SearchTerm);

                        filteredRecipes = filteredRecipes
                            .Where(x => matchingUserIds.Contains(x.user_id))
                            .ToList();
                    }
                    else
                    {
                        // Search by recipe name
                        filteredRecipes = filteredRecipes
                            .Where(x => x.name != null && x.name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    }
                    
                    _logger.LogInformation("After text search '{Term}' (type: {Type}): {Count} recipes", 
                        request.SearchTerm, request.SearchType ?? "recipe", filteredRecipes.Count);
                }

                // 3. Apply ingredient filter (AND logic - recipe phải có TẤT CẢ ingredients)
                if (request.SelectedIngredients != null && request.SelectedIngredients.Any())
                {
                    // Get ingredient IDs from names
                    var ingredientIdsResult = await _supabaseService.Client
                        .From<IngredientMaster>()
                        .Select("ingredient_id, name")
                        .Get();

                    var filteredIngredientIds = ingredientIdsResult.Models
                        .Where(x => x.name != null && request.SelectedIngredients.Contains(x.name))
                        .Select(x => x.ingredient_id)
                        .ToList();

                    _logger.LogInformation("Found {Count} matching ingredient IDs for: {Ingredients}", 
                        filteredIngredientIds.Count, string.Join(", ", request.SelectedIngredients));

                    if (filteredIngredientIds.Any() && filteredIngredientIds.Count == request.SelectedIngredients.Count)
                    {
                        // Get all Recipe_Ingredient relationships
                        var recipeIngredientsResult = await _supabaseService.Client
                            .From<RecipeIngredient>()
                            .Select("recipe_id, ingredient_id")
                            .Get();

                        // Group by recipe_id and filter recipes that have ALL required ingredients
                        var recipeIngredientGroups = recipeIngredientsResult.Models
                            .Where(x => filteredIngredientIds.Contains(x.ingredient_id))
                            .GroupBy(x => x.recipe_id);

                        var filteredRecipeIds = recipeIngredientGroups
                            .Where(g => g.Select(x => x.ingredient_id).Distinct().Count() >= filteredIngredientIds.Count)
                            .Select(g => g.Key)
                            .ToList();

                        _logger.LogInformation("Found {Count} recipes with ALL matching ingredients", filteredRecipeIds.Count);

                        filteredRecipes = filteredRecipes
                            .Where(x => x.recipe_id.HasValue && filteredRecipeIds.Contains(x.recipe_id.Value))
                            .ToList();
                    }
                    else
                    {
                        _logger.LogWarning("Not all ingredients found in database or recipe list is empty");
                        filteredRecipes = new List<Recipe>();
                    }

                    _logger.LogInformation("After ingredient filter (AND): {Count} recipes", filteredRecipes.Count);
                }

                // 4. Apply recipe type filter
                if (request.SelectedTypes != null && request.SelectedTypes.Any())
                {
                    // Get recipe type IDs from names
                    var recipeTypeIdsResult = await _supabaseService.Client
                        .From<RecipeType>()
                        .Select("recipe_type_id, content")
                        .Get();

                    var filteredTypeIds = recipeTypeIdsResult.Models
                        .Where(x => x.content != null && request.SelectedTypes.Contains(x.content))
                        .Select(x => x.recipe_type_id)
                        .ToList();

                    _logger.LogInformation("Found {Count} matching recipe type IDs", filteredTypeIds.Count);

                    if (filteredTypeIds.Any())
                    {
                        // Get recipe IDs from Recipe_RecipeType
                        var recipeTypesResult = await _supabaseService.Client
                            .From<RecipeRecipeType>()
                            .Select("recipe_id, recipe_type_id")
                            .Get();

                        var filteredTypeRecipeIds = recipeTypesResult.Models
                            .Where(x => filteredTypeIds.Contains(x.recipe_type_id))
                            .Select(x => x.recipe_id)
                            .ToList();

                        _logger.LogInformation("Found {Count} recipes with matching types", filteredTypeRecipeIds.Count);

                        filteredRecipes = filteredRecipes
                            .Where(x => x.recipe_id.HasValue && filteredTypeRecipeIds.Contains(x.recipe_id.Value))
                            .ToList();
                    }
                    else
                    {
                        filteredRecipes = new List<Recipe>();
                    }

                    _logger.LogInformation("After type filter: {Count} recipes", filteredRecipes.Count);
                }

                // 5. Apply difficulty filter
                if (request.SelectedDifficulties != null && request.SelectedDifficulties.Any())
                {
                    _logger.LogInformation("Applying difficulty filter for: {Difficulties}", string.Join(", ", request.SelectedDifficulties));
                    
                    filteredRecipes = filteredRecipes
                        .Where(x => x.level != null && request.SelectedDifficulties.Contains(x.level, StringComparer.OrdinalIgnoreCase))
                        .ToList();
                    
                    _logger.LogInformation("After difficulty filter: {Count} recipes", filteredRecipes.Count);
                }

                // 6. Map to SearchResultViewModel FIRST (before sorting, because we need likes count)
                var searchResults = new List<SearchResultViewModel>();
                foreach (var recipe in filteredRecipes)
                {
                    // Get user info
                    var userResult = await _supabaseService.Client
                        .From<User>()
                        .Select("full_name, avatar_img")
                        .Where(x => x.user_id == recipe.user_id)
                        .Get();

                    var userInfo = userResult.Models.FirstOrDefault();

                    // Get likes count
                    var likesResult = await _supabaseService.Client
                        .From<likeDislike>()
                        .Select("ld_id")
                        .Where(x => x.recipe_id == recipe.recipe_id)
                        .Get();

                    // Get comments count
                    var commentsResult = await _supabaseService.Client
                        .From<Comment>()
                        .Select("comment_id")
                        .Where(x => x.recipe_id == recipe.recipe_id)
                        .Get();

                    // Get shares count
                    var sharesResult = await _supabaseService.Client
                        .From<Share>()
                        .Where(x => x.recipe_id == recipe.recipe_id)
                        .Get();

                    searchResults.Add(new SearchResultViewModel
                    {
                        RecipeId = recipe.recipe_id ?? 0,
                        RecipeName = recipe.name ?? "Chưa có tên",
                        ThumbnailImg = recipe.thumbnail_img ?? string.Empty,
                        Level = recipe.level ?? "dễ",
                        UserId = recipe.user_id,
                        UserName = userInfo?.full_name ?? "User",
                        UserAvatarUrl = userInfo?.avatar_img ?? "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png",
                        LikesCount = likesResult.Models.Count,
                        CommentsCount = commentsResult.Models.Count,
                        SharesCount = sharesResult.Models.Count
                    });
                }

                // 7. Apply sorting AFTER getting likes count
                switch (request.SortBy)
                {
                    case "likes_asc":
                        searchResults = searchResults.OrderBy(x => x.LikesCount).ToList();
                        break;
                    case "likes_desc":
                        searchResults = searchResults.OrderByDescending(x => x.LikesCount).ToList();
                        break;
                    case "time_asc":
                        // Need to get created_at from original recipes
                        searchResults = searchResults
                            .OrderBy(x => filteredRecipes.FirstOrDefault(r => r.recipe_id == x.RecipeId)?.created_at)
                            .ToList();
                        break;
                    case "time_desc":
                    default:
                        searchResults = searchResults
                            .OrderByDescending(x => filteredRecipes.FirstOrDefault(r => r.recipe_id == x.RecipeId)?.created_at)
                            .ToList();
                        break;
                }

                _logger.LogInformation("=== SEARCH REQUEST COMPLETE: {Count} results ===", searchResults.Count);
                return Json(new { success = true, results = searchResults, searchType = request.SearchType ?? "recipe" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing search");
                return Json(new { success = false, message = "Không thể thực hiện tìm kiếm: " + ex.Message });
            }
        }

        // Search for users - POST
        [HttpPost]
        public async Task<IActionResult> SearchUsers([FromBody] SearchRequestModel request)
        {
            try
            {
                _logger.LogInformation("=== USER SEARCH REQUEST START ===");
                _logger.LogInformation("Search term: {Term}", request.SearchTerm);

                var users = await _supabaseService.Client
                    .From<User>()
                    .Select("*")
                    .Where(x => x.status == "active")
                    .Get();

                var filteredUsers = users.Models.AsEnumerable();

                // Apply text search
                if (!string.IsNullOrEmpty(request.SearchTerm))
                {
                    filteredUsers = filteredUsers.Where(u => 
                        (u.full_name != null && u.full_name.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase)) ||
                        (u.username != null && u.username.Contains(request.SearchTerm, StringComparison.OrdinalIgnoreCase))
                    );
                }

                var userResults = new List<UserSearchResultViewModel>();
                foreach (var user in filteredUsers)
                {
                    // Count followers
                    var followersResult = await _supabaseService.Client
                        .From<Follow>()
                        .Where(x => x.following_id == user.user_id)
                        .Get();

                    // Count recipes
                    var recipesResult = await _supabaseService.Client
                        .From<Recipe>()
                        .Select("recipe_id")
                        .Where(x => x.user_id == user.user_id)
                        .Get();

                    userResults.Add(new UserSearchResultViewModel
                    {
                        UserId = user.user_id ?? 0,
                        UserName = user.username ?? "User",
                        FullName = user.full_name ?? "User",
                        AvatarUrl = user.avatar_img ?? "/images/default-avatar.jpg",
                        Bio = user.bio ?? "",
                        FollowersCount = followersResult.Models.Count,
                        RecipesCount = recipesResult.Models.Count
                    });
                }

                _logger.LogInformation("=== USER SEARCH COMPLETE: {Count} results ===", userResults.Count);
                return Json(new { success = true, results = userResults, searchType = "user" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing user search");
                return Json(new { success = false, message = "Không thể tìm kiếm người dùng: " + ex.Message });
            }
        }
    }
}