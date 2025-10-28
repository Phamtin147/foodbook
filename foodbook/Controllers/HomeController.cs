using System.Diagnostics;
using foodbook.Models;
using Microsoft.AspNetCore.Mvc;
using foodbook.Attributes;
using foodbook.Services;

namespace foodbook.Controllers
{
    [LoginRequired]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SupabaseService _supabaseService;

        public HomeController(ILogger<HomeController> logger, SupabaseService supabaseService)
        {
            _logger = logger;
            _supabaseService = supabaseService;
        }

        public IActionResult Index()
        {
            // Redirect to Newsfeed
            return RedirectToAction("Newsfeed");
        }

        public async Task<IActionResult> Newsfeed(int page = 1, int pageSize = 4)
        {
            try
            {
                _logger.LogInformation("Loading newsfeed...");
                
                // Get current user ID for comparison
                var currentSessionEmail = HttpContext.Session.GetString("user_email");
                int? currentUserId = null;
                if (!string.IsNullOrEmpty(currentSessionEmail))
                {
                    try
                    {
                        var currentUser = await _supabaseService.Client
                            .From<User>()
                            .Select("user_id")
                            .Where(x => x.email == currentSessionEmail)
                            .Single();
                        currentUserId = currentUser?.user_id;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get current user ID");
                    }
                }
                
                // Lấy recipes với pagination
                var offset = (page - 1) * pageSize;
                var recipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Select("*")
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();
                
                var recipes = recipesResult.Models;
                _logger.LogInformation("Loaded {Count} recipes from DB", recipes.Count);
                
                // Map sang NewfeedViewModel
                var newsfeedItems = new List<NewfeedViewModel>();
                
                foreach (var r in recipes)
                {
                    var recipeId = r.recipe_id ?? 0;
                    
                    // Load tags từ Ingredient và RecipeType
                    var tags = new List<string>();
                    
                    // Thêm level làm tag đầu tiên
                    if (!string.IsNullOrEmpty(r.level))
                    {
                        tags.Add($"{r.level}");
                    }
                    
                    // Load ingredients từ Recipe_Ingredient và Ingredient_Master
                    try
                    {
                        // Get ingredient IDs from Recipe_Ingredient
                        var recipeIngredients = await _supabaseService.Client
                            .From<RecipeIngredient>()
                            .Select("ingredient_id")
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                            
                        foreach (var ri in recipeIngredients.Models)
                        {
                            // Get ingredient name from Ingredient_Master
                            var ingredientResult = await _supabaseService.Client
                                .From<IngredientMaster>()
                                .Select("name")
                                .Where(x => x.ingredient_id == ri.ingredient_id)
                                .Single();
                                
                            if (ingredientResult != null && !string.IsNullOrEmpty(ingredientResult.name))
                            {
                                tags.Add($"{ingredientResult.name.Trim()}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load ingredients for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load recipe types qua bảng trung gian Recipe_RecipeType
                    try
                    {
                        var recipeTypes = await _supabaseService.Client
                            .From<RecipeRecipeType>()
                            .Select("recipe_type_id")
                            .Where(x => x.recipe_id == recipeId)
                            .Get();

                        foreach (var rrt in recipeTypes.Models)
                        {
                            var typeDetail = await _supabaseService.Client
                                .From<RecipeType>()
                                .Select("content")
                                .Where(x => x.recipe_type_id == rrt.recipe_type_id)
                                .Get();

                            if (typeDetail.Models.Any() && !string.IsNullOrEmpty(typeDetail.Models.First().content))
                            {
                                tags.Add($"#{typeDetail.Models.First().content.Trim()}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load recipe types for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load user info
                    string userName = "User";
                    string userAvatarUrl = "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png";
                    int userId = r.user_id;
                    bool isFollowing = false;
                    
                    if (r.user_id > 0)
                    {
                        try
                        {
                            var userResult = await _supabaseService.Client
                                .From<User>()
                                .Select("full_name, avatar_img")
                                .Where(x => x.user_id == r.user_id)
                                .Get();
                                
                            if (userResult.Models.Any())
                            {
                                var user = userResult.Models.First();
                                userName = user.full_name ?? "User";
                                userAvatarUrl = user.avatar_img ?? "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png";
                            }

                            // Check if current user is following this user
                            var sessionEmail = HttpContext.Session.GetString("user_email");
                            if (!string.IsNullOrEmpty(sessionEmail))
                            {
                                try
                                {
                                    var currentUser = await _supabaseService.Client
                                        .From<User>()
                                        .Select("user_id")
                                        .Where(x => x.email == sessionEmail)
                                        .Single();

                                    if (currentUser != null)
                                    {
                                        var followCheck = await _supabaseService.Client
                                            .From<Follow>()
                                            .Where(x => x.follower_id == currentUser.user_id && x.following_id == r.user_id)
                                            .Get();

                                        isFollowing = followCheck.Models.Any();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to check follow status for user {UserId}", r.user_id);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to load user info for recipe {RecipeId}", recipeId);
                        }
                    }
                    
                    // Load like count
                    int likesCount = 0;
                    try
                    {
                        var likesResult = await _supabaseService.Client
                            .From<likeDislike>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        likesCount = likesResult.Models?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load likes for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load comment count
                    int commentsCount = 0;
                    try
                    {
                        var commentsResult = await _supabaseService.Client
                            .From<Comment>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        commentsCount = commentsResult.Models?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load comments for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load share count
                    int sharesCount = 0;
                    try
                    {
                        var sharesResult = await _supabaseService.Client
                            .From<Share>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        sharesCount = sharesResult.Models?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load shares for recipe {RecipeId}", recipeId);
                    }
                    
                    var item = new NewfeedViewModel
                    {
                        RecipeId = recipeId,
                        RecipeName = r.name ?? "Chưa có tên",
                        Description = r.description ?? "",
                        ThumbnailImg = r.thumbnail_img ?? "",
                        CreatedAt = r.created_at,
                        Level = r.level ?? "dễ",
                        UserId = userId,
                        UserName = userName,
                        UserAvatarUrl = userAvatarUrl,
                        LikesCount = likesCount,
                        CommentsCount = commentsCount,
                        SharesCount = sharesCount,
                        Tags = tags,
                        IsFollowing = isFollowing,
                        IsOwnPost = currentUserId.HasValue && userId == currentUserId.Value
                    };
                    
                    newsfeedItems.Add(item);
                }
                
                _logger.LogInformation("Mapped {Count} items to NewfeedViewModel", newsfeedItems.Count);
                
                return View(newsfeedItems);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading newsfeed");
                TempData["Error"] = "Không thể tải bảng tin: " + ex.Message;
                return View(new List<NewfeedViewModel>());
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        // AddRecipe đã được move sang RecipeController

        // Redirect /Home/MyRecipes to /User/Notebook
        public IActionResult MyRecipes()
        {
            return RedirectToAction("Notebook", "User");
        }

        // Test method để kiểm tra LoadMoreRecipes trực tiếp
        [HttpGet]
        public async Task<IActionResult> TestLoadMoreDirect(int page = 1, int pageSize = 4)
        {
            try
            {
                Console.WriteLine($"=== TEST LOAD MORE DIRECT - Page {page} ===");
                
                // Gọi LoadMoreRecipes trực tiếp
                var result = await LoadMoreRecipes(page, pageSize);
                
                // Parse JSON result
                if (result is JsonResult jsonResult)
                {
                    var data = jsonResult.Value;
                    Console.WriteLine($"LoadMoreRecipes returned: {System.Text.Json.JsonSerializer.Serialize(data)}");
                    
                    return Json(new {
                        success = true,
                        message = "LoadMoreRecipes called successfully",
                        originalResult = data,
                        page = page,
                        pageSize = pageSize
                    });
                }
                
                return Json(new {
                    success = false,
                    message = "LoadMoreRecipes did not return JsonResult"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestLoadMoreDirect: {ex.Message}");
                return Json(new {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // Test method để kiểm tra vấn đề counts
        [HttpGet]
        public async Task<IActionResult> TestCountsIssue()
        {
            try
            {
                Console.WriteLine("=== TEST COUNTS ISSUE ===");
                
                // Test Page 1
                Console.WriteLine("Testing Page 1...");
                var page1Result = await LoadMoreRecipes(1, 4);
                var page1Data = (page1Result as JsonResult)?.Value;
                Console.WriteLine($"Page 1 result: {System.Text.Json.JsonSerializer.Serialize(page1Data)}");
                
                // Test Page 2
                Console.WriteLine("Testing Page 2...");
                var page2Result = await LoadMoreRecipes(2, 4);
                var page2Data = (page2Result as JsonResult)?.Value;
                Console.WriteLine($"Page 2 result: {System.Text.Json.JsonSerializer.Serialize(page2Data)}");
                
                return Json(new {
                    success = true,
                    message = "Counts test completed - check console logs",
                    page1 = page1Data,
                    page2 = page2Data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestCountsIssue: {ex.Message}");
                return Json(new {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // Test method để kiểm tra vấn đề counts trong LoadMoreRecipes
        [HttpGet]
        public async Task<IActionResult> TestLoadMoreCounts(int page = 1, int pageSize = 4)
        {
            try
            {
                Console.WriteLine($"=== TEST LOAD MORE COUNTS - Page {page} ===");
                
                // Lấy recipes với pagination
                var offset = (page - 1) * pageSize;
                var recipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Select("*")
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var recipes = recipesResult.Models;
                Console.WriteLine($"Found {recipes.Count} recipes from DB");

                var results = new List<object>();

                foreach (var r in recipes)
                {
                    var recipeId = r.recipe_id ?? 0;
                    Console.WriteLine($"\n--- Processing Recipe {recipeId}: {r.name} ---");

                    // Load like count
                    var likesCount = 0;
                    try
                    {
                        var likes = await _supabaseService.Client
                            .From<likeDislike>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        likesCount = likes.Models?.Count ?? 0;
                        Console.WriteLine($"Likes: {likesCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load likes: {ex.Message}");
                    }

                    // Load comment count
                    var commentsCount = 0;
                    try
                    {
                        var comments = await _supabaseService.Client
                            .From<Comment>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        commentsCount = comments.Models?.Count ?? 0;
                        Console.WriteLine($"Comments: {commentsCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load comments: {ex.Message}");
                    }

                    // Load share count
                    var sharesCount = 0;
                    try
                    {
                        var shares = await _supabaseService.Client
                            .From<Share>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        sharesCount = shares.Models?.Count ?? 0;
                        Console.WriteLine($"Shares: {sharesCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load shares: {ex.Message}");
                    }

                    results.Add(new {
                        recipeId = recipeId,
                        recipeName = r.name,
                        likesCount = likesCount,
                        commentsCount = commentsCount,
                        sharesCount = sharesCount
                    });

                    Console.WriteLine($"Final counts - Likes: {likesCount}, Comments: {commentsCount}, Shares: {sharesCount}");
                }

                Console.WriteLine($"\n=== RETURNING {results.Count} RECIPES ===");

                return Json(new { 
                    success = true, 
                    page = page,
                    pageSize = pageSize,
                    totalRecipes = results.Count,
                    recipes = results
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestLoadMoreCounts: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { 
                    success = false, 
                    message = "Error: " + ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Test method để kiểm tra LoadMoreRecipes gốc
        [HttpGet]
        public async Task<IActionResult> TestOriginalLoadMore(int page = 1, int pageSize = 4)
        {
            try
            {
                Console.WriteLine($"=== TEST ORIGINAL LOAD MORE - Page {page} ===");
                
                // Gọi LoadMoreRecipes gốc
                var result = await LoadMoreRecipes(page, pageSize);
                
                if (result is JsonResult jsonResult)
                {
                    var data = jsonResult.Value;
                    Console.WriteLine($"LoadMoreRecipes returned: {System.Text.Json.JsonSerializer.Serialize(data)}");
                    
                    return Json(new {
                        success = true,
                        message = "LoadMoreRecipes called successfully",
                        originalResult = data,
                        page = page,
                        pageSize = pageSize
                    });
                }
                
                return Json(new {
                    success = false,
                    message = "LoadMoreRecipes did not return JsonResult"
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestOriginalLoadMore: {ex.Message}");
                return Json(new {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // Test method để kiểm tra vấn đề cụ thể
        [HttpGet]
        public async Task<IActionResult> TestNewsfeedIssue()
        {
            try
            {
                Console.WriteLine("=== TEST NEWSFEED ISSUE ===");
                
                // Test một recipe cụ thể
                var recipeId = 1;
                Console.WriteLine($"Testing recipe {recipeId}...");
                
                // Test like count
                var likesResult = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                var likesCount = likesResult.Models?.Count ?? 0;
                Console.WriteLine($"Recipe {recipeId} likes: {likesCount}");
                
                // Test comment count
                var commentsResult = await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                var commentsCount = commentsResult.Models?.Count ?? 0;
                Console.WriteLine($"Recipe {recipeId} comments: {commentsCount}");
                
                // Test share count
                var sharesResult = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                var sharesCount = sharesResult.Models?.Count ?? 0;
                Console.WriteLine($"Recipe {recipeId} shares: {sharesCount}");
                
                // Test LoadMoreRecipes cho Page 1
                Console.WriteLine("Testing LoadMoreRecipes Page 1...");
                var page1Result = await LoadMoreRecipes(1, 4);
                var page1Data = (page1Result as JsonResult)?.Value;
                
                // Extract counts từ Page 1
                if (page1Data != null)
                {
                    var page1Json = System.Text.Json.JsonSerializer.Serialize(page1Data);
                    Console.WriteLine($"Page 1 full result: {page1Json}");
                    
                    // Try to parse recipes array
                    var jsonDoc = System.Text.Json.JsonDocument.Parse(page1Json);
                    if (jsonDoc.RootElement.TryGetProperty("recipes", out var recipesArray))
                    {
                        Console.WriteLine($"Page 1 has {recipesArray.GetArrayLength()} recipes");
                        for (int i = 0; i < recipesArray.GetArrayLength(); i++)
                        {
                            var recipe = recipesArray[i];
                            var recipeIdFromPage = recipe.TryGetProperty("recipeId", out var id) ? id.GetInt32() : 0;
                            var likesFromPage = recipe.TryGetProperty("likesCount", out var likes) ? likes.GetInt32() : 0;
                            var commentsFromPage = recipe.TryGetProperty("commentsCount", out var comments) ? comments.GetInt32() : 0;
                            var sharesFromPage = recipe.TryGetProperty("sharesCount", out var shares) ? shares.GetInt32() : 0;
                            
                            Console.WriteLine($"Recipe {recipeIdFromPage}: Likes={likesFromPage}, Comments={commentsFromPage}, Shares={sharesFromPage}");
                        }
                    }
                }
                
                return Json(new {
                    success = true,
                    message = "Newsfeed issue test completed - check console logs",
                    recipeId = recipeId,
                    directLikes = likesCount,
                    directComments = commentsCount,
                    directShares = sharesCount,
                    page1Result = page1Data
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestNewsfeedIssue: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new {
                    success = false,
                    message = ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Simple test method để kiểm tra counts
        [HttpGet]
        public async Task<IActionResult> TestSimpleCounts(int recipeId = 1)
        {
            try
            {
                Console.WriteLine($"=== TEST SIMPLE COUNTS FOR RECIPE {recipeId} ===");

                // Test like count
                var likesResult = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                var likesCount = likesResult.Models?.Count ?? 0;
                Console.WriteLine($"Likes for recipe {recipeId}: {likesCount}");

                // Test comment count
                var commentsResult = await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                var commentsCount = commentsResult.Models?.Count ?? 0;
                Console.WriteLine($"Comments for recipe {recipeId}: {commentsCount}");

                // Test share count
                var sharesResult = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                var sharesCount = sharesResult.Models?.Count ?? 0;
                Console.WriteLine($"Shares for recipe {recipeId}: {sharesCount}");

                return Json(new {
                    success = true,
                    recipeId = recipeId,
                    likesCount = likesCount,
                    commentsCount = commentsCount,
                    sharesCount = sharesCount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestSimpleCounts: {ex.Message}");
                return Json(new {
                    success = false,
                    message = ex.Message
                });
            }
        }

        // Test method để debug LoadMoreRecipes
        [HttpGet]
        public async Task<IActionResult> TestLoadMoreRecipes(int page = 1, int pageSize = 4)
        {
            try
            {
                Console.WriteLine($"=== TEST LOAD MORE RECIPES ===");
                Console.WriteLine($"Page: {page}, PageSize: {pageSize}");

                // Get current user ID for comparison
                var currentSessionEmail = HttpContext.Session.GetString("user_email");
                int? currentUserId = null;
                if (!string.IsNullOrEmpty(currentSessionEmail))
                {
                    try
                    {
                        var currentUser = await _supabaseService.Client
                            .From<User>()
                            .Select("user_id")
                            .Where(x => x.email == currentSessionEmail)
                            .Single();
                        currentUserId = currentUser?.user_id;
                        Console.WriteLine($"Current User ID: {currentUserId}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to get current user ID: {ex.Message}");
                    }
                }

                // Lấy recipes với pagination
                var offset = (page - 1) * pageSize;
                var recipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Select("*")
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var recipes = recipesResult.Models;
                Console.WriteLine($"Found {recipes.Count} recipes from DB");

                // Map sang NewfeedViewModel
                var newsfeedItems = new List<NewfeedViewModel>();

                foreach (var r in recipes)
                {
                    var recipeId = r.recipe_id ?? 0;
                    Console.WriteLine($"\n--- Processing Recipe {recipeId}: {r.name} ---");

                    // Load tags từ Ingredient và RecipeType
                    var tags = new List<string>();

                    // Thêm level làm tag đầu tiên
                    if (!string.IsNullOrEmpty(r.level))
                    {
                        tags.Add($"{r.level}");
                    }

                    // Load ingredients
                    try
                    {
                        var recipeIngredients = await _supabaseService.Client
                            .From<RecipeIngredient>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        foreach (var ri in recipeIngredients.Models ?? new List<RecipeIngredient>())
                        {
                            var ingredient = await _supabaseService.Client
                                .From<IngredientMaster>()
                                .Where(x => x.ingredient_id == ri.ingredient_id)
                                .Single();
                            if (!string.IsNullOrEmpty(ingredient.name))
                                tags.Add($"{ingredient.name}");
                        }
                        Console.WriteLine($"Loaded {recipeIngredients.Models?.Count ?? 0} ingredients");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load ingredients: {ex.Message}");
                    }

                    // Load recipe types
                    try
                    {
                        var recipeTypes = await _supabaseService.Client
                            .From<RecipeRecipeType>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        foreach (var rt in recipeTypes.Models ?? new List<RecipeRecipeType>())
                        {
                            var type = await _supabaseService.Client
                                .From<RecipeType>()
                                .Where(x => x.recipe_type_id == rt.recipe_type_id)
                                .Single();
                            if (type != null && !string.IsNullOrEmpty(type.content))
                                tags.Add($"#{type.content}");
                        }
                        Console.WriteLine($"Loaded {recipeTypes.Models?.Count ?? 0} recipe types");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load recipe types: {ex.Message}");
                    }

                    // Load user info
                    var userId = r.user_id;
                    var userName = "Unknown User";
                    var userAvatarUrl = "";
                    try
                    {
                        var user = await _supabaseService.Client
                            .From<User>()
                            .Where(x => x.user_id == userId)
                            .Single();
                        userName = user?.full_name ?? "Unknown User";
                        userAvatarUrl = user?.avatar_img ?? "";
                        Console.WriteLine($"User: {userName} (ID: {userId})");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load user info: {ex.Message}");
                    }

                    // Load like count
                    var likesCount = 0;
                    try
                    {
                        var likes = await _supabaseService.Client
                            .From<likeDislike>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        likesCount = likes.Models?.Count ?? 0;
                        Console.WriteLine($"Likes: {likesCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load likes: {ex.Message}");
                    }

                    // Load comment count
                    var commentsCount = 0;
                    try
                    {
                        var comments = await _supabaseService.Client
                            .From<Comment>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        commentsCount = comments.Models?.Count ?? 0;
                        Console.WriteLine($"Comments: {commentsCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load comments: {ex.Message}");
                    }

                    // Load share count
                    var sharesCount = 0;
                    try
                    {
                        var shares = await _supabaseService.Client
                            .From<Share>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        sharesCount = shares.Models?.Count ?? 0;
                        Console.WriteLine($"Shares: {sharesCount}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to load shares: {ex.Message}");
                    }

                    // Check follow status
                    var isFollowing = false;
                    if (currentUserId.HasValue && userId != currentUserId.Value)
                    {
                        try
                        {
                            var follow = await _supabaseService.Client
                                .From<Follow>()
                                .Where(x => x.follower_id == currentUserId.Value && x.following_id == userId)
                                .Single();
                            isFollowing = follow != null;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to check follow status: {ex.Message}");
                        }
                    }

                    newsfeedItems.Add(new NewfeedViewModel
                    {
                        RecipeId = recipeId,
                        RecipeName = r.name ?? "Unknown Recipe",
                        Description = r.description ?? "",
                        ThumbnailImg = r.thumbnail_img ?? "",
                        Level = r.level ?? "Dễ",
                        Tags = tags,
                        UserId = userId,
                        UserAvatarUrl = userAvatarUrl,
                        UserName = userName,
                        LikesCount = likesCount,
                        CommentsCount = commentsCount,
                        SharesCount = sharesCount,
                        IsFollowing = isFollowing,
                        IsOwnPost = currentUserId.HasValue && userId == currentUserId.Value
                    });

                    Console.WriteLine($"Final counts - Likes: {likesCount}, Comments: {commentsCount}, Shares: {sharesCount}");
                }

                Console.WriteLine($"\n=== RETURNING {newsfeedItems.Count} RECIPES ===");
                foreach (var item in newsfeedItems)
                {
                    Console.WriteLine($"Recipe {item.RecipeId}: Likes={item.LikesCount}, Comments={item.CommentsCount}, Shares={item.SharesCount}");
                }

                return Json(new { 
                    success = true, 
                    recipes = newsfeedItems,
                    hasMore = recipes.Count == pageSize,
                    debug = new {
                        page = page,
                        pageSize = pageSize,
                        totalRecipes = newsfeedItems.Count,
                        currentUserId = currentUserId
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in TestLoadMoreRecipes: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { 
                    success = false, 
                    message = "Error: " + ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // API endpoint for infinite scroll
        public async Task<IActionResult> LoadMoreRecipes(int page = 1, int pageSize = 4)
        {
            try
            {
                // Get current user ID for comparison
                var currentSessionEmail = HttpContext.Session.GetString("user_email");
                int? currentUserId = null;
                if (!string.IsNullOrEmpty(currentSessionEmail))
                {
                    try
                    {
                        var currentUser = await _supabaseService.Client
                            .From<User>()
                            .Select("user_id")
                            .Where(x => x.email == currentSessionEmail)
                            .Single();
                        currentUserId = currentUser?.user_id;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get current user ID");
                    }
                }
                
                // Lấy recipes với pagination
                var offset = (page - 1) * pageSize;
                var recipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Select("*")
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();
                
                var recipes = recipesResult.Models;
                
                // Map sang NewfeedViewModel
                var newsfeedItems = new List<NewfeedViewModel>();
                
                foreach (var r in recipes)
                {
                    var recipeId = r.recipe_id ?? 0;
                    
                    // Load tags từ Ingredient và RecipeType
                    var tags = new List<string>();
                    
                    // Thêm level làm tag đầu tiên
                    if (!string.IsNullOrEmpty(r.level))
                    {
                        tags.Add($"{r.level}");
                    }
                    
                    // Load ingredients từ Recipe_Ingredient và Ingredient_Master
                    try
                    {
                        // Get ingredient IDs from Recipe_Ingredient
                        var recipeIngredients = await _supabaseService.Client
                            .From<RecipeIngredient>()
                            .Select("ingredient_id")
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                            
                        foreach (var ri in recipeIngredients.Models)
                        {
                            // Get ingredient name from Ingredient_Master
                            var ingredientResult = await _supabaseService.Client
                                .From<IngredientMaster>()
                                .Select("name")
                                .Where(x => x.ingredient_id == ri.ingredient_id)
                                .Single();
                                
                            if (ingredientResult != null && !string.IsNullOrEmpty(ingredientResult.name))
                            {
                                tags.Add($"{ingredientResult.name.Trim()}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load ingredients for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load recipe types
                    try
                    {
                        var recipeTypes = await _supabaseService.Client
                            .From<RecipeRecipeType>()
                            .Select("recipe_type_id")
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                            
                        foreach (var rt in recipeTypes.Models)
                        {
                            var typeResult = await _supabaseService.Client
                                .From<RecipeType>()
                                .Select("content")
                                .Where(x => x.recipe_type_id == rt.recipe_type_id)
                                .Single();
                                
                            if (typeResult != null && !string.IsNullOrEmpty(typeResult.content))
                            {
                                tags.Add($"#{typeResult.content.Trim()}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load recipe types for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load user info
                    User? user = null;
                    bool isFollowing = false;
                    try
                    {
                        if (r.user_id > 0)
                        {
                            var userResult = await _supabaseService.Client
                                .From<User>()
                                .Select("full_name, avatar_img")
                                .Where(x => x.user_id == r.user_id)
                                .Single();
                            user = userResult;

                            // Check if current user is following this user
                            var sessionEmail = HttpContext.Session.GetString("user_email");
                            if (!string.IsNullOrEmpty(sessionEmail))
                            {
                                try
                                {
                                    var currentUser = await _supabaseService.Client
                                        .From<User>()
                                        .Select("user_id")
                                        .Where(x => x.email == sessionEmail)
                                        .Single();

                                    if (currentUser != null)
                                    {
                                        var followCheck = await _supabaseService.Client
                                            .From<Follow>()
                                            .Where(x => x.follower_id == currentUser.user_id && x.following_id == r.user_id)
                                            .Get();

                                        isFollowing = followCheck.Models.Any();
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to check follow status for user {UserId}", r.user_id);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load user info for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load like count
                    int likesCount = 0;
                    try
                    {
                        var likesResult = await _supabaseService.Client
                            .From<likeDislike>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        likesCount = likesResult.Models?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load likes for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load comment count
                    int commentsCount = 0;
                    try
                    {
                        var commentsResult = await _supabaseService.Client
                            .From<Comment>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        commentsCount = commentsResult.Models?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load comments for recipe {RecipeId}", recipeId);
                    }
                    
                    // Load share count
                    int sharesCount = 0;
                    try
                    {
                        var sharesResult = await _supabaseService.Client
                            .From<Share>()
                            .Where(x => x.recipe_id == recipeId)
                            .Get();
                        sharesCount = sharesResult.Models?.Count ?? 0;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load shares for recipe {RecipeId}", recipeId);
                    }
                    
                    newsfeedItems.Add(new NewfeedViewModel
                    {
                        RecipeId = recipeId,
                        RecipeName = r.name ?? "Unknown Recipe",
                        Description = r.description ?? "",
                        ThumbnailImg = r.thumbnail_img ?? "",
                        Level = r.level ?? "Dễ",
                        Tags = tags,
                        UserId = r.user_id,
                        UserAvatarUrl = user?.avatar_img ?? "",
                        UserName = user?.full_name ?? "Unknown User",
                        LikesCount = likesCount,
                        CommentsCount = commentsCount,
                        SharesCount = sharesCount,
                        IsFollowing = isFollowing,
                        IsOwnPost = currentUserId.HasValue && r.user_id == currentUserId.Value
                    });
                }
                
                _logger.LogInformation("LoadMoreRecipes: Returning {Count} recipes with counts", newsfeedItems.Count);
                foreach (var item in newsfeedItems)
                {
                    _logger.LogInformation("Recipe {RecipeId}: Likes={Likes}, Comments={Comments}, Shares={Shares}", 
                        item.RecipeId, item.LikesCount, item.CommentsCount, item.SharesCount);
                }
                
                return Json(new { 
                    success = true, 
                    recipes = newsfeedItems,
                    hasMore = recipes.Count == pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more recipes");
                return Json(new { 
                    success = false, 
                    message = "Không thể tải thêm công thức: " + ex.Message 
                });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleFollow(int userId, bool isFollowing)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này" });
                }

                // Get current user
                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Select("user_id")
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                if (currentUser.user_id == userId)
                {
                    return Json(new { success = false, message = "Bạn không thể theo dõi chính mình" });
                }

                if (isFollowing)
                {
                    // Follow user
                    var follow = new Follow
                    {
                        follower_id = currentUser.user_id.Value,
                        following_id = userId
                    };

                    await _supabaseService.Client
                        .From<Follow>()
                        .Insert(follow);
                }
                else
                {
                    // Unfollow user
                    await _supabaseService.Client
                        .From<Follow>()
                        .Where(x => x.follower_id == currentUser.user_id && x.following_id == userId)
                        .Delete();
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling follow status");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
