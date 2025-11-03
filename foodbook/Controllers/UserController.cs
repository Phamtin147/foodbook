using System.Diagnostics;
using foodbook.Models;
using Microsoft.AspNetCore.Mvc;
using foodbook.Attributes;
using foodbook.Services;

namespace foodbook.Controllers
{
    [LoginRequired]
    public class UserController : Controller
    {
        private readonly ILogger<UserController> _logger;
        private readonly SupabaseService _supabaseService;

        public UserController(ILogger<UserController> logger, SupabaseService supabaseService)
        {
            _logger = logger;
            _supabaseService = supabaseService;
        }

        // User's Notebook (Sổ tay)
        public async Task<IActionResult> Notebook()
        {
            try
            {
                // Get current user ID from session
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return RedirectToAction("Login", "Account");
                }

                // Get user's saved recipes from Notebook table
                var notebookRecipes = await _supabaseService.Client
                    .From<Notebook>()
                    .Select("recipe_id")
                    .Where(x => x.user_id == userId.Value)
                    .Get();

                var recipeIds = notebookRecipes.Models.Select(x => x.recipe_id).ToList();

                if (recipeIds.Any())
                {
                    // Get full recipe details (chỉ lấy recipes có status = 'active')
                    var recipes = await _supabaseService.Client
                        .From<Recipe>()
                        .Select("*")
                        .Where(x => x.status == "active")
                        .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                        .Get();
                    
                    // Filter recipes by recipeIds
                    var filteredRecipes = recipes.Models.Where(r => recipeIds.Contains(r.recipe_id ?? 0)).ToList();

                    // Get user info for each recipe
                    var notebookItems = new List<NotebookViewModel>();
                    foreach (var recipe in filteredRecipes)
                    {
                        var user = await _supabaseService.Client
                            .From<User>()
                            .Select("full_name, avatar_img")
                            .Where(x => x.user_id == recipe.user_id)
                            .Get();

                        var userInfo = user.Models.FirstOrDefault();
                        
                        // Get likes count
                        var likesCount = await _supabaseService.Client
                            .From<likeDislike>()
                            .Select("ld_id")
                            .Where(x => x.recipe_id == recipe.recipe_id)
                            .Get();

                        // Get comments count
                        var commentsCount = await _supabaseService.Client
                            .From<Comment>()
                            .Select("comment_id")
                            .Where(x => x.recipe_id == recipe.recipe_id)
                            .Get();

                        // Get shares count
                        var sharesCount = await _supabaseService.Client
                            .From<Share>()
                            .Where(x => x.recipe_id == recipe.recipe_id)
                            .Get();

                        notebookItems.Add(new NotebookViewModel
                        {
                            RecipeId = recipe.recipe_id ?? 0,
                            RecipeName = recipe.name ?? "Chưa có tên",
                            ThumbnailImg = recipe.thumbnail_img ?? "",
                            Level = recipe.level ?? "dễ",
                            UserId = recipe.user_id,
                            UserName = userInfo?.full_name ?? "User",
                            UserAvatarUrl = userInfo?.avatar_img ?? "https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png",
                            LikesCount = likesCount.Models.Count,
                            CommentsCount = commentsCount.Models.Count,
                            SharesCount = sharesCount.Models.Count,
                            CreatedAt = recipe.created_at
                        });
                    }

                    return View(notebookItems);
                }

                return View(new List<NotebookViewModel>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading notebook");
                TempData["Error"] = "Không thể tải sổ tay: " + ex.Message;
                return View(new List<NotebookViewModel>());
            }
        }

        // Add recipe to notebook
        [HttpPost]
        public async Task<IActionResult> AddToNotebook(int recipeId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });
                }

                // Check if already in notebook
                var existing = await _supabaseService.Client
                    .From<Notebook>()
                    .Select("user_id, recipe_id")
                    .Where(x => x.user_id == userId.Value && x.recipe_id == recipeId)
                    .Get();

                if (existing.Models.Any())
                {
                    return Json(new { success = false, message = "Công thức đã có trong sổ tay" });
                }

                // Add to notebook
                var notebookItem = new Notebook
                {
                    user_id = userId.Value,
                    recipe_id = recipeId
                };

                await _supabaseService.Client
                    .From<Notebook>()
                    .Insert(notebookItem);

                return Json(new { success = true, message = "Đã thêm vào sổ tay" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding to notebook");
                return Json(new { success = false, message = "Không thể thêm vào sổ tay: " + ex.Message });
            }
        }

        // Remove recipe from notebook
        [HttpPost]
        public async Task<IActionResult> RemoveFromNotebook(int recipeId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });
                }

                await _supabaseService.Client
                    .From<Notebook>()
                    .Where(x => x.user_id == userId.Value && x.recipe_id == recipeId)
                    .Delete();

                return Json(new { success = true, message = "Đã xóa khỏi sổ tay" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing from notebook");
                return Json(new { success = false, message = "Không thể xóa khỏi sổ tay: " + ex.Message });
            }
        }

        // Check if recipe is in notebook
        [HttpGet]
        public async Task<IActionResult> IsInNotebook(int recipeId)
        {
            try
            {
                var userId = HttpContext.Session.GetInt32("UserId");
                if (userId == null)
                {
                    return Json(new { inNotebook = false });
                }

                var existing = await _supabaseService.Client
                    .From<Notebook>()
                    .Select("user_id, recipe_id")
                    .Where(x => x.user_id == userId.Value && x.recipe_id == recipeId)
                    .Get();

                return Json(new { inNotebook = existing.Models.Any() });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking notebook status");
                return Json(new { inNotebook = false });
            }
        }
    }
}
