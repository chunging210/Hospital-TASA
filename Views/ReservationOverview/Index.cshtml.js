// Reservation Overview Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch, nextTick } = Vue;
import * as EnumHelper from '/js/helper.js';

window.$config = {
    setup: () => new function () {

        /* ========= 基本資料 ========= */
        this.isAdmin = ref(false);
        this.currentUserId = ref('');
        this.activeTab = ref('personal');

        /* ========= 搜尋與篩選 ========= */
        this.searchQuery = ref('');
        this.dateRange = ref('');
        this.paymentStatusFilter = ref('');

        /* ========= 資料列表 ========= */
        this.allReservations = ref([]);
        this.personalReservations = ref([]);

        /* ========= 選中項目 ========= */
        this.selectedItem = ref(null);

        /* ========= ✅ DOM Refs (重要!) ========= */
        this.counterPayFiles = ref(null);  // ✅ 加上這個!

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
            let filtered = this.allReservations.value;

            if (this.searchQuery.value) {
                const query = this.searchQuery.value.toLowerCase();
                filtered = filtered.filter(item =>
                    item.reservationNo.toLowerCase().includes(query) ||
                    item.reserverName.toLowerCase().includes(query)
                );
            }

            if (this.paymentStatusFilter.value) {
                filtered = filtered.filter(item =>
                    item.paymentStatus.includes(this.paymentStatusFilter.value)
                );
            }

            return filtered;
        });

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

        this.loadAllReservations = async () => {
            const res = await global.api.reservations.list();

            this.allReservations.value = (res.data || []).map(item => {
                let paymentStatus;

                if (item.Status === '審核拒絕' || item.Status === '已釋放' || item.Status === '待審核') {
                    paymentStatus = '-';
                } else {
                    paymentStatus = item.PaymentStatusText || '未付款';
                }

                return {
                    id: item.Id,
                    reservationNo: item.BookingNo,
                    reserverName: item.ApplicantName,
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
                    slots: item.Slots || []
                };
            });
        };

        this.loadPersonalReservations = async () => {
            const res = await global.api.reservations.mylist();

            this.personalReservations.value = (res.data || []).map(item => {
                let paymentStatus;

                if (item.Status === '審核拒絕' || item.Status === '已釋放' || item.Status === '待審核') {
                    paymentStatus = '-';
                } else {
                    paymentStatus = item.PaymentStatusText || '未付款';
                }

                return {
                    id: item.Id,
                    reservationNo: item.BookingNo,
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
                    slots: item.Slots || []
                };
            });
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

        // ✅ 上傳臨櫃憑證 (修正版)
        this.submitCounterPayment = async () => {

            // ✅ 修正:使用 ref 取得檔案
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

                console.log('reservationNo:', this.selectedItem.value.reservationNo);

                // reservationIds 序列化成 JSON
                formData.append('reservationIds', JSON.stringify([this.selectedItem.value.reservationNo]));
                formData.append('note', this.paymentForm.counterNote || '');

                // 附加所有檔案
                for (let i = 0; i < files.length; i++) {
                    formData.append('files', files[i]);
                }


                // ✅ 呼叫 API
                const response = await global.api.payment.uploadcounter({ body: formData });


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
                console.error('錯誤詳情:', err.message);
                console.error('錯誤堆疊:', err.stack);
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
            // 基本資訊
            this.selectedItem.value = {
                id: item.id,
                reservationNo: item.reservationNo,
                reserverName: item.reserverName,
                reservationDate: item.reservationDate,
                timeSlot: item.timeSlot,
                roomName: item.roomName,
                paymentDeadline: item.paymentDeadline,
                paymentMethod: item.paymentMethod,
                amount: item.amount,
                costCenter: item.costCenter,
                paymentStatus: item.paymentStatus,
                approvalStatus: item.approvalStatus,
                rejectReason: item.rejectReason || ''
            };

            // 如果是匯款,預填金額
            if (this.isTransferPayment(item.paymentMethod)) {
                this.paymentForm.amount = item.amount;
            }

            this.bookingDrawerInstance.value?.show();
        };

        /* ========= Watch ========= */
        watch(
            () => this.activeTab.value,
            (newTab) => {

                if (newTab === 'all') {
                    this.loadAllReservations();
                } else if (newTab === 'personal') {
                    this.loadPersonalReservations();
                }
            },
            { immediate: true }
        );

        /* ========= Mounted ========= */
        onMounted(async () => {
            await this.loadUserInfo();
            await nextTick();

            const bookingDrawerEl = document.getElementById('bookingDrawer');
            if (bookingDrawerEl) {
                this.bookingDrawerInstance.value =
                    bootstrap.Offcanvas.getOrCreateInstance(bookingDrawerEl);
            }
        });

    }
};