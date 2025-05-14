using Microsoft.EntityFrameworkCore;
using PlantShop.Data;
using PlantShop.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add DbContext
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add Custom Services
builder.Services.AddScoped<PythonRecommenderService>();

// Add Authentication - Đơn giản hóa chỉ kiểm tra đăng nhập
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.Cookie.Name = "PlantShop.Auth";
        options.Cookie.HttpOnly = true;
    });

// Add Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Xử lý database một lần duy nhất
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        await context.Database.EnsureDeletedAsync();
        Console.WriteLine("✅ Đã xóa database cũ");

        context.Database.EnsureCreated();
        Console.WriteLine("✅ Đã tạo database mới");

        DbSeeder.Seed(context);
        Console.WriteLine("✅ Đã thêm dữ liệu mẫu thành công!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Lỗi: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

if (args.Contains("seed"))
{
    using (var scope = app.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate(); // Đảm bảo đã migrate
        DbSeeder.Seed(context);
        Console.WriteLine("Database seeded thành công!");
    }
    return;
}

app.Run();
