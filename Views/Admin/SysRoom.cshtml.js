// Admin/SysRoom
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

let currentUser = null;
const isAdmin = ref(false);  // ✅ 改用 ref
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
let imageIndices = reactive({});

const loadCurrentUser = async () => {
    try {
        const userRes = await global.api.auth.me();
        currentUser = userRes.data;
        isAdmin.value = currentUser.IsAdmin || false;  // ✅ 用 .value
        userDepartmentId.value = currentUser.DepartmentId;  // ✅ 用 .value
        userDepartmentName.value = currentUser.DepartmentName || '';  // ✅ 用 .value

        console.log('✅ 使用者資訊:', {
            name: currentUser.Name,
            isAdmin: isAdmin.value,  // ✅ 用 .value
            departmentId: userDepartmentId.value,  // ✅ 用 .value
            departmentName: userDepartmentName.value  // ✅ 用 .value
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
        agreementFileName: null     // ✅ 聲明書檔名
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
            Enabled: data.Enabled ?? true
        };
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
            this.generateCreateTimeSlotDefaults();

            this.vm.ManagerId = null;
            this.form.agreementBase64 = null;    // ✅ 重設聲明書
            this.form.agreementFileName = null;
            manager.clearSelection();

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

    this.save = () => {
        const method = this.vm.Id
            ? global.api.admin.roomupdate
            : global.api.admin.roominsert;

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
            AgreementFileName: this.form.agreementFileName   // ✅ 聲明書檔名
        };

        console.log('🔍 [SAVE normalized]', body);

        method({ body })
            .then(() => {
                addAlert('操作成功');
                this.getList();
                this.offcanvas?.hide();
            })
            .catch(err => {
                addAlert(err.details || '操作失敗', { type: 'danger' });
            });
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

    this.toggleFeeOptions = () => {
        if (this.vm.PricingType === PricingType.Period) {
            this.timeSlots.splice(0);
            this.generateCreateTimeSlotDefaults();
        }
    }

    this.generateCreateTimeSlotDefaults = () => {
        this.timeSlots.splice(0);

        [
            { Name: '上午場', StartTime: '09:00', EndTime: '12:00', Price: 1000, HolidayPrice: 1200 },
            { Name: '中午場', StartTime: '12:00', EndTime: '14:00', Price: 800, HolidayPrice: 1000 },
            { Name: '下午場', StartTime: '14:00', EndTime: '18:00', Price: 1200, HolidayPrice: 1500 }
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
                        Enabled: true
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

    this.viewRoom = (Id) => {
        global.api.admin.roomdetail({ body: { Id } })
            .then((response) => {

                this.detailRoom.value = response.data;
                this.detailRoomCarouselIndex.value = 0;

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
        this.department = department;
        this.isAdmin = isAdmin;
        this.manager = manager;
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
            if (isAdmin.value) {
                console.log('✅ 是管理者,載入分院列表');
                department.getList();
                // ✅ Admin 初始不載入員工,等選擇分院後再載入
            } else {
                // ✅ 非管理者:直接載入自己分院的員工
                if (userDepartmentId.value) {
                    console.log('✅ 非管理者,載入自己分院員工:', userDepartmentId.value);
                    manager.getList(userDepartmentId.value);
                }
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

            // ✅ 監聽分院變更（新增/編輯共用）
            watch(() => room.vm.DepartmentId, (newDeptId, oldDeptId) => {
                if (!newDeptId || newDeptId === oldDeptId) return;
                if (room.vm.Id || isAdmin.value) {
                    console.log('✅ 分院變更,重新載入員工:', newDeptId);
                    manager.clearSelection();
                    manager.getList(newDeptId);
                }
            });
        });
    }
}