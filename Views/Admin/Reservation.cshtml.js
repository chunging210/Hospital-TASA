// Reservation Review - 預約審核 (租借 + 付款)
import global from '/global.js';
import * as EnumHelper from '/js/helper.js';

const { ref, reactive, onMounted, computed, watch } = Vue;

const reservation = new function () {
    // ========= 權限資料 =========
    this.permissions = reactive({
        canApproveReservation: false,
        canApprovePayment: false,
        roles: []
    });

    // ========= Tab 狀態 =========
    this.activeTab = ref('approval');  // 'approval' 或 'payment'

    // ========= 查詢參數 =========
    this.query = reactive({
        keyword: '',
        reservationStatus: 1,    // ✅ 租借審核狀態 (預設:待審核)
        paymentStatus: 2        // ✅ 付款審核狀態 (預設:待查帳)
    });

    // ========= 列表資料 =========
    this.approvalList = reactive([]);  // 租借審核列表
    this.paymentList = reactive([]);   // 付款審核列表

    // ========= 計數 =========
    this.approvalCount = ref(0);
    this.paymentCount = ref(0);

    // ========= 租借審核表單 =========
    this.vm = reactive({
        result: 'approve',
        rejectReason: '',
        discountType: 'none',
        discountPercent: 10,
        discountAmount: 0,
        discountReason: ''
    });

    this.currentReview = reactive({});

    // ========= 付款審核表單 =========
    this.paymentVm = reactive({
        result: 'approve',
        rejectReason: ''
    });

    this.currentPayment = reactive({});

    // ========= 價格明細 =========
    this.pricing = reactive({
        original: 0,
        discount: 0,
        final: 0
    });

    // ========= 初始化:載入權限 =========
    this.loadPermissions = async () => {
        try {
            const res = await global.api.reservations.permissions();
            console.log('🔐 使用者權限:', res.data);

            // ✅ 修正:使用 PascalCase 屬性名稱
            this.permissions.canApproveReservation = res.data.CanApproveReservation;  // ✅ 大寫 C
            this.permissions.canApprovePayment = res.data.CanApprovePayment;        // ✅ 大寫 C
            this.permissions.roles = res.data.Roles || [];                           // ✅ 大寫 R

            console.log('✅ 權限賦值完成:', this.permissions);

            // ✅ 檢查是否至少有一個審核權限
            const hasAnyPermission = this.permissions.canApproveReservation || this.permissions.canApprovePayment;

            if (!hasAnyPermission) {
                addAlert('您沒有審核權限', { type: 'warning' });
                return;
            }

            // ✅ 根據權限設定初始 Tab
            if (this.permissions.canApproveReservation) {
                this.activeTab.value = 'approval';
            } else if (this.permissions.canApprovePayment) {
                this.activeTab.value = 'payment';
            }

        } catch (err) {
            console.error('❌ 無法取得權限資訊:', err);
            addAlert('無法取得權限資訊', { type: 'danger' });
        }
    };

    // ========= 切換 Tab =========
    this.switchTab = (tab) => {
        this.activeTab.value = tab;
        this.query.keyword = '';

        if (tab === 'approval') {
            this.query.reservationStatus = 1;  // ✅ 預設「待審核」
            this.query.paymentStatus = null;     // 清空付款狀態
        } else if (tab === 'payment') {
            this.query.reservationStatus = null;  // 清空租借狀態
            this.query.paymentStatus = 2;       // ✅ 預設「待查帳」
        }

        console.log('✅ 重設後的 query:', this.query);
        this.getList();
    };

    // ========= 取得列表 (根據當前 Tab) =========
    this.getList = async () => {
        if (this.activeTab.value === 'approval') {
            await this.getApprovalList();
        } else if (this.activeTab.value === 'payment') {
            await this.getPaymentList();
        }
    };

    // ========= 取得租借審核列表 =========
    this.getApprovalList = async () => {
        try {
            // ✅ 建立查詢參數
            const queryParams = {
                keyword: this.query.keyword || ''
            };

            // ✅ 只有選擇狀態時才加入參數
            if (this.query.reservationStatus !== null && this.query.reservationStatus !== '') {
                queryParams.reservationStatus = this.query.reservationStatus;
            }

            console.log('🔍 租借審核查詢參數:', queryParams);

            // ✅ 改用 body 而不是 params
            const res = await global.api.reservations.reservationlist({
                body: queryParams  // ✅ 這裡改成 body
            });

            console.log('📋 API 回傳資料:', res.data);

            if (Array.isArray(res.data)) {
                const mapped = res.data.map(x => ({
                    id: x.Id,
                    bookingNo: x.BookingNo,
                    applicantName: x.ApplicantName,
                    date: x.Date,
                    time: x.Time,
                    roomName: x.RoomName,
                    paymentType: x.PaymentType,
                    filePath: x.FilePath,
                    totalAmount: x.TotalAmount,
                    status: x.Status,
                    slots: (x.Slots || []).map(s => ({
                        id: s.Id,
                        slotDate: s.SlotDate,
                        startTime: s.StartTime,
                        endTime: s.EndTime,
                        slotStatus: s.SlotStatus
                    }))
                }));

                this.approvalList.splice(0, this.approvalList.length, ...mapped);
                this.approvalCount.value = mapped.length;

                console.log('✅ 租借審核列表更新完成,共', mapped.length, '筆');
            }
        } catch (error) {
            console.error('❌ 取得租借審核列表失敗:', error);
            addAlert('取得租借審核列表失敗', { type: 'danger' });
        }
    };

    // ========= 取得付款審核列表 =========
    this.getPaymentList = async () => {
        try {
            // ✅ 建立查詢參數
            const queryParams = {
                keyword: this.query.keyword || ''
            };

            // ✅ 只有選擇狀態時才加入參數
            if (this.query.paymentStatus !== null && this.query.paymentStatus !== '') {
                queryParams.paymentStatus = this.query.paymentStatus;
            }

            console.log('🔍 付款審核查詢參數:', queryParams);

            // ✅ 改用 body 而不是 params
            const res = await global.api.reservations.paymentlist({
                body: queryParams  // ✅ 這裡改成 body
            });

            console.log('💰 API 回傳資料:', res.data);

            if (Array.isArray(res.data)) {
                const mapped = res.data.map(x => ({
                    id: x.Id,
                    bookingNo: x.BookingNo,
                    applicantName: x.ApplicantName,
                    date: x.Date,
                    time: x.Time,
                    roomName: x.RoomName,
                    totalAmount: x.TotalAmount,
                    paymentMethod: x.PaymentMethod,
                    paymentType: x.PaymentType,
                    filePath: x.FilePath,
                    uploadTime: x.UploadTime,
                    paymentStatusText: x.PaymentStatusText,
                    lastFiveDigits: x.LastFiveDigits,
                    transferAmount: x.TransferAmount,
                    transferAt: x.TransferAt,
                    note: x.Note,
                    slots: (x.Slots || []).map(s => ({
                        id: s.Id,
                        slotDate: s.SlotDate,
                        startTime: s.StartTime,
                        endTime: s.EndTime,
                        slotStatus: s.SlotStatus
                    }))
                }));

                this.paymentList.splice(0, this.paymentList.length, ...mapped);
                this.paymentCount.value = mapped.length;

                console.log('✅ 付款審核列表更新完成,共', mapped.length, '筆');
            }
        } catch (error) {
            console.error('❌ 取得付款審核列表失敗:', error);
            addAlert('取得付款審核列表失敗', { type: 'danger' });
        }
    };

    // ========= 開啟租借審核抽屜 =========
    this.openApprovalReview = (item) => {
        console.log('📋 開啟租借審核, item:', item);
        copy(this.currentReview, item);

        // 重設表單
        this.vm.result = 'approve';
        this.vm.rejectReason = '';
        this.vm.discountType = 'none';
        this.vm.discountPercent = 10;
        this.vm.discountAmount = 0;
        this.vm.discountReason = '';

        // 計算價格
        this.calculatePricing();
    };

    // ========= 開啟付款審核抽屜 =========
    this.openPaymentReview = (item) => {
        console.log('💰 開啟付款審核, item:', item);
        copy(this.currentPayment, item);

        // 重設表單為預設狀態 (核准)
        this.paymentVm.result = 'approve';
        this.paymentVm.rejectReason = '';
    };

    // ========= 計算價格明細 =========
    this.calculatePricing = () => {
        const base = this.currentReview.totalAmount || 0;
        let discount = 0;

        if (this.vm.discountType === 'percent') {
            discount = Math.round(base * (this.vm.discountPercent / 100));
        } else if (this.vm.discountType === 'amount') {
            discount = this.vm.discountAmount;
        } else if (this.vm.discountType === 'free') {
            discount = base;
        }

        this.pricing.original = base;
        this.pricing.discount = discount;
        this.pricing.final = Math.max(0, base - discount);
    };

    // ========= 關閉抽屜 =========
    this.closeDrawer = (drawerId) => {
        const modalElement = document.getElementById(drawerId);
        const offcanvas = window.bootstrap?.Offcanvas?.getInstance(modalElement);
        if (offcanvas) {
            offcanvas.hide();
        }
    };

    // ========= 驗證租借審核表單 =========
    this.validateApproval = () => {
        if (this.vm.result === 'approve') {
            if (this.vm.discountType === 'free' && !this.vm.discountReason.trim()) {
                addAlert('免單必須填寫原因', { type: 'warning' });
                return false;
            }
        }

        if (this.vm.result === 'reject') {
            if (!this.vm.rejectReason.trim()) {
                addAlert('拒絕必須填寫原因', { type: 'warning' });
                return false;
            }
        }

        return true;
    };

    // ========= 驗證付款審核表單 =========
    this.validatePayment = () => {
        if (this.paymentVm.result === 'reject') {
            if (!this.paymentVm.rejectReason.trim()) {
                addAlert('退回必須填寫原因', { type: 'warning' });
                return false;
            }
        }

        return true;
    };

    // ========= 送出租借審核 =========
    this.submitApprovalReview = async () => {
        console.log('🚀 送出租借審核');

        if (!this.validateApproval()) {
            return;
        }

        if (this.vm.result === 'approve') {
            await this.approveReservation();
        } else {
            await this.rejectReservation();
        }
    };

    // ========= 租借審核通過 =========
    this.approveReservation = async () => {
        try {
            await global.api.reservations.approve({
                body: {
                    conferenceId: this.currentReview.id,
                    discountAmount: this.pricing.discount
                }
            });

            addAlert('租借審核通過!', { type: 'success' });
            this.closeDrawer('approvalDrawer');
            await this.getApprovalList();

        } catch (err) {
            console.error('❌ 審核失敗:', err);
            addAlert('審核失敗：' + (err.message || '未知錯誤'), { type: 'danger' });
        }
    };

    // ========= 租借審核拒絕 =========
    this.rejectReservation = async () => {
        try {
            await global.api.reservations.reject({
                body: {
                    conferenceId: this.currentReview.id,
                    reason: this.vm.rejectReason
                }
            });

            addAlert('已拒絕預約!', { type: 'success' });
            this.closeDrawer('approvalDrawer');
            await this.getApprovalList();

        } catch (err) {
            console.error('❌ 拒絕失敗:', err);
            addAlert('拒絕失敗：' + (err.message || '未知錯誤'), { type: 'danger' });
        }
    };

    // ========= 送出付款審核 =========
    this.submitPaymentReview = async () => {
        console.log('🚀 送出付款審核');

        if (!this.validatePayment()) {
            return;
        }

        if (this.paymentVm.result === 'approve') {
            await this.approvePayment();
        } else {
            await this.rejectPayment();
        }
    };

    // ========= 付款審核通過 =========
    this.approvePayment = async () => {
        try {
            await global.api.reservations.approvepayment({
                body: {
                    reservationId: this.currentPayment.id  // ✅ 改成 reservationId (camelCase)
                }
            });

            addAlert('付款審核通過!', { type: 'success' });
            this.closeDrawer('paymentDrawer');
            await this.getPaymentList();

        } catch (err) {
            console.error('❌ 付款審核失敗:', err);
            addAlert('付款審核失敗:' + (err.message || '未知錯誤'), { type: 'danger' });
        }
    };

    // ========= 付款審核拒絕 =========
    this.rejectPayment = async () => {
        try {
            await global.api.reservations.rejectpayment({
                body: {
                    reservationId: this.currentPayment.id,  // ✅ 改成 reservationId
                    reason: this.paymentVm.rejectReason
                }
            });

            addAlert('已退回付款憑證!', { type: 'success' });
            this.closeDrawer('paymentDrawer');
            await this.getPaymentList();

        } catch (err) {
            console.error('❌ 退回失敗:', err);
            addAlert('退回失敗:' + (err.message || '未知錯誤'), { type: 'danger' });
        }
    };
};

// ========= Helper Functions (全域) =========
function getPaymentMethodText(method) {
    return EnumHelper.getPaymentMethodText(method);
}

function isCounterPayment(method) {
    return EnumHelper.isCounterPayment(method);
}

function isTransferPayment(method) {
    return EnumHelper.isTransferPayment(method);
}

function getApprovalStatusClass(status) {
    return EnumHelper.getReservationStatusClass(status);
}

function getPaymentStatusClass(status) {
    return EnumHelper.getPaymentStatusClass(status);
}

// ========= Vue Setup =========
window.$config = {
    setup() {
        // Watch 折扣變動
        watch(() => reservation.vm.discountType, () => {
            reservation.calculatePricing();
        });

        watch(() => reservation.vm.discountPercent, () => {
            reservation.calculatePricing();
        });

        watch(() => reservation.vm.discountAmount, () => {
            reservation.calculatePricing();
        });

        // 初始化
        onMounted(async () => {
            console.log('🚀 Vue 組件已掛載');
            await reservation.loadPermissions();
            await reservation.getList();
        });

        return {
            reservation,
            getPaymentMethodText,
            isCounterPayment,
            isTransferPayment,
            getApprovalStatusClass,
            getPaymentStatusClass
        };
    }
};