// Reservation Overview Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch, nextTick } = Vue;

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
        this.checkReservations = ref([]);

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

        /* ========= 查帳模式 ========= */
        this.checkMode = reactive({
            batchMode: false,
            selectAll: false,
            selectedCount: 0
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
            if (status === '-') {
                return '';
            }

            const statusMap = {
                '未付款': 'badge-payment',
                '待查帳': 'badge-pending',
                '已收款(全額)': 'badge-success',
                '已收款(訂金30%)': 'badge-success',
                '已收款(尾款70%)': 'badge-success',
                '未知': 'badge-default'
            };

            return `badge ${statusMap[status] || 'badge-default'}`;
        };

        this.getApprovalStatusClass = (status) => {
            const statusMap = {
                '待審核': 'badge-pending',
                '待繳費': 'badge-payment',
                '預約成功': 'badge-success',
                '審核拒絕': 'badge-rejected',
                '已釋放': 'badge-released',
            };

            return `badge ${statusMap[status] || 'badge-default'}`;
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

        this.loadCheckReservations = async () => {
            const res = await global.api.reservations.pendingcheck();

            this.checkReservations.value = (res.data || []).map(item => ({
                id: item.Id,
                reservationNo: item.BookingNo,
                reserverName: item.ApplicantName,
                reservationDate: item.Date,
                timeSlot: item.Time,
                roomName: item.RoomName,
                paymentMethod: item.PaymentMethod || '-',
                amount: item.TotalAmount,
                uploadTime: item.UploadTime || '-',
                selected: false,
                slots: item.Slots || []
            }));
        };

        /* ========= ✅ 付款相關方法 ========= */
        this.getPaymentMethodText = (method) => {
            const methodMap = {
                'transfer': '銀行匯款',
                'cost-sharing': '成本分攤',
                'cash': '現金付款'
            };

            return methodMap[method] || method || '-';
        };

        this.isCounterPayment = (method) => {
            return method === 'cash' || method === '現金付款';
        };

        this.isTransferPayment = (method) => {
            return method === 'transfer' || method === '銀行匯款';
        };

        this.isCostSharingPayment = (method) => {
            return method === 'cost-sharing' || method === '成本分攤';
        };

        // ✅ 上傳臨櫃憑證 (修正版)
        this.submitCounterPayment = async () => {
            console.log('🔍 開始上傳憑證...');
            console.log('counterPayFiles ref:', this.counterPayFiles.value);

            // ✅ 修正:使用 ref 取得檔案
            const fileInput = this.counterPayFiles.value;
            if (!fileInput) {
                console.error('❌ 找不到檔案輸入元素');
                addAlert('找不到檔案輸入元素', { type: 'danger' });
                return;
            }

            const files = fileInput.files;
            console.log('選中的檔案:', files);

            if (!files || files.length === 0) {
                const fileType = this.selectedItem.value.amount === 0 ? '證明文件' : '三聯單檔案';
                addAlert(`請上傳${fileType}`, { type: 'warning' });
                return;
            }

            try {
                const formData = new FormData();

                console.log('📦 準備 FormData...');
                console.log('reservationNo:', this.selectedItem.value.reservationNo);

                // reservationIds 序列化成 JSON
                formData.append('reservationIds', JSON.stringify([this.selectedItem.value.reservationNo]));
                formData.append('note', this.paymentForm.counterNote || '');

                // 附加所有檔案
                for (let i = 0; i < files.length; i++) {
                    console.log(`📎 附加檔案 ${i + 1}:`, files[i].name);
                    formData.append('files', files[i]);
                }

                console.log('🚀 呼叫 API...');

                // ✅ 呼叫 API
                const response = await global.api.payment.uploadcounter({ body: formData });

                console.log('✅ API 回應:', response);

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

        /* ========= 查帳相關方法 ========= */
        this.toggleBatchCheckMode = () => {
            this.checkMode.batchMode = !this.checkMode.batchMode;

            if (!this.checkMode.batchMode) {
                this.checkReservations.value.forEach(item => {
                    item.selected = false;
                });
                this.checkMode.selectAll = false;
            }

            this.updateCheckSelection();
        };

        this.toggleCheckSelectAll = () => {
            this.checkReservations.value.forEach(item => {
                item.selected = this.checkMode.selectAll;
            });
            this.updateCheckSelection();
        };

        this.updateCheckSelection = () => {
            this.checkMode.selectedCount = this.checkReservations.value.filter(
                item => item.selected
            ).length;
        };

        this.viewPaymentProof = (item) => {
            addAlert(`查看預約單 ${item.reservationNo} 的付款憑證`, { type: 'info' });
            // TODO: 實作查看憑證功能
        };

        this.approvePayment = async (item) => {
            if (!confirm(`確定要批准預約單 ${item.reservationNo} 嗎？`)) {
                return;
            }

            try {
                await global.api.payment.approve({
                    body: {
                        reservationId: item.id
                    }
                });

                addAlert('批准成功！', { type: 'success' });
                await this.loadCheckReservations();

            } catch (err) {
                console.error('❌ 批准失敗:', err);
                addAlert('批准失敗', { type: 'danger' });
            }
        };

        this.rejectPayment = async (item) => {
            const reason = prompt('請輸入退回原因：');
            if (!reason) return;

            try {
                await global.api.payment.reject({
                    body: {
                        reservationId: item.id,
                        reason: reason
                    }
                });

                addAlert(`退回成功！原因：${reason}`, { type: 'success' });
                await this.loadCheckReservations();

            } catch (err) {
                console.error('❌ 退回失敗:', err);
                addAlert('退回失敗', { type: 'danger' });
            }
        };

        this.batchApprove = async () => {
            const selected = this.checkReservations.value.filter(item => item.selected);
            if (selected.length === 0) return;

            if (!confirm(`確定要批准 ${selected.length} 個項目嗎？`)) {
                return;
            }

            try {
                await global.api.payment.batchapprove({
                    body: {
                        reservationIds: selected.map(item => item.id)
                    }
                });

                addAlert('批准完成！', { type: 'success' });
                await this.loadCheckReservations();
                this.checkMode.batchMode = false;

            } catch (err) {
                console.error('❌ 批量批准失敗:', err);
                addAlert('批量批准失敗', { type: 'danger' });
            }
        };

        this.batchReject = async () => {
            const selected = this.checkReservations.value.filter(item => item.selected);
            if (selected.length === 0) return;

            const reason = prompt('請輸入退回原因：');
            if (!reason) return;

            try {
                await global.api.payment.batchreject({
                    body: {
                        reservationIds: selected.map(item => item.id),
                        reason: reason
                    }
                });

                addAlert(`退回完成！原因：${reason}`, { type: 'success' });
                await this.loadCheckReservations();
                this.checkMode.batchMode = false;

            } catch (err) {
                console.error('❌ 批量退回失敗:', err);
                addAlert('批量退回失敗', { type: 'danger' });
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

            // ✅ 如果是「待查帳」,載入付款資訊
            if (item.paymentStatus === '待查帳') {
                try {
                    const res = await global.api.reservations.paymentinfo({ body: { id: item.id } });

                    if (res.data) {
                        this.selectedItem.value.uploadTime = res.data.UploadTime;
                        this.selectedItem.value.paymentNote = res.data.Note;

                        // 現金付款的憑證
                        if (res.data.ProofFiles) {
                            this.selectedItem.value.proofFiles = res.data.ProofFiles.map(f => ({
                                name: f.FileName,
                                url: f.FilePath
                            }));
                        }

                        // 匯款資訊
                        if (res.data.Last5) {
                            this.selectedItem.value.last5 = res.data.Last5;
                            this.selectedItem.value.transferAmount = res.data.TransferAmount;
                            this.selectedItem.value.transferAt = res.data.TransferAt;
                        }
                    }
                } catch (err) {
                    console.error('❌ 載入付款資訊失敗:', err);
                }
            }

            // 如果是匯款,預填金額
            if (this.isTransferPayment(item.paymentMethod)) {
                this.paymentForm.amount = item.amount;
            }

            this.bookingDrawerInstance.value?.show();
        };

        this.saveDetailChanges = async () => {
            if (!this.isAdmin.value) {
                addAlert('您沒有權限修改付款狀態', { type: 'warning' });
                return;
            }

            const allowedStatuses = ['待繳費', '預約成功'];
            if (!allowedStatuses.includes(this.selectedItem.value.approvalStatus)) {
                const message = this.selectedItem.value.approvalStatus === '待審核'
                    ? '請先審核通過後才能修改付款狀態'
                    : this.selectedItem.value.approvalStatus === '審核拒絕'
                        ? '已拒絕的預約無法修改付款狀態'
                        : this.selectedItem.value.approvalStatus === '已釋放'
                            ? '已釋放的預約無法修改付款狀態'
                            : '此狀態無法修改付款狀態';

                addAlert(message, { type: 'warning' });
                return;
            }

            try {
                const payload = {
                    id: this.selectedItem.value.id,
                    paymentStatus: this.selectedItem.value.paymentStatus,
                };

                await global.api.reservations.update({
                    body: payload
                });

                addAlert('儲存成功', { type: 'success' });

                this.bookingDrawerInstance.value?.hide();

                if (this.activeTab.value === 'all') {
                    this.loadAllReservations();
                } else if (this.activeTab.value === 'personal') {
                    this.loadPersonalReservations();
                }

            } catch (err) {
                console.error('❌ 儲存失敗', err);
                addAlert('儲存失敗', { type: 'danger' });
            }
        };

        /* ========= Watch ========= */
        watch(
            () => this.activeTab.value,
            (newTab) => {
                if (newTab !== 'check') {
                    this.checkMode.batchMode = false;
                }

                if (newTab === 'all') {
                    this.loadAllReservations();
                } else if (newTab === 'personal') {
                    this.loadPersonalReservations();
                } else if (newTab === 'check') {
                    this.loadCheckReservations();
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