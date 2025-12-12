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

    this.mediaFiles = reactive([]);
    this.timeSlots = reactive([]);
    this.hourlySlots = ref([]);

    // ✅ 詳情資料改成 ref（Vue 響應式）
    this.detailRoom = ref(null);
    this.detailRoomCarouselIndex = ref(0);

    this.getList = () => {
        global.api.admin.roomlist({ body: this.query })
            .then((response) => {
                copy(this.list, response.data);
                if (Array.isArray(response.data)) {
                    response.data.forEach(item => {
                        if (!imageIndices[item.Id]) {
                            imageIndices[item.Id] = 0;
                        }
                    });
                }
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }

    this.offcanvas = null;
    this.vm = reactive(new VM());

    this.getVM = (id) => {
        this.mediaFiles.splice(0);
        this.timeSlots.splice(0);
        this.generateHourlySlots();

        if (id) {
            // ✅ 編輯模式 - 打開編輯 Modal
            global.api.admin.roomdetail({ body: { id } })
                .then((response) => {
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

                    if (response.data.Images && Array.isArray(response.data.Images)) {
                        this.vm.Images = response.data.Images.map((imgPath, idx) => ({
                            id: Date.now() + idx,
                            Type: 'image',
                            Src: imgPath
                        }));
                    } else {
                        this.vm.Images = [];
                    }

                    this.vm.PricingDetails = response.data.PricingDetails || [];

                    // ✅ 打開編輯 Modal
                    const modal = new bootstrap.Modal(document.getElementById('roomEditModal'));
                    modal.show();
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
            this.form.rentalType = 'in';
            this.generateCreateTimeSlotDefaults();

            // ✅ 打開新增 Offcanvas
            this.offcanvas.show();
        }
    }
    this.save = () => {
        const method = this.form.Id ? global.api.admin.roomupdate : global.api.admin.roominsert;

        // ✅ 根據 rentalType 判斷 Status
        let status = 'available';
        if (this.form.rentalType === 'closed') {
            status = 'maintenance';
        }

        const body = {
            Id: this.form.Id,
            Name: this.form.name,
            Building: this.form.building,
            Floor: this.form.floor,
            Number: this.form.roomNumber,
            Description: this.form.description,
            Capacity: this.form.capacity,
            Area: this.form.area,
            Status: status,  // ✅ 根據 rentalType 動態設定
            PricingType: this.form.feeType,
            IsEnabled: this.form.refundEnabled,
            BookingSettings: this.form.rentalType,
            Images: this.mediaFiles,
            PricingDetails: this.getPricingDetails()
        };

        console.log('🔍 [SAVE] Saving body:', body);

        method({ body })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                this.offcanvas.hide();
            })
            .catch(error => {
                addAlert(error.details || '操作失敗', { type: 'danger', click: error.download });
            });
    }

    this.deleteRoom = (id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.roomdelete({ body: { id } })
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
        const defaultSlots = [
            { name: '上午場', startTime: '09:00', endTime: '12:00', fee: 1000 },
            { name: '午餐場', startTime: '12:00', endTime: '14:00', fee: 800 },
            { name: '下午場', startTime: '14:00', endTime: '18:00', fee: 1200 }
        ];

        defaultSlots.forEach((slot) => {
            this.timeSlots.push({
                id: `default_${Date.now()}_${Math.random()}`,
                name: slot.name,
                startTime: slot.startTime,
                endTime: slot.endTime,
                fee: slot.fee,
                enabled: true
            });
        });
    }

    // ===== 小時制相關 =====
    this.generateHourlySlots = () => {
        this.hourlySlots.value = Array.from({ length: 12 }, (_, i) => ({
            hour: i + 8,
            checked: false,
            fee: 500
        }));
    }

    this.selectAllHours = () => {
        this.hourlySlots.value.forEach(slot => {
            slot.checked = true;
        });
    }

    this.deselectAllHours = () => {
        this.hourlySlots.value.forEach(slot => {
            slot.checked = false;
        });
    }

    // ===== 時段制相關 =====
    this.addTimeSlot = () => {
        const timestamp = Date.now();
        this.timeSlots.push({
            id: `custom_${timestamp}`,
            name: '自訂時段',
            startTime: '22:00',
            endTime: '23:00',
            fee: 0,
            enabled: true
        });
    }

    this.removeTimeSlot = (index) => {
        this.timeSlots.splice(index, 1);
    }

    this.getTimeSlotSettings = () => {
        return this.timeSlots.map(slot => ({
            name: slot.name || '未命名時段',
            startTime: slot.startTime,
            endTime: slot.endTime,
            fee: parseInt(slot.fee) || 0,
            enabled: slot.enabled
        }));
    }

    this.validateTimeSlots = () => {
        const settings = this.getTimeSlotSettings();
        const errors = [];

        settings.forEach((setting, index) => {
            if (setting.startTime >= setting.endTime) {
                errors.push(`第${index + 1}個時段：結束時間必須晚於開始時間`);
            }

            if (setting.enabled && setting.fee <= 0) {
                errors.push(`第${index + 1}個時段：開放的時段必須設定費用`);
            }

            if (!setting.name.trim()) {
                errors.push(`第${index + 1}個時段：請輸入時段名稱`);
            }
        });

        return errors;
    }

    // ===== 收費詳情 =====
    this.getPricingDetails = () => {
        const details = [];

        if (this.form.feeType === 'hourly') {
            this.hourlySlots.value.forEach(slot => {
                if (slot.checked) {
                    details.push({
                        name: `${slot.hour}:00 - ${slot.hour + 1}:00`,
                        startTime: `${slot.hour}:00`,
                        endTime: `${slot.hour + 1}:00`,
                        price: slot.fee,
                        enabled: true
                    });
                }
            });
        } else if (this.form.feeType === 'period') {
            this.timeSlots.forEach(slot => {
                if (slot.enabled) {
                    details.push({
                        name: slot.name,
                        startTime: slot.startTime,
                        endTime: slot.endTime,
                        price: slot.fee,
                        enabled: true
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

        const file = files[0];
        const reader = new FileReader();

        reader.onload = (e) => {
            const mediaItem = {
                id: Date.now(),
                type: file.type.startsWith('image/') ? 'image' : 'video',
                src: e.target.result,
                name: file.name
            };
            this.mediaFiles.push(mediaItem);
            event.target.value = '';
        };

        reader.readAsDataURL(file);
    }

    this.removeMedia = (id) => {
        const index = this.mediaFiles.findIndex(m => m.id === id);
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
    this.viewRoom = (id) => {
        global.api.admin.roomdetail({ body: { id } })
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
}

window.$config = {
    setup: () => new function () {
        this.room = room;
        this.roomoffcanvas = ref(null);
        this.getStatusText = getStatusText;
        this.getStatusClass = getStatusClass;
        this.form = room.form;
        this.imageIndices = imageIndices;
        this.hourlySlots = computed(() => room.hourlySlots.value);
        this.timeSlots = room.timeSlots;
        this.mediaFiles = room.mediaFiles;

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
            room.getList();

            room.offcanvas = new bootstrap.Offcanvas(this.roomoffcanvas.value);
        });
    }
}