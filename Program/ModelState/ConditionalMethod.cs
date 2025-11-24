using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace TASA.Program.ModelState
{
    public class ConditionalMethod
    {
        public static bool IsRequired(string? conditionalMethodName, ValidationContext validationContext)
        {
            if (string.IsNullOrEmpty(conditionalMethodName))
            {
                return true;
            }

            // 取得條件方法
            var methodInfo = validationContext.ObjectType.GetMethod(conditionalMethodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            // 檢查方法是否存在
            if (methodInfo == null)
            {
                throw new ArgumentException($"在型別 '{validationContext.ObjectType.Name}' 中找不到名為 '{conditionalMethodName}' 的方法。");
            }

            // 檢查方法的回傳型別
            if (methodInfo.ReturnType != typeof(bool))
            {
                throw new InvalidOperationException($"條件方法 '{conditionalMethodName}' 必須回傳 bool 型別。");
            }

            return (bool)methodInfo.Invoke(validationContext.ObjectInstance, null)!;
        }
    }
}
