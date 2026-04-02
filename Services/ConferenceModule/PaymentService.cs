using Microsoft.EntityFrameworkCore;
using TASA.Extensions;
using TASA.Models;
using TASA.Program;
using TASA.Models.Enums;

namespace TASA.Services.ConferenceModule
{
    public class PaymentService(TASAContext db, ServiceWrapper service, IWebHostEnvironment env) : IService
    {
        private readonly string _uploadPath = Path.Combine(env.WebRootPath, "uploads", "payment-proofs");

        public class UploadCounterVM
        {
            public List<string> ReservationIds { get; set; } = new();
            public List<IFormFile> Files { get; set; } = new();
            public string? Note { get; set; }
            public IFormFile? DiscountProofFile { get; set; }  // 優惠證明
        }


        public class TransferPaymentVM
        {
            public List<string> ReservationIds { get; set; } = new();
            public string Last5 { get; set; } = string.Empty;
            public int Amount { get; set; }
            public DateTime? TransferAt { get; set; }
            public string? Note { get; set; }
            public IFormFile? DiscountProofFile { get; set; }  // 優惠證明
            public IFormFile? ScreenshotFile { get; set; }     // 匯款截圖
        }

        public class ApprovePaymentVM
        {
            public Guid OrderId { get; set; }
        }

        public class RejectPaymentVM
        {
            public Guid OrderId { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        /// <summary>
        /// ✅ 上傳臨櫃付款憑證（建立合併付款訂單）
        /// </summary>
        public async Task<Guid> UploadCounterProof(UploadCounterVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            if (vm.Files == null || vm.Files.Count == 0)
                throw new HttpException("請上傳至少一個檔案");

            if (vm.ReservationIds == null || vm.ReservationIds.Count == 0)
                throw new HttpException("沒有選擇任何預約");

            // 確保上傳目錄存在
            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);

            // 先處理優惠證明
            string? discountProofPath = null;
            string? discountProofName = null;
            if (vm.DiscountProofFile != null && vm.DiscountProofFile.Length > 0)
            {
                var discountProofId = Guid.NewGuid();
                var discountExt = Path.GetExtension(vm.DiscountProofFile.FileName);
                var discountFileName = $"discount_{discountProofId}{discountExt}";
                using (var stream = new FileStream(Path.Combine(_uploadPath, discountFileName), FileMode.Create))
                    await vm.DiscountProofFile.CopyToAsync(stream);
                discountProofPath = $"/uploads/payment-proofs/{discountFileName}";
                discountProofName = vm.DiscountProofFile.FileName;
            }

            // 儲存第一個檔案作為付款憑證（臨櫃只取第一個）
            var mainFile = vm.Files[0];
            var mainProofId = Guid.NewGuid();
            var mainExt = Path.GetExtension(mainFile.FileName);
            var mainFileName = $"{mainProofId}{mainExt}";
            using (var stream = new FileStream(Path.Combine(_uploadPath, mainFileName), FileMode.Create))
                await mainFile.CopyToAsync(stream);

            // 建立合併付款訂單
            var orderId = Guid.NewGuid();
            var order = new ConferencePaymentOrder
            {
                Id = orderId,
                PaymentMethod = "臨櫃",
                FilePath = $"/uploads/payment-proofs/{mainFileName}",
                FileName = mainFile.FileName,
                Note = vm.Note,
                Status = PaymentOrderStatus.PendingVerification,
                DiscountProofPath = discountProofPath,
                DiscountProofName = discountProofName,
                UploadedAt = DateTime.Now,
                UploadedBy = userId,
                CreateAt = DateTime.Now,
                CreateBy = userId
            };
            db.ConferencePaymentOrder.Add(order);

            // 找到所有對應的會議並建立訂單明細
            foreach (var reservationNo in vm.ReservationIds)
            {
                var conference = await db.Conference
                    .Where(c => c.Id.ToString().StartsWith(reservationNo))
                    .FirstOrDefaultAsync()
                    ?? throw new HttpException($"找不到預約單號: {reservationNo}");

                if (conference.ReservationStatus != ReservationStatus.PendingPayment)
                    throw new HttpException($"預約單 {reservationNo} 不在待繳費狀態");

                db.ConferencePaymentOrderItem.Add(new ConferencePaymentOrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ConferenceId = conference.Id,
                    CreateAt = DateTime.Now
                });

                conference.PaymentStatus = PaymentStatus.PendingVerification;
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("payment_voucher",
                $"上傳臨櫃憑證 - 預約單: {string.Join(", ", vm.ReservationIds)}, 訂單: {orderId}");

            // 寄送通知給總務
            foreach (var reservationNo in vm.ReservationIds)
            {
                var conf = await db.Conference
                    .Where(c => c.Id.ToString().StartsWith(reservationNo))
                    .FirstOrDefaultAsync();
                if (conf != null)
                    service.ConferenceMail.PaymentProofUploaded(conf.Id, "臨櫃繳費");
            }

            return orderId;
        }

        /// <summary>
        /// ✅ 提交匯款資訊（建立合併付款訂單）
        /// </summary>
        public async Task<Guid> SubmitTransferInfo(TransferPaymentVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            if (string.IsNullOrEmpty(vm.Last5) || vm.Last5.Length != 5)
                throw new HttpException("請輸入正確的5碼轉帳末碼");

            if (vm.Amount <= 0)
                throw new HttpException("請輸入正確的金額");

            if (vm.ReservationIds == null || vm.ReservationIds.Count == 0)
                throw new HttpException("沒有選擇任何預約");

            // 確保上傳目錄存在
            if (!Directory.Exists(_uploadPath))
                Directory.CreateDirectory(_uploadPath);

            // 先處理優惠證明
            string? discountProofPath = null;
            string? discountProofName = null;
            if (vm.DiscountProofFile != null && vm.DiscountProofFile.Length > 0)
            {
                var discountProofId = Guid.NewGuid();
                var discountExt = Path.GetExtension(vm.DiscountProofFile.FileName);
                var discountFileName = $"discount_{discountProofId}{discountExt}";
                using (var stream = new FileStream(Path.Combine(_uploadPath, discountFileName), FileMode.Create))
                    await vm.DiscountProofFile.CopyToAsync(stream);
                discountProofPath = $"/uploads/payment-proofs/{discountFileName}";
                discountProofName = vm.DiscountProofFile.FileName;
            }

            // 處理匯款截圖
            string? screenshotPath = null;
            string? screenshotName = null;
            if (vm.ScreenshotFile != null && vm.ScreenshotFile.Length > 0)
            {
                var screenshotId = Guid.NewGuid();
                var screenshotExt = Path.GetExtension(vm.ScreenshotFile.FileName);
                var screenshotFileName = $"transfer_{screenshotId}{screenshotExt}";
                using (var stream = new FileStream(Path.Combine(_uploadPath, screenshotFileName), FileMode.Create))
                    await vm.ScreenshotFile.CopyToAsync(stream);
                screenshotPath = $"/uploads/payment-proofs/{screenshotFileName}";
                screenshotName = vm.ScreenshotFile.FileName;
            }

            // 建立合併付款訂單
            var orderId = Guid.NewGuid();
            var order = new ConferencePaymentOrder
            {
                Id = orderId,
                PaymentMethod = "匯款",
                LastFiveDigits = vm.Last5,
                TransferAmount = vm.Amount,
                TransferAt = vm.TransferAt ?? DateTime.Now,
                FilePath = screenshotPath ?? "-",
                FileName = screenshotName ?? "匯款資訊",
                Note = vm.Note,
                Status = PaymentOrderStatus.PendingVerification,
                DiscountProofPath = discountProofPath,
                DiscountProofName = discountProofName,
                UploadedAt = DateTime.Now,
                UploadedBy = userId,
                CreateAt = DateTime.Now,
                CreateBy = userId
            };
            db.ConferencePaymentOrder.Add(order);

            foreach (var reservationNo in vm.ReservationIds)
            {
                var conference = await db.Conference
                    .Where(c => c.Id.ToString().StartsWith(reservationNo))
                    .FirstOrDefaultAsync()
                    ?? throw new HttpException($"找不到預約單號: {reservationNo}");

                if (conference.ReservationStatus != ReservationStatus.PendingPayment)
                    throw new HttpException($"預約單 {reservationNo} 不在待繳費狀態");

                db.ConferencePaymentOrderItem.Add(new ConferencePaymentOrderItem
                {
                    Id = Guid.NewGuid(),
                    OrderId = orderId,
                    ConferenceId = conference.Id,
                    CreateAt = DateTime.Now
                });

                conference.PaymentStatus = PaymentStatus.PendingVerification;
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("payment_voucher",
                $"提交匯款資訊 - 預約單: {string.Join(", ", vm.ReservationIds)}, 末五碼: {vm.Last5}, 訂單: {orderId}");

            // 寄送通知給總務
            foreach (var reservationNo in vm.ReservationIds)
            {
                var conf = await db.Conference
                    .Where(c => c.Id.ToString().StartsWith(reservationNo))
                    .FirstOrDefaultAsync();
                if (conf != null)
                    service.ConferenceMail.PaymentProofUploaded(conf.Id, "銀行匯款");
            }

            return orderId;
        }

        /// <summary>
        /// ✅ 批准付款訂單（核准訂單內所有會議）
        /// </summary>
        public async Task ApprovePayment(ApprovePaymentVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            var order = await db.ConferencePaymentOrder
                .Include(o => o.Items)
                    .ThenInclude(i => i.Conference)
                        .ThenInclude(c => c.ConferenceRoomSlots)
                .FirstOrDefaultAsync(o => o.Id == vm.OrderId && o.DeleteAt == null)
                ?? throw new HttpException("找不到該付款訂單");

            if (order.Status != PaymentOrderStatus.PendingVerification)
                throw new HttpException("該訂單不在待查帳狀態");

            // 更新訂單狀態
            order.Status = PaymentOrderStatus.Paid;
            order.ReviewedAt = DateTime.Now;
            order.ReviewedBy = userId;

            // 核准訂單內每個會議
            foreach (var item in order.Items)
            {
                var conference = item.Conference;
                if (conference == null) continue;

                conference.PaymentStatus = PaymentStatus.Paid;
                conference.ReservationStatus = ReservationStatus.Confirmed;
                conference.ApprovedAt = DateTime.Now;
                conference.PaidAt = DateTime.Now;
                conference.Status = 1;

                // 設定會議時間
                var slots = conference.ConferenceRoomSlots
                    .OrderBy(s => s.SlotDate).ThenBy(s => s.StartTime).ToList();
                if (slots.Any())
                {
                    conference.StartTime = slots.First().SlotDate.ToDateTime(slots.First().StartTime);
                    conference.EndTime = slots.Last().SlotDate.ToDateTime(slots.Last().EndTime);
                }
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("payment_approve",
                $"批准付款訂單 {order.Id}, 共 {order.Items.Count} 筆預約");

            // 寄送通知給每個預約人
            foreach (var item in order.Items)
                service.ConferenceMail.PaymentApproved(item.ConferenceId);
        }

        /// <summary>
        /// ✅ 退回付款訂單（退回訂單內所有會議）
        /// </summary>
        public async Task RejectPayment(RejectPaymentVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            var order = await db.ConferencePaymentOrder
                .Include(o => o.Items)
                    .ThenInclude(i => i.Conference)
                .FirstOrDefaultAsync(o => o.Id == vm.OrderId && o.DeleteAt == null)
                ?? throw new HttpException("找不到該付款訂單");

            if (order.Status != PaymentOrderStatus.PendingVerification)
                throw new HttpException("該訂單不在待查帳狀態");

            // 更新訂單狀態
            order.Status = PaymentOrderStatus.Rejected;
            order.RejectReason = vm.Reason;
            order.ReviewedAt = DateTime.Now;
            order.ReviewedBy = userId;

            // 退回訂單內每個會議
            foreach (var item in order.Items)
            {
                var conference = item.Conference;
                if (conference == null) continue;
                conference.PaymentStatus = PaymentStatus.PendingReupload;
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("payment_approve",
                $"退回付款訂單 {order.Id}, 原因: {vm.Reason}");

            // 寄送通知給每個預約人
            foreach (var item in order.Items)
                service.ConferenceMail.PaymentRejected(item.ConferenceId, vm.Reason);
        }
    }
}