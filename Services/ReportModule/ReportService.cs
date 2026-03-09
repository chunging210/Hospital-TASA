using Microsoft.EntityFrameworkCore;
using TASA.Models;
using TASA.Models.Enums;
using TASA.Program;
using TASA.Extensions;
using TASA.Services;
using ClosedXML.Excel;

namespace TASA.Services.ReportModule
{
    public class ReportService(TASAContext db) : IService
    {
        public class QueryVM : BaseQueryVM
        {
            public DateTime? StartDate { get; set; }
            public DateTime? EndDate { get; set; }
            public Guid? RoomId { get; set; }
            public string? PaymentMethod { get; set; }     // 繳款方式篩選
            public string? DepartmentCode { get; set; }    // 部門代碼篩選（成本分攤用）
            public string? Columns { get; set; }           // 匯出欄位（逗號分隔）
        }

        public class ReportItemVM
        {
            public Guid Id { get; set; }
            public string BookingNo { get; set; } = "";        // 預約單號
            public string ConferenceName { get; set; } = "";
            public string DateRange { get; set; } = "";        // 日期（合併顯示）
            public string RoomName { get; set; } = "";
            public int TotalAmount { get; set; }
            public string PaymentMethodText { get; set; } = "";
            public int? ExpectedAttendees { get; set; }
            public string BorrowingUnitName { get; set; } = ""; // 借用單位（成本分攤用）
        }

        public class ReportSummaryVM
        {
            public int TotalCount { get; set; }
            public int GrandTotal { get; set; }
            public int TotalAttendees { get; set; }
        }

        /// <summary>
        /// 取得有使用過的部門代碼列表（成本分攤用，只抓已付款的）
        /// </summary>
        public List<string> GetDepartmentCodes()
        {
            return db.Conference
                .AsNoTracking()
                .WhereNotDeleted()
                .Where(c => c.PaymentStatus == PaymentStatus.Paid)  // 只抓已付款
                .Where(c => c.PaymentMethod == "cost-sharing")       // 只抓成本分攤
                .Where(c => c.DepartmentCode != null && c.DepartmentCode != "")
                .Select(c => c.DepartmentCode!)
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        public IQueryable<ReportItemVM> List(QueryVM query)
        {
            // 權限控制由 TASAContext.Filters.cs 的 Global Query Filter 處理
            var q = db.Conference.AsNoTracking()
                .Include(c => c.ConferenceRoomSlots)
                    .ThenInclude(s => s.Room)
                .WhereNotDeleted();

            // 只顯示已收款的紀錄
            q = q.Where(c => c.PaymentStatus == PaymentStatus.Paid);

            // 篩選：繳款方式
            if (!string.IsNullOrEmpty(query.PaymentMethod))
            {
                q = q.Where(c => c.PaymentMethod == query.PaymentMethod);
            }

            // 篩選：部門代碼（成本分攤用）
            if (!string.IsNullOrEmpty(query.DepartmentCode))
            {
                q = q.Where(c => c.DepartmentCode == query.DepartmentCode);
            }

            // 篩選：起始日期（會議日期 >= 起始日期）
            if (query.StartDate.HasValue)
            {
                var startDate = DateOnly.FromDateTime(query.StartDate.Value);
                q = q.Where(c => c.ConferenceRoomSlots.Min(s => s.SlotDate) >= startDate);
            }

            // 篩選：結束日期（會議日期 <= 結束日期）
            if (query.EndDate.HasValue)
            {
                var endDate = DateOnly.FromDateTime(query.EndDate.Value);
                q = q.Where(c => c.ConferenceRoomSlots.Max(s => s.SlotDate) <= endDate);
            }

            // 篩選：會議室
            if (query.RoomId.HasValue)
            {
                q = q.Where(c => c.ConferenceRoomSlots.Any(s => s.RoomId == query.RoomId.Value));
            }

            return q.OrderByDescending(c => c.CreateAt)
                .Select(c => new ReportItemVM
                {
                    Id = c.Id,
                    BookingNo = c.Id.ToString().Substring(0, 8).ToUpper(),
                    ConferenceName = c.Name ?? "",
                    DateRange = c.ConferenceRoomSlots.Any()
                        ? (c.ConferenceRoomSlots.Min(s => s.SlotDate) == c.ConferenceRoomSlots.Max(s => s.SlotDate)
                            ? c.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd")
                            : c.ConferenceRoomSlots.Min(s => s.SlotDate).ToString("yyyy/MM/dd") + " ~ " +
                              c.ConferenceRoomSlots.Max(s => s.SlotDate).ToString("yyyy/MM/dd"))
                        : "-",
                    RoomName = c.ConferenceRoomSlots
                        .Select(s => s.Room != null ? s.Room.Name : "-")
                        .FirstOrDefault() ?? "-",
                    TotalAmount = c.TotalAmount,
                    PaymentMethodText = c.PaymentMethod == "cash" ? "現金付款" :
                                        c.PaymentMethod == "transfer" ? "銀行匯款" :
                                        c.PaymentMethod == "cost-sharing" ? "成本分攤" : "-",
                    ExpectedAttendees = c.ExpectedAttendees,
                    BorrowingUnitName = c.DepartmentCode != null
                        ? db.CostCenter.Where(cc => cc.Code == c.DepartmentCode).Select(cc => cc.Name).FirstOrDefault() ?? "-"
                        : "-"
                });
        }

        public ReportSummaryVM GetSummary(QueryVM query)
        {
            var items = List(query).ToList();
            return new ReportSummaryVM
            {
                TotalCount = items.Count,
                GrandTotal = items.Sum(x => x.TotalAmount),
                TotalAttendees = items.Sum(x => x.ExpectedAttendees ?? 0)
            };
        }

        public byte[] ExportExcel(QueryVM query)
        {
            var data = List(query).ToList();

            // 解析要匯出的欄位
            var columns = string.IsNullOrEmpty(query.Columns)
                ? new HashSet<string> { "bookingNo", "borrowingUnit", "conferenceName", "dateRange", "roomName", "paymentMethod", "attendees", "amount" }
                : new HashSet<string>(query.Columns.Split(','));

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("會議報表");

            // 標題列
            var col = 1;
            int? attendeesCol = null;
            int? amountCol = null;

            if (columns.Contains("bookingNo")) worksheet.Cell(1, col++).Value = "預約單號";
            if (columns.Contains("borrowingUnit")) worksheet.Cell(1, col++).Value = "借用單位";
            if (columns.Contains("conferenceName")) worksheet.Cell(1, col++).Value = "會議名稱";
            if (columns.Contains("dateRange")) worksheet.Cell(1, col++).Value = "會議日期";
            if (columns.Contains("roomName")) worksheet.Cell(1, col++).Value = "會議室";
            if (columns.Contains("paymentMethod")) worksheet.Cell(1, col++).Value = "繳款方式";
            if (columns.Contains("attendees")) { attendeesCol = col; worksheet.Cell(1, col++).Value = "人數"; }
            if (columns.Contains("amount")) { amountCol = col; worksheet.Cell(1, col++).Value = "金額"; }

            var totalCols = col - 1;
            if (totalCols == 0) totalCols = 1; // 至少一欄

            // 標題樣式
            var headerRange = worksheet.Range(1, 1, 1, totalCols);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

            // 資料列
            for (int i = 0; i < data.Count; i++)
            {
                var row = i + 2;
                col = 1;
                if (columns.Contains("bookingNo")) worksheet.Cell(row, col++).Value = data[i].BookingNo;
                if (columns.Contains("borrowingUnit")) worksheet.Cell(row, col++).Value = data[i].BorrowingUnitName;
                if (columns.Contains("conferenceName")) worksheet.Cell(row, col++).Value = data[i].ConferenceName;
                if (columns.Contains("dateRange")) worksheet.Cell(row, col++).Value = data[i].DateRange;
                if (columns.Contains("roomName")) worksheet.Cell(row, col++).Value = data[i].RoomName;
                if (columns.Contains("paymentMethod")) worksheet.Cell(row, col++).Value = data[i].PaymentMethodText;
                if (columns.Contains("attendees")) worksheet.Cell(row, col++).Value = data[i].ExpectedAttendees?.ToString() ?? "-";
                if (columns.Contains("amount")) worksheet.Cell(row, col++).Value = data[i].TotalAmount;
            }

            // 加總列
            var summaryRow = data.Count + 2;
            worksheet.Cell(summaryRow, 1).Value = "合計";
            if (totalCols >= 2) worksheet.Cell(summaryRow, 2).Value = $"共 {data.Count} 筆";
            if (attendeesCol.HasValue) worksheet.Cell(summaryRow, attendeesCol.Value).Value = data.Sum(x => x.ExpectedAttendees ?? 0);
            if (amountCol.HasValue) worksheet.Cell(summaryRow, amountCol.Value).Value = data.Sum(x => x.TotalAmount);

            // 加總列樣式
            var summaryRange = worksheet.Range(summaryRow, 1, summaryRow, totalCols);
            summaryRange.Style.Font.Bold = true;
            summaryRange.Style.Fill.BackgroundColor = XLColor.LightYellow;

            // 自動調整欄寬
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
}
