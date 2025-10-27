# FoodBook - Ứng dụng Chia Sẻ Công Thức Nấu Ăn

[![.NET CI/CD](https://github.com/Phamtin147/foodbook/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Phamtin147/foodbook/actions/workflows/dotnet.yml)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Tests](https://img.shields.io/badge/tests-36_passing-success)](TESTING.md)

## 📖 Giới thiệu / Introduction

**FoodBook** là một ứng dụng web ASP.NET Core cho phép người dùng chia sẻ, khám phá và quản lý các công thức nấu ăn. Ứng dụng được xây dựng với kiến trúc MVC và sử dụng Supabase làm backend database và storage.

**FoodBook** is a web application built with ASP.NET Core that allows users to share, discover, and manage cooking recipes. The application is built with MVC architecture and uses Supabase as the backend database and storage.

## 🚀 Tính năng chính / Key Features

### 👤 Quản lý người dùng / User Management
- **Đăng ký/Đăng nhập** với xác thực email
- **Quản lý hồ sơ** cá nhân với avatar và thông tin bio
- **Đổi mật khẩu** và quên mật khẩu
- **Phân quyền** người dùng (user, admin, moderator)

### 🍳 Quản lý công thức / Recipe Management
- **Tạo công thức** với nhiều bước và media
- **Upload ảnh/video** cho từng bước nấu ăn
- **Quản lý nguyên liệu** và phân loại món ăn
- **Tìm kiếm và lọc** công thức theo nhiều tiêu chí
- **Bảng tin** hiển thị công thức mới nhất

### 📱 Giao diện người dùng / User Interface
- **Responsive design** tương thích mobile
- **Bootstrap** cho UI components
- **Infinite scroll** cho bảng tin
- **Upload multiple files** cho media

### 🔧 Quản trị / Administration
- **Dashboard** thống kê tổng quan
- **Quản lý người dùng** (ban/unban)
- **Kiểm duyệt nội dung** và báo cáo
- **Quản lý phân loại** và nguyên liệu

## 🛠️ Công nghệ sử dụng / Technology Stack

### Backend
- **ASP.NET Core 8.0** - Web framework
- **C#** - Programming language
- **MVC Pattern** - Architecture pattern
- **Supabase** - Backend-as-a-Service (Database + Storage)
- **PostgREST** - API layer

### Frontend
- **Razor Views** - Server-side rendering
- **Bootstrap 5** - CSS framework
- **jQuery** - JavaScript library
- **HTML5/CSS3** - Markup and styling

### Services
- **MailKit** - Email service
- **MimeKit** - Email formatting
- **Supabase Client** - Database operations

## 📁 Cấu trúc dự án / Project Structure

```
foodbook/
├── Controllers/           # MVC Controllers
│   ├── AccountController.cs    # Authentication & user management
│   ├── AdminController.cs      # Admin panel functionality
│   ├── HomeController.cs       # Home page & newsfeed
│   ├── RecipeController.cs     # Recipe CRUD operations
│   └── ...
├── Models/               # Data models
│   ├── User.cs                 # User entity
│   ├── Recipe.cs               # Recipe entity
│   ├── Ingredient.cs           # Ingredient entity
│   ├── RecipeStep.cs           # Recipe step entity
│   └── ...
├── Services/             # Business logic services
│   ├── SupabaseService.cs      # Database operations
│   ├── StorageService.cs       # File upload/storage
│   └── EmailService.cs         # Email functionality
├── Views/                # Razor views
│   ├── Account/                # Authentication views
│   ├── Admin/                  # Admin panel views
│   ├── Home/                   # Home & newsfeed views
│   └── ...
├── wwwroot/              # Static files
│   ├── css/                   # Stylesheets
│   ├── js/                    # JavaScript files
│   └── images/                # Static images
└── Attributes/           # Custom attributes
    ├── LoginRequiredAttribute.cs
    └── AdminRequiredAttribute.cs
```

## 🗄️ Cơ sở dữ liệu / Database Schema

### Bảng chính / Main Tables

#### User
- `user_id` (Primary Key)
- `username`, `email`, `password`
- `full_name`, `avatar_img`, `bio`
- `role`, `status`, `is_verified`
- `created_at`

#### Recipe
- `recipe_id` (Primary Key)
- `user_id` (Foreign Key)
- `name`, `description`, `thumbnail_img`
- `cook_time`, `level`, `step_number`
- `created_at`

#### Ingredient
- `ingredient_id` (Primary Key)
- `recipe_id` (Foreign Key)
- `name`, `created_at`

#### RecipeStep
- `recipe_id` (Foreign Key)
- `step`, `instruction`

#### Media
- `media_id` (Primary Key)
- `media_img`, `media_video`

#### RecipeStepMedia (Junction Table)
- `recipe_id`, `step`, `media_id`
- `display_order`

#### RecipeType
- `recipe_type_id` (Primary Key)
- `content`, `created_at`

#### Comment
- `comment_id` (Primary Key)
- `user_id`, `recipe_id`
- `body`, `created_at`

#### likeDislike
- `ld_id` (Primary Key)
- `user_id`, `recipe_id`
- `body` (like/dislike)

## ⚙️ Cài đặt và chạy / Installation & Setup

### Yêu cầu hệ thống / Prerequisites
- **.NET 8.0 SDK**
- **Visual Studio 2022** hoặc **VS Code**
- **Supabase account** (cho database và storage)

### Các bước cài đặt / Installation Steps

1. **Clone repository**
```bash
git clone <repository-url>
cd foodbook
```

2. **Cài đặt dependencies**
```bash
dotnet restore
```

3. **Cấu hình Supabase**
   - Tạo project mới trên [Supabase](https://supabase.com)
   - Lấy URL và API keys
   - Cập nhật `appsettings.json`:

```json
{
  "Supabase": {
    "Url": "YOUR_SUPABASE_URL",
    "AnonKey": "YOUR_SUPABASE_ANON_KEY",
    "ServiceKey": "YOUR_SUPABASE_SERVICE_KEY"
  }
}
```

4. **Cấu hình Email Service**
   - Cập nhật thông tin SMTP trong `appsettings.json`:

```json
{
  "EmailSettings": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 465,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "FromEmail": "your-email@gmail.com",
    "FromName": "Foodbook"
  }
}
```

5. **Tạo database tables**
   - Chạy script SQL trong file `Basesql.sql`
   - Tạo storage buckets: `img` và `videos`

6. **Chạy ứng dụng**
```bash
dotnet run
```

## 🔧 Cấu hình môi trường / Environment Configuration

### Development
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

### Production
- Sử dụng environment variables cho sensitive data
- Cấu hình HTTPS và security headers
- Setup logging và monitoring

## 📱 API Endpoints

### Authentication
- `GET/POST /Account/Login` - Đăng nhập
- `GET/POST /Account/Register` - Đăng ký
- `GET /Account/Logout` - Đăng xuất
- `GET/POST /Account/ForgotPassword` - Quên mật khẩu

### Recipe Management
- `GET /Recipe/Add` - Form thêm công thức
- `POST /Recipe/Add` - Tạo công thức mới
- `GET /Home/Newsfeed` - Bảng tin công thức
- `GET /Home/LoadMoreRecipes` - API infinite scroll

### Admin Panel
- `GET /Admin/Dashboard` - Dashboard tổng quan
- `GET /Admin/UserManagement` - Quản lý người dùng
- `GET /Admin/ContentModeration` - Kiểm duyệt nội dung
- `GET /Admin/CategoryManagement` - Quản lý phân loại

## 🔐 Bảo mật / Security Features

- **Session-based authentication**
- **Email verification** cho tài khoản mới
- **Password reset** với token hết hạn
- **Role-based authorization** (user/admin)
- **CSRF protection** với AntiForgeryToken
- **File upload validation**

## 🧪 Testing

### Test Coverage
- **36 test cases** covering Controllers and Models
- **xUnit** framework với **FluentAssertions**
- **Automated testing** trên GitHub Actions

### Running Tests

```bash
# Run all tests
dotnet test foodbook/foodbook.Tests/foodbook.Tests.csproj

# Run with detailed output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### Test Reports

GitHub Actions tự động tạo test reports cho mỗi commit và pull request:
- ✅ Test summary trong PR checks
- 📊 Code coverage reports
- 📁 Downloadable artifacts (TRX files)

Xem chi tiết tại [TESTING.md](TESTING.md)

## 🚀 Deployment

### Docker
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
COPY . /app
WORKDIR /app
EXPOSE 80
ENTRYPOINT ["dotnet", "foodbook.dll"]
```

### Render.com
- File `render.yaml` đã được cấu hình sẵn
- Tự động deploy từ GitHub repository

## 🤝 Đóng góp / Contributing

1. Fork repository
2. Tạo feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Tạo Pull Request

## 📄 License

Distributed under the MIT License. See `LICENSE` for more information.

## 📞 Liên hệ / Contact

- **Email**: huhume147@gmail.com
- **Project Link**: [https://github.com/Phamtin147/foodbook](https://github.com/Phamtin147/foodbook)

## 🙏 Acknowledgments

- **Supabase** - Backend services
- **Bootstrap** - UI framework
- **ASP.NET Core** - Web framework
- **MailKit** - Email functionality

---

**Lưu ý**: Đây là phiên bản demo của ứng dụng FoodBook. Một số tính năng có thể cần được cải thiện cho môi trường production.

**Note**: This is a demo version of the FoodBook application. Some features may need improvement for production environment.
