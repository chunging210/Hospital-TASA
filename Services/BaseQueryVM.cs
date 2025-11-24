namespace TASA.Services
{
    public class BaseQueryVM
    {
        private DateTime? _Start;
        /// <summary>
        /// 會議開始時間(起)
        /// </summary>
        public DateTime? Start { get => _Start?.ToLocalTime(); set => _Start = value; }

        private DateTime? _End;
        /// <summary>
        /// 會議開始時間(訖)
        /// </summary>
        public DateTime? End { get => _End?.ToLocalTime(); set => _End = value; }

        /// <summary>
        /// 與會地點
        /// </summary>
        public Guid? RoomId { get; set; }

        /// <summary>
        /// 承辦單位
        /// </summary>
        public Guid? DepartmentId { get; set; }

        /// <summary>
        /// 發起會議者
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// 只顯示自己的會議
        /// </summary>
        public bool? Self { get; set; }

        /// <summary>
        /// 啟用
        /// </summary>
        public bool? IsEnabled { get; set; }

        /// <summary>
        /// 關鍵字
        /// </summary>
        public string? Keyword { get; set; }
    }
}
