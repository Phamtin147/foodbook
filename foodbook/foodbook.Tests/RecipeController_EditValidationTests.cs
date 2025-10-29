using Xunit;
using foodbook.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;

namespace foodbook.Tests
{
    /// <summary>
    /// Unit tests for Recipe Edit business logic validation
    /// Tests validation rules without requiring database/service mocking
    /// </summary>
    public class RecipeController_EditValidationTests
    {
        #region Ingredients Validation

        [Fact]
        public void Validation_WithoutIngredients_ShouldFail()
        {
            // Arrange
            var model = CreateValidModel();
            model.Ingredients = new List<string>();

            // Act
            var isValid = ValidateIngredients(model.Ingredients);

            // Assert
            isValid.Should().BeFalse("Recipe must have at least 1 ingredient");
        }

        [Fact]
        public void Validation_WithNullIngredients_ShouldFail()
        {
            // Arrange
            var model = CreateValidModel();
            model.Ingredients = null;

            // Act
            var isValid = ValidateIngredients(model.Ingredients);

            // Assert
            isValid.Should().BeFalse("Null ingredients should fail validation");
        }

        [Fact]
        public void Validation_WithValidIngredients_ShouldPass()
        {
            // Arrange
            var ingredients = new List<string> { "Tomato", "Onion", "Garlic" };

            // Act
            var isValid = ValidateIngredients(ingredients);

            // Assert
            isValid.Should().BeTrue();
        }

        #endregion

        #region Recipe Types Validation

        [Fact]
        public void Validation_WithoutRecipeTypes_ShouldFail()
        {
            // Arrange
            var model = CreateValidModel();
            model.RecipeTypes = new List<string>();

            // Act
            var isValid = ValidateRecipeTypes(model.RecipeTypes);

            // Assert
            isValid.Should().BeFalse("Recipe must have at least 1 type");
        }

        [Fact]
        public void Validation_WithValidRecipeTypes_ShouldPass()
        {
            // Arrange
            var types = new List<string> { "Món chính", "Món Á" };

            // Act
            var isValid = ValidateRecipeTypes(types);

            // Assert
            isValid.Should().BeTrue();
        }

        #endregion

        #region Steps Validation

        [Fact]
        public void Validation_WithoutSteps_ShouldFail()
        {
            // Arrange
            var model = CreateValidModel();
            model.Steps = new List<RecipeStepViewModel>();

            // Act
            var isValid = ValidateSteps(model.Steps);

            // Assert
            isValid.Should().BeFalse("Recipe must have at least 1 step");
        }

        [Fact]
        public void Validation_WithEmptyStepInstruction_ShouldFail()
        {
            // Arrange
            var steps = new List<RecipeStepViewModel>
            {
                new RecipeStepViewModel { StepNumber = 1, Instruction = "" }
            };

            // Act
            var (isValid, errorMessage) = ValidateStepInstructions(steps);

            // Assert
            isValid.Should().BeFalse();
            errorMessage.Should().Contain("Bước 1");
        }

        [Fact]
        public void Validation_WithWhitespaceOnlyInstruction_ShouldFail()
        {
            // Arrange
            var steps = new List<RecipeStepViewModel>
            {
                new RecipeStepViewModel { StepNumber = 1, Instruction = "   " }
            };

            // Act
            var (isValid, errorMessage) = ValidateStepInstructions(steps);

            // Assert
            isValid.Should().BeFalse();
            errorMessage.Should().Contain("Bước 1");
        }

        [Fact]
        public void Validation_WithValidSteps_ShouldPass()
        {
            // Arrange
            var steps = new List<RecipeStepViewModel>
            {
                new RecipeStepViewModel { StepNumber = 1, Instruction = "Cut the vegetables" },
                new RecipeStepViewModel { StepNumber = 2, Instruction = "Heat the pan" }
            };

            // Act
            var (isValid, _) = ValidateStepInstructions(steps);

            // Assert
            isValid.Should().BeTrue();
        }

        #endregion

        #region Cook Time Validation

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void Validation_WithInvalidCookTime_ShouldFail(int cookTime)
        {
            // Act
            var isValid = ValidateCookTime(cookTime);

            // Assert
            isValid.Should().BeFalse($"Cook time {cookTime} should be invalid");
        }

        [Theory]
        [InlineData(1441)]
        [InlineData(2000)]
        [InlineData(int.MaxValue)]
        public void Validation_WithCookTimeTooLarge_ShouldFail(int cookTime)
        {
            // Act
            var isValid = ValidateCookTime(cookTime);

            // Assert
            isValid.Should().BeFalse($"Cook time {cookTime} exceeds 1440 minutes");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(120)]
        [InlineData(1440)]
        public void Validation_WithValidCookTime_ShouldPass(int cookTime)
        {
            // Act
            var isValid = ValidateCookTime(cookTime);

            // Assert
            isValid.Should().BeTrue($"Cook time {cookTime} should be valid");
        }

        #endregion

        #region Level Validation

        [Theory]
        [InlineData("dễ")]
        [InlineData("Dễ")]
        [InlineData("DỄ")]
        [InlineData("trung bình")]
        [InlineData("Trung Bình")]
        [InlineData("khó")]
        [InlineData("Khó")]
        public void Validation_WithValidLevel_ShouldPass(string level)
        {
            // Act
            var isValid = ValidateLevel(level);

            // Assert
            isValid.Should().BeTrue($"Level '{level}' should be valid");
        }

        [Theory]
        [InlineData("easy")]
        [InlineData("impossible")]
        [InlineData("")]
        [InlineData(null)]
        [InlineData("very hard")]
        public void Validation_WithInvalidLevel_ShouldFail(string? level)
        {
            // Act
            var isValid = ValidateLevel(level);

            // Assert
            isValid.Should().BeFalse($"Level '{level}' should be invalid");
        }

        #endregion

        #region File Validation

        [Fact]
        public void Validation_ThumbnailExceeds10MB_ShouldFail()
        {
            // Arrange
            var mockFile = CreateMockFile("image.jpg", 11 * 1024 * 1024); // 11MB

            // Act
            var isValid = ValidateThumbnailSize(mockFile);

            // Assert
            isValid.Should().BeFalse("Thumbnail exceeds 10MB limit");
        }

        [Fact]
        public void Validation_ThumbnailWithin10MB_ShouldPass()
        {
            // Arrange
            var mockFile = CreateMockFile("image.jpg", 5 * 1024 * 1024); // 5MB

            // Act
            var isValid = ValidateThumbnailSize(mockFile);

            // Assert
            isValid.Should().BeTrue();
        }

        [Theory]
        [InlineData("image.jpg")]
        [InlineData("photo.jpeg")]
        [InlineData("pic.png")]
        [InlineData("banner.gif")]
        [InlineData("thumb.webp")]
        public void Validation_ThumbnailWithValidExtension_ShouldPass(string fileName)
        {
            // Arrange
            var mockFile = CreateMockFile(fileName, 1024);

            // Act
            var isValid = ValidateThumbnailFormat(mockFile);

            // Assert
            isValid.Should().BeTrue($"File '{fileName}' should have valid extension");
        }

        [Theory]
        [InlineData("file.exe")]
        [InlineData("doc.pdf")]
        [InlineData("image.bmp")]
        [InlineData("video.mp4")]
        public void Validation_ThumbnailWithInvalidExtension_ShouldFail(string fileName)
        {
            // Arrange
            var mockFile = CreateMockFile(fileName, 1024);

            // Act
            var isValid = ValidateThumbnailFormat(mockFile);

            // Assert
            isValid.Should().BeFalse($"File '{fileName}' should have invalid extension");
        }

        [Fact]
        public void Validation_StepMediaExceeds50MB_ShouldFail()
        {
            // Arrange
            var mockFile = CreateMockFile("video.mp4", 51 * 1024 * 1024); // 51MB

            // Act
            var isValid = ValidateStepMediaSize(mockFile);

            // Assert
            isValid.Should().BeFalse("Step media exceeds 50MB limit");
        }

        [Fact]
        public void Validation_StepMediaWithin50MB_ShouldPass()
        {
            // Arrange
            var mockFile = CreateMockFile("video.mp4", 30 * 1024 * 1024); // 30MB

            // Act
            var isValid = ValidateStepMediaSize(mockFile);

            // Assert
            isValid.Should().BeTrue();
        }

        #endregion

        #region Helper Methods - Validation Logic (Mirrors RecipeController)

        private bool ValidateIngredients(List<string>? ingredients)
        {
            return ingredients != null && ingredients.Any();
        }

        private bool ValidateRecipeTypes(List<string>? types)
        {
            return types != null && types.Any();
        }

        private bool ValidateSteps(List<RecipeStepViewModel>? steps)
        {
            return steps != null && steps.Any();
        }

        private (bool isValid, string? errorMessage) ValidateStepInstructions(List<RecipeStepViewModel> steps)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(steps[i].Instruction))
                {
                    return (false, $"Bước {i + 1}: Vui lòng nhập mô tả");
                }
            }
            return (true, null);
        }

        private bool ValidateCookTime(int cookTime)
        {
            return cookTime >= 1 && cookTime <= 1440;
        }

        private bool ValidateLevel(string? level)
        {
            if (string.IsNullOrWhiteSpace(level)) return false;
            var validLevels = new[] { "dễ", "trung bình", "khó" };
            return validLevels.Contains(level.ToLower());
        }

        private bool ValidateThumbnailSize(IFormFile file)
        {
            const long maxSize = 10 * 1024 * 1024; // 10MB
            return file.Length <= maxSize;
        }

        private bool ValidateThumbnailFormat(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            return allowedExtensions.Contains(extension);
        }

        private bool ValidateStepMediaSize(IFormFile file)
        {
            const long maxSize = 50 * 1024 * 1024; // 50MB
            return file.Length <= maxSize;
        }

        private AddRecipeViewModel CreateValidModel()
        {
            return new AddRecipeViewModel
            {
                Name = "Test Recipe",
                Description = "Test Description",
                CookTime = 30,
                Level = "dễ",
                Ingredients = new List<string> { "Ingredient 1", "Ingredient 2" },
                RecipeTypes = new List<string> { "Món chính" },
                Steps = new List<RecipeStepViewModel>
                {
                    new RecipeStepViewModel
                    {
                        StepNumber = 1,
                        Instruction = "Step 1 instruction"
                    }
                }
            };
        }

        private IFormFile CreateMockFile(string fileName, long size)
        {
            var mockFile = new Mock<IFormFile>();
            mockFile.Setup(f => f.FileName).Returns(fileName);
            mockFile.Setup(f => f.Length).Returns(size);
            return mockFile.Object;
        }

        #endregion
    }
}

