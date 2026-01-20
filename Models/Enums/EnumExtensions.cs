using TASA.Models.Enums;

namespace TASA.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// 取得預約狀態中文名稱
        /// </summary>
        public static string GetDisplayName(this ReservationStatus status)
        {
            return status switch
            {
                ReservationStatus.PendingApproval => "待審核",
                ReservationStatus.PendingPayment => "待繳費",
                ReservationStatus.Confirmed => "預約成功",
                ReservationStatus.Rejected => "審核拒絕",
                ReservationStatus.Cancelled => "已取消",
                _ => "未知"
            };
        }

        /// <summary>
        /// 取得付款狀態中文名稱
        /// </summary>
        public static string GetDisplayName(this PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Unpaid => "未付款",
                PaymentStatus.PendingVerification => "待查帳",
                PaymentStatus.Paid => "已收款",
                _ => "未知"
            };
        }

        /// <summary>
        /// 取得時段狀態中文名稱
        /// </summary>
        public static string GetDisplayName(this SlotStatus status)
        {
            return status switch
            {
                SlotStatus.Available => "可預約",
                SlotStatus.Locked => "鎖定中",
                SlotStatus.Reserved => "已預約",
                _ => "未知"
            };
        }

        /// <summary>
        /// 取得憑證狀態中文名稱
        /// </summary>
        public static string GetDisplayName(this ProofStatus status)
        {
            return status switch
            {
                ProofStatus.PendingReview => "待審核",
                ProofStatus.Approved => "已批准",
                ProofStatus.Rejected => "已退回",
                _ => "未知"
            };
        }

        /// <summary>
        /// 取得預約狀態的 Bootstrap CSS Class
        /// </summary>
        public static string GetBadgeClass(this ReservationStatus status)
        {
            return status switch
            {
                ReservationStatus.PendingApproval => "bg-warning",
                ReservationStatus.PendingPayment => "bg-info",
                ReservationStatus.Confirmed => "bg-success",
                ReservationStatus.Rejected => "bg-danger",
                ReservationStatus.Cancelled => "bg-secondary",
                _ => "bg-secondary"
            };
        }

        /// <summary>
        /// 取得付款狀態的 Bootstrap CSS Class
        /// </summary>
        public static string GetBadgeClass(this PaymentStatus status)
        {
            return status switch
            {
                PaymentStatus.Unpaid => "bg-secondary",
                PaymentStatus.PendingVerification => "bg-warning",
                PaymentStatus.Paid => "bg-success",
                _ => "bg-secondary"
            };
        }

        /// <summary>
        /// 取得憑證狀態的 Bootstrap CSS Class
        /// </summary>
        public static string GetBadgeClass(this ProofStatus status)
        {
            return status switch
            {
                ProofStatus.PendingReview => "bg-warning",
                ProofStatus.Approved => "bg-success",
                ProofStatus.Rejected => "bg-danger",
                _ => "bg-secondary"
            };
        }
    }
}