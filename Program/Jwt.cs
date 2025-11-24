using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace TASA.Program
{
    public static class Jwt
    {
        private static string? Issuer { get; set; }
        private static SymmetricSecurityKey? Key { get; set; }
        private static string? TokenCookie { get; set; }
        private static string NameCookie { get; set; } = "n";
        private static string ExpCookie { get; set; } = "e";
        private static int ExpirationMinutes { get; set; }

        /// <summary>
        /// Jwt 初始化
        /// </summary>
        public static IServiceCollection AddJwt(this IServiceCollection services, IConfigurationSection jwtConfig)
        {
            Issuer = jwtConfig["issuer"] ?? throw new NullReferenceException("Jwt:issuer not configured");
            var secret = jwtConfig["secret"] ?? throw new NullReferenceException("Jwt:secret not configured");
            Key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            TokenCookie = jwtConfig["cookieName"] ?? throw new NullReferenceException("Jwt:cookieName not configured");
            ExpirationMinutes = jwtConfig.GetValue<int>("expirationMinutes");
            return services;
        }

        /// <summary>
        /// Jwt 配置
        /// </summary>
        public static void Bearer(JwtBearerOptions options)
        {
            // 配置如何驗證 Jwt
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = Key,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
            // 設定 Jwt 驗證服務
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // 先嘗試從 Authorization 標頭中讀取 Bearer Token
                    var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();

                    // 如果標頭中沒有 Token，則從 Cookie 中讀取
                    if (string.IsNullOrEmpty(token))
                    {
                        token = context.Request.Cookies[TokenCookie!];
                    }

                    context.Token = token;
                    return Task.CompletedTask;
                },
                OnTokenValidated = context =>
                {
                    var token = context.Request.Headers.Authorization.FirstOrDefault()?.Split(" ").Last();
                    if (!string.IsNullOrEmpty(token))
                    {
                        // 刷新 Jwt Authorization
                        var currentToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
                        context.Response.Headers.Append("jwt", Generate(currentToken.Claims));
                    }
                    if (context.Request.Cookies.Keys.Contains(TokenCookie!))
                    {
                        // 刷新 Jwt CooKie
                        var currentToken = new JwtSecurityTokenHandler().ReadJwtToken(context.Request.Cookies[TokenCookie!]);
                        GenerateCookie(context.Response.Cookies, currentToken.Claims);
                    }
                    return Task.CompletedTask;
                }
            };
        }

        private static string GenerateToken(DateTime expires, IEnumerable<Claim> claims)
        {
            var signingCredentials = new SigningCredentials(Key, SecurityAlgorithms.HmacSha256Signature);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: expires,
                issuer: Issuer,
                signingCredentials: signingCredentials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 產生 Jwt Token
        /// </summary>
        public static string Generate(IEnumerable<Claim> claims)
        {
            var expires = DateTime.Now.AddMinutes(ExpirationMinutes);
            return GenerateToken(expires, claims);
        }

        /// <summary>
        /// Response 加入 Jwt Cookie
        /// </summary>
        public static void GenerateCookie(IResponseCookies cookies, IEnumerable<Claim> claims)
        {
            var expires = DateTime.Now.AddMinutes(ExpirationMinutes);
            var jwt = GenerateToken(expires, claims);
            cookies.Append(TokenCookie!, jwt, new CookieOptions
            {
                HttpOnly = true,
                //Secure = true,
                Expires = expires,
                Path = "/",
                SameSite = SameSiteMode.Lax
            });
            cookies.Append(NameCookie, claims.FirstOrDefault(x => x.Type == "name")?.Value ?? "", new CookieOptions
            {
                HttpOnly = false,
                //Secure = true,
                Expires = expires,
                Path = "/",
                SameSite = SameSiteMode.Lax
            });
            cookies.Append(ExpCookie, new DateTimeOffset(expires).ToUnixTimeMilliseconds().ToString(), new CookieOptions
            {
                HttpOnly = false,
                //Secure = true,
                Expires = expires,
                Path = "/",
                SameSite = SameSiteMode.Lax
            });
        }

        /// <summary>
        /// Response 移除 Jwt Cookie
        /// </summary>
        public static void DeleteCookie(IResponseCookies cookies)
        {
            cookies.Delete(TokenCookie!);
            cookies.Delete(NameCookie!);
            cookies.Delete(ExpCookie!);
        }
    }
}
