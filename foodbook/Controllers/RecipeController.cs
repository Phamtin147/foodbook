using System.Diagnostics;
using System.Text.Json;
using foodbook.Models;
using Microsoft.AspNetCore.Mvc;
using foodbook.Attributes;
using foodbook.Services;
using foodbook.Helpers;

namespace foodbook.Controllers
{
    [LoginRequired]
    public class RecipeController : Controller
    {
        private readonly ILogger<RecipeController> _logger;
        private readonly SupabaseService _supabaseService;
        private readonly StorageService _storageService;

        public RecipeController(
            ILogger<RecipeController> logger, 
            SupabaseService supabaseService,
            StorageService storageService)
        {
            _logger = logger;
            _supabaseService = supabaseService;
            _storageService = storageService;
        }

        [HttpGet]
        public IActionResult Add()
        {
            try
            {
                // Load suggestions from DB: distinct Ingredient names and all RecipeType contents
                var ingredientNames = _supabaseService.Client
                    .From<IngredientMaster>()
                    .Select("name")
                    .Get().Result.Models
                    .Where(i => !string.IsNullOrWhiteSpace(i.name))
                    .Select(i => i.name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                var typeContents = _supabaseService.Client
                    .From<RecipeType>()
                    .Select("content")
                    .Get().Result.Models
                    .Where(t => !string.IsNullOrWhiteSpace(t.content))
                    .Select(t => t.content!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                ViewBag.IngredientSuggestions = ingredientNames;
                ViewBag.TypeSuggestions = typeContents;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load suggestions for Add Recipe");
                ViewBag.IngredientSuggestions = new List<string>();
                ViewBag.TypeSuggestions = new List<string>();
            }

            // Return initialized model to prevent NullReferenceException in view
            return View(new AddRecipeViewModel 
            { 
                CookTime = 30, // Default cook time
                Level = "dễ"   // Default difficulty level
            });
        }

        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 5368709120)]
        public async Task<IActionResult> Add(AddRecipeViewModel model)
        {
            _logger.LogInformation("=== ADD RECIPE STARTED ===");
            
            // Log validation errors
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogError("ModelState invalid: {Errors}", string.Join(", ", errors));
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin! " + string.Join(", ", errors);
                return View(model);
            }

            try
            {
                _logger.LogInformation("Model: Name={Name}, CookTime={CookTime}, Level={Level}, Steps={StepsCount}", 
                    model.Name, model.CookTime, model.Level, model.Steps?.Count ?? 0);
                
                // Lấy user_id từ session
                var userId = HttpContext.Session.GetInt32("UserId");
                
                // Debug session
                var sessionKeys = new[] { "UserId", "user_id", "username", "user_email", "role" };
                foreach (var key in sessionKeys)
                {
                    var value = HttpContext.Session.GetString(key);
                    _logger.LogInformation("Session[{Key}] = {Value}", key, value ?? "NULL");
                }
                _logger.LogInformation("Session.GetInt32('UserId') = {UserId}", userId);
                
                if (userId == null || userId == 0)
                {
                    _logger.LogError("UserId not found in session or = 0");
                    TempData["Error"] = "Vui lòng đăng nhập lại! (Session timeout)";
                    return RedirectToAction("Login", "Account");
                }
                
                _logger.LogInformation("UserId from session: {UserId}", userId);

                // 1. Tạo Recipe trong DB trước (chưa có thumbnail)
                _logger.LogInformation("Creating Recipe record...");
                
                var recipe = new Recipe
                {
                    user_id = userId.Value,
                    name = model.Name,
                    thumbnail_img = null, // Sẽ update sau
                    description = model.Description,
                    cook_time = model.CookTime,
                    level = model.Level,
                    step_number = Math.Max(1, model.Steps?.Count ?? 0),
                    created_at = DateTime.UtcNow
                };

                _logger.LogInformation("Recipe object: {@Recipe}", recipe);

                var recipeResult = await _supabaseService.Client
                    .From<Recipe>()
                    .Insert(recipe);

                _logger.LogInformation("Recipe insert result: {Count} records", recipeResult.Models.Count);

                var createdRecipe = recipeResult.Models.FirstOrDefault();
                if (createdRecipe == null || createdRecipe.recipe_id == null)
                {
                    _logger.LogError("Failed to create recipe - no recipe_id returned");
                    throw new Exception("Không thể tạo công thức - không nhận được ID");
                }

                var recipeId = createdRecipe.recipe_id.Value;
                _logger.LogInformation("Recipe created successfully with ID: {RecipeId}", recipeId);

                // 2. Upload thumbnail với recipe ID và update lại Recipe
                string? thumbnailUrl = null;
                if (model.MainMedia != null)
                {
                    _logger.LogInformation("Uploading MainMedia as thumbnail: {FileName} ({Size} bytes)", 
                        model.MainMedia.FileName, model.MainMedia.Length);
                    
                    var isVideo = _storageService.IsVideoFile(model.MainMedia);
                    
                    thumbnailUrl = await _storageService.UploadFileAsync(
                        model.MainMedia, 
                        isVideo: isVideo, 
                        folderPath: $"recipes/{recipeId}" // ← CÓ RECIPE ID!
                    );
                    
                    _logger.LogInformation("MainMedia uploaded as thumbnail: {Url}", thumbnailUrl);
                    
                    // Update Recipe với thumbnail URL
                    if (!string.IsNullOrEmpty(thumbnailUrl))
                    {
                        var updateResult = await _supabaseService.Client
                            .From<Recipe>()
                            .Where(x => x.recipe_id == recipeId)
                            .Set(x => x.thumbnail_img, thumbnailUrl!)
                            .Update();
                            
                        _logger.LogInformation("Updated {Count} recipe records with thumbnail", updateResult.Models.Count);
                    }
                        
                    _logger.LogInformation("Recipe updated with thumbnail URL");
                }
                else
                {
                    _logger.LogInformation("No MainMedia provided for thumbnail");
                }

                // 3. Lưu Ingredients với cấu trúc mới (giữ nguyên giao diện cũ)
                if (model.Ingredients != null && model.Ingredients.Any())
                {
                    _logger.LogInformation("Saving {Count} ingredients", model.Ingredients.Count);
                    
                    foreach (var ingredientName in model.Ingredients)
                    {
                        if (string.IsNullOrWhiteSpace(ingredientName)) continue;

                        var name = ingredientName.Trim();
                        _logger.LogInformation("Processing ingredient: {Name}", name);

                        // Tìm hoặc tạo nguyên liệu trong bảng Ingredient_Master
                        var existingIngredient = await _supabaseService.Client
                            .From<IngredientMaster>()
                            .Select("ingredient_id, name")
                            .Where(x => x.name == name)
                            .Get();

                        int ingredientId;
                        if (existingIngredient.Models.Any())
                        {
                            ingredientId = existingIngredient.Models.First().ingredient_id.Value;
                            _logger.LogInformation("  - Found existing ingredient: ID={Id}, Name={Name}", 
                                ingredientId, name);
                        }
                        else
                        {
                            // Tạo nguyên liệu mới
                            var newIngredient = new IngredientMaster 
                            { 
                                name = name, 
                                created_at = DateTime.UtcNow 
                            };
                            var insertResult = await _supabaseService.Client.From<IngredientMaster>().Insert(newIngredient);
                            ingredientId = insertResult.Models.FirstOrDefault()?.ingredient_id ?? 0;
                            _logger.LogInformation("  - Created new ingredient: ID={Id}, Name={Name}", 
                                ingredientId, name);
                        }

                        // Tạo link trong bảng trung gian Recipe_Ingredient
                        var recipeIngredient = new RecipeIngredient
                        {
                            recipe_id = recipeId,
                            ingredient_id = ingredientId,
                            created_at = DateTime.UtcNow
                        };

                        await _supabaseService.Client.From<RecipeIngredient>().Insert(recipeIngredient);
                        _logger.LogInformation("  - Created Recipe_Ingredient link: RecipeId={RecipeId}, IngredientId={IngredientId}", 
                            recipeId, ingredientId);
                    }
                }
                else
                {
                    _logger.LogInformation("No ingredients to save");
                }

                // 4. Lưu Recipe Types (hỗ trợ nhiều types per recipe)
                if (model.RecipeTypes != null && model.RecipeTypes.Any())
                {
                    _logger.LogInformation("Processing {Count} recipe types", model.RecipeTypes.Count);

                    foreach (var typeNameRaw in model.RecipeTypes)
                    {
                        var typeName = (typeNameRaw ?? string.Empty).Trim();
                        if (string.IsNullOrEmpty(typeName)) continue;

                        _logger.LogInformation("Processing recipe type: {Type}", typeName);

                        // Tìm type đã tồn tại
                        var existingType = await _supabaseService.Client
                            .From<RecipeType>()
                            .Select("recipe_type_id, content")
                            .Where(x => x.content == typeName)
                            .Get();

                        int recipeTypeId;
                        if (existingType.Models.Any())
                        {
                            recipeTypeId = existingType.Models.First().recipe_type_id.Value;
                            _logger.LogInformation("  - Found existing RecipeType: ID={Id}, Content={Content}", 
                                recipeTypeId, typeName);
                        }
                        else
                        {
                            // Tạo type mới
                            var newType = new RecipeType 
                            { 
                                content = typeName, 
                                created_at = DateTime.UtcNow 
                            };
                            var insertResult = await _supabaseService.Client.From<RecipeType>().Insert(newType);
                            recipeTypeId = insertResult.Models.FirstOrDefault()?.recipe_type_id ?? 0;
                            _logger.LogInformation("  - Created new RecipeType: ID={Id}, Content={Content}", 
                                recipeTypeId, typeName);
                        }

                        // Tạo link trong bảng trung gian
                        var recipeRecipeType = new RecipeRecipeType
                        {
                            recipe_id = recipeId,
                            recipe_type_id = recipeTypeId,
                            created_at = DateTime.UtcNow
                        };

                        await _supabaseService.Client.From<RecipeRecipeType>().Insert(recipeRecipeType);
                        _logger.LogInformation("  - Created Recipe_RecipeType link: RecipeId={RecipeId}, TypeId={TypeId}", 
                            recipeId, recipeTypeId);
                    }
                }

                // 5. Lưu Recipe Steps với nhiều Media
                if (model.Steps != null && model.Steps.Any())
                {
                    _logger.LogInformation("Saving {Count} steps", model.Steps.Count);
                    
                    for (int i = 0; i < model.Steps.Count; i++)
                    {
                        var step = model.Steps[i];
                        var stepNumber = i + 1;

                        var instructionPreview = step.Instruction != null && step.Instruction.Length > 50 
                            ? step.Instruction.Substring(0, 50) + "..." 
                            : step.Instruction ?? "";
                        _logger.LogInformation("Step {StepNumber}: {Instruction}", stepNumber, instructionPreview);

                        // Tạo RecipeStep trước
                        var recipeStep = new RecipeStep
                        {
                            recipe_id = recipeId,
                            step = stepNumber,
                            instruction = step.Instruction ?? ""
                        };

                        await _supabaseService.Client
                            .From<RecipeStep>()
                            .Insert(recipeStep);
                            
                        _logger.LogInformation("  - RecipeStep saved");

                        // Lấy danh sách files cần upload
                        var mediaFiles = new List<IFormFile>();
                        
                        // Ưu tiên StepMedia (nhiều files)
                        if (step.StepMedia != null && step.StepMedia.Any())
                        {
                            mediaFiles.AddRange(step.StepMedia);
                        }
                        // Fallback sang StepImage (1 file) nếu không có StepMedia
                        else if (step.StepImage != null)
                        {
                            mediaFiles.Add(step.StepImage);
                        }

                        // Upload và link tất cả media files với step này
                        if (mediaFiles.Any())
                        {
                            _logger.LogInformation("  - Processing {Count} media files", mediaFiles.Count);
                            
                            for (int mediaIndex = 0; mediaIndex < mediaFiles.Count; mediaIndex++)
                            {
                                var mediaFile = mediaFiles[mediaIndex];
                                
                                _logger.LogInformation("    [{Index}] {FileName} ({Size} bytes)", 
                                    mediaIndex + 1, mediaFile.FileName, mediaFile.Length);
                                
                                // Kiểm tra loại file
                                var isVideo = _storageService.IsVideoFile(mediaFile);
                                _logger.LogInformation("      Type: {Type}", isVideo ? "Video" : "Image");
                                
                                // Upload lên storage
                                var mediaUrl = await _storageService.UploadFileAsync(
                                    mediaFile, 
                                    isVideo: isVideo, 
                                    folderPath: $"recipes/{recipeId}/steps/{stepNumber}"
                                );
                                
                                _logger.LogInformation("      Uploaded to: {Url}", mediaUrl);

                                // Tạo Media record trong DB
                                var media = new Media
                                {
                                    media_img = isVideo ? null : mediaUrl,
                                    media_video = isVideo ? mediaUrl : null
                                };

                                var mediaResult = await _supabaseService.Client
                                    .From<Media>()
                                    .Insert(media);

                                var createdMedia = mediaResult.Models.FirstOrDefault();
                                if (createdMedia?.media_id != null)
                                {
                                    _logger.LogInformation("      Media record created: ID={MediaId}", createdMedia.media_id);
                                    
                                    // Tạo RecipeStep_Media để link step với media
                                    var recipeStepMedia = new RecipeStepMedia
                                    {
                                        recipe_id = recipeId,
                                        step = stepNumber,
                                        media_id = createdMedia.media_id.Value,
                                        display_order = mediaIndex + 1  // Thứ tự hiển thị
                                    };

                                    await _supabaseService.Client
                                        .From<RecipeStepMedia>()
                                        .Insert(recipeStepMedia);
                                        
                                    _logger.LogInformation("      RecipeStep_Media link created");
                                }
                                else
                                {
                                    _logger.LogWarning("      Failed to create Media record");
                                }
                            }
                        }
                        else
                        {
                            _logger.LogInformation("  - No media files for this step");
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No steps to save");
                }

                // 6. Lưu Recipe Types vào bảng trung gian Recipe_RecipeType
                if (model.RecipeTypes != null && model.RecipeTypes.Any())
                {
                    _logger.LogInformation("Saving {Count} recipe types", model.RecipeTypes.Count);
                    
                    foreach (var recipeTypeName in model.RecipeTypes)
                    {
                        try
                        {
                            // Tìm hoặc tạo RecipeType
                            var existingType = await _supabaseService.Client
                                .From<RecipeType>()
                                .Where(x => x.content == recipeTypeName)
                                .Get();

                            int recipeTypeId;

                            if (existingType.Models != null && existingType.Models.Any())
                            {
                                recipeTypeId = existingType.Models.First().recipe_type_id ?? 0;
                                _logger.LogInformation("  - Found existing type: {Name} (ID: {Id})", recipeTypeName, recipeTypeId);
                            }
                            else
                            {
                                // Tạo mới RecipeType
                                var newType = new RecipeType
                                {
                                    content = recipeTypeName,
                                    created_at = DateTime.UtcNow
                                };

                                var typeResult = await _supabaseService.Client
                                    .From<RecipeType>()
                                    .Insert(newType);

                                recipeTypeId = typeResult.Models.First().recipe_type_id ?? 0;
                                _logger.LogInformation("  - Created new type: {Name} (ID: {Id})", recipeTypeName, recipeTypeId);
                            }

                            // Lưu vào bảng trung gian Recipe_RecipeType
                            var recipeRecipeType = new RecipeRecipeType
                            {
                                recipe_id = recipeId,
                                recipe_type_id = recipeTypeId,
                                created_at = DateTime.UtcNow
                            };

                            await _supabaseService.Client
                                .From<RecipeRecipeType>()
                                .Insert(recipeRecipeType);

                            _logger.LogInformation("  - Linked recipe with type: {Name}", recipeTypeName);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "  - Failed to save recipe type: {Name}", recipeTypeName);
                            // Continue với các types khác
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("No recipe types to save");
                }

                _logger.LogInformation("=== ADD RECIPE COMPLETED SUCCESSFULLY ===");
                TempData["Success"] = $"Đã thêm công thức '{model.Name}' thành công!";
                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "=== ADD RECIPE FAILED ===");
                _logger.LogError("Exception Type: {Type}", ex.GetType().Name);
                _logger.LogError("Message: {Message}", ex.Message);
                
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner Exception: {InnerMessage}", ex.InnerException.Message);
                }
                
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
                return View(model);
            }
        }

        // Edit Recipe - GET
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                if (!currentUserId.HasValue)
                {
                    TempData["Error"] = "Bạn cần đăng nhập để chỉnh sửa công thức";
                    return RedirectToAction("Login", "Account");
                }

                // Load recipe
                var recipe = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.recipe_id == id)
                    .Single();

                if (recipe == null)
                {
                    TempData["Error"] = "Không tìm thấy công thức";
                    return RedirectToAction("Newsfeed", "Home");
                }

                // Check if current user is owner
                if (recipe.user_id != currentUserId.Value)
                {
                    TempData["Error"] = "Bạn không có quyền chỉnh sửa công thức này";
                    return RedirectToAction("Detail", new { id = id });
                }

                // Load ingredients suggestions
                var ingredientNames = _supabaseService.Client
                    .From<IngredientMaster>()
                    .Select("name")
                    .Get().Result.Models
                    .Where(i => !string.IsNullOrWhiteSpace(i.name))
                    .Select(i => i.name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                var typeContents = _supabaseService.Client
                    .From<RecipeType>()
                    .Select("content")
                    .Get().Result.Models
                    .Where(t => !string.IsNullOrWhiteSpace(t.content))
                    .Select(t => t.content!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(n => n)
                    .ToList();

                ViewBag.IngredientSuggestions = ingredientNames;
                ViewBag.TypeSuggestions = typeContents;

                // Load current recipe data
                var model = new AddRecipeViewModel
                {
                    Name = recipe.name,
                    Description = recipe.description,
                    CookTime = recipe.cook_time ?? 0,
                    Level = recipe.level ?? "dễ",
                    ThumbnailUrl = recipe.thumbnail_img
                };

                // Load ingredients
                var recipeIngredients = await _supabaseService.Client
                    .From<RecipeIngredient>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                model.Ingredients = new List<string>();
                foreach (var ri in recipeIngredients.Models ?? new List<RecipeIngredient>())
                {
                    var ingredient = await _supabaseService.Client
                        .From<IngredientMaster>()
                        .Where(x => x.ingredient_id == ri.ingredient_id)
                        .Single();
                    if (ingredient != null && !string.IsNullOrEmpty(ingredient.name))
                        model.Ingredients.Add(ingredient.name);
                }

                // Load recipe types
                var recipeTypes = await _supabaseService.Client
                    .From<RecipeRecipeType>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                model.RecipeTypes = new List<string>();
                foreach (var rt in recipeTypes.Models ?? new List<RecipeRecipeType>())
                {
                    var type = await _supabaseService.Client
                        .From<RecipeType>()
                        .Where(x => x.recipe_type_id == rt.recipe_type_id)
                        .Single();
                    if (type != null && !string.IsNullOrEmpty(type.content))
                        model.RecipeTypes.Add(type.content);
                }

                // Load steps
                var steps = await _supabaseService.Client
                    .From<RecipeStep>()
                    .Where(x => x.recipe_id == id)
                    .Order("step", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                model.Steps = new List<RecipeStepViewModel>();
                foreach (var step in steps.Models ?? new List<RecipeStep>())
                {
                    var stepModel = new RecipeStepViewModel
                    {
                        StepNumber = step.step,
                        Instruction = step.instruction
                    };

                    // Load media for this step
                    var mediaLinks = await _supabaseService.Client
                        .From<RecipeStepMedia>()
                        .Where(x => x.recipe_id == id && x.step == step.step)
                        .Order("display_order", Supabase.Postgrest.Constants.Ordering.Ascending)
                        .Get();

                    stepModel.ExistingMediaUrls = new List<string>();
                    foreach (var link in mediaLinks.Models ?? new List<RecipeStepMedia>())
                    {
                        var media = await _supabaseService.Client
                            .From<Media>()
                            .Where(x => x.media_id == link.media_id)
                            .Single();
                        
                        if (media != null)
                        {
                            if (!string.IsNullOrEmpty(media.media_img))
                                stepModel.ExistingMediaUrls.Add(media.media_img);
                            else if (!string.IsNullOrEmpty(media.media_video))
                                stepModel.ExistingMediaUrls.Add(media.media_video);
                        }
                    }

                    model.Steps.Add(stepModel);
                }

                ViewBag.RecipeId = id;
                return View(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recipe for edit");
                TempData["Error"] = "Có lỗi xảy ra khi tải công thức";
                return RedirectToAction("Newsfeed", "Home");
            }
        }

        // Edit Recipe - POST
        [HttpPost]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 5368709120)]
        public async Task<IActionResult> Edit(int id, AddRecipeViewModel model)
        {
            _logger.LogInformation("=== EDIT RECIPE STARTED === ID: {RecipeId}", id);

            // ============ VALIDATION PHASE ============
            
            // 1. Model State Validation
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
                _logger.LogError("ModelState invalid: {Errors}", string.Join(", ", errors));
                TempData["Error"] = "Vui lòng điền đầy đủ thông tin! " + string.Join(", ", errors);
                ViewBag.RecipeId = id;
                return View(model);
            }

            // 2. Authentication Check
            var currentUserId = HttpContext.Session.GetInt32("UserId");
            if (!currentUserId.HasValue)
            {
                TempData["Error"] = "Vui lòng đăng nhập lại! (Session timeout)";
                return RedirectToAction("Login", "Account");
            }

            // 3. Business Logic Validation
            var validationErrors = new List<string>();

            // Check ingredients
            if (model.Ingredients == null || !model.Ingredients.Any())
            {
                validationErrors.Add("Phải có ít nhất 1 nguyên liệu");
            }

            // Check recipe types
            if (model.RecipeTypes == null || !model.RecipeTypes.Any())
            {
                validationErrors.Add("Phải chọn ít nhất 1 phân loại");
            }

            // Check steps
            if (model.Steps == null || !model.Steps.Any())
            {
                validationErrors.Add("Phải có ít nhất 1 bước thực hiện");
            }
            else
            {
                // Validate each step
                for (int i = 0; i < model.Steps.Count; i++)
                {
                    var step = model.Steps[i];
                    if (string.IsNullOrWhiteSpace(step.Instruction))
                    {
                        validationErrors.Add($"Bước {i + 1}: Vui lòng nhập mô tả");
                    }
                }
            }

            // Check cook time
            if (model.CookTime < 1 || model.CookTime > 1440)
            {
                validationErrors.Add("Thời gian nấu phải từ 1-1440 phút");
            }

            // Check level
            var validLevels = new[] { "dễ", "trung bình", "khó" };
            if (!validLevels.Contains(model.Level?.ToLower()))
            {
                validationErrors.Add("Độ khó không hợp lệ");
            }

            // 4. File Validation (if new thumbnail uploaded)
            if (model.ThumbnailImage != null)
            {
                const long maxFileSize = 10 * 1024 * 1024; // 10MB
                if (model.ThumbnailImage.Length > maxFileSize)
                {
                    validationErrors.Add("Ảnh thumbnail không được vượt quá 10MB");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(model.ThumbnailImage.FileName).ToLower();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    validationErrors.Add("Ảnh thumbnail chỉ chấp nhận định dạng: jpg, jpeg, png, gif, webp");
                }
            }

            // 5. Validate step media files
            if (model.Steps != null)
            {
                foreach (var step in model.Steps)
                {
                    if (step.StepMedia != null)
                    {
                        foreach (var file in step.StepMedia)
                        {
                            const long maxMediaSize = 50 * 1024 * 1024; // 50MB
                            if (file.Length > maxMediaSize)
                            {
                                validationErrors.Add($"File {file.FileName} vượt quá 50MB");
                            }
                        }
                    }
                }
            }

            // If validation failed, return with errors
            if (validationErrors.Any())
            {
                _logger.LogWarning("Validation failed: {Errors}", string.Join(", ", validationErrors));
                TempData["Error"] = string.Join("<br/>", validationErrors);
                ViewBag.RecipeId = id;
                
                // Reload suggestions for re-render
                var ingredientNames = _supabaseService.Client.From<IngredientMaster>().Select("name").Get().Result.Models.Where(i => !string.IsNullOrWhiteSpace(i.name)).Select(i => i.name!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
                var typeContents = _supabaseService.Client.From<RecipeType>().Select("content").Get().Result.Models.Where(t => !string.IsNullOrWhiteSpace(t.content)).Select(t => t.content!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList();
                ViewBag.IngredientSuggestions = ingredientNames;
                ViewBag.TypeSuggestions = typeContents;
                
                return View(model);
            }

            try
            {
                // 6. Recipe Existence & Ownership Check
                var recipe = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.recipe_id == id)
                    .Single();

                if (recipe == null)
                {
                    TempData["Error"] = "Không tìm thấy công thức";
                    return RedirectToAction("Newsfeed", "Home");
                }

                if (recipe.user_id != currentUserId.Value)
                {
                    TempData["Error"] = "Bạn không có quyền chỉnh sửa công thức này";
                    return RedirectToAction("Detail", new { id = id });
                }

                // ============ UPDATE PHASE (All validations passed) ============
                
                _logger.LogInformation("Starting update phase for recipe {RecipeId}", id);

                // ============ PRE-UPDATE PREPARATION (Validate all data before modifying DB) ============
                
                var preparedIngredients = new List<(string name, int id)>();
                var preparedRecipeTypes = new List<(string name, int id)>();
                var uploadedThumbnail = recipe.thumbnail_img; // Keep old by default
                
                try
                {
                    // 1. Prepare & validate thumbnail upload (if new)
                    if (model.ThumbnailImage != null)
                    {
                        _logger.LogInformation("Uploading new thumbnail...");
                        uploadedThumbnail = await _storageService.UploadFileAsync(
                            model.ThumbnailImage,
                            isVideo: false,
                            folderPath: $"recipes/{id}"
                        );
                        _logger.LogInformation("Thumbnail uploaded successfully: {Url}", uploadedThumbnail);
                    }
                    
                    // 2. Prepare ingredients (validate all can be found/created)
                    _logger.LogInformation("Preparing ingredients...");
                    foreach (var ingredientName in model.Ingredients ?? new List<string>())
                    {
                        if (string.IsNullOrWhiteSpace(ingredientName)) continue;
                        
                        var trimmedName = ingredientName.Trim();
                        var ingredientMaster = await _supabaseService.Client
                            .From<IngredientMaster>()
                            .Where(x => x.name == trimmedName)
                            .Single();
                        
                        int ingredientId;
                        if (ingredientMaster == null)
                        {
                            // Create new ingredient
                            var newIngredient = new IngredientMaster { name = trimmedName };
                            var result = await _supabaseService.Client
                                .From<IngredientMaster>()
                                .Insert(newIngredient);
                            ingredientId = result.Models.First().ingredient_id ?? 0;
                            _logger.LogInformation("Created new ingredient: {Name} (ID: {Id})", trimmedName, ingredientId);
                        }
                        else
                        {
                            ingredientId = ingredientMaster.ingredient_id ?? 0;
                        }
                        
                        preparedIngredients.Add((trimmedName, ingredientId));
                    }
                    
                    // 3. Prepare recipe types (validate all can be found/created)
                    _logger.LogInformation("Preparing recipe types...");
                    foreach (var typeName in model.RecipeTypes ?? new List<string>())
                    {
                        if (string.IsNullOrWhiteSpace(typeName)) continue;
                        
                        var trimmedType = typeName.Trim();
                        var recipeType = await _supabaseService.Client
                            .From<RecipeType>()
                            .Where(x => x.content == trimmedType)
                            .Single();
                        
                        int typeId;
                        if (recipeType == null)
                        {
                            // Create new type
                            var newType = new RecipeType { content = trimmedType };
                            var result = await _supabaseService.Client
                                .From<RecipeType>()
                                .Insert(newType);
                            typeId = result.Models.First().recipe_type_id ?? 0;
                            _logger.LogInformation("Created new recipe type: {Name} (ID: {Id})", trimmedType, typeId);
                        }
                        else
                        {
                            typeId = recipeType.recipe_type_id ?? 0;
                        }
                        
                        preparedRecipeTypes.Add((trimmedType, typeId));
                    }
                    
                    // 4. Validate steps data
                    if (model.Steps == null || !model.Steps.Any())
                    {
                        throw new InvalidOperationException("Không có bước nào để cập nhật");
                    }
                    
                    _logger.LogInformation("All preparation successful. Starting DB update...");
                }
                catch (Exception prepEx)
                {
                    _logger.LogError(prepEx, "Preparation phase failed - aborting update");
                    TempData["Error"] = $"Lỗi khi chuẩn bị dữ liệu: {prepEx.Message}";
                    ViewBag.RecipeId = id;
                    return View(model);
                }
                
                // ============ ATOMIC UPDATE (All prep successful - now update DB) ============

                recipe.name = model.Name;
                recipe.description = model.Description;
                recipe.cook_time = model.CookTime;
                recipe.level = model.Level;
                recipe.step_number = Math.Max(1, model.Steps?.Count ?? 0);
                recipe.thumbnail_img = uploadedThumbnail;

                await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.recipe_id == id)
                    .Update(recipe);

                // 2. Delete old data
                // Delete old ingredients
                var oldIngredients = await _supabaseService.Client
                    .From<RecipeIngredient>()
                    .Where(x => x.recipe_id == id)
                    .Get();
                
                foreach (var item in oldIngredients.Models ?? new List<RecipeIngredient>())
                {
                    await _supabaseService.Client
                        .From<RecipeIngredient>()
                        .Where(x => x.recipe_id == id && x.ingredient_id == item.ingredient_id)
                        .Delete();
                }

                // Delete old recipe types
                var oldTypes = await _supabaseService.Client
                    .From<RecipeRecipeType>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                foreach (var item in oldTypes.Models ?? new List<RecipeRecipeType>())
                {
                    await _supabaseService.Client
                        .From<RecipeRecipeType>()
                        .Where(x => x.recipe_id == id && x.recipe_type_id == item.recipe_type_id)
                        .Delete();
                }

                // Delete old steps and media
                var oldSteps = await _supabaseService.Client
                    .From<RecipeStep>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                foreach (var step in oldSteps.Models ?? new List<RecipeStep>())
                {
                    // Delete all media links for this step (no need to loop - 2 conditions only)
                    await _supabaseService.Client
                        .From<RecipeStepMedia>()
                        .Where(x => x.recipe_id == id && x.step == step.step)
                        .Delete();

                    // Delete step
                    await _supabaseService.Client
                        .From<RecipeStep>()
                        .Where(x => x.recipe_id == id && x.step == step.step)
                        .Delete();
                }

                // 3. Insert new ingredients (using prepared data)
                _logger.LogInformation("Inserting {Count} ingredients...", preparedIngredients.Count);
                foreach (var (name, ingredientId) in preparedIngredients)
                {
                    var recipeIngredient = new RecipeIngredient
                    {
                        recipe_id = id,
                        ingredient_id = ingredientId
                    };
                    await _supabaseService.Client
                        .From<RecipeIngredient>()
                        .Insert(recipeIngredient);
                }

                // 4. Insert new recipe types (using prepared data)
                _logger.LogInformation("Inserting {Count} recipe types...", preparedRecipeTypes.Count);
                foreach (var (name, typeId) in preparedRecipeTypes)
                {
                    var recipeRecipeType = new RecipeRecipeType
                    {
                        recipe_id = id,
                        recipe_type_id = typeId
                    };
                    await _supabaseService.Client
                        .From<RecipeRecipeType>()
                        .Insert(recipeRecipeType);
                }

                // 5. Insert new steps and media
                if (model.Steps != null && model.Steps.Any())
                {
                    foreach (var step in model.Steps)
                    {
                        var stepNumber = step.StepNumber;

                        var recipeStep = new RecipeStep
                        {
                            recipe_id = id,
                            step = stepNumber,
                            instruction = step.Instruction ?? ""
                        };

                        await _supabaseService.Client
                            .From<RecipeStep>()
                            .Insert(recipeStep);

                        int displayOrder = 1;

                        // 1. Re-insert existing media (preserve old media)
                        if (step.ExistingMediaUrls != null && step.ExistingMediaUrls.Any())
                        {
                            foreach (var existingMediaUrl in step.ExistingMediaUrls)
                            {
                                if (string.IsNullOrEmpty(existingMediaUrl)) continue;

                                // Find media_id from URL (try both img and video)
                                var existingMedia = await _supabaseService.Client
                                    .From<Media>()
                                    .Where(x => x.media_img == existingMediaUrl)
                                    .Single();
                                
                                if (existingMedia == null)
                                {
                                    existingMedia = await _supabaseService.Client
                                        .From<Media>()
                                        .Where(x => x.media_video == existingMediaUrl)
                                        .Single();
                                }

                                if (existingMedia?.media_id != null)
                                {
                                    var recipeStepMedia = new RecipeStepMedia
                                    {
                                        recipe_id = id,
                                        step = stepNumber,
                                        media_id = existingMedia.media_id.Value,
                                        display_order = displayOrder++
                                    };

                                    await _supabaseService.Client
                                        .From<RecipeStepMedia>()
                                        .Insert(recipeStepMedia);
                                }
                            }
                        }

                        // 2. Upload and insert new media
                        var mediaFiles = new List<IFormFile>();
                        if (step.StepMedia != null && step.StepMedia.Any())
                        {
                            mediaFiles.AddRange(step.StepMedia);
                        }
                        else if (step.StepImage != null)
                        {
                            mediaFiles.Add(step.StepImage);
                        }

                        if (mediaFiles.Any())
                        {
                            foreach (var mediaFile in mediaFiles)
                            {
                                var isVideo = _storageService.IsVideoFile(mediaFile);
                                
                                var mediaUrl = await _storageService.UploadFileAsync(
                                    mediaFile,
                                    isVideo: isVideo,
                                    folderPath: $"recipes/{id}/steps/{stepNumber}"
                                );

                                var media = new Media
                                {
                                    media_img = isVideo ? null : mediaUrl,
                                    media_video = isVideo ? mediaUrl : null
                                };

                                var mediaResult = await _supabaseService.Client
                                    .From<Media>()
                                    .Insert(media);

                                var createdMedia = mediaResult.Models.FirstOrDefault();
                                if (createdMedia?.media_id != null)
                                {
                                    var recipeStepMedia = new RecipeStepMedia
                                    {
                                        recipe_id = id,
                                        step = stepNumber,
                                        media_id = createdMedia.media_id.Value,
                                        display_order = displayOrder++
                                    };

                                    await _supabaseService.Client
                                        .From<RecipeStepMedia>()
                                        .Insert(recipeStepMedia);
                                }
                            }
                        }
                    }
                }

                TempData["Success"] = "Cập nhật công thức thành công!";
                return RedirectToAction("Detail", new { id = id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating recipe");
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
                ViewBag.RecipeId = id;
                return View(model);
            }
        }

        // Recipe Detail
        [HttpGet]
        public async Task<IActionResult> Detail(int id)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetInt32("UserId");
                
                // 1. Lấy thông tin Recipe
                var recipe = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.recipe_id == id)
                    .Single();

                if (recipe == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy công thức";
                    return RedirectToAction("Newsfeed", "Home");
                }

                // 2. Lấy thông tin User (tác giả)
                var author = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.user_id == recipe.user_id)
                    .Single();

                // 3. Lấy danh sách Ingredients từ Recipe_Ingredient và Ingredient_Master
                var recipeIngredientsResult = await _supabaseService.Client
                    .From<RecipeIngredient>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                var ingredients = new List<IngredientMaster>();
                foreach (var ri in recipeIngredientsResult.Models ?? new List<RecipeIngredient>())
                {
                    var ingredient = await _supabaseService.Client
                        .From<IngredientMaster>()
                        .Where(x => x.ingredient_id == ri.ingredient_id)
                        .Single();
                    if (ingredient != null)
                        ingredients.Add(ingredient);
                }

                // 4. Lấy các RecipeSteps
                var steps = await _supabaseService.Client
                    .From<RecipeStep>()
                    .Where(x => x.recipe_id == id)
                    .Order("step", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                // 5. Lấy Media cho từng bước
                var stepMedia = new Dictionary<int, List<Media>>();
                _logger.LogInformation("Loading media for recipe {RecipeId}", id);
                
                // Load TẤT CẢ RecipeStep_Media links của recipe này 1 lần
                var allMediaLinksResult = await _supabaseService.Client
                    .From<RecipeStepMedia>()
                    .Where(x => x.recipe_id == id)
                    .Order("display_order", Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();
                
                var allMediaLinks = allMediaLinksResult.Models ?? new List<RecipeStepMedia>();
                _logger.LogInformation("Total media links found for recipe: {Count}", allMediaLinks.Count);
                
                foreach (var step in steps.Models ?? new List<RecipeStep>())
                {
                    _logger.LogInformation("Loading media for step {StepNumber}", step.step);
                    
                    try
                    {
                        // Filter media links cho step này bằng LINQ
                        var stepMediaLinks = allMediaLinks
                            .Where(x => x.step == step.step)
                            .OrderBy(x => x.display_order)
                            .ToList();

                        _logger.LogInformation("Found {Count} media links for step {StepNumber}", 
                            stepMediaLinks.Count, step.step);

                        var mediaList = new List<Media>();
                        
                        if (stepMediaLinks.Any())
                        {
                            foreach (var link in stepMediaLinks)
                            {
                                _logger.LogInformation("Loading media_id {MediaId} for step {Step}", 
                                    link.media_id, step.step);
                                
                                try
                                {
                                    var media = await _supabaseService.Client
                                        .From<Media>()
                                        .Where(x => x.media_id == link.media_id)
                                        .Single();
                                    
                                    if (media != null)
                                    {
                                        mediaList.Add(media);
                                        _logger.LogInformation("Added media {MediaId}: img={HasImg}, video={HasVideo}", 
                                            link.media_id,
                                            !string.IsNullOrEmpty(media.media_img), 
                                            !string.IsNullOrEmpty(media.media_video));
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Media {MediaId} not found in database", link.media_id);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "Error loading media {MediaId}", link.media_id);
                                }
                            }
                        }
                        
                        stepMedia[step.step] = mediaList;
                        _logger.LogInformation("Step {StepNumber} has {Count} media items loaded", step.step, mediaList.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error loading media for step {StepNumber}", step.step);
                        stepMedia[step.step] = new List<Media>();
                    }
                }

                // 6. Đếm số likes
                var likes = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.recipe_id == id)
                    .Get();
                var likeCount = likes.Models?.Count ?? 0;

                // 7. Check xem user đã like chưa
                bool isLiked = false;
                if (currentUserId.HasValue)
                {
                    var userLike = await _supabaseService.Client
                        .From<likeDislike>()
                        .Where(x => x.user_id == currentUserId.Value && x.recipe_id == id)
                        .Get();
                    isLiked = userLike.Models?.Count > 0;
                }

                // 8. Lấy Comments
                var comments = await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.recipe_id == id)
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Get();

                // 9. Lấy thông tin user cho từng comment
                var commentDetails = new List<dynamic>();
                foreach (var comment in comments.Models ?? new List<Comment>())
                {
                    var commenter = await _supabaseService.Client
                        .From<User>()
                        .Where(x => x.user_id == comment.user_id)
                        .Single();

                    commentDetails.Add(new
                    {
                        Comment = comment,
                        User = commenter
                    });
                }

                // 10. Check xem user đã lưu vào notebook chưa
                bool isSaved = false;
                if (currentUserId.HasValue)
                {
                    var notebook = await _supabaseService.Client
                        .From<Notebook>()
                        .Where(x => x.user_id == currentUserId.Value && x.recipe_id == id)
                        .Get();
                    isSaved = notebook.Models?.Count > 0;
                }

                // 11. Lấy Recipe Types
                var recipeTypes = await _supabaseService.Client
                    .From<RecipeRecipeType>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                var typeNames = new List<string>();
                foreach (var rt in recipeTypes.Models ?? new List<RecipeRecipeType>())
                {
                    var type = await _supabaseService.Client
                        .From<RecipeType>()
                        .Where(x => x.recipe_type_id == rt.recipe_type_id)
                        .Single();
                    if (type != null && !string.IsNullOrEmpty(type.content))
                        typeNames.Add(type.content);
                }

                // 12. Check if current user is following the author
                bool isFollowing = false;
                bool isOwnRecipe = false;
                if (currentUserId.HasValue)
                {
                    isOwnRecipe = currentUserId.Value == author.user_id;
                    if (!isOwnRecipe)
                    {
                        try
                        {
                            var followCheck = await _supabaseService.Client
                                .From<Follow>()
                                .Where(x => x.follower_id == currentUserId.Value && x.following_id == author.user_id)
                                .Single();
                            isFollowing = followCheck != null;
                        }
                        catch
                        {
                            isFollowing = false;
                        }
                    }
                }

                // 13. Get share count
                var shareCount = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.recipe_id == id)
                    .Get();

                // 14. Tạo ViewModel
                var viewModel = new
                {
                    Recipe = recipe,
                    Author = author,
                    Ingredients = ingredients,
                    Steps = steps.Models ?? new List<RecipeStep>(),
                    StepMedia = stepMedia,
                    LikeCount = likeCount,
                    IsLiked = isLiked,
                    Comments = commentDetails,
                    CommentCount = commentDetails.Count,
                    IsSaved = isSaved,
                    TypeNames = typeNames,
                    CurrentUserId = currentUserId,
                    IsFollowing = isFollowing,
                    IsOwnRecipe = isOwnRecipe,
                    ShareCount = shareCount.Models?.Count ?? 0
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recipe detail");
                TempData["ErrorMessage"] = "Lỗi khi tải công thức: " + ex.Message;
                return RedirectToAction("Newsfeed", "Home");
            }
        }



        // Add Comment
        [HttpPost]
        public async Task<IActionResult> AddComment([FromBody] JsonElement data)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này" });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                int recipeId = data.GetProperty("recipeId").GetInt32();
                string body = data.GetProperty("body").GetString();

                if (string.IsNullOrWhiteSpace(body))
                {
                    return Json(new { success = false, message = "Nội dung bình luận không được để trống" });
                }

                var newComment = new Comment
                {
                    user_id = currentUser.user_id.Value,
                    recipe_id = recipeId,
                    body = body.Trim(),
                    created_at = DateTime.UtcNow
                };

                await _supabaseService.Client
                    .From<Comment>()
                    .Insert(newComment);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding comment");
                return Json(new { success = false, message = ex.Message });
            }
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleLike(int recipeId, bool isLiked)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này" });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                if (isLiked)
                {
                    // Unlike - remove from like_dislike table
                    await _supabaseService.Client
                        .From<likeDislike>()
                        .Where(x => x.user_id == currentUser.user_id && x.recipe_id == recipeId)
                        .Delete();
                }
                else
                {
                    // Like - add to like_dislike table
                    var like = new likeDislike
                    {
                        user_id = currentUser.user_id.Value,
                        recipe_id = recipeId,
                        body = "", // Empty body for likes
                        created_at = DateTime.UtcNow
                    };
                    await _supabaseService.Client.From<likeDislike>().Insert(like);
                }

                // Get updated like count
                var likeCount = await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();

                return Json(new { 
                    success = true, 
                    isLiked = !isLiked, 
                    likeCount = likeCount.Models.Count 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling like status");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleSave(int recipeId, bool isSaved)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này" });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null || !currentUser.user_id.HasValue)
                {
                    _logger.LogError("CurrentUser is null or user_id is null. SessionEmail: {SessionEmail}, UserId: {UserId}", 
                        sessionEmail, currentUser?.user_id);
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }
                
                _logger.LogInformation("ToggleSave - UserId: {UserId}, RecipeId: {RecipeId}", 
                    currentUser.user_id.Value, recipeId);

                // Check if already saved in database (don't trust frontend)
                var existingNotebook = await _supabaseService.Client
                    .From<Notebook>()
                    .Where(x => x.user_id == currentUser.user_id && x.recipe_id == recipeId)
                    .Get();

                bool isCurrentlySaved = existingNotebook.Models?.Count > 0;

                if (isCurrentlySaved)
                {
                    // Remove from notebook
                    await _supabaseService.Client
                        .From<Notebook>()
                        .Where(x => x.user_id == currentUser.user_id && x.recipe_id == recipeId)
                        .Delete();
                    
                    _logger.LogInformation("Removed recipe {RecipeId} from notebook for user {UserId}", recipeId, currentUser.user_id.Value);
                }
                else
                {
                    // Add to notebook
                    var notebook = new Notebook
                    {
                        user_id = currentUser.user_id.Value,
                        recipe_id = recipeId,
                        created_at = DateTime.UtcNow
                    };
                    await _supabaseService.Client.From<Notebook>().Insert(notebook);
                    
                    _logger.LogInformation("Added recipe {RecipeId} to notebook for user {UserId}", recipeId, currentUser.user_id.Value);
                }

                return Json(new { 
                    success = true, 
                    isSaved = !isCurrentlySaved 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling save status");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordShare(int recipeId)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này" });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                // Check if already shared by this user
                var existingShare = await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.user_id == currentUser.user_id && x.recipe_id == recipeId)
                    .Get();

                if (existingShare.Models?.Count == 0)
                {
                    // Add new share record
                    var share = new Share
                    {
                        user_id = currentUser.user_id.Value,
                        recipe_id = recipeId,
                        created_at = DateTime.UtcNow
                    };
                    await _supabaseService.Client.From<Share>().Insert(share);
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording share");
                return Json(new { success = false, message = "Có lỗi xảy ra: " + ex.Message });
            }
        }

        // Delete recipe
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int recipeId)
        {
            try
            {
                var sessionEmail = HttpContext.Session.GetString("user_email");
                if (string.IsNullOrEmpty(sessionEmail))
                {
                    return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này" });
                }

                var currentUser = await _supabaseService.Client
                    .From<User>()
                    .Where(x => x.email == sessionEmail)
                    .Single();

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                // Kiểm tra xem recipe có thuộc về user hiện tại không
                var recipe = await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.recipe_id == recipeId && x.user_id == currentUser.user_id)
                    .Single();

                if (recipe == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy công thức hoặc bạn không có quyền xóa" });
                }

                // Xóa các bản ghi liên quan trước
                // Xóa likes
                await _supabaseService.Client
                    .From<likeDislike>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                // Xóa comments
                await _supabaseService.Client
                    .From<Comment>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                // Xóa shares
                await _supabaseService.Client
                    .From<Share>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                // Xóa recipe types
                await _supabaseService.Client
                    .From<RecipeRecipeType>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                // Xóa recipe steps và media
                var recipeSteps = await _supabaseService.Client
                    .From<RecipeStep>()
                    .Where(x => x.recipe_id == recipeId)
                    .Get();

                foreach (var step in recipeSteps.Models ?? new List<RecipeStep>())
                {
                    // Xóa step media
                    await _supabaseService.Client
                        .From<RecipeStepMedia>()
                        .Where(x => x.step == step.step)
                        .Delete();
                }

                // Xóa recipe steps
                await _supabaseService.Client
                    .From<RecipeStep>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                // Xóa recipe ingredients (chỉ xóa link, không xóa nguyên liệu gốc)
                await _supabaseService.Client
                    .From<RecipeIngredient>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                // Cuối cùng xóa recipe
                await _supabaseService.Client
                    .From<Recipe>()
                    .Where(x => x.recipe_id == recipeId)
                    .Delete();

                return Json(new { success = true, message = "Xóa công thức thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting recipe {RecipeId}", recipeId);
                return Json(new { success = false, message = "Có lỗi xảy ra khi xóa công thức: " + ex.Message });
            }
        }

        // POST: /Recipe/ReportRecipe
        [HttpPost]
        public async Task<IActionResult> ReportRecipe([FromBody] ReportRecipeRequest request)
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
                    .Filter("email", Supabase.Postgrest.Constants.Operator.Equals, sessionEmail)
                    .Single();

                if (currentUser?.user_id == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thông tin người dùng" });
                }

                var userId = currentUser.user_id.Value;
                var recipeId = request.RecipeId;

                // Kiểm tra xem đã report chưa
                var existingReport = await _supabaseService.Client
                    .From<Report>()
                    .Where(x => x.user_id == userId && x.recipe_id == recipeId)
                    .Get();

                if (existingReport.Models.Any())
                {
                    return Json(new { success = false, message = "Bạn đã báo cáo công thức này rồi" });
                }

                // Tạo report mới
                var newReport = new Report
                {
                    user_id = userId,
                    recipe_id = recipeId,
                    body = request.Reason ?? "Không có lý do cụ thể",
                    status = "Đang xử lý",
                    created_at = DateTime.UtcNow
                };

                await _supabaseService.Client.From<Report>().Insert(newReport);

                return Json(new { success = true, message = "Báo cáo đã được gửi thành công" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting recipe {RecipeId}", request.RecipeId);
                
                // Check for duplicate key error
                if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key"))
                {
                    return Json(new { success = false, message = "Bạn đã báo cáo công thức này rồi" });
                }
                
                return Json(new { success = false, message = "Có lỗi xảy ra. Vui lòng thử lại." });
            }
        }

        // Request model for ReportRecipe
        public class ReportRecipeRequest
        {
            public int RecipeId { get; set; }
            public string? Reason { get; set; }
        }

        // TODO: Thêm các actions khác sau
        // [HttpGet] public IActionResult Edit(int id)
        // [HttpPost] public IActionResult Edit(AddRecipeViewModel model)
        // [HttpGet] public IActionResult List() // My recipes
    }
}
