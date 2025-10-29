using foodbook.Models;
using foodbook.Services;
using Microsoft.AspNetCore.Mvc;
using static Supabase.Postgrest.Constants;

namespace foodbook.Controllers
{
    public class ProfileController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly StorageService _storageService;

        public ProfileController(SupabaseService supabaseService, StorageService storageService)
        {
            _supabaseService = supabaseService;
            _storageService = storageService;
        }

        // GET: /Profile/Info hoặc /Profile/Info?id=123
        public async Task<IActionResult> Info(int? id)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");

                // Nếu không có ID → Xem profile của chính mình
                if (!id.HasValue)
                {
                    if (string.IsNullOrEmpty(sessionEmail))
                    {
                        TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem hồ sơ.";
                        return RedirectToAction("Login", "Auth");
                    }

                    // Lấy user theo email trực tiếp từ bảng User
                    var currentUserResp = await _supabaseService.Client
                        .From<User>()
                        .Filter("email", Operator.Equals, sessionEmail)
                        .Single();

                    if (currentUserResp == null)
                    {
                        return View("~/Views/Account/PersonalInfo.cshtml", new ProfileViewModel());
                    }

                    id = currentUserResp.user_id;
                }

                // Lấy user theo user_id
                var userResp = await _supabaseService.Client
                    .From<User>()
                    .Filter("user_id", Operator.Equals, id.Value)
                    .Single();

                if (userResp == null)
                    return View("~/Views/Account/PersonalInfo.cshtml", new ProfileViewModel());

                // Lấy tất cả active recipes của user để đếm (chỉ lấy recipes có status = 'active')
                var allRecipesResp = await _supabaseService.Client
                    .From<Recipe>()
                    .Filter("user_id", Operator.Equals, userResp.user_id)
                    .Filter("status", Operator.Equals, "active")
                    .Get();

                var allRecipeModels = allRecipesResp.Models ?? new List<Recipe>();
                
                // Lấy 8 bài đầu tiên để hiển thị (chỉ lấy recipes có status = 'active')
                var recipesResp = await _supabaseService.Client
                    .From<Recipe>()
                    .Filter("user_id", Operator.Equals, userResp.user_id)
                    .Filter("status", Operator.Equals, "active")
                    .Order("created_at", Ordering.Descending)
                    .Range(0, 7) // Lấy 8 bài đầu tiên (0-7)
                    .Get();

                var recipeModels = recipesResp.Models ?? new List<Recipe>();

                // Followers / Following
                var followersResp = await _supabaseService.Client
                    .From<Follow>()
                    .Filter("following_id", Operator.Equals, userResp.user_id)
                    .Get();

                var followingResp = await _supabaseService.Client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, userResp.user_id)
                    .Get();

                int followersCount = followersResp?.Models?.Count ?? 0;
                int followingCount = followingResp?.Models?.Count ?? 0;

                // Kiểm tra xem user hiện tại có đang follow user này không
                bool isFollowing = false;
                if (!string.IsNullOrEmpty(sessionEmail))
                {
                    try
                    {
                        var currentUser = await _supabaseService.Client
                            .From<User>()
                            .Select("user_id")
                            .Where(x => x.email == sessionEmail)
                            .Single();

                        if (currentUser?.user_id.HasValue == true && currentUser.user_id != userResp.user_id)
                        {
                            var followCheck = await _supabaseService.Client
                                .From<Follow>()
                                .Where(x => x.follower_id == currentUser.user_id && x.following_id == userResp.user_id)
                                .Single();
                            isFollowing = followCheck != null;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Nếu không tìm thấy follow record, isFollowing = false
                        isFollowing = false;
                    }
                }

                // Map sang RecipeViewModel
                var recipeVMs = new List<RecipeViewModel>();
                foreach (var r in recipeModels)
                {
                    var likesResp = await _supabaseService.Client
                        .From<likeDislike>()
                        .Filter("recipe_id", Operator.Equals, r.recipe_id)
                        .Get();

                    int likes = likesResp?.Models?.Count ?? 0;

                    recipeVMs.Add(new RecipeViewModel
                    {
                        RecipeId = r.recipe_id ?? 0,
                        Name = r.name,
                        ThumbnailImg = r.thumbnail_img,
                        CookTime = r.cook_time ?? 0,
                        Likes = likes,
                        CreatedAt = r.created_at,
                        Level = r.level ?? "dễ"
                    });
                }

                var viewModel = new ProfileViewModel
                {
                    User = userResp,
                    Recipes = recipeVMs,
                    RecipeCount = allRecipeModels.Count, // Đếm tất cả recipes
                    FollowersCount = followersCount,
                    FollowingCount = followingCount,
                    IsFollowing = isFollowing
                };

                return View("~/Views/Account/PersonalInfo.cshtml", viewModel);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tải hồ sơ: " + ex.Message;
                return View("~/Views/Account/PersonalInfo.cshtml", new ProfileViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var sessionEmail = HttpContext.Session.GetString("user_email");
            if (string.IsNullOrEmpty(sessionEmail))
                return RedirectToAction("Login", "Auth");

            var user = await _supabaseService.Client
                .From<User>()
                .Filter("user_id", Operator.Equals, id)
                .Single();

            if (user == null)
                return NotFound();

            if (!string.Equals(user.email, sessionEmail, StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = "Bạn không có quyền chỉnh sửa hồ sơ này.";
                return RedirectToAction("Info", new { id = id });
            }

            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(User model, IFormFile? avatar)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                    return RedirectToAction("Login", "Auth");

                if (!string.Equals(model.email, sessionEmail, StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "Bạn không có quyền cập nhật hồ sơ này.";
                    return RedirectToAction("Info", new { id = model.user_id });
                }

                // Lấy user hiện tại để tránh ghi đè các trường nhạy cảm (password, email, username)
                var existingUser = await _supabaseService.Client
                    .From<User>()
                    .Filter("user_id", Operator.Equals, model.user_id)
                    .Single();

                if (existingUser == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                    return RedirectToAction("Info", new { id = model.user_id });
                }

                // Upload avatar nếu có
                string? avatarUrl = existingUser.avatar_img;
                if (avatar != null && avatar.Length > 0)
                {
                    // Chỉ cho phép ảnh, lưu vào bucket 'img'
                    avatarUrl = await _storageService.UploadFileAsync(avatar, isVideo: false, folderPath: "avatars");
                }

                // Cập nhật chỉ các trường cho phép
                var updateResult = await _supabaseService.Client
                    .From<User>()
                    .Filter("user_id", Operator.Equals, model.user_id)
                    .Set(x => x.full_name, model.full_name)
                    .Set(x => x.bio, model.bio)
                    .Set(x => x.avatar_img, avatarUrl)
                    .Update();

                // Cập nhật lại session avatar để navbar thay đổi ngay
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    HttpContext.Session.SetString("avatar_img", avatarUrl);
                }

                TempData["SuccessMessage"] = "Cập nhật hồ sơ thành công";
                return RedirectToAction("Info", new { id = model.user_id });
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Cập nhật thất bại: " + ex.Message;
                return View(model);
            }
        }

        // API endpoint for infinite scroll - Load more profile posts
        public async Task<IActionResult> LoadMoreProfilePosts(int? userId = null, int page = 1, int pageSize = 8)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập" });
                }

                // Determine which user's posts to load
                int targetUserId;
                if (userId.HasValue)
                {
                    // Load posts for specific user (when viewing someone else's profile)
                    targetUserId = userId.Value;
                }
                else
                {
                    // Load posts for current user (when viewing own profile)
                    var currentUser = await _supabaseService.Client
                        .From<User>()
                        .Filter("email", Operator.Equals, sessionEmail)
                        .Single();

                    if (currentUser == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy người dùng" });
                    }

                    targetUserId = currentUser.user_id.Value;
                }

                // Get recipes with pagination
                var offset = (page - 1) * pageSize;
                var recipesResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Filter("user_id", Operator.Equals, targetUserId)
                    .Order("created_at", Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var recipes = recipesResult.Models ?? new List<Recipe>();

                // Map to simple objects for JSON response
                var recipeData = recipes.Select(r => new
                {
                    recipeId = r.recipe_id ?? 0,
                    name = r.name ?? "Unknown Recipe",
                    thumbnailImg = r.thumbnail_img ?? "",
                    cookTime = r.cook_time ?? 0,
                    likes = 0, // TODO: Implement actual like count
                    description = r.description ?? ""
                }).ToList();

                return Json(new
                {
                    success = true,
                    recipes = recipeData,
                    hasMore = recipes.Count == pageSize
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi khi tải thêm bài đăng: " + ex.Message
                });
            }
        }

        // API endpoint for infinite scroll - Load more reacted posts
        public async Task<IActionResult> LoadMoreReactedPosts(int page = 1, int pageSize = 8)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập" });
                }

                // Get current user
                var user = await _supabaseService.Client
                    .From<User>()
                    .Filter("email", Operator.Equals, sessionEmail)
                    .Single();

                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng" });
                }

                // Get liked recipes with pagination
                var offset = (page - 1) * pageSize;
                var likesResult = await _supabaseService.Client
                    .From<likeDislike>()
                    .Filter("user_id", Operator.Equals, user.user_id)
                    .Filter("is_like", Operator.Equals, true)
                    .Order("created_at", Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var likes = likesResult.Models ?? new List<likeDislike>();

                if (!likes.Any())
                {
                    return Json(new
                    {
                        success = true,
                        recipes = new List<object>(),
                        hasMore = false
                    });
                }

                // Get recipe details for liked recipes
                var recipeIds = likes.Select(l => l.recipe_id).ToList();
                var recipes = new List<Recipe>();

                foreach (var recipeId in recipeIds)
                {
                    var recipe = await _supabaseService.Client
                        .From<Recipe>()
                        .Filter("recipe_id", Operator.Equals, recipeId)
                        .Single();
                    
                    if (recipe != null)
                    {
                        recipes.Add(recipe);
                    }
                }

                // Map to simple objects for JSON response
                var recipeData = recipes.Select(r => new
                {
                    recipeId = r.recipe_id ?? 0,
                    name = r.name ?? "Unknown Recipe",
                    thumbnailImg = r.thumbnail_img ?? "",
                    cookTime = r.cook_time ?? 0,
                    likes = 0, // TODO: Implement actual like count
                    description = r.description ?? ""
                }).ToList();

                return Json(new
                {
                    success = true,
                    recipes = recipeData,
                    hasMore = likes.Count == pageSize
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = "Lỗi khi tải thêm bài đã reaction: " + ex.Message
                });
            }
        }

        // GET: /Profile/Followers?id=123
        public async Task<IActionResult> Followers(int? id)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem danh sách theo dõi.";
                    return RedirectToAction("Login", "Account");
                }

                // Nếu không có ID → Xem followers của chính mình
                if (!id.HasValue)
                {
                    var currentUser = await _supabaseService.Client
                        .From<User>()
                        .Filter("email", Operator.Equals, sessionEmail)
                        .Single();

                    if (currentUser == null)
                    {
                        return View(new List<User>());
                    }

                    id = currentUser.user_id;
                }

                // Lấy danh sách followers
                var followersResult = await _supabaseService.Client
                    .From<Follow>()
                    .Filter("following_id", Operator.Equals, id.Value)
                    .Get();

                var followers = new List<User>();
                if (followersResult?.Models != null)
                {
                    foreach (var follow in followersResult.Models)
                    {
                        var user = await _supabaseService.Client
                            .From<User>()
                            .Filter("user_id", Operator.Equals, follow.follower_id)
                            .Single();
                        
                        if (user != null)
                        {
                            followers.Add(user);
                        }
                    }
                }

                return View(followers);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tải danh sách người theo dõi: " + ex.Message;
                return View(new List<User>());
            }
        }

        // GET: /Profile/Following?id=123
        public async Task<IActionResult> Following(int? id)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem danh sách theo dõi.";
                    return RedirectToAction("Login", "Account");
                }

                // Nếu không có ID → Xem following của chính mình
                if (!id.HasValue)
                {
                    var currentUser = await _supabaseService.Client
                        .From<User>()
                        .Filter("email", Operator.Equals, sessionEmail)
                        .Single();

                    if (currentUser == null)
                    {
                        return View(new List<User>());
                    }

                    id = currentUser.user_id;
                }

                // Lấy danh sách following
                var followingResult = await _supabaseService.Client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, id.Value)
                    .Get();

                var following = new List<User>();
                if (followingResult?.Models != null)
                {
                    foreach (var follow in followingResult.Models)
                    {
                        var user = await _supabaseService.Client
                            .From<User>()
                            .Filter("user_id", Operator.Equals, follow.following_id)
                            .Single();
                        
                        if (user != null)
                        {
                            following.Add(user);
                        }
                    }
                }

                return View(following);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tải danh sách đang theo dõi: " + ex.Message;
                return View(new List<User>());
            }
        }

        // GET: /Profile/Reaction
        public async Task<IActionResult> Reaction()
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    TempData["ErrorMessage"] = "Vui lòng đăng nhập để xem các bài đã reaction.";
                    return RedirectToAction("Login", "Account");
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy thông tin người dùng.";
                    return RedirectToAction("Login", "Account");
                }

                // Lấy danh sách recipes đã reaction (like, comment, share) với pagination
                var reactions = await GetReactedRecipesForUser(currentUser.user_id.Value, page: 1, pageSize: 10);

                return View(reactions);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tải danh sách bài đã reaction: " + ex.Message;
                return View(new List<NewfeedViewModel>());
            }
        }


        // POST: /Profile/LoadMoreReactions
        [HttpPost]
        public async Task<IActionResult> LoadMoreReactions(int page = 1, int pageSize = 10)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập." });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                }

                // Sử dụng helper method để lấy recipes đã reaction
                var reactions = await GetReactedRecipesForUser(currentUser.user_id.Value, page, pageSize);

                return Json(new { success = true, recipes = reactions });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi tải thêm bài: " + ex.Message });
            }
        }

        // Helper method để lấy recipes đã reaction của user
        private async Task<List<NewfeedViewModel>> GetReactedRecipesForUser(int userId, int page = 1, int pageSize = 10)
        {
            try
            {
                Console.WriteLine($"GetReactedRecipesForUser called: userId={userId}, page={page}, pageSize={pageSize}");
                
                // Lấy tất cả recipe IDs đã reaction (like, comment, share)
                var allRecipeIds = new HashSet<int>();

                // 1. Lấy recipes đã like
                var likedRecipes = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.user_id == userId)
                    .Get();
                
                Console.WriteLine($"Found {likedRecipes.Models?.Count ?? 0} likes for user {userId}");
                
                foreach (var like in likedRecipes.Models ?? new List<likeDislike>())
                {
                    allRecipeIds.Add(like.recipe_id);
                }

                // 2. Lấy recipes đã comment
                var commentedRecipes = await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.user_id == userId)
                    .Get();
                
                Console.WriteLine($"Found {commentedRecipes.Models?.Count ?? 0} comments for user {userId}");
                
                foreach (var comment in commentedRecipes.Models ?? new List<Comment>())
                {
                    allRecipeIds.Add(comment.recipe_id);
                }

                // 3. Lấy recipes đã share
                var sharedRecipes = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.user_id == userId)
                    .Get();
                
                Console.WriteLine($"Found {sharedRecipes.Models?.Count ?? 0} shares for user {userId}");
                
                foreach (var share in sharedRecipes.Models ?? new List<Share>())
                {
                    allRecipeIds.Add(share.recipe_id);
                }

                Console.WriteLine($"Total unique recipe IDs: {allRecipeIds.Count}");

                // Lấy thông tin chi tiết của recipes với pagination
                var reactions = new List<NewfeedViewModel>();
                var recipeIds = allRecipeIds.ToList();
                
                // Sắp xếp theo thời gian tạo mới nhất
                var recipesWithTime = new List<(int recipeId, DateTime createdAt)>();
                
                foreach (var recipeId in recipeIds)
                {
                    try
                    {
                        var recipe = await _supabaseService.Client
                            .From<Recipe>()
                            .Where(x => x.recipe_id == recipeId)
                            .Single();
                        
                        if (recipe != null)
                        {
                            recipesWithTime.Add((recipeId, recipe.created_at));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting recipe {recipeId}: {ex.Message}");
                    }
                }
                
                // Sắp xếp theo thời gian tạo mới nhất
                recipesWithTime = recipesWithTime.OrderByDescending(x => x.createdAt).ToList();
                
                // Phân trang
                var startIndex = (page - 1) * pageSize;
                var endIndex = Math.Min(startIndex + pageSize, recipesWithTime.Count);
                
                Console.WriteLine($"Processing recipes from index {startIndex} to {endIndex-1} out of {recipesWithTime.Count} total");
                
                for (int i = startIndex; i < endIndex; i++)
                {
                    var recipeId = recipesWithTime[i].recipeId;
                    Console.WriteLine($"Processing recipe ID: {recipeId}");
                    
                    // Lấy thông tin recipe
                    var recipe = await _supabaseService.Client
                        .From<Recipe>()
                        .Where(x => x.recipe_id == recipeId)
                        .Single();

                    if (recipe == null) 
                    {
                        Console.WriteLine($"Recipe {recipeId} not found, skipping");
                        continue;
                    }

                    // Lấy thông tin tác giả
                    var author = await _supabaseService.Client
                        .From<User>()
                        .Where(x => x.user_id == recipe.user_id)
                        .Single();

                    // Lấy counts cho recipe này
                    var counts = await GetRecipeCounts(recipeId);

                    // Lấy tags (recipe types)
                    var tags = await GetRecipeTags(recipeId);

                    reactions.Add(new NewfeedViewModel
                    {
                        RecipeId = recipe.recipe_id ?? 0,
                        RecipeName = recipe.name ?? "Không có tên",
                        Description = recipe.description ?? "",
                        ThumbnailImg = recipe.thumbnail_img ?? "/images/no-thumbnail.jpg",
                        CreatedAt = recipe.created_at,
                        Level = recipe.level?.ToString() ?? "dễ",
                        UserId = author?.user_id ?? 0,
                        UserName = author?.full_name ?? author?.username ?? "Ẩn danh",
                        UserAvatarUrl = author?.avatar_img ?? "/images/default-avatar.jpg",
                        LikesCount = counts.LikesCount,
                        CommentsCount = counts.CommentsCount,
                        SharesCount = counts.SharesCount,
                        Tags = tags,
                        IsFollowing = false,
                        IsOwnPost = false
                    });
                }

                Console.WriteLine($"Returning {reactions.Count} reactions");
                return reactions;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetReactedRecipesForUser: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return new List<NewfeedViewModel>();
            }
        }

        // Helper method để lấy counts của recipe
        private async Task<(int LikesCount, int CommentsCount, int SharesCount)> GetRecipeCounts(int recipeId)
        {
            try
            {
                // Lấy like count
                var likesResult = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                int likesCount = likesResult.Models?.Count ?? 0;

                // Lấy comment count
                var commentsResult = await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                int commentsCount = commentsResult.Models?.Count ?? 0;

                // Lấy share count
                var sharesResult = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();
                int sharesCount = sharesResult.Models?.Count ?? 0;

                return (likesCount, commentsCount, sharesCount);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting counts for recipe {recipeId}: {ex.Message}");
                return (0, 0, 0);
            }
        }

        // Helper method để lấy tags của recipe
        private async Task<List<string>> GetRecipeTags(int recipeId)
        {
            try
            {
                var tags = new List<string>();
                
                // Lấy recipe types thông qua bảng trung gian
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
                    {
                        tags.Add(type.content);
                    }
                }

                return tags;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting tags for recipe {recipeId}: {ex.Message}");
                return new List<string>();
            }
        }

        // API endpoint for infinite scroll - Load more profile posts
        [HttpGet]
        public async Task<IActionResult> LoadMoreProfilePosts(int userId, int page = 1, int pageSize = 8)
        {
            try
            {
                // Lấy recipes với pagination
                var offset = (page - 1) * pageSize;
                var recipesResp = await _supabaseService.Client
                    .From<Recipe>()
                    .Filter("user_id", Operator.Equals, userId)
                    .Filter("status", Operator.Equals, "active")
                    .Order("created_at", Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var recipeModels = recipesResp.Models ?? new List<Recipe>();

                // Map sang RecipeViewModel với counts
                var recipeVMs = new List<RecipeViewModel>();
                foreach (var r in recipeModels)
                {
                    // Load like count
                    var likesResp = await _supabaseService.Client
                        .From<likeDislike>()
                        .Filter("recipe_id", Operator.Equals, r.recipe_id)
                        .Get();
                    int likes = likesResp?.Models?.Count ?? 0;

                    // Load comment count
                    var commentsResp = await _supabaseService.Client
                        .From<Comment>()
                        .Filter("recipe_id", Operator.Equals, r.recipe_id)
                        .Get();
                    int comments = commentsResp?.Models?.Count ?? 0;

                    // Load share count
                    var sharesResp = await _supabaseService.Client
                        .From<Share>()
                        .Filter("recipe_id", Operator.Equals, r.recipe_id)
                        .Get();
                    int shares = sharesResp?.Models?.Count ?? 0;

                    recipeVMs.Add(new RecipeViewModel
                    {
                        RecipeId = r.recipe_id ?? 0,
                        Name = r.name,
                        ThumbnailImg = r.thumbnail_img,
                        CookTime = r.cook_time ?? 0,
                        Likes = likes,
                        CreatedAt = r.created_at,
                        Level = r.level ?? "dễ"
                    });
                }

                return Json(new { 
                    success = true, 
                    recipes = recipeVMs,
                    hasMore = recipeModels.Count == pageSize
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Không thể tải thêm công thức: " + ex.Message 
                });
            }
        }

        // GET: /Profile/TestSimpleCounts
        [HttpGet]
        public async Task<IActionResult> TestSimpleCounts()
        {
            try
            {
                // Lấy tất cả likes, comments, shares trong database
                var allLikes = await _supabaseService.Client
                    .From<likeDislike>()
                    .Get();
                
                var allComments = await _supabaseService.Client
                    .From<Comment>()
                    .Get();
                
                var allShares = await _supabaseService.Client
                    .From<Share>()
                    .Get();

                // Lấy tất cả active recipes
                var allRecipes = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.status == "active")
                    .Get();

                // Tạo dictionary counts cho từng recipe
                var recipeCounts = new Dictionary<int, object>();
                
                foreach (var recipe in allRecipes.Models.Take(5)) // Lấy 5 recipe đầu tiên
                {
                    var recipeId = recipe.recipe_id ?? 0;
                    var likesCount = allLikes.Models.Count(x => x.recipe_id == recipeId);
                    var commentsCount = allComments.Models.Count(x => x.recipe_id == recipeId);
                    var sharesCount = allShares.Models.Count(x => x.recipe_id == recipeId);
                    
                    recipeCounts[recipeId] = new {
                        recipeName = recipe.name,
                        likesCount = likesCount,
                        commentsCount = commentsCount,
                        sharesCount = sharesCount
                    };
                }

                return Json(new { 
                    success = true,
                    totalLikesInDB = allLikes.Models.Count,
                    totalCommentsInDB = allComments.Models.Count,
                    totalSharesInDB = allShares.Models.Count,
                    recipeCounts = recipeCounts
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // GET: /Profile/TestReactionData
        [HttpGet]
        public async Task<IActionResult> TestReactionData()
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập." });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                }

                // Test lấy tất cả likes của user
                var allLikes = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.user_id == currentUser.user_id.Value)
                    .Get();

                // Test lấy tất cả comments của user
                var allComments = await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.user_id == currentUser.user_id.Value)
                    .Get();

                // Test lấy tất cả shares của user
                var allShares = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.user_id == currentUser.user_id.Value)
                    .Get();

                // Test lấy tất cả likes trong database (không filter theo user)
                var allLikesInDB = await _supabaseService.Client
                    .From<likeDislike>()
                    .Get();

                // Test lấy tất cả comments trong database
                var allCommentsInDB = await _supabaseService.Client
                    .From<Comment>()
                    .Get();

                // Test lấy tất cả shares trong database
                var allSharesInDB = await _supabaseService.Client
                    .From<Share>()
                    .Get();

                // Test method mới
                var reactedRecipes = await GetReactedRecipesForUser(currentUser.user_id.Value, 1, 5);

                return Json(new { 
                    success = true, 
                    userId = currentUser.user_id.Value,
                    userEmail = sessionEmail,
                    userLikesCount = allLikes.Models.Count,
                    userCommentsCount = allComments.Models.Count,
                    userSharesCount = allShares.Models.Count,
                    totalLikesInDB = allLikesInDB.Models.Count,
                    totalCommentsInDB = allCommentsInDB.Models.Count,
                    totalSharesInDB = allSharesInDB.Models.Count,
                    reactedRecipesCount = reactedRecipes.Count,
                    reactedRecipes = reactedRecipes.Select(r => new { 
                        r.RecipeId, 
                        r.RecipeName, 
                        r.LikesCount, 
                        r.CommentsCount, 
                        r.SharesCount 
                    }).ToList(),
                    userLikes = allLikes.Models.Select(l => new { l.recipe_id, l.created_at }).ToList(),
                    userComments = allComments.Models.Select(c => new { c.recipe_id, c.created_at }).ToList(),
                    userShares = allShares.Models.Select(s => new { s.recipe_id, s.created_at }).ToList(),
                    allLikes = allLikesInDB.Models.Select(l => new { l.recipe_id, l.user_id, l.created_at }).ToList(),
                    allComments = allCommentsInDB.Models.Select(c => new { c.recipe_id, c.user_id, c.created_at }).ToList(),
                    allShares = allSharesInDB.Models.Select(s => new { s.recipe_id, s.user_id, s.created_at }).ToList()
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // GET: /Profile/TestSimpleReaction
        [HttpGet]
        public async Task<IActionResult> TestSimpleReaction()
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập." });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                }

                // Test đơn giản - chỉ lấy 1 recipe đã like
                var oneLike = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.user_id == currentUser.user_id.Value)
                    .Limit(1)
                    .Get();

                if (oneLike.Models.Count > 0)
                {
                    var like = oneLike.Models.First();
                    var recipe = await _supabaseService.Client
                        .From<Recipe>()
                        .Where(x => x.recipe_id == like.recipe_id)
                        .Single();

                    return Json(new { 
                        success = true, 
                        message = "Tìm thấy recipe đã like",
                        like = new { like.recipe_id, like.created_at },
                        recipe = recipe != null ? new { recipe.recipe_id, recipe.name } : null
                    });
                }
                else
                {
                    return Json(new { 
                        success = true, 
                        message = "Chưa có like nào",
                        userId = currentUser.user_id.Value
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // GET: /Profile/TestDatabaseConnection
        [HttpGet]
        public async Task<IActionResult> TestDatabaseConnection()
        {
            try
            {
                // Test kết nối database cơ bản
                var allUsers = await _supabaseService.Client
                    .From<User>()
                    .Limit(5)
                    .Get();

                var allRecipes = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.status == "active")
                    .Limit(5)
                    .Get();

                var allLikes = await _supabaseService.Client
                    .From<likeDislike>()
                    .Limit(5)
                    .Get();

                var allComments = await _supabaseService.Client
                    .From<Comment>()
                    .Limit(5)
                    .Get();

                var allShares = await _supabaseService.Client
                    .From<Share>()
                    .Limit(5)
                    .Get();

                return Json(new { 
                    success = true, 
                    message = "Database connection OK",
                    counts = new {
                        users = allUsers.Models?.Count ?? 0,
                        recipes = allRecipes.Models?.Count ?? 0,
                        likes = allLikes.Models?.Count ?? 0,
                        comments = allComments.Models?.Count ?? 0,
                        shares = allShares.Models?.Count ?? 0
                    },
                    sampleData = new {
                        users = allUsers.Models?.Select(u => new { u.user_id, u.username, u.email }).ToList(),
                        recipes = allRecipes.Models?.Select(r => new { r.recipe_id, r.name, r.user_id }).ToList(),
                        likes = allLikes.Models?.Select(l => new { l.recipe_id, l.user_id, l.created_at }).ToList(),
                        comments = allComments.Models?.Select(c => new { c.recipe_id, c.user_id, c.created_at }).ToList(),
                        shares = allShares.Models?.Select(s => new { s.recipe_id, s.user_id, s.created_at }).ToList()
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Database connection failed: " + ex.Message });
            }
        }

        // GET: /Profile/CreateTestData
        [HttpGet]
        public async Task<IActionResult> CreateTestData()
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập." });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                }

                // Lấy recipe đầu tiên để test
                var firstRecipe = await _supabaseService.Client
                    .From<Recipe>()
                    .Limit(1)
                    .Get();

                if (firstRecipe.Models?.Count == 0)
                {
                    return Json(new { success = false, message = "Không có recipe nào trong database để test." });
                }

                var recipe = firstRecipe.Models.First();
                var userId = currentUser.user_id.Value;
                var recipeId = recipe.recipe_id ?? 0;

                // Tạo test like
                var testLike = new likeDislike
                {
                    user_id = userId,
                    recipe_id = recipeId,
                    body = "Test like",
                    created_at = DateTime.UtcNow
                };

                await _supabaseService.Client.From<likeDislike>().Insert(testLike);

                // Tạo test comment
                var testComment = new Comment
                {
                    user_id = userId,
                    recipe_id = recipeId,
                    body = "Test comment",
                    created_at = DateTime.UtcNow
                };

                await _supabaseService.Client.From<Comment>().Insert(testComment);

                // Tạo test share
                var testShare = new Share
                {
                    user_id = userId,
                    recipe_id = recipeId,
                    created_at = DateTime.UtcNow
                };

                await _supabaseService.Client.From<Share>().Insert(testShare);

                return Json(new { 
                    success = true, 
                    message = "Đã tạo test data thành công",
                    data = new {
                        userId = userId,
                        recipeId = recipeId,
                        recipeName = recipe.name
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi tạo test data: " + ex.Message });
            }
        }

        // GET: /Profile/TestReactionMethod
        [HttpGet]
        public async Task<IActionResult> TestReactionMethod()
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập." });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng." });
                }

                // Test method GetReactedRecipesForUser trực tiếp
                var reactions = await GetReactedRecipesForUser(currentUser.user_id.Value, 1, 10);

                return Json(new { 
                    success = true, 
                    message = "Test method GetReactedRecipesForUser",
                    userId = currentUser.user_id.Value,
                    reactionsCount = reactions.Count,
                    reactions = reactions.Select(r => new {
                        r.RecipeId,
                        r.RecipeName,
                        r.LikesCount,
                        r.CommentsCount,
                        r.SharesCount,
                        r.CreatedAt
                    }).ToList()
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi test method: " + ex.Message, stackTrace = ex.StackTrace });
            }
        }

        // Method để load reacted recipes cho Profile/Info tab
        [HttpGet]
        public async Task<IActionResult> LoadReactedForProfile()
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Chưa đăng nhập" });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy user" });
                }

                var reactions = await GetReactedRecipesForUser(currentUser.user_id.Value, 1, 8);
                
                return Json(new { 
                    success = true, 
                    reactions = reactions,
                    count = reactions.Count
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = ex.Message
                });
            }
        }

        // GET: /Profile/Followers/{id}
        [HttpGet]
        public async Task<IActionResult> Followers(int id)
        {
            try
            {
                var user = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.user_id == id)
                    .Single();

                if (user == null)
                {
                    return NotFound("Không tìm thấy người dùng");
                }

                // Lấy danh sách followers
                var followers = await GetFollowersForUser(id, 1, 12);

                var viewModel = new ProfileViewModel
                {
                    User = user,
                    Followers = followers
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading followers for user {id}: {ex.Message}");
                return View("Error");
            }
        }

        // GET: /Profile/Following/{id}
        [HttpGet]
        public async Task<IActionResult> Following(int id)
        {
            try
            {
                var user = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.user_id == id)
                    .Single();

                if (user == null)
                {
                    return NotFound("Không tìm thấy người dùng");
                }

                // Lấy danh sách following
                var following = await GetFollowingForUser(id, 1, 12);

                var viewModel = new ProfileViewModel
                {
                    User = user,
                    Following = following
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading following for user {id}: {ex.Message}");
                return View("Error");
            }
        }

        // Helper method để lấy followers của user
        private async Task<List<FollowerViewModel>> GetFollowersForUser(int userId, int page = 1, int pageSize = 12)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                
                // Lấy user hiện tại để check follow status
                var sessionEmail = HttpContext.Session.GetString("user_email");
                int? currentUserId = null;
                if (!string.IsNullOrEmpty(sessionEmail))
                {
                    var currentUser = await _supabaseService.Client
                        .From<User>()
                        .Filter("email", Operator.Equals, sessionEmail)
                        .Single();
                    currentUserId = currentUser?.user_id;
                }
                
                // Lấy danh sách user_id của những người follow user này
                var follows = await _supabaseService.Client
                    .From<Follow>()
                    .Where(x => x.following_id == userId)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var followerIds = follows.Models?.Select(f => f.follower_id).ToList() ?? new List<int>();
                
                if (!followerIds.Any())
                {
                    return new List<FollowerViewModel>();
                }

                // Lấy thông tin chi tiết của các followers
                var followers = new List<FollowerViewModel>();
                foreach (var followerId in followerIds)
                {
                    var follower = await _supabaseService.Client
                        .From<User>()
                        .Where(x => x.user_id == followerId)
                        .Single();

                    if (follower != null)
                    {
                        // Lấy số lượng recipes của follower
                        var recipeCount = await _supabaseService.Client
                            .From<Recipe>()
                            .Where(x => x.user_id == followerId)
                            .Get();

                        // Lấy số lượng followers của follower
                        var followersCount = await _supabaseService.Client
                            .From<Follow>()
                            .Where(x => x.following_id == followerId)
                            .Get();

                        // Check nếu current user có đang follow follower này không
                        bool isFollowing = false;
                        if (currentUserId.HasValue && currentUserId.Value != followerId)
                        {
                            var followCheck = await _supabaseService.Client
                                .From<Follow>()
                                .Where(x => x.follower_id == currentUserId.Value && x.following_id == followerId)
                                .Get();
                            isFollowing = followCheck.Models?.Any() ?? false;
                        }

                        followers.Add(new FollowerViewModel
                        {
                            UserId = follower.user_id ?? 0,
                            Username = follower.username,
                            FullName = follower.full_name,
                            Bio = follower.bio,
                            AvatarUrl = follower.avatar_img,
                            RecipeCount = recipeCount.Models?.Count ?? 0,
                            FollowersCount = followersCount.Models?.Count ?? 0,
                            CreatedAt = follower.created_at,
                            IsFollowing = isFollowing
                        });
                    }
                }

                return followers;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting followers for user {userId}: {ex.Message}");
                return new List<FollowerViewModel>();
            }
        }

        // Helper method để lấy following của user
        private async Task<List<FollowerViewModel>> GetFollowingForUser(int userId, int page = 1, int pageSize = 12)
        {
            try
            {
                var offset = (page - 1) * pageSize;
                
                // Lấy danh sách user_id của những người mà user này đang follow
                var follows = await _supabaseService.Client
                    .From<Follow>()
                    .Where(x => x.follower_id == userId)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var followingIds = follows.Models?.Select(f => f.following_id).ToList() ?? new List<int>();
                
                if (!followingIds.Any())
                {
                    return new List<FollowerViewModel>();
                }

                // Lấy thông tin chi tiết của các following
                var following = new List<FollowerViewModel>();
                foreach (var followingId in followingIds)
                {
                    var user = await _supabaseService.Client
                        .From<User>()
                        .Where(x => x.user_id == followingId)
                        .Single();

                    if (user != null)
                    {
                        // Lấy số lượng recipes của user
                        var recipeCount = await _supabaseService.Client
                            .From<Recipe>()
                            .Where(x => x.user_id == followingId)
                            .Get();

                        // Lấy số lượng followers của user
                        var followersCount = await _supabaseService.Client
                            .From<Follow>()
                            .Where(x => x.following_id == followingId)
                            .Get();

                        following.Add(new FollowerViewModel
                        {
                            UserId = user.user_id ?? 0,
                            Username = user.username,
                            FullName = user.full_name,
                            Bio = user.bio,
                            AvatarUrl = user.avatar_img,
                            RecipeCount = recipeCount.Models?.Count ?? 0,
                            FollowersCount = followersCount.Models?.Count ?? 0,
                            CreatedAt = user.created_at
                        });
                    }
                }

                return following;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting following for user {userId}: {ex.Message}");
                return new List<FollowerViewModel>();
            }
        }

        // API endpoint để load more followers
        [HttpGet]
        public async Task<IActionResult> LoadMoreFollowers(int userId, int page = 1, int pageSize = 12)
        {
            try
            {
                var followers = await GetFollowersForUser(userId, page, pageSize);
                
                return Json(new { 
                    success = true, 
                    followers = followers,
                    hasMore = followers.Count == pageSize
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Không thể tải thêm người theo dõi: " + ex.Message 
                });
            }
        }

        // API endpoint để load more following
        [HttpGet]
        public async Task<IActionResult> LoadMoreFollowing(int userId, int page = 1, int pageSize = 12)
        {
            try
            {
                var following = await GetFollowingForUser(userId, page, pageSize);
                
                return Json(new { 
                    success = true, 
                    following = following,
                    hasMore = following.Count == pageSize
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Không thể tải thêm người đang theo dõi: " + ex.Message 
                });
            }
        }

        // Method để lấy posts của user - xây lại từ đầu
        [HttpGet]
        public async Task<IActionResult> GetUserPosts(int userId, int page = 1, int pageSize = 8)
        {
            try
            {
                Console.WriteLine($"GetUserPosts called: userId={userId}, page={page}, pageSize={pageSize}");

                // Tính offset
                var offset = (page - 1) * pageSize;
                Console.WriteLine($"Offset: {offset}");

                // Lấy recipes với pagination từ Recipe table (chỉ lấy recipes có status = 'active')
                var recipesResp = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.user_id == userId && x.status == "active")
                    .Order("created_at", Ordering.Descending)
                    .Range(offset, offset + pageSize - 1)
                    .Get();

                var recipeModels = recipesResp.Models ?? new List<Recipe>();
                Console.WriteLine($"Found {recipeModels.Count} recipes for user {userId}");

                // Map sang RecipeViewModel với counts
                var recipeVMs = new List<RecipeViewModel>();
                foreach (var r in recipeModels)
                {
                    // Load like count từ like_dislike table
                    var likesResp = await _supabaseService.Client
                        .From<likeDislike>()
                        .Where(x => x.recipe_id == r.recipe_id)
                        .Get();
                    int likes = likesResp?.Models?.Count ?? 0;

                    // Load comment count từ Comment table
                    var commentsResp = await _supabaseService.Client
                        .From<Comment>()
                        .Where(x => x.recipe_id == r.recipe_id)
                        .Get();
                    int comments = commentsResp?.Models?.Count ?? 0;

                    // Load share count từ Share table
                    var sharesResp = await _supabaseService.Client
                        .From<Share>()
                        .Where(x => x.recipe_id == r.recipe_id)
                        .Get();
                    int shares = sharesResp?.Models?.Count ?? 0;

                    recipeVMs.Add(new RecipeViewModel
                    {
                        RecipeId = r.recipe_id ?? 0,
                        Name = r.name ?? "Không có tên",
                        ThumbnailImg = r.thumbnail_img ?? "/images/no-thumbnail.png",
                        CookTime = r.cook_time ?? 0,
                        Likes = likes,
                        CreatedAt = r.created_at,
                        Level = r.level?.ToString() ?? "dễ"
                    });

                    Console.WriteLine($"Recipe {r.recipe_id}: {r.name}, likes: {likes}, comments: {comments}, shares: {shares}");
                }

                Console.WriteLine($"Returning {recipeVMs.Count} recipes");
                return Json(new { 
                    success = true, 
                    recipes = recipeVMs,
                    hasMore = recipeModels.Count == pageSize,
                    totalLoaded = recipeVMs.Count
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetUserPosts: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                return Json(new { 
                    success = false, 
                    message = "Không thể tải bài đăng: " + ex.Message 
                });
            }
        }

        // Test method để kiểm tra bài đăng của user
        [HttpGet]
        public async Task<IActionResult> TestUserPosts(int userId)
        {
            try
            {
                // Lấy tất cả active recipes của user
                var allRecipes = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.user_id == userId && x.status == "active")
                    .Order("created_at", Ordering.Descending)
                    .Get();

                var totalCount = allRecipes.Models?.Count ?? 0;

                // Test pagination
                var page1 = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.user_id == userId && x.status == "active")
                    .Order("created_at", Ordering.Descending)
                    .Range(0, 7) // page 1: 8 items
                    .Get();

                var page2 = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.user_id == userId && x.status == "active")
                    .Order("created_at", Ordering.Descending)
                    .Range(8, 15) // page 2: 8 items
                    .Get();

                var page3 = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.user_id == userId && x.status == "active")
                    .Order("created_at", Ordering.Descending)
                    .Range(16, 23) // page 3: 8 items
                    .Get();

                return Json(new { 
                    success = true,
                    userId = userId,
                    totalRecipes = totalCount,
                    page1Count = page1.Models?.Count ?? 0,
                    page2Count = page2.Models?.Count ?? 0,
                    page3Count = page3.Models?.Count ?? 0,
                    page1Recipes = page1.Models?.Select(r => new {
                        recipe_id = r.recipe_id,
                        name = r.name,
                        created_at = r.created_at
                    }).Cast<object>().ToList() ?? new List<object>(),
                    page2Recipes = page2.Models?.Select(r => new {
                        recipe_id = r.recipe_id,
                        name = r.name,
                        created_at = r.created_at
                    }).Cast<object>().ToList() ?? new List<object>(),
                    page3Recipes = page3.Models?.Select(r => new {
                        recipe_id = r.recipe_id,
                        name = r.name,
                        created_at = r.created_at
                    }).Cast<object>().ToList() ?? new List<object>()
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Lỗi test user posts: " + ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Test method để kiểm tra LoadMoreProfilePosts
        [HttpGet]
        public async Task<IActionResult> TestLoadMorePosts(int userId, int page = 1, int pageSize = 8)
        {
            try
            {
                // Lấy tất cả active recipes của user để so sánh
                var allRecipes = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.user_id == userId && x.status == "active")
                    .Order("created_at", Ordering.Descending)
                    .Get();

                var totalCount = allRecipes.Models?.Count ?? 0;

                // Test LoadMoreProfilePosts method
                var result = await LoadMoreProfilePosts(userId, page, pageSize);
                
                // Parse JSON result
                var jsonResult = result as JsonResult;
                var data = jsonResult?.Value;

                return Json(new { 
                    success = true,
                    userId = userId,
                    page = page,
                    pageSize = pageSize,
                    totalRecipes = totalCount,
                    loadMoreResult = data,
                    expectedHasMore = totalCount > (page * pageSize)
                });
            }
            catch (Exception ex)
            {
                return Json(new { 
                    success = false, 
                    message = "Lỗi test load more posts: " + ex.Message,
                    stackTrace = ex.StackTrace
                });
            }
        }

        // Toggle Follow/Unfollow
        [HttpPost]
        public async Task<IActionResult> ToggleFollow([FromBody] ToggleFollowRequest request)
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
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                var currentUserId = currentUser.user_id.Value;
                var targetUserId = request.UserId;

                if (currentUserId == targetUserId)
                {
                    return Json(new { success = false, message = "Bạn không thể theo dõi chính mình" });
                }

                // Check if already following
                var existingFollow = await _supabaseService.Client
                    .From<Follow>()
                    .Where(x => x.follower_id == currentUserId && x.following_id == targetUserId)
                    .Get();

                bool isNowFollowing;

                if (existingFollow.Models.Any())
                {
                    // Already following -> Unfollow
                    await _supabaseService.Client
                        .From<Follow>()
                        .Where(x => x.follower_id == currentUserId && x.following_id == targetUserId)
                        .Delete();
                    isNowFollowing = false;
                }
                else
                {
                    // Not following -> Follow
                    var newFollow = new Follow
                    {
                        follower_id = currentUserId,
                        following_id = targetUserId
                    };
                    await _supabaseService.Client.From<Follow>().Insert(newFollow);
                    isNowFollowing = true;
                }

                return Json(new { success = true, isFollowing = isNowFollowing });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // Request model for ToggleFollow
        public class ToggleFollowRequest
        {
            public int UserId { get; set; }
        }

        // POST: /Profile/ReportUser
        [HttpPost]
        public async Task<IActionResult> ReportUser([FromBody] ReportUserRequest request)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Vui lòng đăng nhập" });
                }

                // Get current user
                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Filter("email", Operator.Equals, sessionEmail)
                    .Single();

                if (currentUser?.user_id == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                var reporterId = currentUser.user_id.Value;
                var reportedUserId = request.UserId;

                // Không cho phép report chính mình
                if (reporterId == reportedUserId)
                {
                    return Json(new { success = false, message = "Bạn không thể báo cáo chính mình" });
                }

                // Kiểm tra xem đã report chưa
                var existingReport = await _supabaseService.Client
                    .From<UserReport>()
                    .Where(x => x.reporter_id == reporterId && x.reported_user_id == reportedUserId)
                    .Get();

                if (existingReport.Models.Any())
                {
                    return Json(new { success = false, message = "Bạn đã báo cáo người dùng này rồi" });
                }

                // Tạo report mới
                var newReport = new UserReport
                {
                    reporter_id = reporterId,
                    reported_user_id = reportedUserId,
                    body = request.Reason ?? "Không có lý do cụ thể",
                    status = "Đang xử lý",
                    created_at = DateTime.UtcNow
                };

                await _supabaseService.Client.From<UserReport>().Insert(newReport);

                return Json(new { success = true, message = "Báo cáo đã được gửi thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // Request model for ReportUser
        public class ReportUserRequest
        {
            public int UserId { get; set; }
            public string? Reason { get; set; }
        }
    }
}