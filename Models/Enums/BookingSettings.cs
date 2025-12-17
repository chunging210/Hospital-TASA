namespace TASA.Models.Enums
{
    public enum BookingSettings
    {
        InternalOnly = 0,   // 僅限內部
        InternalAndExternal = 1, // 內外皆可
        Closed = 2,         // 不開放租借
        Free = 3            // 免費使用
    }
}
