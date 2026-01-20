namespace TASA.Models.Enums
{
    /// <summary>
    /// 預約狀態 (Conference.ReservationStatus)
    /// </summary>
    public enum ReservationStatus : byte
    {
        /// <summary>待審核 - 使用者提交預約,等待主任審核</summary>
        PendingApproval = 1,

        /// <summary>待繳費 - 審核通過,等待使用者繳費</summary>
        PendingPayment = 2,

        /// <summary>預約成功 - 已繳費且審核通過,預約完成</summary>
        Confirmed = 3,

        /// <summary>審核拒絕 - 主任拒絕此預約</summary>
        Rejected = 4,

        /// <summary>已取消 - 使用者主動取消預約</summary>
        Cancelled = 5
    }

    /// <summary>
    /// 付款狀態 (Conference.PaymentStatus)
    /// </summary>
    public enum PaymentStatus
    {
        /// <summary>未付款 - 尚未上傳付款憑證</summary>
        Unpaid = 1,

        /// <summary>待查帳 - 已上傳憑證,等待會計審核</summary>
        PendingVerification = 2,

        /// <summary>已收款 - 會計確認收款</summary>
        Paid = 3
    }

    /// <summary>
    /// 時段狀態 (ConferenceRoomSlot.SlotStatus)
    /// </summary>
    public enum SlotStatus
    {
        /// <summary>可預約 - 時段可用</summary>
        Available = 0,

        /// <summary>鎖定中 - 預約待審核,暫時鎖定</summary>
        Locked = 1,

        /// <summary>已預約 - 審核通過,時段已佔用</summary>
        Reserved = 2
    }

    /// <summary>
    /// 憑證狀態 (ConferencePaymentProof.Status)
    /// </summary>
    public enum ProofStatus
    {
        /// <summary>待審核 - 已上傳,等待會計審核</summary>
        PendingReview = 0,

        /// <summary>已批准 - 會計確認通過</summary>
        Approved = 1,

        /// <summary>已退回 - 會計退回,需重新上傳</summary>
        Rejected = 2
    }
}