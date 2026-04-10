// Admin/SysRoom
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

let currentUser = null;
const isAdmin = ref(false);  // ✅ 改用 ref
const isGlobalAdmin = ref(false);  // ✅ 全院管理者
const isDepartmentAdmin = ref(false);  // ✅ 分院管理者
const userDepartmentId = ref(null);  // ✅ 改用 ref
const userDepartmentName = ref('');  // ✅ 改用 ref

class VM {
    Id = null;
    Name = '';
    Building = '';
    Floor = '';
    Description = '';
    Capacity = null;
    Area = null;
    Status = 0;
    PricingType = 1;
    BookingSettings = 0;
    IsEnabled = true;
    DepartmentId = null;
    ManagerId = null;
    AgreementPath = null;
    EnableParkingTicket = false;  // ✅ 停車券功能
    ParkingTicketPrice = 100;     // ✅ 停車券單價
    Sequence = 0;                 // ✅ 排序順序
    PaymentContactInfo = '';      // 付款聯絡資訊
    AllowTransfer = true;         // 允許銀行匯款
    AllowCash = true;             // 允許現金繳費
    AllowCostSharing = true;      // 允許成本分攤
}

// ✅ Enum 定義（與後端對應）
const RoomStatus = {
    Available: 0,
    Maintenance: 1
};

const PricingType = {
    Hourly: 0,
    Period: 1
};

const BookingSettings = {
    InternalOnly: 0,
    InternalAndExternal: 1,
    Closed: 2,
    Free: 3
};
const getBookingSettingsClass = (bookingSettings) => {
    // ✅ 處理 null/undefined
    if (bookingSettings === null || bookingSettings === undefined) {
        return 'status-internal';  // 預設樣式
    }

    const classMap = {
        [BookingSettings.InternalOnly]: 'status-internal',
        [BookingSettings.InternalAndExternal]: 'status-open',
        [BookingSettings.Closed]: 'status-closed',
        [BookingSettings.Free]: 'status-free'
    };
    return classMap[bookingSettings] || 'status-internal';
};


const getStatusText = (status) => {
    const statusMap = {
        [RoomStatus.Available]: '可用',
        [RoomStatus.Maintenance]: '維護中'
    };
    return statusMap[status] || String(status);
};

const getStatusClass = (status) => {
    const classMap = {
        [RoomStatus.Available]: 'status-available',
        [RoomStatus.Maintenance]: 'status-maintenance'
    };
    return classMap[status] || '';
};

const getPricingTypeText = (pricingType) => {
    const typeMap = {
        [PricingType.Hourly]: '時租',
        [PricingType.Period]: '時段'
    };
    return typeMap[pricingType] || String(pricingType);
};

const getBookingSettingsText = (bookingSettings) => {
    // ✅ 處理 null/undefined
    if (bookingSettings === null || bookingSettings === undefined) {
        return '未設定';  // 或改成 '僅限內部'(給預設值)
    }

    const settingsMap = {
        [BookingSettings.InternalOnly]: '僅限內部',
        [BookingSettings.InternalAndExternal]: '內外皆可',
        [BookingSettings.Closed]: '不開放租借',
        [BookingSettings.Free]: '免費使用'
    };
    return settingsMap[bookingSettings] || '未設定';
};


const isVideoFile = (filePath) => {
    if (!filePath) return false;
    const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
    return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
};

// ✅ 生成 30 分鐘間隔的時間選項 (00:00, 00:30, 01:00, ..., 23:30)
const timeOptions = [];
for (let hour = 0; hour < 24; hour++) {
    for (let min = 0; min < 60; min += 30) {
        const timeStr = `${hour.toString().padStart(2, '0')}:${min.toString().padStart(2, '0')}`;
        timeOptions.push(timeStr);
    }
}

const department = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.department()
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(err => {
                console.error('取得分院列表失敗:', err);
            });
    }
}
const manager = new function () {
    this.list = reactive([]);
    this.searchKeyword = ref('');
    this.selectedManager = ref(null);

    // ✅ 加上 departmentId 參數
    // ✅ 修改這裡
    this.getList = async (departmentId = null) => {
        try {
            console.log('🔍 [前端] 準備載入員工,分院ID:', departmentId);

            // ✅ 關鍵:如果有 departmentId 才傳,否則不傳 body
            const params = departmentId
                ? { body: { departmentId } }  // ← 會自動轉成 ?departmentId=xxx
                : {};

            const response = await global.api.select.internaluser(params);

            console.log('✅ [前端] API 回應:', response.data);

            copy(this.list, response.data || []);
            console.log(`✅ 載入內部員工列表: ${this.list.length} 人`);
        } catch (err) {
            console.error('❌ 取得員工列表失敗:', err);
            this.list.splice(0);
        }
    };

    // 搜尋使用者
    this.filteredList = computed(() => {
        if (!this.searchKeyword.value) return this.list;
        const keyword = this.searchKeyword.value.toLowerCase();
        return this.list.filter(u =>
            u.Name?.toLowerCase().includes(keyword) ||
            u.Email?.toLowerCase().includes(keyword) ||
            u.RoleDisplay?.toLowerCase().includes(keyword)
        );
    });

    // 選擇管理者
    this.selectManager = (user) => {
        this.selectedManager.value = user;
        console.log('✅ 選擇管理者:', user.Name);
    };

    // 清除選擇
    this.clearSelection = () => {
        this.selectedManager.value = null;
        this.searchKeyword.value = '';
    };
};

// ✅ 審核關卡管理
const approvalChain = new function () {
    this.levels = reactive([]);  // 審核關卡列表
    this.availableApprovers = reactive([]);  // 可選的審核人
    this.searchKeyword = ref('');
    this.isAddingLevel = ref(false);

    // 載入會議室的審核關卡
    this.loadLevels = async (roomId) => {
        if (!roomId) {
            this.levels.splice(0);
            return;
        }

        try {
            const response = await fetch(`/api/RoomApprovalLevel/list/${roomId}`);
            if (!response.ok) throw new Error('載入失敗');
            const data = await response.json();
            copy(this.levels, data || []);
            console.log(`✅ 載入審核關卡: ${this.levels.length} 關`);
        } catch (err) {
            console.error('❌ 載入審核關卡失敗:', err);
            this.levels.splice(0);
        }
    };

    // 載入可選的審核人
    this.loadAvailableApprovers = async (roomId) => {
        // 如果有 roomId，從 API 載入
        if (roomId) {
            try {
                // 排除已選的審核人
                const excludeIds = this.levels.map(l => l.ApproverId);
                const queryString = excludeIds.length > 0
                    ? `?${excludeIds.map(id => `excludeIds=${id}`).join('&')}`
                    : '';

                const response = await fetch(`/api/RoomApprovalLevel/approvers/${roomId}${queryString}`);
                if (!response.ok) throw new Error('載入失敗');
                const data = await response.json();
                copy(this.availableApprovers, data || []);
                console.log(`✅ 載入可選審核人: ${this.availableApprovers.length} 人`);
            } catch (err) {
                console.error('❌ 載入可選審核人失敗:', err);
                this.availableApprovers.splice(0);
            }
        } else {
            // 新增模式：使用 manager.list 作為可選審核人來源
            this.refreshFromManagerList();
        }
    };

    // 從 manager.list 刷新可選審核人（用於新增模式）
    this.refreshFromManagerList = () => {
        const excludeIds = this.levels.map(l => l.ApproverId);
        const filtered = manager.list.filter(u => !excludeIds.includes(u.Id));
        copy(this.availableApprovers, filtered.map(u => ({
            Id: u.Id,
            Name: u.Name,
            Email: u.Email,
            UnitName: u.UnitName  // 部門
        })));
        console.log(`✅ 從員工列表刷新可選審核人: ${this.availableApprovers.length} 人`);
    };

    // 過濾審核人列表
    this.filteredApprovers = computed(() => {
        if (!this.searchKeyword.value) return this.availableApprovers;
        const keyword = this.searchKeyword.value.toLowerCase();
        return this.availableApprovers.filter(u =>
            u.Name?.toLowerCase().includes(keyword) ||
            u.Email?.toLowerCase().includes(keyword) ||
            u.UnitName?.toLowerCase().includes(keyword)  // 部門搜尋
        );
    });

    // 開始新增關卡
    this.addLevel = () => {
        // 檢查是否已選擇分院
        if (!room.vm.DepartmentId) {
            addAlert('請先選擇分院', { type: 'warning' });
            return;
        }

        // 檢查員工列表是否已載入
        if (manager.list.length === 0) {
            addAlert('員工列表尚未載入，請稍後再試', { type: 'warning' });
            return;
        }

        this.isAddingLevel.value = true;
        this.searchKeyword.value = '';
        // 使用 manager.list（已根據選擇的分院載入正確的員工）
        this.refreshFromManagerList();
    };

    // 取消新增
    this.cancelAdd = () => {
        this.isAddingLevel.value = false;
        this.searchKeyword.value = '';
    };

    // 選擇審核人
    this.selectApprover = (user) => {
        // 檢查是否重複
        if (this.levels.some(l => l.ApproverId === user.Id)) {
            addAlert('此審核人已在關卡中', { type: 'warning' });
            return;
        }

        this.levels.push({
            Id: null,  // 新增的還沒有 ID
            Level: this.levels.length + 1,
            ApproverId: user.Id,
            ApproverName: user.Name,
            ApproverEmail: user.Email,
            ApproverUnitName: user.UnitName  // 部門
        });

        this.isAddingLevel.value = false;
        this.searchKeyword.value = '';

        // 更新可選審核人列表（移除已選的）
        const idx = this.availableApprovers.findIndex(a => a.Id === user.Id);
        if (idx > -1) {
            this.availableApprovers.splice(idx, 1);
        }

        console.log(`✅ 新增審核關卡: ${user.Name}`);
    };

    // 移除關卡
    this.removeLevel = (index) => {
        const removed = this.levels.splice(index, 1)[0];
        if (removed) {
            // 將移除的審核人加回可選列表
            this.availableApprovers.push({
                Id: removed.ApproverId,
                Name: removed.ApproverName,
                Email: removed.ApproverEmail,
                UnitName: removed.ApproverUnitName
            });
        }
        console.log(`✅ 移除審核關卡: index=${index}`);
    };

    // 儲存審核關卡
    this.save = async (roomId) => {
        if (!roomId) return;

        try {
            const response = await fetch('/api/RoomApprovalLevel/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    RoomId: roomId,
                    Approvers: this.levels.map(l => ({ ApproverId: l.ApproverId }))
                })
            });
            if (!response.ok) throw new Error('儲存失敗');
            console.log('✅ 儲存審核關卡成功');
        } catch (err) {
            console.error('❌ 儲存審核關卡失敗:', err);
            throw err;
        }
    };

    // 清空
    this.clear = () => {
        this.levels.splice(0);
        this.availableApprovers.splice(0);
        this.isAddingLevel.value = false;
        this.searchKeyword.value = '';
    };
};
let imageIndices = reactive({});

const loadCurrentUser = async () => {
    try {
        const userRes = await global.api.auth.me();
        currentUser = userRes.data;
        isAdmin.value = currentUser.IsAdmin || false;
        isGlobalAdmin.value = currentUser.IsGlobalAdmin || false;  // ✅ 全院管理者
        isDepartmentAdmin.value = currentUser.IsDepartmentAdmin || false;  // ✅ 分院管理者
        userDepartmentId.value = currentUser.DepartmentId;
        userDepartmentName.value = currentUser.DepartmentName || '';

        console.log('✅ 使用者資訊:', {
            name: currentUser.Name,
            isAdmin: isAdmin.value,
            isGlobalAdmin: isGlobalAdmin.value,
            isDepartmentAdmin: isDepartmentAdmin.value,
            departmentId: userDepartmentId.value,
            departmentName: userDepartmentName.value
        });
    } catch (err) {
        console.error('❌ 無法取得使用者資訊:', err);
    }
};

const room = new function () {
    this.query = reactive({ keyword: '' });
    this.list = reactive([]);
    this.form = reactive({
        name: '',
        building: '',
        floor: '',
        description: '',
        capacity: null,
        area: null,
        feeType: PricingType.Period,
        rentalType: BookingSettings.InternalOnly,
        departmentId: null,
        managerId: null,
        agreementBase64: null,      // ✅ 聲明書 Base64
        agreementFileName: null,    // ✅ 聲明書檔名
        panoramaBase64: null,       // 全景圖 Base64
        panoramaFileName: null      // 全景圖檔名
    });
    // ✅ 新增:今日時程
    this.todaySchedule = ref([]);
    this.scheduleRefreshInterval = null;

    // editModal removed - using offcanvas for both create and edit
    this.page = {};
    this.mediaFiles = reactive([]);
    this.timeSlots = reactive([]);
    // this.hourlySlots = ref([]);

    this.detailRoom = ref(null);
    this.detailRoomCarouselIndex = ref(0);
    this.carouselInterval = null;
    this.carouselDirection = 'next';
    this.panoramaMode = ref(false);
    this._pannellumViewer = null;

    this.setPanoramaMode = (enabled) => {
        this.panoramaMode.value = enabled;
        if (enabled && this.detailRoom.value?.PanoramaUrl) {
            this.stopCarousel();
            setTimeout(() => {
                if (this._pannellumViewer) {
                    this._pannellumViewer.destroy();
                    this._pannellumViewer = null;
                }
                this._pannellumViewer = pannellum.viewer('panorama-viewer', {
                    type: 'equirectangular',
                    panorama: this.detailRoom.value.PanoramaUrl,
                    autoLoad: true,
                    showZoomCtrl: true,
                    mouseZoom: true,
                    strings: { loadButtonLabel: '點擊載入全景圖', loadingLabel: '載入中...' }
                });
            }, 100);
        } else {
            if (this._pannellumViewer) {
                this._pannellumViewer.destroy();
                this._pannellumViewer = null;
            }
            this.startCarousel();
        }
    };

    // ✅ 載入今日時程
    this.loadTodaySchedule = async (roomId) => {
        if (!roomId) return;

        try {
            const res = await global.api.select.roombyschedule({
                body: { roomId: roomId }
            });
            this.todaySchedule.value = res.data || [];
        } catch (err) {
            console.error('❌ 載入今日時程失敗:', err);
            this.todaySchedule.value = [];
        }
    };

    // ✅ 啟動自動重新整理
    this.startScheduleRefresh = (roomId) => {
        this.stopScheduleRefresh();
        this.loadTodaySchedule(roomId);

        this.scheduleRefreshInterval = setInterval(() => {
            this.loadTodaySchedule(roomId);
        }, 60000); // 每 1 分鐘
    };

    // ✅ 停止自動重新整理
    this.stopScheduleRefresh = () => {
        if (this.scheduleRefreshInterval) {
            clearInterval(this.scheduleRefreshInterval);
            this.scheduleRefreshInterval = null;
        }
    };

    // ✅ 狀態轉換
    this.getStatusBadgeClass = (status) => {
        const classMap = {
            'upcoming': 'bg-warning',
            'ongoing': 'bg-danger',
            'completed': 'bg-success'
        };
        return classMap[status] || 'bg-secondary';
    };

    this.getStatusText = (status) => {
        const textMap = {
            'upcoming': '待開始',
            'ongoing': '進行中',
            'completed': '已完成'
        };
        return textMap[status] || '未知';
    };

    // ✅ 新增：啟動自動輪播
    this.startCarousel = () => {
        // 清除舊的計時器
        if (this.carouselInterval) {
            clearInterval(this.carouselInterval);
        }

        // 每 3 秒自動切換一張
        this.carouselInterval = setInterval(() => {
            if (!this.detailRoom.value || !this.detailRoom.value.Images) return;
            this.carouselDirection = 'next';
            const length = this.detailRoom.value.Images.length;
            this.detailRoomCarouselIndex.value = (this.detailRoomCarouselIndex.value + 1) % length;
        }, 5000);  // 3 秒換一張（可改為 1000、2000 等）
    };

    // ✅ 新增：停止自動輪播
    this.stopCarousel = () => {
        if (this.carouselInterval) {
            clearInterval(this.carouselInterval);
            this.carouselInterval = null;
        }
    };

    this.prevDetailImage = () => {
        if (!this.detailRoom.value || !this.detailRoom.value.Images) return;
        this.stopCarousel();  // 停止自動輪播
        this.carouselDirection = 'prev';
        const length = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value = (this.detailRoomCarouselIndex.value - 1 + length) % length;
        this.startCarousel();  // 重新啟動輪播
    }

    this.nextDetailImage = () => {
        if (!this.detailRoom.value || !this.detailRoom.value.Images) return;
        this.stopCarousel();  // 停止自動輪播
        this.carouselDirection = 'next';
        const length = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value = (this.detailRoomCarouselIndex.value + 1) % length;
        this.startCarousel();  // 重新啟動輪播
    }


    // this.createHourlySlot = (hour, data = {}) => {
    //     return {
    //         Id: data.Id ?? null,
    //         Hour: hour,
    //         Checked: data.Checked ?? false,
    //         Fee: data.Fee ?? 500
    //     };
    // };

    this.createPeriodSlot = (data = {}) => {
        const st = (data.StartTime ?? '09:00').split(':');
        const et = (data.EndTime ?? '10:00').split(':');
        return {
            Id: data.Id ?? `tmp_${Date.now()}_${Math.random()}`,
            Name: data.Name ?? '新時段',
            StartTime: data.StartTime ?? '09:00',
            EndTime: data.EndTime ?? '10:00',
            StartHour: st[0], StartMin: st[1] ?? '00',
            EndHour: et[0], EndMin: et[1] ?? '00',
            Price: data.Price ?? 0,
            HolidayPrice: data.HolidayPrice ?? 0,
            SetupPrice: data.SetupPrice ?? null,
            Enabled: data.Enabled ?? true,
            Slots: reactive((data.Slots ?? []).map(s => ({
                Id: `tmp_${Date.now()}_${Math.random()}`,
                StartHour: (s.StartTime ?? '09:00').split(':')[0],
                StartMin: (s.StartTime ?? '09:00').split(':')[1] ?? '00',
                EndHour: (s.EndTime ?? '10:00').split(':')[0],
                EndMin: (s.EndTime ?? '10:00').split(':')[1] ?? '00'
            }))),
            // 新增子區間用的暫存 picker 狀態
            pendingSubSlot: reactive({ show: false, StartHour: '09', StartMin: '00', EndHour: '10', EndMin: '00', error: '' })
        };
    };

    this.openAddSubSlot = (parentSlot) => {
        parentSlot.pendingSubSlot.StartHour = parentSlot.StartHour;
        parentSlot.pendingSubSlot.StartMin = parentSlot.StartMin;
        parentSlot.pendingSubSlot.EndHour = parentSlot.StartHour;
        parentSlot.pendingSubSlot.EndMin = parentSlot.StartMin;
        parentSlot.pendingSubSlot.error = '';
        parentSlot.pendingSubSlot.show = true;
    };

    this.confirmAddSubSlot = (parentSlot) => {
        const p = parentSlot.pendingSubSlot;
        const toMin = (h, m) => parseInt(h) * 60 + parseInt(m);

        const newStart = toMin(p.StartHour, p.StartMin);
        const newEnd   = toMin(p.EndHour, p.EndMin);
        const pStart   = toMin(parentSlot.StartHour, parentSlot.StartMin);
        const pEnd     = toMin(parentSlot.EndHour, parentSlot.EndMin);

        // 1. 開始必須早於結束
        if (newStart >= newEnd) {
            p.error = '開始時間必須早於結束時間';
            return;
        }
        // 2. 必須在主區間範圍內
        if (newStart < pStart || newEnd > pEnd) {
            p.error = `子區間必須在主區間範圍內（${parentSlot.StartHour}:${parentSlot.StartMin} — ${parentSlot.EndHour}:${parentSlot.EndMin}）`;
            return;
        }
        // 3. 不能與現有子區間重疊
        const overlap = parentSlot.Slots.some(s => {
            const sStart = toMin(s.StartHour, s.StartMin);
            const sEnd   = toMin(s.EndHour, s.EndMin);
            return newStart < sEnd && newEnd > sStart;
        });
        if (overlap) {
            p.error = '與已設定的子區間時間重疊';
            return;
        }

        parentSlot.Slots.push({
            Id: `tmp_${Date.now()}_${Math.random()}`,
            StartHour: p.StartHour,
            StartMin: p.StartMin,
            EndHour: p.EndHour,
            EndMin: p.EndMin
        });
        p.show = false;
        p.error = '';
    };

    this.cancelAddSubSlot = (parentSlot) => {
        parentSlot.pendingSubSlot.show = false;
        parentSlot.pendingSubSlot.error = '';
    };

    this.removeSubSlot = (parentSlot, index) => {
        parentSlot.Slots.splice(index, 1);
    };

    this.onParentTimeChange = (parentSlot) => {
        if (!parentSlot.Slots || parentSlot.Slots.length === 0) return;
        const toMin = (h, m) => parseInt(h) * 60 + parseInt(m);
        const pStart = toMin(parentSlot.StartHour, parentSlot.StartMin);
        const pEnd   = toMin(parentSlot.EndHour, parentSlot.EndMin);

        const removed = parentSlot.Slots.filter(s => {
            const sStart = toMin(s.StartHour, s.StartMin);
            const sEnd   = toMin(s.EndHour, s.EndMin);
            return sStart < pStart || sEnd > pEnd;
        });

        if (removed.length > 0) {
            // 移除超出範圍的子區間
            for (let i = parentSlot.Slots.length - 1; i >= 0; i--) {
                const s = parentSlot.Slots[i];
                const sStart = toMin(s.StartHour, s.StartMin);
                const sEnd   = toMin(s.EndHour, s.EndMin);
                if (sStart < pStart || sEnd > pEnd) {
                    parentSlot.Slots.splice(i, 1);
                }
            }
            addAlert(`已自動移除 ${removed.length} 個超出主區間範圍的子區間`, { type: 'warning' });
        }
    };

    this.getList = (page) => {
        if (typeof page === 'number') {
            this.page.data.page = page;
        } else if (!this.page.data.page) {
            this.page.data.page = 1;
        }

        global.api.admin.roomlist(
            this.page.setHeaders({ body: this.query })
        )
            .then(this.page.setTotal)
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    };

    this.offcanvas = null;
    this.vm = reactive(new VM());

    this.getVM = (Id) => {
        this.mediaFiles.splice(0);
        this.timeSlots.splice(0);

        if (Id) {
            global.api.admin.roomdetail({ body: { Id } })
                .then(async (response) => {  // ✅ 改成 async
                    this.vm.Id = response.data.Id;
                    this.vm.Name = response.data.Name;
                    this.vm.Building = response.data.Building;
                    this.vm.Floor = response.data.Floor;
                    this.vm.Description = response.data.Description;
                    this.vm.Capacity = response.data.Capacity;
                    this.vm.Area = response.data.Area;
                    this.vm.Status = response.data.Status;
                    this.vm.PricingType = response.data.PricingType;
                    this.vm.IsEnabled = response.data.IsEnabled;
                    this.vm.BookingSettings = response.data.BookingSettings;
                    this.vm.DepartmentId = response.data.DepartmentId || response.data.Department?.Id;
                    this.vm.ManagerId = response.data.ManagerId;
                    this.vm.AgreementPath = response.data.AgreementPath;  // ✅ 聲明書路徑
                    this.vm.PanoramaUrl = response.data.PanoramaUrl || null;
                    this.vm.EnableParkingTicket = response.data.EnableParkingTicket || false;  // ✅ 停車券
                    this.vm.ParkingTicketPrice = response.data.ParkingTicketPrice || 100;
                    this.vm.Sequence = response.data.Sequence || 0;  // ✅ 排序順序
                    this.vm.PaymentContactInfo = response.data.PaymentContactInfo || '';
                    this.vm.AllowTransfer = response.data.AllowTransfer ?? true;
                    this.vm.AllowCash = response.data.AllowCash ?? true;
                    this.vm.AllowCostSharing = response.data.AllowCostSharing ?? true;

                    // ✅ 編輯時清空新上傳的聲明書
                    this.form.agreementBase64 = null;
                    this.form.agreementFileName = null;

                    // ✅ 1. 先載入員工列表
                    await manager.getList(this.vm.DepartmentId);

                    // ✅ 2. 等員工列表載入完成後,再設定已選擇的管理者
                    if (response.data.Manager) {
                        // ✅ 從員工列表中找到對應的管理者(確保資料一致)
                        const managerFromList = manager.list.find(
                            u => u.Id === response.data.Manager.Id
                        );

                        if (managerFromList) {
                            // ✅ 如果在列表中找到,用列表的資料
                            manager.selectedManager.value = managerFromList;
                            console.log('✅ [編輯] 從列表中找到管理者:', managerFromList.Name);
                        } else {
                            // ✅ 找不到的話,用 API 回傳的資料
                            manager.selectedManager.value = {
                                Id: response.data.Manager.Id,
                                Name: response.data.Manager.Name,
                                Email: response.data.Manager.Email,
                                RoleDisplay: response.data.Manager.RoleDisplay || '員工'
                            };
                            console.log('⚠️ [編輯] 管理者不在列表中,使用 API 資料');
                        }
                    } else {
                        // ✅ 沒有管理者,確保清空
                        manager.clearSelection();
                        console.log('✅ [編輯] 無管理者');
                    }

                    // ✅ 處理圖片
                    (response.data.Images || []).forEach((imgPath, idx) => {
                        this.mediaFiles.push({
                            Id: Date.now() + idx,
                            type: isVideoFile(imgPath) ? 'video' : 'image',
                            src: imgPath,
                            name: ''
                        });
                    });

                    // ✅ 處理時段
                    this.vm.Images = [];
                    this.vm.PricingDetails = response.data.PricingDetails || [];
                    if (response.data.PricingType === PricingType.Period) {
                        this.timeSlots.splice(0);
                        response.data.PricingDetails?.forEach(p => {
                            this.timeSlots.push(this.createPeriodSlot(p));
                        });
                    }

                    // ✅ 載入審核關卡
                    await approvalChain.loadLevels(response.data.Id);
                    await approvalChain.loadAvailableApprovers(response.data.Id);

                    if (this.offcanvas) {
                        this.offcanvas.show();
                    }
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            // ✅ 新增模式
            this.vm.Id = null;
            this.vm.Name = '';
            this.vm.Building = '';
            this.vm.Floor = '';
            this.vm.Description = '';
            this.vm.Capacity = null;
            this.vm.Area = null;
            this.vm.Status = RoomStatus.Available;
            this.vm.PricingType = PricingType.Period;
            this.vm.BookingSettings = BookingSettings.InternalOnly;
            this.vm.AgreementPath = null;
            this.vm.PanoramaUrl = null;
            this.vm.EnableParkingTicket = false;  // ✅ 停車券
            this.vm.ParkingTicketPrice = 100;
            this.generateCreateTimeSlotDefaults();

            this.vm.ManagerId = null;
            this.form.agreementBase64 = null;    // ✅ 重設聲明書
            this.form.agreementFileName = null;
            this.form.panoramaBase64 = null;
            this.form.panoramaFileName = null;
            manager.clearSelection();
            approvalChain.clear();  // ✅ 清空審核關卡

            if (!isAdmin.value && userDepartmentId.value) {
                this.vm.DepartmentId = userDepartmentId.value;
                manager.getList(userDepartmentId.value);
            } else {
                this.vm.DepartmentId = null;
                manager.list.splice(0);
            }

            if (this.offcanvas) {
                this.offcanvas.show();
            }
        }
    }

    this.save = async () => {
        const method = this.vm.Id
            ? global.api.admin.roomupdate
            : global.api.admin.roominsert;

        // 驗證子區間
        if (this.vm.PricingType === PricingType.Period) {
            const enabledSlots = this.timeSlots.filter(s => s.Enabled);
            const withSub    = enabledSlots.filter(s => s.Slots && s.Slots.length > 0).length;
            const withoutSub = enabledSlots.filter(s => !s.Slots || s.Slots.length === 0).length;

            if (withSub > 0 && withoutSub > 0) {
                addAlert('子區間設定不一致：請所有時段都設定子區間，或全部都不設定', { type: 'danger' });
                return;
            }

            if (withSub > 0) {
                const toMin = (h, m) => parseInt(h) * 60 + parseInt(m);
                for (const slot of enabledSlots) {
                    const pStart = toMin(slot.StartHour, slot.StartMin);
                    const pEnd   = toMin(slot.EndHour, slot.EndMin);
                    const sorted = [...slot.Slots].sort((a, b) =>
                        toMin(a.StartHour, a.StartMin) - toMin(b.StartHour, b.StartMin));

                    if (toMin(sorted[0].StartHour, sorted[0].StartMin) !== pStart) {
                        addAlert(`時段「${slot.Name}」的子區間未從主區間起始時間開始`, { type: 'danger' });
                        return;
                    }
                    if (toMin(sorted[sorted.length - 1].EndHour, sorted[sorted.length - 1].EndMin) !== pEnd) {
                        addAlert(`時段「${slot.Name}」的子區間未覆蓋到主區間結束時間`, { type: 'danger' });
                        return;
                    }
                    for (let i = 0; i < sorted.length - 1; i++) {
                        const curEnd  = toMin(sorted[i].EndHour, sorted[i].EndMin);
                        const nextStart = toMin(sorted[i + 1].StartHour, sorted[i + 1].StartMin);
                        if (curEnd !== nextStart) {
                            addAlert(`時段「${slot.Name}」的子區間有空隙（${sorted[i].EndHour}:${sorted[i].EndMin} 到 ${sorted[i+1].StartHour}:${sorted[i+1].StartMin}）`, { type: 'danger' });
                            return;
                        }
                    }
                }
            }
        }

        // 驗證付款方式至少勾一個
        if (!this.vm.AllowTransfer && !this.vm.AllowCash && !this.vm.AllowCostSharing) {
            addAlert('請至少勾選一種付款方式', { type: 'danger' });
            return;
        }

        // 驗證：內外皆可的會議室，外部人員必須有可用的付款方式（銀行匯款或現金繳費）
        if (this.vm.BookingSettings === BookingSettings.InternalAndExternal &&
            !this.vm.AllowTransfer && !this.vm.AllowCash) {
            addAlert('租借設定為「內外皆可」時，至少須開放「銀行匯款」或「現金繳費」，否則外部人員無法付款', { type: 'danger' });
            return;
        }

        // ✅【關鍵】數字欄位正規化（避免 uint / decimal 爆炸）
        const capacity = Number(this.vm.Capacity ?? 0);
        const area = Number(this.vm.Area ?? 0);

        const body = {
            Id: this.vm.Id ?? null,
            Name: this.vm.Name,
            Building: this.vm.Building,
            Floor: this.vm.Floor,
            Description: this.vm.Description,

            // ✅ 一定是 number
            Capacity: isNaN(capacity) ? 0 : capacity,
            Area: isNaN(area) ? 0 : area,

            Status: this.vm.Status ?? RoomStatus.Available,
            PricingType: this.vm.PricingType,
            BookingSettings: this.vm.BookingSettings,
            DepartmentId: this.vm.DepartmentId,
            ManagerId: manager.selectedManager.value?.Id ?? this.vm.ManagerId,
            Images: this.mediaFiles.map((m, idx) => ({
                type: m.type,
                src: m.src,
                fileSize: m.src?.length ?? 0,
                sortOrder: idx
            })),
            PricingDetails: this.getPricingDetails(),
            AgreementBase64: this.form.agreementBase64,      // ✅ 聲明書
            AgreementFileName: this.form.agreementFileName,  // ✅ 聲明書檔名
            PanoramaBase64: this.form.panoramaBase64,
            PanoramaUrl: this.vm.PanoramaUrl ?? null,
            EnableParkingTicket: this.vm.EnableParkingTicket,  // ✅ 停車券
            ParkingTicketPrice: this.vm.ParkingTicketPrice || 100,
            Sequence: this.vm.Sequence || 0,  // ✅ 排序順序
            PaymentContactInfo: this.vm.PaymentContactInfo || null,
            AllowTransfer: this.vm.AllowTransfer,
            AllowCash: this.vm.AllowCash,
            AllowCostSharing: this.vm.AllowCostSharing,
        };

        console.log('🔍 [SAVE normalized]', body);

        try {
            const response = await method({ body });
            const roomId = response.data?.Id || this.vm.Id;

            // ✅ 儲存審核關卡（編輯模式時一律儲存，即使清空也要讓後端軟刪除舊的）
            if (roomId) {
                await approvalChain.save(roomId);
            }

            addAlert('操作成功');
            this.getList();
            this.offcanvas?.hide();
        } catch (err) {
            addAlert(err.details || '操作失敗', { type: 'danger' });
        }
    };


    this.deleteRoom = (Id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.roomdelete({ body: { Id } })
                .then((response) => {
                    addAlert('操作成功');
                    this.getList();
                })
                .catch(error => {
                    addAlert(getMessage(error), { type: 'danger', click: error.download });
                });
        }
    }

    // ✅ 上移會議室
    this.moveUp = async (Id) => {
        try {
            const res = await global.api.admin.roommoveup({ body: { Id } });
            if (res.data?.Success) {
                this.getList();
            }
        } catch (error) {
            console.error('上移失敗:', error);
            addAlert(getMessage(error) || error?.message || '上移失敗', { type: 'danger' });
        }
    }

    // ✅ 下移會議室
    this.moveDown = async (Id) => {
        try {
            const res = await global.api.admin.roommovedown({ body: { Id } });
            if (res.data?.Success) {
                this.getList();
            }
        } catch (error) {
            console.error('下移失敗:', error);
            addAlert(getMessage(error) || error?.message || '下移失敗', { type: 'danger' });
        }
    }

    // ✅ 初始化排序（給既有資料用）
    this.initSequence = async () => {
        if (!confirm('確定要初始化所有會議室的排序順序嗎？')) return;
        try {
            await global.api.admin.roominitsequence();
            addAlert('初始化排序成功');
            this.getList();
        } catch (error) {
            console.error('初始化失敗:', error);
            addAlert(getMessage(error) || error?.message || '初始化失敗', { type: 'danger' });
        }
    }

    this.toggleFeeOptions = () => {
        if (this.vm.PricingType === PricingType.Period) {
            this.timeSlots.splice(0);
            this.generateCreateTimeSlotDefaults();
        }
    }

    this.generateCreateTimeSlotDefaults = () => {
        this.timeSlots.splice(0);

        [
            { Name: '上午場', StartTime: '08:00', EndTime: '12:00', Price: 1000, HolidayPrice: 1200, SetupPrice: null },
            { Name: '下午場', StartTime: '13:00', EndTime: '17:00', Price: 1200, HolidayPrice: 1500, SetupPrice: null }
        ].forEach(s => {
            this.timeSlots.push(this.createPeriodSlot(s));
        });
    };

    // this.generateHourlySlots = () => {
    //     this.hourlySlots.value = Array.from(
    //         { length: 12 },
    //         (_, i) => this.createHourlySlot(i + 8)
    //     );
    // };

    // this.selectAllHours = () => {
    //     this.hourlySlots.value.forEach(slot => {
    //         slot.Checked = true;
    //     });
    // }

    // this.deselectAllHours = () => {
    //     this.hourlySlots.value.forEach(slot => {
    //         slot.Checked = false;
    //     });
    // }

    this.addTimeSlot = () => {
        this.timeSlots.push(this.createPeriodSlot());
    };

    this.removeTimeSlot = (index) => {
        this.timeSlots.splice(index, 1);
    }

    this.getPricingDetails = () => {
        const details = [];

        const pricingType = this.vm.PricingType;

        // if (pricingType === PricingType.Hourly) {
        //     this.hourlySlots.value.forEach(slot => {
        //         if (slot.Checked) {
        //             details.push({
        //                 Id: slot.Id,
        //                 Name: `${slot.Hour}:00 - ${slot.Hour + 1}:00`,
        //                 StartTime: `${String(slot.Hour).padStart(2, '0')}:00`,
        //                 EndTime: `${String(slot.Hour + 1).padStart(2, '0')}:00`,
        //                 Price: slot.Fee,
        //                 Enabled: true
        //             });
        //         }
        //     });
        // } else
        if (pricingType === PricingType.Period) {
            this.timeSlots.forEach(slot => {
                if (slot.Enabled) {
                    // ✅ 修正：優先使用 StartHour:StartMin 組合（新增模式）
                    // 如果沒有 StartHour/EndHour，才使用 StartTime/EndTime（編輯模式）
                    const startTime = (slot.StartHour !== undefined && slot.StartMin !== undefined)
                        ? `${slot.StartHour}:${slot.StartMin}`
                        : slot.StartTime;
                    const endTime = (slot.EndHour !== undefined && slot.EndMin !== undefined)
                        ? `${slot.EndHour}:${slot.EndMin}`
                        : slot.EndTime;

                    details.push({
                        Id: slot.Id,
                        Name: slot.Name,
                        StartTime: startTime,
                        EndTime: endTime,
                        Price: slot.Price,
                        HolidayPrice: slot.HolidayPrice,
                        SetupPrice: slot.SetupPrice ?? null,
                        Enabled: true,
                        Slots: (slot.Slots ?? []).map(s => ({
                            StartTime: `${s.StartHour}:${s.StartMin}`,
                            EndTime: `${s.EndHour}:${s.EndMin}`
                        }))
                    });
                }
            });
        }

        return details;
    }

    this.selectRentalOption = (type) => {
        this.vm.BookingSettings = type;
    }

    this.handleMediaUpload = (event) => {
        const files = event.target.files;
        if (!files || files.length === 0) return;

        Array.from(files).forEach(file => {
            const reader = new FileReader();

            reader.onload = (e) => {
                this.mediaFiles.push({
                    Id: Date.now() + Math.random(),
                    type: file.type.startsWith('image/') ? 'image' : 'video',
                    src: e.target.result,
                    name: file.name
                });
            };

            reader.readAsDataURL(file);
        });

        event.target.value = '';
    };

    this.handleMediaDrop = (event) => {
        const files = event.dataTransfer?.files;
        if (!files || files.length === 0) return;

        Array.from(files).forEach(file => {
            if (!file.type.startsWith('image/') && !file.type.startsWith('video/')) return;
            const reader = new FileReader();
            reader.onload = (e) => {
                this.mediaFiles.push({
                    Id: Date.now() + Math.random(),
                    type: file.type.startsWith('image/') ? 'image' : 'video',
                    src: e.target.result,
                    name: file.name
                });
            };
            reader.readAsDataURL(file);
        });
    };

    this.removeMedia = (Id) => {
        const index = this.mediaFiles.findIndex(m => m.Id === Id);
        if (index > -1) {
            this.mediaFiles.splice(index, 1);
        }
    }

    this.getMediaInfo = () => {
        return this.mediaFiles.map((item, index) => ({
            type: item.type,
            src: item.src,
            fileSize: item.src.length,
            sortOrder: index
        }));
    }

    this.triggerMediaUpload = () => {
        document.getElementById('mediaUpload').click();
    }

    this.triggerAgreementUpload = () => {
        document.getElementById('agreementUpload').click();
    }

    // ✅ 聲明書上傳處理
    this.handleAgreementUpload = (event) => {
        const file = event.target.files[0];
        if (!file) return;

        if (file.type !== 'application/pdf') {
            addAlert('請上傳 PDF 格式的檔案', { type: 'warning' });
            event.target.value = '';
            return;
        }

        const reader = new FileReader();
        reader.onload = (e) => {
            this.form.agreementBase64 = e.target.result;
            this.form.agreementFileName = file.name;
        };
        reader.readAsDataURL(file);
        event.target.value = '';
    };

    // ✅ 清除已選擇的聲明書
    this.clearAgreement = () => {
        this.form.agreementBase64 = null;
        this.form.agreementFileName = null;
    };

    // ✅ 移除現有聲明書（編輯時）
    this.removeExistingAgreement = () => {
        this.vm.AgreementPath = null;
    };

    this.triggerPanoramaUpload = () => {
        document.getElementById('panoramaUpload').click();
    };

    this.handlePanoramaUpload = (event) => {
        const file = event.target.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = (e) => {
            this.form.panoramaBase64 = e.target.result;
            this.form.panoramaFileName = file.name;
        };
        reader.readAsDataURL(file);
        event.target.value = '';
    };

    this.clearPanorama = () => {
        this.form.panoramaBase64 = null;
        this.form.panoramaFileName = null;
    };

    this.removePanorama = () => {
        this.vm.PanoramaUrl = null;
    };

    this.viewRoom = (Id) => {
        global.api.admin.roomdetail({ body: { Id } })
            .then((response) => {

                this.detailRoom.value = response.data;
                this.detailRoomCarouselIndex.value = 0;
                this.panoramaMode.value = false;
                if (this._pannellumViewer) { this._pannellumViewer.destroy(); this._pannellumViewer = null; }

                const modal = new bootstrap.Modal(document.getElementById('roomDetailModal'));
                modal.show();
                this.startCarousel();
                this.loadTodaySchedule(Id);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }


    this.onPricingTypeChange = () => {
        // if (this.vm.PricingType === PricingType.Hourly) {
        //     this.generateHourlySlots();
        //     return;
        // }

        if (this.vm.PricingType === PricingType.Period) {
            if (this.timeSlots.length > 0) {
                return;
            }
            this.generateCreateTimeSlotDefaults();
        }
    };
}

window.$config = {
    setup: () => new function () {
        this.room = room;
        this.roomoffcanvas = ref(null);
        this.getStatusText = getStatusText;
        this.getStatusClass = getStatusClass;
        this.getPricingTypeText = getPricingTypeText;
        this.getBookingSettingsText = getBookingSettingsText;
        this.form = room.form;
        this.imageIndices = imageIndices;
        this.roompage = ref(null);
        this.timeSlots = room.timeSlots;
        this.timeOptions = timeOptions;  // ✅ 30 分鐘間隔的時間選項
        this.mediaFiles = room.mediaFiles;
        // this.hourlySlots = room.hourlySlots;
        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;
        this.panoramaMode = room.panoramaMode;
        this.department = department;
        this.isAdmin = isAdmin;
        this.isGlobalAdmin = isGlobalAdmin;  // ✅ 全院管理者
        this.isDepartmentAdmin = isDepartmentAdmin;  // ✅ 分院管理者
        this.manager = manager;
        this.approvalChain = approvalChain;  // ✅ 審核關卡
        this.userDepartmentName = userDepartmentName;
        // ✅ 加上這些
        this.todaySchedule = room.todaySchedule;
        this.getStatusBadgeClass = room.getStatusBadgeClass;
        this.getStatusText = room.getStatusText;
        this.getBookingSettingsClass = getBookingSettingsClass;

        this.currentDetailImage = computed(() => {
            if (!room.detailRoom.value || !room.detailRoom.value.Images) {
                return null;
            }
            return room.detailRoom.value.Images[room.detailRoomCarouselIndex.value];
        });

        this.hasDetailImages = computed(() => {
            return room.detailRoom.value &&
                room.detailRoom.value.Images &&
                room.detailRoom.value.Images.length > 0;
        });

        this.prevImage = (roomId) => {
            if (!imageIndices[roomId]) imageIndices[roomId] = 0;
            const roomData = room.list.find(r => r.Id === roomId);
            if (roomData && roomData.Images && roomData.Images.length > 0) {
                imageIndices[roomId] = (imageIndices[roomId] - 1 + roomData.Images.length) % roomData.Images.length;
            }
        };

        this.nextImage = (roomId) => {
            if (!imageIndices[roomId]) imageIndices[roomId] = 0;
            const roomData = room.list.find(r => r.Id === roomId);
            if (roomData && roomData.Images && roomData.Images.length > 0) {
                imageIndices[roomId] = (imageIndices[roomId] + 1) % roomData.Images.length;
            }
        };

        this.isVideoFile = (filePath) => {
            if (!filePath) return false;
            const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
            return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
        };

        this.isDetailVideoFile = (filePath) => {
            if (!filePath) return false;
            const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
            return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
        };

        onMounted(async () => {
            await loadCurrentUser();
            room.page = this.roompage.value;

            // ✅ 初始化 offcanvas
            room.offcanvas = new bootstrap.Offcanvas(this.roomoffcanvas.value);

            room.getList(1);

            // ✅ 載入分院列表或員工
            if (isGlobalAdmin.value) {
                // 全院管理者：載入分院列表，等選擇分院後再載入員工
                console.log('✅ 全院管理者，載入分院列表');
                department.getList();
            } else if (userDepartmentId.value) {
                // 分院管理者或非管理者：直接載入自己分院的員工
                console.log('✅ 載入自己分院員工:', userDepartmentId.value);
                manager.getList(userDepartmentId.value);
            }

            // ✅ Modal 事件監聽
            const detailModalElement = document.getElementById('roomDetailModal');
            if (detailModalElement) {
                detailModalElement.addEventListener('hidden.bs.modal', () => {
                    room.stopCarousel();
                    room.stopScheduleRefresh();
                });

                detailModalElement.addEventListener('shown.bs.tab', (event) => {
                    const targetId = event.target.getAttribute('data-bs-target');
                    if (targetId === '#schedule') {
                        if (room.detailRoom.value?.Id) {
                            room.startScheduleRefresh(room.detailRoom.value.Id);
                        }
                    } else {
                        room.stopScheduleRefresh();
                    }
                });
            }
            watch(() => room.query.keyword, () => {
                room.getList(1);
            });

            // ✅ 監聯分院變更（新增/編輯共用）
            watch(() => room.vm.DepartmentId, (newDeptId, oldDeptId) => {
                if (!newDeptId || newDeptId === oldDeptId) return;

                console.log('✅ 分院變更,重新載入員工:', newDeptId);
                manager.clearSelection();
                manager.getList(newDeptId);

                // ✅ 分院變更時，清空審核關卡（因為審核人來自該分院）
                if (oldDeptId) {
                    if (approvalChain.levels.length > 0) {
                        console.log('⚠️ 分院變更，清空已選的審核關卡');
                        approvalChain.clear();
                        addAlert('分院已變更，審核關卡已重置', { type: 'warning' });
                    } else if (approvalChain.isAddingLevel.value) {
                        // 如果正在新增關卡但還沒選人，也要關閉面板並清空
                        console.log('⚠️ 分院變更，關閉新增關卡面板');
                        approvalChain.cancelAdd();
                    }
                }
            });
        });
    }
}