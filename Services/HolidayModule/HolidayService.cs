using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Program;

namespace TASA.Services.HolidayModule
{
    public class HolidayService(TASAContext db, ServiceWrapper service) : IService
    {
        // 政府開放資料 API - 中華民國政府行政機關辦公日曆表
        private const string GovApiUrl = "https://data.ntpc.gov.tw/api/datasets/308DCD75-6434-45BC-A95F-584DA4FED251/json?size=1000";

        // 備用 API（行政院人事行政總處）
        private const string BackupApiUrl = "https://cdn.jsdelivr.net/gh/ruyut/TaiwanCalendar/data/{0}.json";

        #region ViewModel

        public class HolidayVM
        {
            public Guid Id { get; set; }
            public string Date { get; set; }
            public string Name { get; set; }
            public bool IsWorkday { get; set; }
            public string Source { get; set; }
            public int Year { get; set; }
            public bool IsEnabled { get; set; }
        }

        public class CreateHolidayVM
        {
            public DateOnly Date { get; set; }
            public string Name { get; set; }
            public bool IsWorkday { get; set; } = false;
        }

        public class GovHolidayResponse
        {
            public string date { get; set; }
            public string name { get; set; }
            public string isHoliday { get; set; }
            public string holidayCategory { get; set; }
        }

        // 備用 API 的回應格式 (https://cdn.jsdelivr.net/gh/ruyut/TaiwanCalendar)
        public class BackupHolidayResponse
        {
            public string date { get; set; }        // 格式: YYYYMMDD
            public string week { get; set; }        // 星期幾
            public bool isHoliday { get; set; }     // 是否放假
            public string description { get; set; } // 假日名稱 (空字串表示普通日)
        }

        #endregion

        #region 判斷假日

        /// <summary>
        /// 判斷指定日期是否為假日（用於價格計算）
        /// 優先順序：補班日(平日價) > 國定假日(假日價) > 週六日(假日價) > 平日
        /// </summary>
        public bool IsHoliday(DateOnly date)
        {
            // 1. 先查是否為補班日（補班日要上班，使用平日價格）
            var holiday = db.SysHoliday
                .AsNoTracking()
                .FirstOrDefault(h => h.Date == date && h.IsEnabled && h.DeleteAt == null);

            if (holiday != null)
            {
                // 如果是補班日，回傳 false（使用平日價格）
                if (holiday.IsWorkday)
                    return false;

                // 如果是國定假日，回傳 true（使用假日價格）
                return true;
            }

            // 2. 沒有特別設定，判斷是否為週六日
            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>
        /// 判斷指定日期是否為假日（DateTime 版本）
        /// </summary>
        public bool IsHoliday(DateTime dateTime)
        {
            return IsHoliday(DateOnly.FromDateTime(dateTime));
        }

        #endregion

        #region CRUD

        /// <summary>
        /// 取得指定年度的假日列表
        /// </summary>
        public List<HolidayVM> GetByYear(int year)
        {
            return db.SysHoliday
                .AsNoTracking()
                .Where(h => h.Year == year && h.DeleteAt == null)
                .OrderBy(h => h.Date)
                .Select(h => new HolidayVM
                {
                    Id = h.Id,
                    Date = h.Date.ToString("yyyy-MM-dd"),
                    Name = h.Name,
                    IsWorkday = h.IsWorkday,
                    Source = h.Source,
                    Year = h.Year,
                    IsEnabled = h.IsEnabled
                })
                .ToList();
        }

        /// <summary>
        /// 手動新增假日
        /// </summary>
        public void Create(CreateHolidayVM vm)
        {
            // 檢查是否已存在
            var exists = db.SysHoliday
                .Any(h => h.Date == vm.Date && h.DeleteAt == null);

            if (exists)
                throw new HttpException($"日期 {vm.Date:yyyy-MM-dd} 已存在");

            var holiday = new SysHoliday
            {
                Id = Guid.NewGuid(),
                Date = vm.Date,
                Name = vm.Name,
                IsWorkday = vm.IsWorkday,
                Source = "Manual",
                Year = vm.Date.Year,
                IsEnabled = true,
                CreateAt = DateTime.Now
            };

            db.SysHoliday.Add(holiday);
            db.SaveChanges();

            _ = service.LogServices.LogAsync("holiday_insert", $"{vm.Date:yyyy-MM-dd} {vm.Name} (手動新增)");
        }

        /// <summary>
        /// 刪除假日
        /// </summary>
        public void Delete(Guid id)
        {
            var holiday = db.SysHoliday.FirstOrDefault(h => h.Id == id && h.DeleteAt == null)
                ?? throw new HttpException("找不到該假日");

            holiday.DeleteAt = DateTime.Now;
            db.SaveChanges();

            _ = service.LogServices.LogAsync("holiday_delete", $"{holiday.Date:yyyy-MM-dd} {holiday.Name}");
        }

        /// <summary>
        /// 切換啟用狀態
        /// </summary>
        public void ToggleEnabled(Guid id)
        {
            var holiday = db.SysHoliday.FirstOrDefault(h => h.Id == id && h.DeleteAt == null)
                ?? throw new HttpException("找不到該假日");

            holiday.IsEnabled = !holiday.IsEnabled;
            holiday.UpdateAt = DateTime.Now;
            db.SaveChanges();

            _ = service.LogServices.LogAsync("holiday_status_update", $"{holiday.Date:yyyy-MM-dd} {holiday.Name} - {(holiday.IsEnabled ? "啟用" : "停用")}");
        }

        #endregion

        #region 匯入 JSON 檔案

        /// <summary>
        /// 從上傳的 JSON 檔案匯入假日資料
        /// JSON 格式: [{ "date": "20250101", "week": "三", "isHoliday": true, "description": "開國紀念日" }, ...]
        /// </summary>
        public (int added, int updated, string message) ImportFromJson(string jsonContent)
        {
            try
            {
                var holidays = JsonSerializer.Deserialize<List<BackupHolidayResponse>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (holidays == null || holidays.Count == 0)
                {
                    throw new HttpException("JSON 檔案內容為空或格式錯誤");
                }

                int added = 0;
                int updated = 0;
                int? detectedYear = null;

                foreach (var item in holidays)
                {
                    // 只處理有 description 的日期（國定假日或補班日）
                    if (string.IsNullOrWhiteSpace(item.description))
                        continue;

                    // 解析日期 (格式: YYYYMMDD)
                    if (item.date.Length != 8)
                        continue;

                    if (!int.TryParse(item.date.Substring(0, 4), out var y) ||
                        !int.TryParse(item.date.Substring(4, 2), out var m) ||
                        !int.TryParse(item.date.Substring(6, 2), out var d))
                        continue;

                    var date = new DateOnly(y, m, d);
                    detectedYear ??= date.Year;

                    // 檢查是否已存在
                    var existing = db.SysHoliday
                        .FirstOrDefault(h => h.Date == date && h.DeleteAt == null);

                    // 判斷是否為補班日 (isHoliday = false 但有 description)
                    var isWorkday = !item.isHoliday;

                    if (existing != null)
                    {
                        // 更新現有資料
                        existing.Name = item.description;
                        existing.IsWorkday = isWorkday;
                        existing.Source = "Upload";
                        existing.UpdateAt = DateTime.Now;
                        updated++;
                    }
                    else
                    {
                        // 新增
                        db.SysHoliday.Add(new SysHoliday
                        {
                            Id = Guid.NewGuid(),
                            Date = date,
                            Name = item.description,
                            IsWorkday = isWorkday,
                            Source = "Upload",
                            Year = date.Year,
                            IsEnabled = true,
                            CreateAt = DateTime.Now
                        });
                        added++;
                    }
                }

                db.SaveChanges();

                var yearText = detectedYear.HasValue ? $"{detectedYear} 年" : "";
                var message = $"匯入完成：新增 {added} 筆，更新 {updated} 筆";
                _ = service.LogServices.LogAsync("holiday_import", $"{yearText} - {message}");

                return (added, updated, message);
            }
            catch (JsonException ex)
            {
                throw new HttpException($"JSON 格式錯誤：{ex.Message}");
            }
            catch (Exception ex) when (ex is not HttpException)
            {
                _ = service.LogServices.LogAsync("holiday_import_failed", ex.Message);
                throw new HttpException($"匯入失敗：{ex.Message}");
            }
        }

        /// <summary>
        /// 從政府 API 同步假日資料
        /// </summary>
        public async Task<(int added, int updated, string message)> SyncFromGovApi(int year)
        {
            try
            {
                var holidays = await FetchFromBackupApi(year);

                if (holidays == null || holidays.Count == 0)
                {
                    throw new HttpException($"無法取得 {year} 年的假日資料，請確認該年度資料是否已發布");
                }

                int added = 0;
                int updated = 0;

                foreach (var item in holidays)
                {
                    // 只處理有 description 的日期（國定假日或補班日）
                    if (string.IsNullOrWhiteSpace(item.description))
                        continue;

                    // 解析日期 (格式: YYYYMMDD)
                    if (item.date.Length != 8)
                        continue;

                    if (!int.TryParse(item.date.Substring(0, 4), out var y) ||
                        !int.TryParse(item.date.Substring(4, 2), out var m) ||
                        !int.TryParse(item.date.Substring(6, 2), out var d))
                        continue;

                    var date = new DateOnly(y, m, d);

                    // 檢查是否已存在
                    var existing = await db.SysHoliday
                        .FirstOrDefaultAsync(h => h.Date == date && h.DeleteAt == null);

                    // 判斷是否為補班日 (isHoliday = false 但有 description)
                    var isWorkday = !item.isHoliday;

                    if (existing != null)
                    {
                        // 更新現有資料
                        existing.Name = item.description;
                        existing.IsWorkday = isWorkday;
                        existing.Source = "GovApi";
                        existing.UpdateAt = DateTime.Now;
                        updated++;
                    }
                    else
                    {
                        // 新增
                        db.SysHoliday.Add(new SysHoliday
                        {
                            Id = Guid.NewGuid(),
                            Date = date,
                            Name = item.description,
                            IsWorkday = isWorkday,
                            Source = "GovApi",
                            Year = date.Year,
                            IsEnabled = true,
                            CreateAt = DateTime.Now
                        });
                        added++;
                    }
                }

                await db.SaveChangesAsync();

                var message = $"同步完成：新增 {added} 筆，更新 {updated} 筆";
                _ = service.LogServices.LogAsync("holiday_sync", $"{year} 年 - {message}");

                return (added, updated, message);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpException($"無法連線至政府 API：{ex.Message}");
            }
            catch (Exception ex) when (ex is not HttpException)
            {
                _ = service.LogServices.LogAsync("holiday_sync_failed", $"{year} 年 - {ex.Message}");
                throw new HttpException($"同步失敗：{ex.Message}");
            }
        }

        /// <summary>
        /// 從備用 API 取得假日資料（較穩定）
        /// </summary>
        private async Task<List<BackupHolidayResponse>> FetchFromBackupApi(int year)
        {
            using var httpClient = new HttpClient();
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            var url = string.Format(BackupApiUrl, year);
            var response = await httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"API 回應錯誤：{response.StatusCode}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<List<BackupHolidayResponse>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return data ?? new List<BackupHolidayResponse>();
        }

        /// <summary>
        /// 檢查指定年度是否已有資料
        /// </summary>
        public bool HasDataForYear(int year)
        {
            return db.SysHoliday
                .AsNoTracking()
                .Any(h => h.Year == year && h.DeleteAt == null);
        }

        /// <summary>
        /// 取得已有資料的年度列表
        /// </summary>
        public List<int> GetAvailableYears()
        {
            return db.SysHoliday
                .AsNoTracking()
                .Where(h => h.DeleteAt == null)
                .Select(h => h.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToList();
        }

        #endregion
    }
}
