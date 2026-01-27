namespace TASA.Models.Auth
{
    /// <summary>
    /// 系統在「一次 Request 中」認定的使用者身分與資料邊界
    /// ⚠️ 不是資料庫 Entity
    /// ⚠️ 不會出現在 DbContext
    /// </summary>
    public class UserContext
    {
        public Guid UserId { get; init; }

        public Guid? DepartmentId { get; init; }
        public string? DepartmentName { get; init; }

        public bool IsAdmin { get; init; }
        public bool IsDirector { get; init; }
        public bool IsAccountant { get; init; }
    }
}
