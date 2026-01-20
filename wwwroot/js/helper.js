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
        '待審核': 'badge-pending',
        '待繳費': 'badge-payment',
        '預約成功': 'badge-success',
        '審核拒絕': 'badge-rejected',
        '已釋放': 'badge-released'
    };
    return `badge ${map[status] || 'badge-default'}`;
}

// ========== 付款狀態 (PaymentStatus) ==========
export const PaymentStatus = {
    Unpaid: 1,
    PendingVerification: 2,
    Paid: 3
};

export function getPaymentStatusText(status) {
    const map = {
        1: '未付款',
        2: '待查帳',
        3: '已收款'
    };
    return map[status] || '未知';
}

export function getPaymentStatusClass(status) {
    if (status === '-') return '';

    const map = {
        '未付款': 'bg-secondary',
        '待查帳': 'bg-warning',
        '已收款': 'bg-success'
    };
    return map[status] || 'bg-secondary';
}

export function getPaymentStatusBadgeClass(status) {
    if (status === '-') return '';

    const map = {
        '未付款': 'badge-payment',
        '待查帳': 'badge-pending',
        '已收款': 'badge-success'
    };
    return `badge ${map[status] || 'badge-default'}`;
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