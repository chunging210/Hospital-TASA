/**
 * ========================================
 * Enums Helper - 狀態相關的共用函數
 * ========================================
 * 統一管理所有狀態文字與樣式
 */

// ========== 預約狀態 (ReservationStatus) ==========
export const ReservationStatus = {
    PendingApproval: 1,
    PendingPayment: 2,
    Confirmed: 3,
    Rejected: 4,
    Cancelled: 5
};

export function getReservationStatusText(status) {
    const map = {
        1: '待審核',
        2: '待繳費',
        3: '預約成功',
        4: '審核拒絕',
        5: '已取消'
    };
    return map[status] || '未知';
}

export function getReservationStatusClass(status) {
    const map = {
        '待審核': 'bg-warning',
        '待繳費': 'bg-info',
        '預約成功': 'bg-success',
        '審核拒絕': 'bg-danger',
        '已取消': 'bg-secondary',
        '已釋放': 'bg-secondary'
    };
    return map[status] || 'bg-secondary';
}

export function getReservationStatusBadgeClass(status) {
    const map = {
        '待審核': 'badge bg-warning text-dark',
        '待繳費': 'badge bg-info text-white',
        '預約成功': 'badge bg-success text-white',
        '審核拒絕': 'badge bg-danger text-white',
        '已取消': 'badge bg-secondary text-white',    // ✅
        '已釋放': 'badge bg-secondary text-white'     // ✅
    };
    return map[status] || 'badge bg-secondary text-white';
}


// ========== 會議狀態 (ConferenceStatus) ==========
export const ConferenceStatus = {
    Scheduled: 1,
    InProgress: 2,
    Completed: 3,
    NoShow: 4
};

export function getConferenceStatusText(status) {
    const map = {
        1: '已排程',
        2: '進行中',
        3: '已完成',
        4: '未出席'
    };
    return map[status] || '-';
}

export function getConferenceStatusClass(status) {
    const map = {
        '已排程': 'bg-primary',
        '進行中': 'bg-info',
        '已完成': 'bg-success',
        '未出席': 'bg-warning'
    };
    return map[status] || 'bg-secondary';
}

export function getConferenceStatusBadgeClass(status) {
    const map = {
        '已排程': 'badge-primary',
        '進行中': 'badge-info',
        '已完成': 'badge-success',
        '未出席': 'badge-warning'
    };
    return `badge ${map[status] || 'badge-default'}`;
}

// ========== 付款狀態 (PaymentStatus) ==========
export const PaymentStatus = {
    Unpaid: 1,
    PendingVerification: 2,
    Paid: 3,
    PendingReupload: 4
};

export function getPaymentStatusText(status) {
    const map = {
        1: '未付款',
        2: '待查帳',
        3: '已收款',
        4: '待重新上傳'
    };
    return map[status] || '未知';
}

export function getPaymentStatusClass(status) {
    if (status === '-') return '';

    const map = {
        '未付款': 'bg-secondary',
        '待查帳': 'bg-warning',
        '已收款': 'bg-success',
        '待重新上傳': 'bg-danger'
    };
    return map[status] || 'bg-secondary';
}

export function getPaymentStatusBadgeClass(status) {
    if (status === '-') return '';

    const map = {
        '未付款': 'badge bg-secondary text-white',    // ✅
        '待查帳': 'badge bg-warning text-dark',
        '已收款': 'badge bg-success text-white',
        '待重新上傳': 'badge bg-danger text-white'
    };
    return map[status] || 'badge bg-secondary text-white';
}

// ========== 付款方式 ==========
export function getPaymentMethodText(method) {
    const map = {
        'transfer': '銀行匯款',
        'cost-sharing': '成本分攤',
        'cash': '現金付款'
    };
    return map[method] || method || '-';
}

export function isCounterPayment(method) {
    return method === 'cash' || method === '現金付款';
}

export function isTransferPayment(method) {
    return method === 'transfer' || method === '銀行匯款';
}

export function isCostSharingPayment(method) {
    return method === 'cost-sharing' || method === '成本分攤';
}

// ========== ✅ 使用者友善狀態 (個人預約用) ==========

/**
 * 取得使用者看到的簡化狀態
 * 用於「個人預約」Tab
 * @param {string} reservationStatusText - 預約狀態文字 (待審核/待繳費/預約成功/審核拒絕/已取消)
 * @param {string} paymentStatusText - 付款狀態文字 (未付款/待查帳/已收款/待重新上傳)
 * @returns {string} 使用者看到的狀態
 */
export function getUserFriendlyStatus(reservationStatusText, paymentStatusText) {
    // 終態直接顯示
    if (reservationStatusText === '預約成功') return '預約成功';
    if (reservationStatusText === '審核拒絕') return '已拒絕';
    if (reservationStatusText === '已取消') return '已取消';

    // 待審核
    if (reservationStatusText === '待審核') return '審核中';

    // 待繳費階段 - 根據付款狀態細分
    if (reservationStatusText === '待繳費') {
        if (paymentStatusText === '未付款') return '待繳費';
        if (paymentStatusText === '待查帳') return '審核中';
        if (paymentStatusText === '待重新上傳') return '待重新上傳';
        return '處理中';
    }

    return '未知';
}

/**
 * 取得使用者友善狀態的 Badge 樣式
 * @param {string} userFriendlyStatus - getUserFriendlyStatus 回傳的狀態
 * @returns {string} Badge class
 */
export function getUserFriendlyStatusBadgeClass(userFriendlyStatus) {
    const map = {
        '審核中': 'badge bg-warning text-dark',      // 黃色
        '待繳費': 'badge bg-info text-white',         // 藍色
        '待重新上傳': 'badge bg-danger text-white',   // 紅色
        '處理中': 'badge bg-warning text-dark',       // 黃色
        '預約成功': 'badge bg-success text-white',    // 綠色
        '已拒絕': 'badge bg-danger text-white',       // 紅色
        '已取消': 'badge bg-secondary text-white'     // 灰色 ✅ 加上 text-white
    };
    return map[userFriendlyStatus] || 'badge bg-secondary text-white';
}

/**
 * 取得管理者詳細狀態 (兩欄顯示)
 * 用於「所有預約」Tab
 * @param {string} reservationStatusText - 預約狀態文字
 * @param {string} paymentStatusText - 付款狀態文字
 * @returns {object} { reservation, payment }
 */
export function getAdminDetailedStatus(reservationStatusText, paymentStatusText) {
    return {
        reservation: reservationStatusText,
        payment: paymentStatusText === '未知' ? '-' : paymentStatusText
    };
}

/**
 * ✅ 取得個人預約的篩選選項
 * @returns {Array} 篩選選項陣列
 */
export function getUserStatusFilterOptions() {
    return [
        { value: '', text: '全部狀態' },
        { value: 'pending', text: '審核中' },
        { value: 'payment', text: '待繳費' },
        { value: 'reupload', text: '待重新上傳' },
        { value: 'confirmed', text: '預約成功' },
        { value: 'rejected', text: '已拒絕' },
        { value: 'cancelled', text: '已取消' }
    ];
}

/**
 * ✅ 根據篩選條件過濾預約列表 (前端用)
 * @param {Array} reservations - 預約列表
 * @param {string} filterValue - 篩選值
 * @returns {Array} 過濾後的列表
 */
export function filterReservationsByUserStatus(reservations, filterValue) {
    if (!filterValue) return reservations;

    return reservations.filter(item => {
        const userStatus = getUserFriendlyStatus(item.Status, item.PaymentStatusText);

        switch (filterValue) {
            case 'pending':
                return userStatus === '審核中';
            case 'payment':
                return userStatus === '待繳費';
            case 'reupload':
                return userStatus === '待重新上傳';
            case 'confirmed':
                return userStatus === '預約成功';
            case 'rejected':
                return userStatus === '已拒絕';
            case 'cancelled':
                return userStatus === '已取消';
            default:
                return true;
        }
    });
}

// ========== ✅ 權限判斷 ==========

/**
 * 判斷是否可以編輯預約
 * @param {number} reservationStatus
 * @returns {boolean}
 */
export function canEditReservation(reservationStatus) {
    return reservationStatus === ReservationStatus.PendingApproval;
}

/**
 * 判斷是否可以取消預約
 * @param {number} reservationStatus
 * @param {number} paymentStatus
 * @param {string} reservationDate - 預約日期 (YYYY-MM-DD)
 * @param {number} minAdvanceDays - 最少提前天數
 * @returns {boolean}
 */
export function canCancelReservation(reservationStatus, paymentStatus, reservationDate, minAdvanceDays = 3) {
    const rsText = getReservationStatusText(reservationStatus);

    // 待審核、待繳費 → 可以取消
    if (rsText === '待審核' || rsText === '待繳費') return true;

    // 待重新上傳 → 可以取消
    if (paymentStatus === PaymentStatus.PendingReupload) return true;

    // 預約成功 → 檢查是否在允許取消的時間內
    if (rsText === '預約成功') {
        const daysUntil = getDaysUntilReservation(reservationDate);
        return daysUntil >= minAdvanceDays;
    }

    return false;
}

/**
 * 判斷是否可以刪除預約
 * @param {number} reservationStatus
 * @returns {boolean}
 */
export function canDeleteReservation(reservationStatus) {
    const rsText = getReservationStatusText(reservationStatus);
    return rsText === '待審核' || rsText === '審核拒絕';
}

/**
 * 判斷是否可以上傳付款憑證
 * @param {number} reservationStatus
 * @param {number} paymentStatus
 * @returns {boolean}
 */
export function canUploadProof(reservationStatus, paymentStatus) {
    const rsText = getReservationStatusText(reservationStatus);
    const psText = getPaymentStatusText(paymentStatus);

    // 待繳費 + 未付款
    if (rsText === '待繳費' && psText === '未付款') return true;

    // 待重新上傳
    if (psText === '待重新上傳') return true;

    return false;
}

/**
 * 計算距離預約日期的天數
 * @param {string} dateStr - 日期字串 (YYYY-MM-DD 或 YYYY/MM/DD)
 * @returns {number} 天數
 */
export function getDaysUntilReservation(dateStr) {
    const reservationDate = new Date(dateStr.replace(/\//g, '-'));
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    reservationDate.setHours(0, 0, 0, 0);
    return Math.ceil((reservationDate - today) / (1000 * 60 * 60 * 24));
}

/**
 * 取得操作提示文字
 * @param {number} reservationStatus
 * @param {number} paymentStatus
 * @returns {string}
 */
export function getActionHint(reservationStatus, paymentStatus) {
    const rsText = getReservationStatusText(reservationStatus);
    const psText = getPaymentStatusText(paymentStatus);

    if (rsText === '待審核') return '審核前可自由修改或取消';
    if (rsText === '待繳費' && psText === '未付款') return '請於期限內上傳付款憑證';
    if (psText === '待查帳') return '等待會計確認付款';
    if (psText === '已收款') return '預約已確認';
    if (psText === '待重新上傳') return '付款憑證有誤,請重新上傳';
    if (rsText === '審核拒絕') return '可刪除此紀錄或重新預約';

    return '';
}

/**
 * 取得取消警告訊息
 * @param {number} reservationStatus
 * @param {number} paymentStatus
 * @param {string} reservationDate
 * @param {number} minAdvanceDays
 * @returns {string}
 */
export function getCancelWarning(reservationStatus, paymentStatus, reservationDate, minAdvanceDays = 3) {
    const rsText = getReservationStatusText(reservationStatus);
    const psText = getPaymentStatusText(paymentStatus);
    const daysUntil = getDaysUntilReservation(reservationDate);

    if (rsText === '待審核') return '取消後此預約將被刪除';
    if (rsText === '待繳費') return '取消後時段將釋放,需重新預約';
    if (psText === '待查帳') return daysUntil >= minAdvanceDays ? '取消後將不退款' : `距離會議不足${minAdvanceDays}天,無法取消`;
    if (psText === '已收款') return daysUntil >= minAdvanceDays ? '取消將退款 80%,扣除 20% 手續費' : `距離會議不足${minAdvanceDays}天,無法取消`;
    if (psText === '待重新上傳') return '可選擇取消或重新上傳正確憑證';

    return '';
}