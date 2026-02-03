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
        }


        public class TransferPaymentVM
        {
            public List<string> ReservationIds { get; set; } = new();
            public string Last5 { get; set; } = string.Empty;
            public int Amount { get; set; }
            public DateTime? TransferAt { get; set; }
            public string? Note { get; set; }
        }

        public class ApprovePaymentVM
        {
            public Guid ReservationId { get; set; }
        }

        public class RejectPaymentVM
        {
            public Guid ReservationId { get; set; }
            public string Reason { get; set; } = string.Empty;
        }

        /// <summary>
        /// ✅ 上傳臨櫃付款憑證
        /// </summary>
        public async Task<List<Guid>> UploadCounterProof(UploadCounterVM vm)
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

            var proofIds = new List<Guid>();

            // 處理每個預約單號
            foreach (var reservationNo in vm.ReservationIds)
            {
                // 根據預約單號前8碼找到會議
                var conference = await db.Conference
                    .Where(c => c.Id.ToString().StartsWith(reservationNo))
                    .FirstOrDefaultAsync()
                    ?? throw new HttpException($"找不到預約單號: {reservationNo}");

                // 檢查會議狀態
                if (conference.ReservationStatus != ReservationStatus.PendingPayment)
                    throw new HttpException($"預約單 {reservationNo} 不在待繳費狀態");

                // 儲存每個檔案
                foreach (var file in vm.Files)
                {
                    var proofId = Guid.NewGuid();
                    var fileExtension = Path.GetExtension(file.FileName);
                    var fileName = $"{proofId}{fileExtension}";
                    var filePath = Path.Combine(_uploadPath, fileName);

                    // 儲存檔案
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // ✅ 改回 ConferencePaymentProof (單數)
                    var proof = new ConferencePaymentProof
                    {
                        Id = proofId,
                        ConferenceId = conference.Id,
                        FilePath = $"/uploads/payment-proofs/{fileName}",
                        FileName = file.FileName,
                        PaymentType = "臨櫃",
                        Note = vm.Note,
                        Status = ProofStatus.PendingReview,
                        UploadedAt = DateTime.Now,
                        UploadedBy = userId
                    };

                    db.ConferencePaymentProof.Add(proof);
                    proofIds.Add(proofId);
                }

                // 更新會議付款狀態為「待查帳」
                conference.PaymentStatus = PaymentStatus.PendingVerification;
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("付款憑證",
                $"上傳臨櫃憑證 - 預約單: {string.Join(", ", vm.ReservationIds)}, 檔案數: {vm.Files.Count}");

            return proofIds;
        }

        /// <summary>
        /// ✅ 提交匯款資訊
        /// </summary>
        public async Task<List<Guid>> SubmitTransferInfo(TransferPaymentVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            if (string.IsNullOrEmpty(vm.Last5) || vm.Last5.Length != 5)
                throw new HttpException("請輸入正確的5碼轉帳末碼");

            if (vm.Amount <= 0)
                throw new HttpException("請輸入正確的金額");

            if (vm.ReservationIds == null || vm.ReservationIds.Count == 0)
                throw new HttpException("沒有選擇任何預約");

            var proofIds = new List<Guid>();

            foreach (var reservationNo in vm.ReservationIds)
            {
                var conference = await db.Conference
                    .Where(c => c.Id.ToString().StartsWith(reservationNo))
                    .FirstOrDefaultAsync()
                    ?? throw new HttpException($"找不到預約單號: {reservationNo}");

                if (conference.ReservationStatus != ReservationStatus.PendingPayment)
                    throw new HttpException($"預約單 {reservationNo} 不在待繳費狀態");

                var proofId = Guid.NewGuid();

                // ✅ 改回 ConferencePaymentProof (單數)
                var proof = new ConferencePaymentProof
                {
                    Id = proofId,
                    ConferenceId = conference.Id,
                    FilePath = "-",  // 匯款不需要檔案
                    FileName = "匯款資訊",
                    PaymentType = "匯款",
                    LastFiveDigits = vm.Last5,
                    TransferAmount = vm.Amount,
                    TransferAt = vm.TransferAt ?? DateTime.Now,
                    Note = vm.Note,
                    Status = ProofStatus.PendingReview,
                    UploadedAt = DateTime.Now,
                    UploadedBy = userId
                };

                db.ConferencePaymentProof.Add(proof);
                proofIds.Add(proofId);

                // 更新會議付款狀態為「待查帳」
                conference.PaymentStatus = PaymentStatus.PendingVerification;
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("付款憑證",
                $"提交匯款資訊 - 預約單: {string.Join(", ", vm.ReservationIds)}, 末五碼: {vm.Last5}");

            return proofIds;
        }

        /// <summary>
        /// ✅ 批准付款憑證
        /// </summary>
        public async Task ApprovePayment(ApprovePaymentVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            var conference = await db.Conference
                .Include(c => c.ConferencePaymentProofs)
                .FirstOrDefaultAsync(c => c.Id == vm.ReservationId)
                ?? throw new HttpException("找不到該預約");

            if (conference.ReservationStatus != ReservationStatus.PendingPayment)
                throw new HttpException("該預約不在待繳費狀態");

            if (conference.PaymentStatus != PaymentStatus.PendingVerification)
                throw new HttpException("該預約未上傳憑證");

            // 更新憑證狀態
            var proofs = conference.ConferencePaymentProofs.Where(p => p.Status == ProofStatus.PendingReview).ToList();
            // ✅ Navigation Property 用複數
            foreach (var proof in proofs)
            {
                proof.Status = ProofStatus.Approved;
                proof.ReviewedAt = DateTime.Now;
                proof.ReviewedBy = userId;
            }

            // 更新會議付款狀態為「已收款(全額)」
            conference.PaymentStatus = PaymentStatus.Paid;


            // ✅ 如果全額付清,變更為「預約成功」
            conference.ReservationStatus = ReservationStatus.Confirmed;

            conference.ApprovedAt = DateTime.Now;
            conference.PaidAt = DateTime.Now;
            conference.Status = 1;

            // 設定會議時間
            var slots = await db.ConferenceRoomSlot
                .Where(s => s.ConferenceId == conference.Id)
                .OrderBy(s => s.SlotDate)
                .ThenBy(s => s.StartTime)
                .ToListAsync();

            if (slots.Any())
            {
                var firstSlot = slots.First();
                conference.StartTime = firstSlot.SlotDate.ToDateTime(firstSlot.StartTime);

                var lastSlot = slots.Last();
                conference.EndTime = lastSlot.SlotDate.ToDateTime(lastSlot.EndTime);
            }

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("付款審核",
                $"批准付款 - {conference.Name} ({conference.Id})");
        }

        /// <summary>
        /// ✅ 退回付款憑證
        /// </summary>
        public async Task RejectPayment(RejectPaymentVM vm)
        {
            var userId = service.UserClaimsService.Me()?.Id
                ?? throw new HttpException("無法取得使用者資訊");

            var conference = await db.Conference
                .Include(c => c.ConferencePaymentProofs)  // ✅ Navigation Property 用複數
                .FirstOrDefaultAsync(c => c.Id == vm.ReservationId)
                ?? throw new HttpException("找不到該預約");

            if (conference.PaymentStatus != PaymentStatus.PendingVerification)
                throw new HttpException("該預約未上傳憑證");

            // 更新憑證狀態
            var proofs = conference.ConferencePaymentProofs.Where(p => p.Status == ProofStatus.PendingReview).ToList();
            // ✅ Navigation Property 用複數
            foreach (var proof in proofs)
            {
                proof.Status = ProofStatus.Rejected;
                proof.ReviewedAt = DateTime.Now;
                proof.ReviewedBy = userId;
                proof.RejectReason = vm.Reason;
            }

            // 付款狀態改回「未付款」
            conference.PaymentStatus = PaymentStatus.PendingReupload;

            await db.SaveChangesAsync();

            _ = service.LogServices.LogAsync("付款審核",
                $"退回付款 - {conference.Name} ({conference.Id}), 原因: {vm.Reason}");
        }
    }
}