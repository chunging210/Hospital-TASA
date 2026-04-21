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

        /* ========= 取消預約 Modal ========= */
        this.cancelModal = reactive({
            reason: '',
            warning: '',
            submit: () => {}
        });
        this.cancelModalInstance = ref(null);
        this.isDirector = ref(false);      // ✅ 主任
        this.isAccountant = ref(false);    // ✅ 總務
        this.isRoomManager = ref(false);   // ✅ 房間管理者 / 代理人
        this.currentUserId = ref('');
        this.activeTab = ref('personal');
        this.minAdvanceDays = ref(7);  // ✅ 從設定檔讀取,預設7天

        // ✅ 可以查看「所有預約」的權限（管理員、主任、總務、房間管理者、代理人）
        this.canViewAllReservations = computed(() => {
            return this.isAdmin.value || this.isDirector.value ||
                this.isAccountant.value || this.isRoomManager.value;
        });

        /* ========= 搜尋與篩選 ========= */
        this.searchQuery = ref('');
        // this.dateRange = ref('');
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
        this.approvalHistoryExpanded = ref(false);  // ✅ 審核歷程展開狀態

        /* ========= Bootstrap Instances ========= */
        this.bookingDrawerInstance = ref(null);
        this.batchPayDrawerInstance = ref(null);

        /* ========= ✅ 批次付款 ========= */
        this.selectedForBatch = ref([]);   // 勾選的預約項目
        this.batchPayFiles = ref(null);    // 臨櫃檔案 input ref
        this.batchTransferFile = ref(null); // 匯款截圖 input ref
        this.batchPayForm = reactive({
            mode: 'counter',  // 'counter' | 'transfer'
            counterNote: '',
            last5: '',
            amount: 0,
            transferAt: '',
            transferNote: ''
        });

        /* ========= ✅ 三聯單草稿 ========= */
        this.myDraftOrders = ref([]);
        this.batchSlipLoading = ref(false);
        this.singleSlipLoading = ref(false);

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

        /* ========= ✅ 批次付款 - 計算屬性 ========= */

        // 基本條件：待繳費/待重新上傳 且非成本分攤
        this.isBatchPayable = (item) => {
            const needsPayment =
                (item.approvalStatus === '待繳費' && item.paymentStatus === '未付款') ||
                item.paymentStatus === '待重新上傳';
            return needsPayment && !this.isCostSharingPayment(item.paymentMethod);
        };

        // 與已選清單相容（同 ManagerId + 同 PaymentMethod）
        // 若清單為空，所有符合基本條件的都相容
        this.isBatchCompatible = (item) => {
            if (this.selectedForBatch.value.length === 0) return true;
            const first = this.selectedForBatch.value[0];
            return item.managerId === first.managerId &&
                   item.paymentMethod === first.paymentMethod;
        };

        this.isBatchSelected = (item) => {
            return this.selectedForBatch.value.some(s => s.id === item.id);
        };

        this.batchTotalAmount = computed(() => {
            return this.selectedForBatch.value.reduce((sum, s) => sum + (s.amount || 0), 0);
        });

        this.batchPayMode = computed(() => {
            if (this.selectedForBatch.value.length === 0) return null;
            return this.isCounterPayment(this.selectedForBatch.value[0].paymentMethod)
                ? 'counter' : 'transfer';
        });

        this.batchAllCash = computed(() => {
            return this.selectedForBatch.value.length > 0 &&
                   this.selectedForBatch.value.every(s => s.paymentMethod === 'cash');
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

        // ✅ 審核歷程時間軸樣式
        this.getTimelineClass = (status) => {
            switch (status) {
                case 'Approved': return 'timeline-approved';
                case 'Rejected': return 'timeline-rejected';
                default: return 'timeline-pending';
            }
        };

        // ✅ 審核進度摘要（例如：2/3 關已通過）
        this.getApprovalProgress = (history) => {
            if (!history || history.length === 0) return '';

            // ✅ 優先以最終審核狀態為準
            const finalStatus = this.selectedItem.value?.approvalStatus;
            if (finalStatus === '審核拒絕') return '已拒絕';
            if (finalStatus === '已取消') return '已取消';

            const total = history.length;
            const approved = history.filter(h => h.Status === 'Approved').length;
            const rejected = history.some(h => h.Status === 'Rejected');
            if (rejected) return '已拒絕';
            if (approved === total) return '全部通過';
            return `${approved}/${total} 關已通過`;
        };

        // ✅ 進度徽章樣式
        this.getProgressBadgeClass = (history) => {
            if (!history || history.length === 0) return 'bg-secondary';

            // ✅ 優先以最終審核狀態為準
            const finalStatus = this.selectedItem.value?.approvalStatus;
            if (finalStatus === '審核拒絕') return 'bg-danger';
            if (finalStatus === '已取消') return 'bg-secondary';

            const rejected = history.some(h => h.Status === 'Rejected');
            if (rejected) return 'bg-danger';
            const allApproved = history.every(h => h.Status === 'Approved');
            if (allApproved) return 'bg-success';
            return 'bg-secondary';
        };

        // ✅ 步驟樣式（done/decision/skipped/pending/rejected）
        this.getStepClass = (history) => {
            if (history.Status === 'Rejected') return 'rejected';
            if (history.Status === 'Approved') {
                if (history.Reason?.includes('決行') && !history.Reason?.includes('跳過')) return 'decision';
                if (history.Reason?.includes('跳過')) return 'skipped';
                return 'done';
            }
            return 'pending';
        };

        /* ========= 資料載入 ========= */
        this.loadUserInfo = async () => {
            try {
                const res = await global.api.auth.me();
                const user = res.data;

                this.currentUserId.value = user.Id;
                this.isAdmin.value = user.IsAdmin || false;
                this.isDirector.value = user.IsDirector || false;
                this.isAccountant.value = user.IsAccountant || false;
                this.isRoomManager.value = user.IsRoomManager || false;

                // ✅ 可以看「所有預約」的人預設顯示 all，否則顯示 personal
                if (this.canViewAllReservations.value) {
                    this.activeTab.value = 'all';
                } else {
                    this.activeTab.value = 'personal';
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
                    if (this.paymentStatusFilter.value === 'overdue') {
                        queryParams.reservationStatus = 6;
                    } else {
                        queryParams.paymentStatus = this.paymentStatusFilter.value;
                    }
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
                        contactPhone: item.ContactPhone,  // 聯絡電話
                        contactEmail: item.ContactEmail,  // 電子郵件
                        conferenceName: item.ConferenceName,
                        organizerUnit: item.OrganizerUnit,
                        chairman: item.Chairman,
                        reservationDate: item.Date,
                        timeSlot: item.Time,
                        roomName: item.RoomName,
                        paymentDeadline: item.PaymentDeadline || '-',
                        paymentMethod: item.PaymentMethod || '-',
                        amount: item.TotalAmount,
                        parkingTicketCount: item.ParkingTicketCount || 0,  // ✅ 停車券張數
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
                        'payment': { reservationStatus: 2, paymentStatus: 1 },  // 待繳費（未付款）
                        'reupload': { reservationStatus: 2, paymentStatus: 4 },     // 待重新上傳
                        'confirmed': { reservationStatus: 3 }, // 預約成功
                        'rejected': { reservationStatus: 4 },  // 審核拒絕
                        'cancelled': { reservationStatus: 5 },  // 已取消
                        'overdue': { reservationStatus: 6 }    // 逾期未繳
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
                        reserverName: item.ApplicantName,
                        contactPhone: item.ContactPhone,  // 聯絡電話
                        contactEmail: item.ContactEmail,  // 電子郵件
                        reservationDate: item.Date,
                        conferenceName: item.ConferenceName,
                        organizerUnit: item.OrganizerUnit,
                        chairman: item.Chairman,
                        timeSlot: item.Time,
                        roomName: item.RoomName,
                        paymentDeadline: item.PaymentDeadline || '-',
                        paymentMethod: item.PaymentMethod || '-',
                        amount: item.TotalAmount,
                        parkingTicketCount: item.ParkingTicketCount || 0,  // ✅ 停車券張數
                        paymentStatus: paymentStatus,
                        approvalStatus: item.Status,
                        costCenter: item.DepartmentCode || '-',
                        rejectReason: item.RejectReason || '',
                        paymentRejectReason: item.PaymentRejectReason || '',
                        slots: item.Slots || [],
                        managerId: item.ManagerId || null
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

        // 只允許輸入數字
        /* ========= 詳情相關方法 ========= */
        this.openDetailDrawer = async (item) => {
            try {
                // 調用 API 獲取完整詳情（包含設備、附件等）
                const response = await global.api.reservations.detailview(item.id);
                const detail = response.data;

                this.selectedItem.value = {
                    id: detail.Id,
                    reservationNo: detail.BookingNo,
                    reserverName: detail.ApplicantName,
                    contactPhone: detail.ContactPhone,  // 聯絡電話
                    contactEmail: detail.ContactEmail,  // 電子郵件
                    conferenceName: detail.ConferenceName,
                    description: detail.Description || '',  // 會議內容
                    expectedAttendees: detail.ExpectedAttendees || null,  // ✅ 預計人數
                    organizerUnit: detail.OrganizerUnit,
                    chairman: detail.Chairman,
                    reservationDate: detail.Date,
                    timeSlot: detail.Time,
                    roomName: detail.RoomName,
                    paymentDeadline: detail.PaymentDeadline || '-',
                    paymentMethod: detail.PaymentMethod,
                    amount: detail.TotalAmount,
                    parkingTicketCount: detail.ParkingTicketCount || 0,  // ✅ 停車券張數
                    costCenter: detail.DepartmentCode || '-',
                    paymentStatus: detail.PaymentStatusText,
                    approvalStatus: detail.Status,
                    rejectReason: detail.RejectReason || '',
                    paymentRejectReason: detail.PaymentRejectReason || '',

                    // ✅ 付款憑證
                    paymentFilePath: detail.PaymentFilePath || null,
                    paymentFileName: detail.PaymentFileName || null,
                    paymentNote: detail.PaymentNote || null,
                    discountProofPath: detail.DiscountProofPath || null,
                    discountProofName: detail.DiscountProofName || null,

                    // 新增欄位
                    equipments: detail.Equipments || [],  // 加租設備
                    booths: detail.Booths || [],  // 攤位加租
                    smallBooths: detail.SmallBooths || [],  // 小型攤位
                    attachments: detail.Attachments || [],  // 附件

                    // ✅ 折扣資訊
                    discountAmount: detail.DiscountAmount || null,
                    discountReason: detail.DiscountReason || '',

                    // ✅ 審核歷程
                    approvalHistory: detail.ApprovalHistory || [],

                    // ✅ 取消資訊
                    cancelledByName: detail.CancelledByName || null,
                    cancelledAt: detail.CancelledAt || null,
                    cancelledReason: detail.CancelledReason || null,

                    paymentContactInfo: detail.PaymentContactInfo || null,

                    openedFrom: this.activeTab.value
                };

                // ✅ 重置審核歷程展開狀態
                this.approvalHistoryExpanded.value = false;

                this.bookingDrawerInstance.value?.show();
            } catch (err) {
                console.error('❌ 載入預約詳情失敗:', err);
                addAlert('載入預約詳情失敗', { type: 'danger' });
            }
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

        // 使用者可以取消（待審核 且 尚未有任何人審核）
        this.canCancel = (item) => {
            if (!item) return false;
            if (item.approvalStatus !== '待審核') return false;
            if (item.approvalHistory && item.approvalHistory.some(h => h.Status !== 'Pending')) return false;
            return true;
        };

        // 管理者可以編輯（只有待審核狀態）
        this.canAdminEdit = (item) => {
            if (!item) return false;
            return item.approvalStatus === '待審核';
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

        /* ========= ✅ 批次付款 - 操作方法 ========= */
        this.toggleBatchSelect = (item, event) => {
            event?.stopPropagation();
            if (!this.isBatchPayable(item)) return;
            const idx = this.selectedForBatch.value.findIndex(s => s.id === item.id);
            if (idx >= 0) {
                this.selectedForBatch.value.splice(idx, 1);
            } else {
                this.selectedForBatch.value.push(item);
            }
        };

        this.clearBatchSelection = () => {
            this.selectedForBatch.value = [];
        };

        // ── 下載單筆繳費通知單（先建草稿 order 再開新分頁）──
        this.downloadSingleSlip = async (item) => {
            if (!item) return;
            this.singleSlipLoading.value = true;
            try {
                const res = await global.api.payment.createDraft({ body: { ConferenceIds: [item.id] } });
                window.open(`/ReservationOverview/PaymentSlip?orderId=${res.data.orderId}`, '_blank');
            } catch (e) {
                addAlert(e?.response?.data?.message || '產生通知單失敗', { type: 'danger' });
            } finally {
                this.singleSlipLoading.value = false;
            }
        };

        // ── 下載合併繳費通知單（先建草稿 order 再開新分頁）──
        this.downloadBatchSlip = async () => {
            if (this.selectedForBatch.value.length === 0) return;
            this.batchSlipLoading.value = true;
            try {
                const ids = this.selectedForBatch.value.map(s => s.id);
                const res = await global.api.payment.createDraft({ body: { ConferenceIds: ids } });
                window.open(`/ReservationOverview/PaymentSlip?orderId=${res.data.orderId}`, '_blank');
            } catch (e) {
                addAlert(e?.response?.data?.message || '產生通知單失敗', { type: 'danger' });
            } finally {
                this.batchSlipLoading.value = false;
            }
        };

        // ── 載入草稿清單 ──
        this.loadMyDraftOrders = async () => {
            try {
                const res = await global.api.payment.myDrafts();
                this.myDraftOrders.value = res.data;
            } catch { /* 靜默失敗，不影響主流程 */ }
        };

        // ── 將草稿 order 的預約載入 selectedForBatch ──
        this.loadDraftOrder = (draft) => {
            // 找到 personalReservations 中對應的項目
            const matched = draft.items
                .map(di => this.personalReservations.value.find(r => r.id === di.conferenceId))
                .filter(Boolean);
            if (matched.length === 0) {
                addAlert('找不到對應的預約，可能狀態已變更', { type: 'warning' });
                return;
            }
            this.selectedForBatch.value = matched;
            addAlert(`已載入 ${matched.length} 筆預約`, { type: 'success' });
        };

        this.openBatchPayDrawer = () => {
            if (this.selectedForBatch.value.length === 0) {
                addAlert('請先勾選要付款的預約', { type: 'warning' });
                return;
            }
            // 每次開啟都重新載入草稿清單
            this.loadMyDraftOrders();
            // 預填金額（匯款模式）
            if (this.batchPayMode.value === 'transfer') {
                this.batchPayForm.amount = this.batchTotalAmount.value;
            }
            this.batchPayDrawerInstance.value?.show();
        };

        this.submitBatchCounter = async () => {
            const fileInput = this.batchPayFiles.value;
            if (!fileInput?.files?.length) {
                addAlert('請上傳付款憑證', { type: 'warning' });
                return;
            }
            try {
                const formData = new FormData();
                const ids = this.selectedForBatch.value.map(s => s.reservationNo);
                formData.append('reservationIds', JSON.stringify(ids));
                formData.append('note', this.batchPayForm.counterNote || '');
                for (let i = 0; i < fileInput.files.length; i++) {
                    formData.append('files', fileInput.files[i]);
                }
                await global.api.payment.uploadcounter({ body: formData });
                addAlert('憑證已上傳，等待審核', { type: 'success' });
                this.batchPayDrawerInstance.value?.hide();
                this.clearBatchSelection();
                this.batchPayForm.counterNote = '';
                fileInput.value = '';
                await this.loadPersonalReservations(true);
            } catch (err) {
                addAlert(`上傳失敗: ${err.message || '未知錯誤'}`, { type: 'danger' });
            }
        };

        this.submitBatchTransfer = async () => {
            if (!this.batchPayForm.last5 || this.batchPayForm.last5.length !== 5) {
                addAlert('請輸入正確的 5 碼轉帳末碼', { type: 'warning' });
                return;
            }
            if (!this.batchPayForm.amount || this.batchPayForm.amount <= 0) {
                addAlert('請輸入正確的金額', { type: 'warning' });
                return;
            }
            try {
                const formData = new FormData();
                const ids = this.selectedForBatch.value.map(s => s.reservationNo);
                formData.append('reservationIds', JSON.stringify(ids));
                formData.append('last5', this.batchPayForm.last5);
                formData.append('amount', parseInt(this.batchPayForm.amount));
                if (this.batchPayForm.transferAt) {
                    formData.append('transferAt', this.batchPayForm.transferAt);
                }
                formData.append('note', this.batchPayForm.transferNote || '');
                const ssInput = this.batchTransferFile?.value;
                if (ssInput?.files?.[0]) {
                    formData.append('screenshotFile', ssInput.files[0]);
                }
                await global.api.payment.transfer({ body: formData });
                addAlert('匯款資訊已提交，等待審核', { type: 'success' });
                this.batchPayDrawerInstance.value?.hide();
                this.clearBatchSelection();
                Object.assign(this.batchPayForm, { last5: '', amount: 0, transferAt: '', transferNote: '' });
                if (ssInput) ssInput.value = '';
                await this.loadPersonalReservations(true);
            } catch (err) {
                addAlert(`提交失敗: ${err.message || '未知錯誤'}`, { type: 'danger' });
            }
        };

        // 確認取消預約
        this.confirmCancel = (item) => {
            this.cancelModal.reason = '';
            this.cancelModal.warning = this.getCancelWarning(item);
            this.cancelModal.submit = async () => {
                try {
                    this.cancelModalInstance.value?.hide();
                    await global.api.reservations.cancel({
                        body: { reservationId: item.id, reason: this.cancelModal.reason || null }
                    });
                    addAlert('預約已取消', { type: 'success' });
                    this.bookingDrawerInstance.value?.hide();
                    await this.loadPersonalReservations(true);
                } catch (err) {
                    console.error('❌ 取消預約失敗:', err);
                    addAlert(err?.message || '取消預約失敗', { type: 'danger' });
                }
            };
            this.cancelModalInstance.value?.show();
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
                await this.loadPersonalReservations(true);

            } catch (err) {
                console.error('❌ 刪除預約失敗:', err);
                addAlert('刪除預約失敗', { type: 'danger' });
            }
        };

        /* ========= ✅ 管理員操作功能 ========= */

        // 管理員可以取消的狀態（排除已取消、審核拒絕）
        this.canAdminCancel = (item) => {
            if (!item) return false;
            return !['已取消', '審核拒絕'].includes(item.approvalStatus);
        };

        // 管理員確認取消預約
        this.confirmAdminCancel = (item) => {
            this.cancelModal.reason = '';
            this.cancelModal.warning = `取消「${item.conferenceName}」的預約（${item.reserverName}）`;
            this.cancelModal.submit = async () => {
                try {
                    this.cancelModalInstance.value?.hide();
                    await global.api.reservations.cancel({
                        body: { reservationId: item.id, reason: this.cancelModal.reason || null }
                    });
                    addAlert('預約已取消', { type: 'success' });
                    this.bookingDrawerInstance.value?.hide();
                    await this.loadAllReservations(true);
                } catch (err) {
                    console.error('❌ 取消預約失敗:', err);
                    addAlert(err?.message || '取消預約失敗', { type: 'danger' });
                }
            };
            this.cancelModalInstance.value?.show();
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

            const batchPayDrawerEl = document.getElementById('batchPayDrawer');
            if (batchPayDrawerEl) {
                this.batchPayDrawerInstance.value =
                    bootstrap.Offcanvas.getOrCreateInstance(batchPayDrawerEl);
            }

            const cancelModalEl = document.getElementById('cancelReasonModal');
            if (cancelModalEl) {
                this.cancelModalInstance.value = new bootstrap.Modal(cancelModalEl);
            }
        });

    }
};