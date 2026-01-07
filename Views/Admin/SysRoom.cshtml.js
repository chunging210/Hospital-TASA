// Admin/SysRoom
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

class VM {
    Id = null;
    Name = '';
    Description = '';
    IsEnabled = true;
    DepartmentId = null;
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
    const settingsMap = {
        [BookingSettings.InternalOnly]: '僅限內部',
        [BookingSettings.InternalAndExternal]: '內外皆可',
        [BookingSettings.Closed]: '不開放租借',
        [BookingSettings.Free]: '免費使用'
    };
    return settingsMap[bookingSettings] || String(bookingSettings);
};


const isVideoFile = (filePath) => {
    if (!filePath) return false;
    const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
    return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
};

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

let imageIndices = reactive({});

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
        refundEnabled: true,
        feeType: PricingType.Hourly,
        rentalType: BookingSettings.InternalOnly,
        departmentId: null
    });

    this.editModal = null;
    this.page = {};
    this.mediaFiles = reactive([]);
    this.timeSlots = reactive([]);
    this.hourlySlots = ref([]);

    this.detailRoom = ref(null);
    this.detailRoomCarouselIndex = ref(0);
    this.carouselInterval = null;
    this.carouselDirection = 'next';

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


    this.createHourlySlot = (hour, data = {}) => {
        return {
            Id: data.Id ?? null,
            Hour: hour,
            Checked: data.Checked ?? false,
            Fee: data.Fee ?? 500
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
            global.api.admin.roomdetail({ body: { Id } })
                .then((response) => {
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

                    (response.data.Images || []).forEach((imgPath, idx) => {
                        this.mediaFiles.push({
                            Id: Date.now() + idx,
                            type: isVideoFile(imgPath) ? 'video' : 'image',
                            src: imgPath,
                            name: ''
                        });
                    });

                    this.vm.Images = [];
                    this.vm.PricingDetails = response.data.PricingDetails || [];

                    if (response.data.PricingType === PricingType.Hourly) {
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
                    } else if (response.data.PricingType === PricingType.Period) {
                        this.timeSlots.splice(0);
                        response.data.PricingDetails?.forEach(p => {
                            this.timeSlots.push(this.createPeriodSlot(p));
                        });
                    }

                    this.editModal.show();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            this.form.name = '';
            this.form.building = '';
            this.form.floor = '';
            this.form.description = '';
            this.form.capacity = null;
            this.form.area = null;
            this.form.refundEnabled = true;
            this.form.feeType = PricingType.Hourly;
            this.vm.PricingType = PricingType.Hourly;
            this.form.rentalType = BookingSettings.InternalOnly;
            this.form.departmentId = null;

            this.generateHourlySlots();
            this.timeSlots.splice(0);
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

        let status = source.Status ?? RoomStatus.Available;
        if (source.BookingSettings === BookingSettings.Closed) {
            status = RoomStatus.Maintenance;
        }

        // ✅【關鍵】數字欄位正規化（避免 uint / decimal 爆炸）
        const capacity = Number(source.Capacity ?? source.capacity ?? 0);
        const area = Number(source.Area ?? source.area ?? 0);

        const body = {
            Id: this.vm.Id ?? null,
            Name: source.Name ?? source.name,
            Building: source.Building ?? source.building,
            Floor: source.Floor ?? source.floor,
            Description: source.Description ?? source.description,

            // ✅ 一定是 number
            Capacity: isNaN(capacity) ? 0 : capacity,
            Area: isNaN(area) ? 0 : area,

            Status: status,
            PricingType: pricingType,
            IsEnabled: source.IsEnabled ?? source.refundEnabled,
            BookingSettings: source.BookingSettings ?? source.rentalType,
            DepartmentId: source.DepartmentId ?? source.departmentId,
            Images: this.mediaFiles.map((m, idx) => ({
                type: m.type,
                src: m.src,
                fileSize: m.src?.length ?? 0,
                sortOrder: idx
            })),
            PricingDetails: this.getPricingDetails()
        };

        console.log('🔍 [SAVE normalized]', body);

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

    this.toggleFeeOptions = () => {
        if (this.form.feeType === PricingType.Hourly) {
            this.generateHourlySlots();
            this.timeSlots.splice(0);
        } else if (this.form.feeType === PricingType.Period) {
            this.timeSlots.splice(0);
            this.generateCreateTimeSlotDefaults();
        }
    }

    this.generateCreateTimeSlotDefaults = () => {
        this.timeSlots.splice(0);

        [
            { Name: '上午場', StartTime: '09:00', EndTime: '12:00', Price: 1000 },
            { Name: '中午場', StartTime: '12:00', EndTime: '14:00', Price: 800 },
            { Name: '下午場', StartTime: '14:00', EndTime: '18:00', Price: 1200 }
        ].forEach(s => {
            this.timeSlots.push(this.createPeriodSlot(s));
        });
    };

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

    this.addTimeSlot = () => {
        this.timeSlots.push(this.createPeriodSlot());
    };

    this.removeTimeSlot = (index) => {
        this.timeSlots.splice(index, 1);
    }

    this.getPricingDetails = () => {
        const details = [];

        const pricingType = this.vm.Id
            ? this.vm.PricingType
            : this.form.feeType;

        if (pricingType === PricingType.Hourly) {
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
        } else if (pricingType === PricingType.Period) {
            this.timeSlots.forEach(slot => {
                if (slot.Enabled) {
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

    this.selectRentalOption = (type) => {
        this.form.rentalType = type;
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

    this.viewRoom = (Id) => {
        global.api.admin.roomdetail({ body: { Id } })
            .then((response) => {

                this.detailRoom.value = response.data;
                this.detailRoomCarouselIndex.value = 0;

                const modal = new bootstrap.Modal(document.getElementById('roomDetailModal'));
                modal.show();
                this.startCarousel();
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }


    this.onPricingTypeChange = () => {
        if (this.vm.PricingType === PricingType.Hourly) {
            this.generateHourlySlots();
            return;
        }

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
        this.mediaFiles = room.mediaFiles;
        this.hourlySlots = room.hourlySlots;
        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;
        this.department = department;
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
            department.getList();

            const detailModalElement = document.getElementById('roomDetailModal');
            detailModalElement.addEventListener('hidden.bs.modal', () => {
                room.stopCarousel();
            });

            room.offcanvas = new bootstrap.Offcanvas(this.roomoffcanvas.value);
        });
    }
}