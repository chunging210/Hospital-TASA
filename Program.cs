using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;
using TASA.Services;

var builder = WebApplication.CreateBuilder(args);

var optionsAction = (DbContextOptionsBuilder options) =>
{
    var connectionString = builder.Configuration.GetConnectionString("dbconnection");
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString));
};
builder.Services.AddDbContextFactory<TASAContext>(optionsAction);
builder.Services.AddDbContext<TASAContext>(optionsAction);

builder.Services.AddLazyResolution();
ServiceCollectionServiceExtensions.AddScoped<TASAContext>(builder.Services);
ServiceCollectionExtension.AddImplementationScoped<IService>(builder.Services);
ServiceCollectionServiceExtensions.AddScoped<ServiceWrapper>(builder.Services);

builder.AddInfrastructure();

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.UseInfrastructure();

// MVC
app.MapControllerRoute(name: "default", pattern: "{controller=Auth}/{action=Index}");

app.Run();