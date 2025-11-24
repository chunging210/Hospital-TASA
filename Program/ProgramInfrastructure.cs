using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using TASA.Program.JsonConverters;
using TASA.Program.ModelState;
using TASA.Services.ConferenceModule;

namespace TASA.Program
{
    public static class ProgramInfrastructure
    {
        private static readonly string AllowAnyOrigin = "AllowAnyOrigin";
        public static WebApplicationBuilder AddInfrastructure(this WebApplicationBuilder builder)
        {
            // 加入CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(AllowAnyOrigin, builder => builder
                    .SetIsOriginAllowed(origin => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            // 加入Jwt
            builder.Services.AddJwt(builder.Configuration.GetSection("Jwt"));

            // Jwt + Cookie
            builder.Services
                .AddAuthentication(configure =>
                {
                    configure.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    configure.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                })
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
                {
                    options.LoginPath = "/";
                })
                .AddJwtBearer(Jwt.Bearer);

            // 加入IHttpContextAccessor
            builder.Services.AddHttpContextAccessor();

            builder.Services
                // ResponseException->ObjectResult
                .AddControllers(options => options.Filters.Add(new HttpExceptionAttribute()))
                // ModelState 錯誤訊息轉換
                .ConfigureApiBehaviorOptions(options => { options.InvalidModelStateResponseFactory = ModelStateResult.ToBadRequestObject; })
                .AddJsonOptions(options =>
                {
                    // 使用原始屬性名稱
                    options.JsonSerializerOptions.PropertyNamingPolicy = null;
                    // 加入 Nullable Json 轉換
                    options.JsonSerializerOptions.Converters.Add(new NullableConverterFactory());
                    // 加入 DateTime(UTC) 轉換
                    options.JsonSerializerOptions.Converters.Add(new DateTimeConverter());
                    options.JsonSerializerOptions.Converters.Add(new NullableDateTimeConverter());
                });

            // 加入排程
            builder.Services.AddHostedService<StatusChangeBackgroundService>();
            builder.Services.AddHostedService<RefreshTokenBackgroundService>();

            return builder;
        }

        public static WebApplication UseInfrastructure(this WebApplication app)
        {
            // 處理錯誤
            if (app.Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage(); // 確保錯誤信息能夠在開發環境顯示
            }

            // CORS
            app.UseCors(AllowAnyOrigin);

            // HTTPS 轉向
            app.UseHttpsRedirection();

            // 靜態文件
            app.UseDefaultFiles();
            // 靜態文件快取
            app.UseStaticFiles(new StaticFileCacheControl());

            // 啟用路由
            app.UseRouting();

            // 身份驗證與授權
            app.UseAuthentication();
            app.UseAuthorization();

            // 路由匹配
            app.MapControllers();

            return app;
        }
    }
}
