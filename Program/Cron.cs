using NCrontab;

namespace TASA.Program
{
    public class Cron
    {
        public static int GetDelayMilliseconds(string cronExpression)
        {
            try
            {
                var expression = CrontabSchedule.Parse(cronExpression);
                var nextRunTime = expression.GetNextOccurrence(DateTime.Now);
                var timeDifference = nextRunTime - DateTime.Now;
                return (int)timeDifference.TotalMilliseconds;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"處理 Cron 表達式時發生錯誤: {ex.Message}");
                throw;
            }
        }
    }
}
