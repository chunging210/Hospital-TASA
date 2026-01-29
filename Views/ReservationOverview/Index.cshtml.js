// Reservation Overview Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch, nextTick } = Vue;
import * as EnumHelper from '/js/helper.js';

let allReservationsPageRef = null;
let personalReservationsPageRef = null;

window.$config = {
    setup: () => new function () {

        /* ========= 基本資料 ========= */
        this.isAdmin = ref(false);
        this.currentUserId = ref('');
        this.activeTab = ref('personal');
        this.minAdvanceDays = ref(7);  // ✅ 從設定檔讀取,預設7天

        /* ========= 搜尋與篩選 ========= */
        this.searchQuery = ref('');
        this.dateRange = ref('');
        this.approvalStatusFilter = ref('');
        this.paymentStatusFilter = ref('');
        this.userStatusFilter = ref('');

        /* ========= 資料列表 ========= */
        this.allReservations = ref([]);
        this.personalReservations = ref([]);
        this.loading = ref(false);
        /* ========= ✅ Page Refs ========= */
        this.allReservationsPage = ref(null);
        this.personalReservationsPage = ref(null);
        /* ========= 選中項目 ========= */
        this.selectedItem = ref(null);

        /* ========= ✅ DOM Refs (重要!) ========= */
        this.counterPayFiles = ref(null);

        /* ========= ✅ 付款表單資料 ========= */
        this.paymentForm = reactive({
            // 臨櫃付款
            counterNote: '',
            // 匯款付款
            last5: '',
            amount: 0,
            transferAt: '',
            transferNote: ''
        });

        /* ========= Bootstrap Instances ========= */
        this.bookingDrawerInstance = ref(null);

        /* ========= 計算屬性 ========= */
        this.filteredAllReservations = computed(() => {
            return this.allReservations.value;  // ✅ 不篩選,直接回傳
        });

        this.filteredPersonalReservations = computed(() => {
            return this.personalReservations.value;  // ✅ 不篩選,直接回傳
        });

        this.filteredPersonalReservations = computed(() => {
            let filtered = this.personalReservations.value;

            // 關鍵字搜尋 (前端篩選)
            if (this.searchQuery.value) {
                const query = this.searchQuery.value.toLowerCase();
                filtered = filtered.filter(item =>
                    item.reservationNo.toLowerCase().includes(query)
                );
            }

            // ✅ 使用者友善狀態篩選 (前端篩選)
            if (this.userStatusFilter.value) {
                filtered = filtered.filter(item => {
                    const userStatus = this.getUserFriendlyStatus(item);

                    switch (this.userStatusFilter.value) {
                        case 'pending': return userStatus === '審核中';
                        case 'payment': return userStatus === '待繳費';
                        case 'reupload': return userStatus === '待重新上傳';
                        case 'confirmed': return userStatus === '預約成功';
                        case 'rejected': return userStatus === '已拒絕';
                        case 'cancelled': return userStatus === '已取消';
                        default: return true;
                    }
                });
            }

            return filtered;
        });

        this.getUserFriendlyStatus = (item) => {
            return EnumHelper.getUserFriendlyStatus(
                item.approvalStatus,
                item.paymentStatus
            );
        };
        this.userStatusOptions = computed(() => {
            return EnumHelper.getUserStatusFilterOptions();
        });

        this.getUserFriendlyStatusClass = (item) => {
            const status = this.getUserFriendlyStatus(item);
            return EnumHelper.getUserFriendlyStatusBadgeClass(status);
        };

        /* ========= 樣式相關方法 ========= */
        this.getPaymentStatusClass = (status) => {
            return EnumHelper.getPaymentStatusBadgeClass(status);
        };

        this.getApprovalStatusClass = (status) => {
            return EnumHelper.getReservationStatusBadgeClass(status);
        };

        this.getPaymentMethodText = (method) => {
            return EnumHelper.getPaymentMethodText(method);
        };

        /* ========= 資料載入 ========= */
        this.loadUserInfo = async () => {
            try {
                const res = await global.api.auth.me();
                const user = res.data;

                this.currentUserId.value = user.Id;
                this.isAdmin.value = user.IsAdmin || false;

                if (!this.isAdmin.value) {
                    this.activeTab.value = 'personal';
                } else {
                    this.activeTab.value = 'all';
                }

            } catch (err) {
                console.error('❌ 無法取得使用者資訊:', err);
                addAlert('無法取得使用者資訊', { type: 'danger' });
            }
        };

        this.loadAllReservations = async (pagination) => {
            try {
                this.loading.value = true;

                // ✅ 準備查詢參數
                const queryParams = {};

                if (this.searchQuery.value) {
                    queryParams.keyword = this.searchQuery.value;
                }

                if (this.approvalStatusFilter.value) {
                    queryParams.reservationStatus = this.approvalStatusFilter.value;
                }

                if (this.paymentStatusFilter.value) {
                    queryParams.paymentStatus = this.paymentStatusFilter.value;
                }

                const options = { body: queryParams };

                const request = pagination && allReservationsPageRef
                    ? global.api.reservations.list(allReservationsPageRef.setHeaders(options))
                    : global.api.reservations.list(options);

                const response = await request;

                if (pagination && allReservationsPageRef) {
                    allReservationsPageRef.setTotal(response);
                }

                // ✅ 完整的資料處理
                this.allReservations.value = (response.data || []).map(item => {
                    let paymentStatus;

                    if (item.Status === '審核拒絕' || item.Status === '已取消' || item.Status === '待審核') {
                        paymentStatus = '-';
                    } else {
                        paymentStatus = item.PaymentStatusText || '未付款';
                    }

                    return {
                        id: item.Id,
                        reservationNo: item.BookingNo,
                        reserverName: item.ApplicantName,
                        conferenceName: item.ConferenceName,
                        reservationDate: item.Date,
                        timeSlot: item.Time,
                        roomName: item.RoomName,
                        paymentDeadline: item.PaymentDeadline || '-',
                        paymentMethod: item.PaymentMethod || '-',
                        amount: item.TotalAmount,
                        paymentStatus: paymentStatus,
                        approvalStatus: item.Status,
                        costCenter: item.DepartmentCode || '-',
                        rejectReason: item.RejectReason || '',
                        paymentRejectReason: item.PaymentRejectReason || '',
                        slots: item.Slots || []
                    };
                });

            } catch (err) {
                console.error('❌ 載入所有預約失敗:', err);
                addAlert('載入預約資料失敗', { type: 'danger' });
            } finally {
                this.loading.value = false;
            }
        };

        // ✅ 修改 loadPersonalReservations
        this.loadPersonalReservations = async (pagination) => {
            try {
                this.loading.value = true;

                const queryParams = {};

                if (this.searchQuery.value) {
                    queryParams.keyword = this.searchQuery.value;
                }

                // ✅ 使用者友善狀態 → 轉換成後端的狀態碼
                if (this.userStatusFilter.value) {
                    const mapping = {
                        'pending': { reservationStatus: 1 },  // 待審核
                        'payment': { reservationStatus: 2 },  // 待繳費
                        'reupload': { paymentStatus: 4 },     // 待重新上傳
                        'confirmed': { reservationStatus: 3 }, // 預約成功
                        'rejected': { reservationStatus: 4 },  // 審核拒絕
                        'cancelled': { reservationStatus: 5 }  // 已取消
                    };

                    const filter = mapping[this.userStatusFilter.value];
                    if (filter) {
                        Object.assign(queryParams, filter);
                    }
                }

                const options = { body: queryParams };

                const request = pagination && personalReservationsPageRef
                    ? global.api.reservations.mylist(personalReservationsPageRef.setHeaders(options))
                    : global.api.reservations.mylist(options);

                const response = await request;

                if (pagination && personalReservationsPageRef) {
                    personalReservationsPageRef.setTotal(response);
                }

                // ✅ 完整的資料處理
                this.personalReservations.value = (response.data || []).map(item => {
                    let paymentStatus;

                    if (item.Status === '審核拒絕' || item.Status === '已取消' || item.Status === '待審核') {
                        paymentStatus = '-';
                    } else {
                        paymentStatus = item.PaymentStatusText || '未付款';
                    }

                    return {
                        id: item.Id,
                        reservationNo: item.BookingNo,
                        reservationDate: item.Date,
                        conferenceName: item.ConferenceName,
                        timeSlot: item.Time,
                        roomName: item.RoomName,
                        paymentDeadline: item.PaymentDeadline || '-',
                        paymentMethod: item.PaymentMethod || '-',
                        amount: item.TotalAmount,
                        paymentStatus: paymentStatus,
                        approvalStatus: item.Status,
                        costCenter: item.DepartmentCode || '-',
                        rejectReason: item.RejectReason || '',
                        paymentRejectReason: item.PaymentRejectReason || '',
                        slots: item.Slots || []
                    };
                });

            } catch (err) {
                console.error('❌ 載入個人預約失敗:', err);
                addAlert('載入預約資料失敗', { type: 'danger' });
            } finally {
                this.loading.value = false;
            }
        };

        this.isCounterPayment = (method) => {
            return EnumHelper.isCounterPayment(method);
        };

        this.isTransferPayment = (method) => {
            return EnumHelper.isTransferPayment(method);
        };

        this.isCostSharingPayment = (method) => {
            return EnumHelper.isCostSharingPayment(method);
        };

        // ✅ 上傳臨櫃憑證
        this.submitCounterPayment = async () => {
            const fileInput = this.counterPayFiles.value;
            if (!fileInput) {
                console.error('❌ 找不到檔案輸入元素');
                addAlert('找不到檔案輸入元素', { type: 'danger' });
                return;
            }

            const files = fileInput.files;

            if (!files || files.length === 0) {
                const fileType = this.selectedItem.value.amount === 0 ? '證明文件' : '三聯單檔案';
                addAlert(`請上傳${fileType}`, { type: 'warning' });
                return;
            }

            try {
                const formData = new FormData();

                // ✅ 使用 reservationNo
                formData.append('reservationIds', JSON.stringify([this.selectedItem.value.reservationNo]));
                formData.append('note', this.paymentForm.counterNote || '');

                // 附加所有檔案
                for (let i = 0; i < files.length; i++) {
                    formData.append('files', files[i]);
                }

                // ✅ 呼叫 API
                await global.api.payment.uploadcounter({ body: formData });

                addAlert('憑證已上傳，等待審核', { type: 'success' });
                this.bookingDrawerInstance.value?.hide();
                await this.loadPersonalReservations();

                // 清空表單
                this.paymentForm.counterNote = '';
                if (fileInput) {
                    fileInput.value = '';
                }

            } catch (err) {
                console.error('❌ 上傳憑證失敗:', err);
                addAlert(`上傳憑證失敗: ${err.message || '未知錯誤'}`, { type: 'danger' });
            }
        };

        // ✅ 上傳匯款資訊
        this.submitTransferPayment = async () => {
            if (!this.paymentForm.last5 || this.paymentForm.last5.length !== 5) {
                addAlert('請輸入正確的 5 碼轉帳末碼', { type: 'warning' });
                return;
            }

            if (!this.paymentForm.amount || this.paymentForm.amount <= 0) {
                addAlert('請輸入正確的金額', { type: 'warning' });
                return;
            }

            try {
                const payload = {
                    reservationIds: [this.selectedItem.value.reservationNo],
                    last5: this.paymentForm.last5,
                    amount: parseInt(this.paymentForm.amount),
                    transferAt: this.paymentForm.transferAt || null,
                    note: this.paymentForm.transferNote || ''
                };

                await global.api.payment.transfer({ body: payload });

                addAlert('匯款資訊已提交，等待審核', { type: 'success' });
                this.bookingDrawerInstance.value?.hide();
                await this.loadPersonalReservations();

                // 清空表單
                this.paymentForm.last5 = '';
                this.paymentForm.amount = 0;
                this.paymentForm.transferAt = '';
                this.paymentForm.transferNote = '';

            } catch (err) {
                console.error('❌ 提交匯款資訊失敗:', err);
                addAlert('提交匯款資訊失敗', { type: 'danger' });
            }
        };

        /* ========= 詳情相關方法 ========= */
        this.openDetailDrawer = async (item) => {
            this.selectedItem.value = {
                id: item.id,
                reservationNo: item.reservationNo,
                reserverName: item.reserverName,
                conferenceName: item.conferenceName,
                reservationDate: item.reservationDate,
                timeSlot: item.timeSlot,
                roomName: item.roomName,
                paymentDeadline: item.paymentDeadline,
                paymentMethod: item.paymentMethod,
                amount: item.amount,
                costCenter: item.costCenter,
                paymentStatus: item.paymentStatus,
                approvalStatus: item.approvalStatus,
                rejectReason: item.rejectReason || '',
                paymentRejectReason: item.paymentRejectReason || '',

                openedFrom: this.activeTab.value
            };

            // 如果是匯款,預填金額
            if (this.isTransferPayment(item.paymentMethod)) {
                this.paymentForm.amount = item.amount;
            }

            this.bookingDrawerInstance.value?.show();
        };

        /* ========= ✅ 權限控制方法 ========= */

        // 計算距離預約日期的天數
        this.getDaysUntilReservation = (dateStr) => {
            const reservationDate = new Date(dateStr);
            const today = new Date();
            today.setHours(0, 0, 0, 0);
            reservationDate.setHours(0, 0, 0, 0);
            return Math.ceil((reservationDate - today) / (1000 * 60 * 60 * 24));
        };

        // 可以編輯
        this.canEdit = (item) => {
            return item.approvalStatus === '待審核';
        };

        // 可以取消
        this.canCancel = (item) => {
            // 待審核 → 可以取消
            if (item.approvalStatus === '待審核') return true;

            // 待繳費 → 可以取消
            if (item.approvalStatus === '待繳費') return true;

            // 待重新上傳 → 可以取消
            if (item.paymentStatus === '待重新上傳') return true;

            // 待查帳 或 已收款 (預約成功) → 需檢查時間
            if (item.paymentStatus === '待查帳' || item.approvalStatus === '預約成功') {
                const daysUntil = this.getDaysUntilReservation(item.reservationDate);
                return daysUntil >= this.minAdvanceDays.value;  // ✅ 從設定檔讀取
            }

            return false;
        };

        // 可以刪除
        this.canDelete = (item) => {
            // 只有「待審核」和「審核拒絕」可以刪除
            return ['待審核', '審核拒絕'].includes(item.approvalStatus);
        };

        // ✅ 判斷是否顯示操作按鈕區
        this.shouldShowActionButtons = (item) => {
            if (!item) return false;
            return this.canEdit(item) || this.canCancel(item) || this.canDelete(item);
        };

        // 取得操作提示
        this.getActionHint = (item) => {
            if (!item) return '';

            if (item.approvalStatus === '待審核') {
                return '審核前可自由修改或取消';
            }
            if (item.approvalStatus === '待繳費') {
                return '請於期限內上傳付款憑證';
            }
            if (item.paymentStatus === '待查帳') {
                return '等待總務確認付款';
            }
            if (item.approvalStatus === '預約成功' || item.paymentStatus === '已收款') {
                return '預約已確認';
            }
            if (item.paymentStatus === '待重新上傳') {
                return '付款憑證有誤,請重新上傳';
            }
            if (item.approvalStatus === '審核拒絕') {
                return '可刪除此紀錄或重新預約';
            }

            return '';
        };

        // 取得取消警告訊息
        this.getCancelWarning = (item) => {
            if (!item || !this.canCancel(item)) return '';

            const daysUntil = this.getDaysUntilReservation(item.reservationDate);

            if (item.approvalStatus === '待審核') {
                return '取消後此預約將被刪除';
            }
            if (item.approvalStatus === '待繳費') {
                return '取消後時段將釋放,需重新預約';
            }
            if (item.paymentStatus === '待查帳') {
                return daysUntil >= this.minAdvanceDays.value ? '取消後將不退款' : '';
            }
            if (item.approvalStatus === '預約成功' || item.paymentStatus === '已收款') {
                return daysUntil >= this.minAdvanceDays.value ? '取消將退款 80%,扣除 20% 手續費' : '';
            }
            if (item.paymentStatus === '待重新上傳') {
                return '可選擇取消或重新上傳正確憑證';
            }

            return '';
        };

        /* ========= ✅ 操作功能方法 ========= */

        // 編輯預約
        this.editReservation = (item) => {
            // ✅ 使用 id (Guid)
            window.location.href = `/Conference/Create?id=${item.id}`;
        };

        // 確認取消預約
        this.confirmCancel = async (item) => {
            const warning = this.getCancelWarning(item);
            const confirmText = warning
                ? `${warning}\n\n確定要取消此預約嗎?`
                : '確定要取消此預約嗎?';

            if (!confirm(confirmText)) return;

            try {
                // ✅ 使用 id (Guid)
                await global.api.reservations.cancel({
                    body: { reservationId: item.id }
                });

                addAlert('預約已取消', { type: 'success' });
                this.bookingDrawerInstance.value?.hide();
                await this.loadPersonalReservations();

            } catch (err) {
                console.error('❌ 取消預約失敗:', err);
                addAlert('取消預約失敗', { type: 'danger' });
            }
        };

        // 確認刪除預約
        this.confirmDelete = async (item) => {
            const confirmMsg = item.approvalStatus === '待審核'
                ? '確定要移除此預約嗎?\n\n移除後將從您的預約列表中消失,但資料仍會保留在系統中。'
                : '確定要移除此紀錄嗎?\n\n移除後將從列表中消失。';

            if (!confirm(confirmMsg)) return;

            try {
                // ✅ 使用 id (Guid)
                await global.api.reservations.delete({
                    body: { reservationId: item.id }
                });

                addAlert('預約紀錄已刪除', { type: 'success' });
                this.bookingDrawerInstance.value?.hide();
                await this.loadPersonalReservations();

            } catch (err) {
                console.error('❌ 刪除預約失敗:', err);
                addAlert('刪除預約失敗', { type: 'danger' });
            }
        };

        this.switchTab = (tab) => {
            // 1️⃣ 切換 Tab
            this.activeTab.value = tab;

            // 2️⃣ 重置所有篩選條件
            this.searchQuery.value = '';
            this.approvalStatusFilter.value = '';
            this.paymentStatusFilter.value = '';
            this.userStatusFilter.value = '';

            // 3️⃣ 重置分頁到第一頁
            if (tab === 'all' && allReservationsPageRef) {
                allReservationsPageRef.go(1);
                this.loadAllReservations(true);
            } else if (tab === 'personal' && personalReservationsPageRef) {
                personalReservationsPageRef.go(1);
                this.loadPersonalReservations(true);
            }
        };

        /* ========= Watch ========= */
        watch([
            () => this.searchQuery.value,
            () => this.approvalStatusFilter.value,
            () => this.paymentStatusFilter.value
        ], () => {
            // ✅ 只在 'all' Tab 時才觸發
            if (this.activeTab.value !== 'all') return;

            if (allReservationsPageRef) {
                allReservationsPageRef.go(1);
            }
            this.loadAllReservations(true);
        });

        // 個人預約的篩選
        watch([
            () => this.searchQuery.value,
            () => this.userStatusFilter.value
        ], () => {
            // ✅ 只在 'personal' Tab 時才觸發
            if (this.activeTab.value !== 'personal') return;

            if (personalReservationsPageRef) {
                personalReservationsPageRef.go(1);
            }
            this.loadPersonalReservations(true);
        });

        /* ========= Mounted ========= */
        onMounted(async () => {
            await this.loadUserInfo();
            await nextTick();

            // ✅ 初始化 Page Refs
            allReservationsPageRef = this.allReservationsPage.value;
            personalReservationsPageRef = this.personalReservationsPage.value;


            if (this.activeTab.value === 'all') {
                this.loadAllReservations(true);
            } else {
                this.loadPersonalReservations(true);
            }


            const bookingDrawerEl = document.getElementById('bookingDrawer');
            if (bookingDrawerEl) {
                this.bookingDrawerInstance.value =
                    bootstrap.Offcanvas.getOrCreateInstance(bookingDrawerEl);
            }
        });

    }
};