# Hướng dẫn Testing cho Foodbook

## 📋 Tổng quan

Dự án Foodbook sử dụng **xUnit** để testing với **36 test cases** bao phủ Controllers và Models.

### Công nghệ sử dụng
- **xUnit** - Framework testing
- **FluentAssertions** - Assertions dễ đọc
- **System.ComponentModel.DataAnnotations** - Model validation testing

## 🏗️ Cấu trúc Test Project

```
foodbook.Tests/
├── AccountControllerTests.cs    (9 test cases)
├── RecipeControllerTests.cs     (10 test cases)
├── ProfileControllerTests.cs    (7 test cases)
└── ModelsTests.cs               (10 test cases)
```

**Tổng cộng: 36 test cases** ✅

## 🚀 Chạy Tests

### 1. Chạy tất cả tests

```bash
dotnet test foodbook/foodbook.Tests/foodbook.Tests.csproj
```

### 2. Chạy tests với output chi tiết

```bash
dotnet test foodbook/foodbook.Tests/foodbook.Tests.csproj --verbosity detailed
```

### 3. Chạy tests với code coverage

```bash
dotnet test foodbook/foodbook.Tests/foodbook.Tests.csproj /p:CollectCoverage=true
```

### 4. Chạy một test class cụ thể

```bash
dotnet test --filter "FullyQualifiedName~AccountControllerTests"
```

### 5. Chạy một test method cụ thể

```bash
dotnet test --filter "FullyQualifiedName~Login_GET_ReturnsViewWithModel"
```

## 📝 Chi tiết Test Cases

### AccountControllerTests (9 tests)

Test validation cho các ViewModels của Account:

1. ✅ `LoginViewModel_WithValidData_ShouldBeValid` - Kiểm tra LoginViewModel với dữ liệu hợp lệ
2. ✅ `LoginViewModel_WithEmptyEmailOrPhone_ShouldBeInvalid` - Kiểm tra LoginViewModel thiếu email/phone
3. ✅ `LoginViewModel_WithEmptyPassword_ShouldBeInvalid` - Kiểm tra LoginViewModel thiếu password
4. ✅ `RegisterViewModel_WithValidData_ShouldBeValid` - Kiểm tra RegisterViewModel với dữ liệu hợp lệ
5. ✅ `RegisterViewModel_WithMismatchedPasswords_ShouldBeInvalid` - Kiểm tra password không khớp
6. ✅ `RegisterViewModel_WithInvalidEmail_ShouldBeInvalid` - Kiểm tra email không hợp lệ
7. ✅ `ChangePasswordViewModel_WithValidData_ShouldBeValid` - Kiểm tra ChangePasswordViewModel hợp lệ
8. ✅ `ChangePasswordViewModel_WithMismatchedPasswords_ShouldBeInvalid` - Kiểm tra password mới không khớp
9. ✅ `ForgotPasswordViewModel_WithValidEmail_ShouldBeValid` - Kiểm tra ForgotPasswordViewModel hợp lệ

### RecipeControllerTests (10 tests)

Test Recipe Model và AddRecipeViewModel:

1. ✅ `Recipe_Model_HasRequiredProperties` - Kiểm tra Recipe model có đủ properties
2. ✅ `Recipe_Level_ShouldBeValid` - Kiểm tra level của recipe hợp lệ (dễ, trung bình, khó)
3. ✅ `Recipe_Status_DefaultsToActive` - Kiểm tra status mặc định là "active"
4. ✅ `AddRecipeViewModel_WithValidData_ShouldBeValid` - Kiểm tra AddRecipeViewModel hợp lệ
5. ✅ `AddRecipeViewModel_WithEmptyName_ShouldBeInvalid` - Kiểm tra validation tên recipe
6. ✅ `AddRecipeViewModel_WithInvalidCookTime_ShouldBeInvalid` - Kiểm tra validation cook time
7. ✅ `AddRecipeViewModel_ValidatesCookTime` - Kiểm tra cook time phải > 0
8. ✅ `AddRecipeViewModel_CanHaveMultipleIngredients` - Kiểm tra recipe có nhiều ingredients
9. ✅ `AddRecipeViewModel_CanHaveMultipleSteps` - Kiểm tra recipe có nhiều steps
10. ✅ `AddRecipeViewModel_CanHaveMultipleRecipeTypes` - Kiểm tra recipe có nhiều types

### ProfileControllerTests (7 tests)

Test User Model và ProfileViewModel:

1. ✅ `User_Model_HasRequiredProperties` - Kiểm tra User model có đủ properties cần thiết
2. ✅ `User_DefaultRole_ShouldBeUser` - Kiểm tra default role là "user"
3. ✅ `User_Email_ShouldBeValid` - Kiểm tra email hợp lệ (có @ và domain)
4. ✅ `User_CanHaveAvatar` - Kiểm tra user có thể có avatar URL
5. ✅ `User_CanHaveBio` - Kiểm tra user có thể có bio
6. ✅ `ProfileViewModel_CanTrackFollowers` - Kiểm tra tracking followers/following count
7. ✅ `Follow_Model_HasRequiredRelationships` - Kiểm tra Follow model có đủ relationship IDs

### ModelsTests (10 tests)

Test các Models khác:

1. ✅ `Recipe_CreatedAt_ShouldBeSet` - Kiểm tra Recipe có created_at timestamp
2. ✅ `Comment_Model_HasRequiredProperties` - Kiểm tra Comment model (user_id, recipe_id, body)
3. ✅ `LikeDislike_Model_TracksUserInteraction` - Kiểm tra LikeDislike model tracking "like"/"dislike"
4. ✅ `Notebook_Model_SavesUserRecipe` - Kiểm tra Notebook model lưu recipe của user
5. ✅ `Share_Model_TracksSharing` - Kiểm tra Share model tracking việc share recipe
6. ✅ `Report_Model_HasStatusAndBody` - Kiểm tra Report model có status và body
7. ✅ `Ingredient_Model_HasName` - Kiểm tra Ingredient model có tên
8. ✅ `RecipeType_Model_HasContent` - Kiểm tra RecipeType model có content
9. ✅ `RecipeStep_Model_HasInstructionAndOrder` - Kiểm tra RecipeStep có instruction và order
10. ✅ `Media_Model_CanBeImageOrVideo` - Kiểm tra Media model có thể là image hoặc video

## 🔄 GitHub Actions CI/CD

Tests tự động chạy khi:
- Push code lên branch `main`
- Tạo Pull Request vào branch `main`

### Workflow Steps

1. **Checkout code** - Lấy code từ repository
2. **Setup .NET 8.0 & 9.0** - Cài đặt .NET SDK
3. **Restore dependencies** - Khôi phục dependencies của project chính
4. **Build project** - Build project chính (Release mode)
5. **Restore test dependencies** - Khôi phục dependencies của test project
6. **Build tests** - Build test project (Release mode)
7. **Run tests with coverage** - Chạy tests với code coverage collection
8. **Publish Test Results** - Tạo test report với dorny/test-reporter
9. **Upload Test Results** - Upload test results artifacts (TRX files)
10. **Upload Code Coverage** - Upload coverage reports (Cobertura XML)

### Permissions

Workflow được cấp permissions sau:
- `contents: read` - Đọc code từ repository
- `checks: write` - Tạo check runs và annotations
- `pull-requests: write` - Comment test results vào PR

### Test Reports

GitHub Actions tự động tạo các reports:

1. **Test Summary** - Hiển thị trong PR checks
   - ✅ Tổng số tests passed/failed
   - ⏱️ Thời gian chạy
   - 📊 Test coverage percentage

2. **Detailed Report** - Xem trong Actions tab
   - Chi tiết từng test case
   - Stack traces cho failed tests
   - Test execution timeline

3. **Artifacts** - Download được:
   - `test-results` - TRX files với chi tiết tests
   - `code-coverage` - Coverage reports (XML format)
   - Lưu giữ trong 30 ngày

### Xem kết quả tests trên GitHub

#### Cách 1: Trong Pull Request

1. Tạo Pull Request
2. Scroll xuống phần **Checks**
3. Xem **Test Results** check
4. Click "Details" để xem chi tiết

#### Cách 2: Trong Actions Tab

1. Vào repository trên GitHub
2. Click tab **Actions**
3. Chọn workflow run mới nhất
4. Xem các sections:
   - **Summary** - Tổng quan
   - **Test Results** - Chi tiết tests
   - **Artifacts** - Download reports

#### Cách 3: Download Artifacts

1. Vào Actions → Chọn workflow run
2. Scroll xuống phần **Artifacts**
3. Download:
   - `test-results.zip` - TRX files
   - `code-coverage.zip` - Coverage XML files

### Test Coverage Report

Để xem coverage local:

```bash
# Chạy tests với coverage
dotnet test foodbook/foodbook.Tests/foodbook.Tests.csproj \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Generate HTML report
reportgenerator \
  -reports:"./TestResults/*/coverage.cobertura.xml" \
  -targetdir:"./TestResults/CoverageReport" \
  -reporttypes:Html

# Open report
start ./TestResults/CoverageReport/index.html
```

## 🛠️ Viết Tests mới

### Ví dụ 1: Test Model Properties

```csharp
[Fact]
public void Model_Property_ShouldMeetCriteria()
{
    // Arrange & Act
    var model = new Recipe 
    { 
        recipe_id = 1,
        user_id = 1,
        name = "Test Recipe",
        level = "dễ"
    };

    // Assert
    model.recipe_id.Should().Be(1);
    model.name.Should().Be("Test Recipe");
    model.level.Should().Be("dễ");
}
```

### Ví dụ 2: Test Model Validation

```csharp
[Fact]
public void LoginViewModel_WithEmptyEmail_ShouldBeInvalid()
{
    // Arrange
    var model = new LoginViewModel
    {
        EmailOrPhone = "",
        Password = "password123"
    };

    // Act
    var validationResults = ValidateModel(model);

    // Assert
    validationResults.Should().NotBeEmpty();
    validationResults.Should().Contain(v => v.MemberNames.Contains("EmailOrPhone"));
}

// Helper method
private IList<ValidationResult> ValidateModel(object model)
{
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(model, null, null);
    Validator.TryValidateObject(model, validationContext, validationResults, true);
    return validationResults;
}
```

### Ví dụ 3: Test Collections và Lists

```csharp
[Fact]
public void AddRecipeViewModel_CanHaveMultipleIngredients()
{
    // Arrange
    var model = new AddRecipeViewModel
    {
        Name = "Test Recipe",
        CookTime = 30,
        Ingredients = new List<string> { "Ingredient 1", "Ingredient 2", "Ingredient 3" }
    };

    // Assert
    model.Ingredients.Should().HaveCount(3);
    model.Ingredients.Should().Contain("Ingredient 1");
    model.Ingredients.First().Should().Be("Ingredient 1");
}
```

## 📊 Best Practices

### ✅ DO (Nên làm)

- ✅ Đặt tên test rõ ràng: `Model_Scenario_ExpectedResult`
- ✅ Sử dụng pattern AAA: Arrange, Act, Assert
- ✅ Test Model validation và business logic
- ✅ Test một điều trong mỗi test case
- ✅ Sử dụng FluentAssertions cho assertions dễ đọc
- ✅ Test cả success và failure scenarios
- ✅ Test với dữ liệu hợp lệ và không hợp lệ
- ✅ Verify validation messages khi test fail scenarios

### ❌ DON'T (Không nên làm)

- ❌ Test quá nhiều thứ trong 1 test case
- ❌ Test implementation details thay vì behavior
- ❌ Copy-paste tests mà không hiểu
- ❌ Ignore failing tests
- ❌ Đặt tên test không rõ ràng
- ❌ Quên test edge cases (null, empty, boundary values)

## 🐛 Debug Tests

### VS Code

1. Mở test file
2. Click vào dấu debug bên cạnh test method
3. Hoặc nhấn F5 để debug

### Visual Studio

1. Right-click vào test method
2. Chọn "Debug Test"

### Command Line

```bash
# Debug với detailed output
dotnet test --logger "console;verbosity=detailed"
```

## 📈 Code Coverage

### Cài đặt coverage tool

```bash
dotnet tool install --global coverlet.console
```

### Chạy với coverage

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Xem coverage report

```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:coverage.opencover.xml -targetdir:coveragereport
```

## 🔧 Troubleshooting

### Lỗi: "Validation failed but no errors in result"

**Giải pháp**: Đảm bảo validate object với flag `validateAllProperties` = true:
```csharp
Validator.TryValidateObject(model, validationContext, validationResults, true);
```

### Lỗi: "FluentAssertions comparison failed"

**Giải pháp**: Kiểm tra các properties có giá trị chính xác:
```csharp
// Thay vì
model.CreatedAt.Should().Be(DateTime.UtcNow);

// Nên dùng
model.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
```

### Tests fail trên GitHub Actions nhưng pass local

**Giải pháp**: 
- Kiểm tra file paths (case-sensitive trên Linux)
- Kiểm tra timezone và datetime
- Đảm bảo dùng `BeCloseTo()` cho datetime assertions
- Kiểm tra line endings (CRLF vs LF)

## 📚 Tài liệu tham khảo

- [xUnit Documentation](https://xunit.net/)
- [FluentAssertions Documentation](https://fluentassertions.com/)
- [ASP.NET Core Testing](https://docs.microsoft.com/en-us/aspnet/core/test/)
- [Data Annotations Validation](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation)

## 🤝 Đóng góp

Khi thêm code mới, vui lòng:

1. Viết tests cho code mới
2. Đảm bảo tất cả tests pass
3. Maintain test coverage > 70%
4. Follow naming conventions

---

**Happy Testing! 🎉**

