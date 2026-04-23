using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;
using TASA.Services;
using TASA.Services.ConferenceModule;

var builder = WebApplication.CreateBuilder(args);

// 允許大檔案上傳（影片）
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 2L * 1024 * 1024 * 1024; // 2GB
});

var connectionString = builder.Configuration.GetConnectionString("dbconnection");

// ✅ 1. 先註冊 HttpContextAccessor
builder.Services.AddHttpContextAccessor();

// ✅ 2. 註冊 DbContextFactory (給需要 Factory 的 Service 用)
builder.Services.AddDbContextFactory<TASAContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

// ✅ 3. 註冊 DbContext (給一般 HTTP Request 用)
builder.Services.AddDbContext<TASAContext>(options =>
{
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
});

builder.Services.AddLazyResolution();
builder.Services.AddHostedService<ReservationAutoManagementService>();
builder.Services.AddHostedService<PaymentReminderBackgroundService>();

ServiceCollectionExtension.AddImplementationScoped<IService>(builder.Services);
ServiceCollectionServiceExtensions.AddScoped<ServiceWrapper>(builder.Services);

builder.AddInfrastructure();

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseInfrastructure();


app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' cdn.jsdelivr.net cdnjs.cloudflare.com; " +
        "style-src 'self' 'unsafe-inline' cdn.jsdelivr.net cdnjs.cloudflare.com fonts.googleapis.com; " +
        "font-src 'self' fonts.gstatic.com cdnjs.cloudflare.com; " +
        "img-src 'self' data: blob:;");
    await next();
});

app.UseStaticFiles();
// MVC
app.MapControllerRoute(name: "default", pattern: "{controller=Auth}/{action=Index}");

await app.RunAsync();