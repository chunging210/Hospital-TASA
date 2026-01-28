using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;
using TASA.Services;

var builder = WebApplication.CreateBuilder(args);

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

ServiceCollectionExtension.AddImplementationScoped<IService>(builder.Services);
ServiceCollectionServiceExtensions.AddScoped<ServiceWrapper>(builder.Services);

builder.AddInfrastructure();

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseInfrastructure();
app.UseStaticFiles();
// MVC
app.MapControllerRoute(name: "default", pattern: "{controller=Auth}/{action=Index}");

app.Run();