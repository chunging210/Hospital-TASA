using SkiaSharp;
using TASA.Program;

namespace TASA.Services.CaptchaModule
{
    public class CaptchaService(IHttpContextAccessor httpContextAccessor) : IService
    {
        private const string SessionKey = "CaptchaCode";
        private const int CodeLength = 4;
        private const int ImageWidth = 120;
        private const int ImageHeight = 40;

        // 使用易辨識的字元（排除容易混淆的 0, O, I, l, 1）
        private static readonly char[] AllowedChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789".ToCharArray();

        /// <summary>
        /// 產生隨機驗證碼
        /// </summary>
        public string GenerateCode()
        {
            var random = new Random();
            var code = new char[CodeLength];
            for (int i = 0; i < CodeLength; i++)
            {
                code[i] = AllowedChars[random.Next(AllowedChars.Length)];
            }
            return new string(code);
        }

        /// <summary>
        /// 產生驗證碼圖片
        /// </summary>
        public byte[] GenerateImage(string code)
        {
            var random = new Random();

            using var surface = SKSurface.Create(new SKImageInfo(ImageWidth, ImageHeight));
            var canvas = surface.Canvas;

            // 背景色
            canvas.Clear(new SKColor(240, 240, 240));

            // 繪製干擾線
            using (var linePaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            })
            {
                for (int i = 0; i < 5; i++)
                {
                    linePaint.Color = new SKColor(
                        (byte)random.Next(100, 200),
                        (byte)random.Next(100, 200),
                        (byte)random.Next(100, 200)
                    );
                    canvas.DrawLine(
                        random.Next(ImageWidth), random.Next(ImageHeight),
                        random.Next(ImageWidth), random.Next(ImageHeight),
                        linePaint
                    );
                }
            }

            // 繪製干擾點
            using (var dotPaint = new SKPaint
            {
                Style = SKPaintStyle.Fill
            })
            {
                for (int i = 0; i < 50; i++)
                {
                    dotPaint.Color = new SKColor(
                        (byte)random.Next(100, 200),
                        (byte)random.Next(100, 200),
                        (byte)random.Next(100, 200)
                    );
                    canvas.DrawCircle(
                        random.Next(ImageWidth),
                        random.Next(ImageHeight),
                        1,
                        dotPaint
                    );
                }
            }

            // 繪製文字
            using var textPaint = new SKPaint
            {
                IsAntialias = true,
                TextSize = 28,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };

            float x = 10;
            foreach (char c in code)
            {
                textPaint.Color = new SKColor(
                    (byte)random.Next(0, 100),
                    (byte)random.Next(0, 100),
                    (byte)random.Next(0, 100)
                );

                // 隨機旋轉角度
                float angle = random.Next(-15, 15);
                float y = 30 + random.Next(-3, 3);

                canvas.Save();
                canvas.RotateDegrees(angle, x + 10, y);
                canvas.DrawText(c.ToString(), x, y, textPaint);
                canvas.Restore();

                x += 25;
            }

            // 轉換為 PNG 圖片
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        /// <summary>
        /// 儲存驗證碼到 Session
        /// </summary>
        public void SaveToSession(string code)
        {
            var session = httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                session.SetString(SessionKey, code.ToUpper());
            }
        }

        /// <summary>
        /// 驗證驗證碼
        /// </summary>
        public bool Validate(string inputCode)
        {
            if (string.IsNullOrWhiteSpace(inputCode))
                return false;

            var session = httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return false;

            var savedCode = session.GetString(SessionKey);
            if (string.IsNullOrEmpty(savedCode))
                return false;

            // 驗證後清除（每個驗證碼只能用一次）
            session.Remove(SessionKey);

            return string.Equals(savedCode, inputCode.ToUpper(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 產生新驗證碼並回傳圖片
        /// </summary>
        public byte[] GetCaptchaImage()
        {
            var code = GenerateCode();
            SaveToSession(code);
            return GenerateImage(code);
        }
    }
}
