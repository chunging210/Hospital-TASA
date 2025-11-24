using System.Security.Cryptography;
using System.Text;

namespace TASA.Extensions
{
    public static class RandomString
    {
        private const string LowercaseChars = "abcdefghjkmnpqrstuvwxyz";// 排除 l, i, o
        private const string UppercaseChars = "ABCDEFGHJKMNPQRSTUVWXYZ";// 排除 I, O
        private const string NumericChars = "23456789";                 // 排除 0, 1
        private const string SymbolChars = "@$!%*?&";

        /// <summary>
        /// 亂數字串
        /// </summary>
        public static string Generate(int length = 10, int minLowercase = 1, int minUppercase = 1, int minSymbols = 1)
        {
            // 檢查最小條件的總和是否超過總長度
            int minRequiredLength = minLowercase + minUppercase + minSymbols;
            // 在不包含數字的情況下，剩餘長度只能由小寫、大寫、符號來填補
            if (length < minRequiredLength)
            {
                throw new ArgumentException("總長度(N)必須至少等於最小必需字元總數(X+Y+Z)。");
            }

            var passwordChars = new List<char>();
            // 包含小寫字母
            passwordChars.AddRange(GenerateRandomChars(LowercaseChars, minLowercase));
            // 包含大寫字母
            passwordChars.AddRange(GenerateRandomChars(UppercaseChars, minUppercase));
            // 包含符號
            passwordChars.AddRange(GenerateRandomChars(SymbolChars, minSymbols));

            // 剩餘的長度
            int remainingLength = length - passwordChars.Count;

            // 建立所有可用字元的集合
            var allAvailableChars = new StringBuilder();
            allAvailableChars.Append(LowercaseChars);
            allAvailableChars.Append(UppercaseChars);
            allAvailableChars.Append(SymbolChars);
            allAvailableChars.Append(NumericChars);

            // 3. 填充剩餘
            if (remainingLength > 0 && allAvailableChars.Length > 0)
            {
                passwordChars.AddRange(GenerateRandomChars(allAvailableChars.ToString(), remainingLength));
            }

            // 4. 打亂順序
            for (int i = passwordChars.Count - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (passwordChars[i], passwordChars[j]) = (passwordChars[j], passwordChars[i]); // 交換
            }

            // 5. 組合為字串
            return new string([.. passwordChars]);
        }

        /// <summary>
        /// 從指定的字元集中產生指定數量的亂數字元。
        /// </summary>
        private static char[] GenerateRandomChars(string characterSet, int count)
        {
            if (count <= 0)
            {
                return [];
            }

            var chars = new char[count];
            var setLength = characterSet.Length;

            for (int i = 0; i < count; i++)
            {
                int index = RandomNumberGenerator.GetInt32(setLength);
                chars[i] = characterSet[index];
            }

            return chars;
        }
    }
}
