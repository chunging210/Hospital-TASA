// Admin/SysRoom
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

class VM {
    Id = null;
    Name = '';
    Description = '';
    IsEnabled = true;
}

const getStatusText = (status) => {
    const statusMap = {
        'available': '可用',
        'occupied': '使用中',
        'maintenance': '維護中'
    };
    return statusMap[status] || status;
};

const getStatusClass = (status) => {
    const classMap = {
        'available': 'status-available',
        'occupied': 'status-occupied',
        'maintenance': 'status-maintenance'
    };
    return classMap[status] || '';
};

let imageIndices = reactive({});

const room = new function () {
    this.query = reactive({ keyword: '' });
    this.list = reactive([]);
    this.form = reactive({
        name: '',
        building: '',
        floor: '',
        roomNumber: '',
        description: '',
        capacity: null,
        area: null,
        refundEnabled: true,
        feeType: 'hourly',
        rentalType: 'in'
    });

    this.editModal = null;
    this.page = {};
    this.mediaFiles = reactive([]);
    this.timeSlots = reactive([]);
    this.hourlySlots = ref([]);

    // ✅ 詳情資料改成 ref（Vue 響應式）
    this.detailRoom = ref(null);
    this.detailRoomCarouselIndex = ref(0);

    this.createHourlySlot = (hour, data = {}) => {
        return {
            Id: data.Id ?? null,              // DB Id（編輯時才有）
            Hour: hour,                       // 8 ~ 19
            Checked: data.Checked ?? false,   // 是否啟用
            Fee: data.Fee ?? 500              // 預設費用
        };
    };

    this.createPeriodSlot = (data = {}) => {
        return {
            Id: data.Id ?? `tmp_${Date.now()}_${Math.random()}`,
            Name: data.Name ?? '新時段',
            StartTime: data.StartTime ?? '09:00',
            EndTime: data.EndTime ?? '10:00',
            Price: data.Price ?? 0,
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
        this.hourlySlots.value = [];

        if (Id) {
            // ✅ 編輯模式 - 打開編輯 Modal
            global.api.admin.roomdetail({ body: { Id } })
                .then((response) => {
                    // ✅ 基本資料
                    this.vm.Id = response.data.Id;
                    this.vm.Name = response.data.Name;
                    this.vm.Building = response.data.Building;
                    this.vm.Floor = response.data.Floor;
                    this.vm.Number = response.data.Number;
                    this.vm.Description = response.data.Description;
                    this.vm.Capacity = response.data.Capacity;
                    this.vm.Area = response.data.Area;
                    this.vm.Status = response.data.Status;
                    this.vm.PricingType = response.data.PricingType;
                    this.vm.IsEnabled = response.data.IsEnabled;
                    this.vm.BookingSettings = response.data.BookingSettings;

                    // ✅ 圖片格式轉換
                    (response.data.Images || []).forEach((imgPath, idx) => {
                        this.mediaFiles.push({
                            Id: Date.now() + idx,
                            type: 'image',
                            src: imgPath,
                            name: ''
                        });
                    });

                    // ⚠️ 不再使用 vm.Images 畫畫面
                    this.vm.Images = [];

                    // ✅ 收費詳情
                    this.vm.PricingDetails = response.data.PricingDetails || [];

                    // ✅ 根據收費方式初始化
                    if (response.data.PricingType === 'hourly') {
                        const dbPricingMap = {};
                        response.data.PricingDetails?.forEach(p => {
                            const hour = parseInt(p.StartTime.split(':')[0]);
                            dbPricingMap[hour] = {
                                Id: p.Id,
                                Checked: p.Enabled,
                                Fee: p.Price
                            };
                        });

                        this.hourlySlots.value = Array.from(
                            { length: 12 },
                            (_, i) => this.createHourlySlot(i + 8, dbPricingMap[i + 8])
                        );
                    } else if (response.data.PricingType === 'period') {
                        // 時段制：載入時段資料
                        this.timeSlots.splice(0);
                        response.data.PricingDetails?.forEach(p => {
                            this.timeSlots.push(this.createPeriodSlot(p));
                        });
                    }

                    // ✅ 打開編輯 Modal
                    this.editModal.show();

                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            // ✅ 新增模式 - 打開新增 Offcanvas
            this.form.name = '';
            this.form.building = '';
            this.form.floor = '';
            this.form.roomNumber = '';
            this.form.description = '';
            this.form.capacity = null;
            this.form.area = null;
            this.form.refundEnabled = true;
            this.form.feeType = 'hourly';
            this.vm.PricingType = 'hourly';
            this.form.rentalType = 'in';

            // ✅ 初始化新增模式的時段
            this.generateHourlySlots();
            this.timeSlots.splice(0);
            // ✅ 打開新增 Offcanvas
            this.offcanvas.show();
        }
    }

    this.save = () => {
        const isEdit = !!this.vm.Id;
        const method = isEdit
            ? global.api.admin.roomupdate
            : global.api.admin.roominsert;

        const source = isEdit ? this.vm : this.form;

        const pricingType = isEdit
            ? this.vm.PricingType
            : this.form.feeType;

        let status = source.Status ?? 'available';
        if (source.BookingSettings === 'closed') {
            status = 'maintenance';
        }

        const body = {
            Id: this.vm.Id ?? null,
            Name: source.Name ?? source.name,
            Building: source.Building ?? source.building,
            Floor: source.Floor ?? source.floor,
            Number: source.Number ?? source.roomNumber,
            Description: source.Description ?? source.description,
            Capacity: source.Capacity ?? source.capacity,
            Area: source.Area ?? source.area,
            Status: status,
            PricingType: pricingType,
            IsEnabled: source.IsEnabled ?? source.refundEnabled,
            BookingSettings: source.BookingSettings ?? source.rentalType,
            Images: this.mediaFiles.map((m, idx) => ({
                type: m.type,
                src: m.src,
                fileSize: m.src?.length ?? 0,
                sortOrder: idx
            })),
            PricingDetails: this.getPricingDetails()
        };

        console.log('🔍 [SAVE]', body);

        method({ body })
            .then(() => {
                addAlert('操作成功');
                this.getList();
                this.offcanvas?.hide();
                this.editModal?.hide();
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

    // ===== 切換收費方式 =====
    this.toggleFeeOptions = () => {
        if (this.form.feeType === 'hourly') {
            this.generateHourlySlots();
            this.timeSlots.splice(0);
        } else if (this.form.feeType === 'period') {
            this.timeSlots.splice(0);
            this.generateCreateTimeSlotDefaults();
        }
    }

    // ===== 生成預設時段 =====
    this.generateCreateTimeSlotDefaults = () => {
        this.timeSlots.splice(0);

        [
            { Name: '上午場', StartTime: '09:00', EndTime: '12:00', Price: 1000 },
            { Name: '午餐場', StartTime: '12:00', EndTime: '14:00', Price: 800 },
            { Name: '下午場', StartTime: '14:00', EndTime: '18:00', Price: 1200 }
        ].forEach(s => {
            this.timeSlots.push(this.createPeriodSlot(s));
        });
    };

    // ===== 小時制相關 =====
    this.generateHourlySlots = () => {
        this.hourlySlots.value = Array.from(
            { length: 12 },
            (_, i) => this.createHourlySlot(i + 8)
        );
    };

    this.selectAllHours = () => {
        this.hourlySlots.value.forEach(slot => {
            slot.Checked = true;
        });
    }

    this.deselectAllHours = () => {
        this.hourlySlots.value.forEach(slot => {
            slot.Checked = false;
        });
    }

    // ===== 時段制相關 =====
    this.addTimeSlot = () => {
        this.timeSlots.push(this.createPeriodSlot());
    };

    this.removeTimeSlot = (index) => {
        this.timeSlots.splice(index, 1);
    }

    // ===== 收費詳情 =====
    this.getPricingDetails = () => {
        const details = [];

        const pricingType = this.vm.Id
            ? this.vm.PricingType
            : this.form.feeType;


        if (pricingType === 'hourly') {
            this.hourlySlots.value.forEach(slot => {
                if (slot.Checked) {
                    details.push({
                        Id: slot.Id,
                        Name: `${slot.Hour}:00 - ${slot.Hour + 1}:00`,
                        StartTime: `${String(slot.Hour).padStart(2, '0')}:00`,
                        EndTime: `${String(slot.Hour + 1).padStart(2, '0')}:00`,
                        Price: slot.Fee,
                        Enabled: true
                    });
                }
            });
        } else if (pricingType === 'period') {
            this.timeSlots.forEach(slot => {
                if (slot.Enabled) {  // 只保存啟用的時段
                    details.push({
                        Id: slot.Id,
                        Name: slot.Name,
                        StartTime: slot.StartTime,
                        EndTime: slot.EndTime,
                        Price: slot.Price,
                        Enabled: true
                    });
                }
            });
        }

        return details;
    }

    // ===== 租借權限 =====
    this.selectRentalOption = (type) => {
        this.form.rentalType = type;
    }

    // ===== 媒體上傳 =====
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

        // 關鍵：清空 input，否則同檔案第二次選不到
        event.target.value = '';
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

    // ✅ 改成 Vue 響應式
    this.viewRoom = (Id) => {
        global.api.admin.roomdetail({ body: { Id } })
            .then((response) => {

                console.log('🔍 [viewRoom] 完整回應:', response);
                console.log('🔍 [viewRoom] response.data:', response.data);
                console.log('🔍 [viewRoom] Images 陣列:', response.data.Images);
                console.log('🔍 [viewRoom] Images 長度:', response.data.Images?.length);


                // 直接設定到 ref
                this.detailRoom.value = response.data;
                this.detailRoomCarouselIndex.value = 0;

                // 顯示 Modal
                const modal = new bootstrap.Modal(document.getElementById('roomDetailModal'));
                modal.show();
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }

    // ✅ 圖片輪播控制
    this.prevDetailImage = () => {
        if (!this.detailRoom.value || !this.detailRoom.value.Images) return;
        const length = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value = (this.detailRoomCarouselIndex.value - 1 + length) % length;
    }

    this.nextDetailImage = () => {
        if (!this.detailRoom.value || !this.detailRoom.value.Images) return;
        const length = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value = (this.detailRoomCarouselIndex.value + 1) % length;
    }

    this.onPricingTypeChange = () => {

        // 切到小時制
        if (this.vm.PricingType === 'hourly') {

            // 小時制永遠顯示完整小時（未勾）
            this.generateHourlySlots();

            // ❗ 不要清 timeSlots（那是 period 的 DB 資料）
            return;
        }

        // 切到時段制
        if (this.vm.PricingType === 'period') {

            // 👉 如果 DB 有 SysRoomPricePeriod（已載入）
            if (this.timeSlots.length > 0) {
                // 直接顯示，不要動
                return;
            }

            // 👉 DB 沒有 period 明細（代表原本是 hourly）
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
        this.form = room.form;
        this.imageIndices = imageIndices;
        this.roompage = ref(null);
        // this.hourlySlots = computed(() => room.hourlySlots.value);
        this.timeSlots = room.timeSlots;
        this.mediaFiles = room.mediaFiles;
        this.hourlySlots = room.hourlySlots;
        // ✅ 暴露詳情資料
        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;

        // ✅ Computed 計算當前圖片
        this.currentDetailImage = computed(() => {
            if (!room.detailRoom.value || !room.detailRoom.value.Images) {
                return null;
            }
            return room.detailRoom.value.Images[room.detailRoomCarouselIndex.value];
        });

        // ✅ Computed 計算是否有圖片
        this.hasDetailImages = computed(() => {
            return room.detailRoom.value &&
                room.detailRoom.value.Images &&
                room.detailRoom.value.Images.length > 0;
        });

        // 圖片輪播
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

        // 搜尋功能
        this.clearSearch = () => {
            room.query.keyword = '';
            room.getList();
        };



        onMounted(() => {
            room.page = this.roompage.value;

            room.getList(1);

            room.editModal = new bootstrap.Modal(
                document.getElementById('roomEditModal'),
                { backdrop: 'static' }
            );
            room.offcanvas = new bootstrap.Offcanvas(this.roomoffcanvas.value);
        });
    }
}