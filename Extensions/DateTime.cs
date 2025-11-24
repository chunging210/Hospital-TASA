namespace TASA.Extensions
{
    public static partial class Extension
    {
        /// <summary>
        /// 設定時間
        /// </summary>
        public static DateTime Set(this DateTime source, int? year = null, int? month = null, int? day = null, int? hour = null, int? minute = null, int? second = null)
        {
            return new DateTime(
                  year ?? source.Year,
                  month ?? source.Month,
                  day ?? source.Day,
                  hour ?? source.Hour,
                  minute ?? source.Minute,
                  second ?? source.Second,
                  0
              );
        }
    }
}
