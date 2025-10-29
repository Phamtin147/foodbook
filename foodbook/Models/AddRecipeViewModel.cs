using System.ComponentModel.DataAnnotations;

namespace foodbook.Models
{
    public class AddRecipeViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập tên công thức")]
        [Display(Name = "Tên công thức")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Ảnh hoặc Video chính")]
        public IFormFile? MainMedia { get; set; }

        [Display(Name = "Ảnh thumbnail")]
        public IFormFile? ThumbnailImage { get; set; }

        // For Edit: existing thumbnail URL
        public string? ThumbnailUrl { get; set; }

        [Display(Name = "Mô tả nguyên liệu")]
        public string? Description { get; set; }

        [Display(Name = "Nguyên liệu")]
        public List<string> Ingredients { get; set; } = new List<string>();

        [Display(Name = "Phân loại")]
        public List<string> RecipeTypes { get; set; } = new List<string>();

        [Required(ErrorMessage = "Vui lòng nhập thời gian nấu")]
        [Range(1, 1440, ErrorMessage = "Thời gian nấu phải từ 1-1440 phút (24 giờ)")]
        [Display(Name = "Thời gian nấu (phút)")]
        public int CookTime { get; set; } = 30; // Default 30 phút

        [Required(ErrorMessage = "Vui lòng chọn độ khó")]
        [Display(Name = "Độ khó")]
        public string Level { get; set; } = "dễ";

        [Display(Name = "Các bước thực hiện")]
        public List<RecipeStepViewModel> Steps { get; set; } = new List<RecipeStepViewModel>();
    }

    public class RecipeStepViewModel
    {
        public int StepNumber { get; set; }
        
        // Không required vì có thể chỉ có ảnh mà không có text
        public string? Instruction { get; set; }
        
        // Hỗ trợ nhiều ảnh/video cho mỗi step
        public List<IFormFile>? StepMedia { get; set; } = new List<IFormFile>();
        
        // Giữ lại để backward compatible với view cũ
        public IFormFile? StepImage { get; set; }

        // For Edit: existing media URLs for this step
        public List<string>? ExistingMediaUrls { get; set; } = new List<string>();
    }
}

