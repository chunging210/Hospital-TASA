// Admin/SysRoomCalendar - Global Query Filter 版本
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch, toRaw } = Vue;

let currentUser = null;
const isAdmin = ref(false);
const userDepartmentId = ref(null);
const userDepartmentName = ref('');

const buildings = ref([]);
const selectedBuilding = ref('');
const selectedFloor = ref('');
const departments = ref([]);
const selectedDepartment = ref(null);
const smartSearch = reactive({
    date: '',
    minCapacity: null,
    equipmentTypes: [],
    keyword: '',
    building: '',
    departmentId: null
});

const isSearchMode = ref(false);  // 是否在搜尋模式
const searchResults = reactive([]); // 搜尋結果
const floorOptions = computed(() => {
    const b = buildings.value.find(x => x.Building === selectedBuilding.value);
    return b ? b.Floors : [];
});

const loadCurrentUser = async () => {
    try {
        const userRes = await global.api.auth.me();
        currentUser = userRes.data;
        isAdmin.value = currentUser.IsAdmin || false;
        userDepartmentId.value = currentUser.DepartmentId;
        userDepartmentName.value = currentUser.DepartmentName || '';

        console.log('✅ 使用者資訊:', {
            name: currentUser.Name,
            isAdmin: isAdmin.value,
            departmentId: userDepartmentId.value,
            departmentName: userDepartmentName.value
        });
    } catch (err) {
        console.error('❌ 無法取得使用者資訊:', err);
    }
};


const room = new function () {
    this.query = reactive({
        keyword: '',
        building: '',
        departmentId: '',
        floor: '',
        isEnabled: true
    });

    this.todaySchedule = ref([]);
    this.scheduleRefreshInterval = null;
    this.list = reactive([]);
    this.page = {};

    this.loadTodaySchedule = async (roomId) => {
        if (!roomId) {
            console.warn('⚠️ 沒有 roomId,無法載入今日時程');
            return;
        }

        try {
            console.log('📅 載入今日時程:', roomId);
            const res = await global.api.select.roombyschedule({
                body: { roomId: roomId }
            });

            this.todaySchedule.value = res.data || [];
            console.log('✅ 今日時程:', this.todaySchedule.value);
        } catch (err) {
            console.error('❌ 載入今日時程失敗:', err);
            this.todaySchedule.value = [];
        }
    };

    this.startScheduleRefresh = (roomId) => {
        // 清除舊的計時器
        this.stopScheduleRefresh();

        // 立即載入一次
        this.loadTodaySchedule(roomId);

        // 每 1 分鐘自動重新整理
        this.scheduleRefreshInterval = setInterval(() => {
            console.log('🔄 自動重新整理今日時程');
            this.loadTodaySchedule(roomId);
        }, 60000);
    };

    this.stopScheduleRefresh = () => {
        if (this.scheduleRefreshInterval) {
            clearInterval(this.scheduleRefreshInterval);
            this.scheduleRefreshInterval = null;
            console.log('⏹️ 停止自動重新整理');
        }
    };

    this.getStatusBadgeClass = (status) => {
        const classMap = {
            'upcoming': 'bg-warning',   // 黃色
            'ongoing': 'bg-danger',     // 紅色
            'completed': 'bg-success'   // 綠色
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

    /* ====== 載入分院 ====== */
    this.loadDepartments = () => {
        global.api.select.department()
            .then(res => {
                departments.value = res.data || [];
            })
            .catch(() => {
                addAlert('取得分院列表失敗', { type: 'danger' });
            });
    };

    /* ====== 載入樓層（根據大樓） ====== */
    this.loadFloorsByBuilding = (building) => {
        if (!building) return;

        const payload = { building: building };  // ✅ 必須傳 building

        // ✅ 如果有選擇分院,傳給後端
        if (selectedDepartment.value) {
            payload.departmentId = selectedDepartment.value;
        }

        console.log('📤 [loadFloorsByBuilding] payload:', payload);

        global.api.select.floorsbybuilding({ body: payload })
            .then(res => {
                console.log('✅ 樓層列表:', res.data);
                const buildingItem = buildings.value.find(b => b.Building === building);
                if (buildingItem) {
                    buildingItem.Floors = (res.data || []).map(f => f.Name);
                }
            })
            .catch(() => {
                addAlert('取得樓層列表失敗', { type: 'danger' });
            });
    };

    /* ====== 載入大樓 ====== */
    this.loadBuildingsByDepartment = () => {
        const payload = {};

        // ✅ 正確:在 SysRoomCalendar 中應該用 selectedDepartment.value
        if (selectedDepartment.value) {
            payload.departmentId = selectedDepartment.value;
        }

        console.log('📤 [loadBuildingsByDepartment] payload:', payload);

        global.api.select.buildingsbydepartment({ body: payload })
            .then(res => {
                console.log('✅ 大樓列表:', res.data);
                buildings.value = res.data || [];  // ✅ 正確:直接用 buildings.value
            })
            .catch(() => {
                addAlert('取得大樓列表失敗', { type: 'danger' });
            });
    };

    this.isVideoFile = (filePath) => {
        if (!filePath) return false;
        const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
        return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
    };

    /* ========= 列表 ========= */
    this.getList = (payload) => {
        if (payload?.page) this.page.data.page = payload.page;
        if (payload?.perPage) this.page.data.perPage = payload.perPage;

        const rawQuery = { ...toRaw(this.query) };
        const req = this.page.setHeaders({ body: rawQuery });

        global.api.select.roomlist(req)
            .then(this.page.setTotal)
            .then(res => {
                this.list.splice(0);
                res.data.forEach(x => this.list.push(x));
            })
            .catch((error) => {
                console.error('API 錯誤:', error.response?.data || error.message);
                addAlert('取得會議室失敗', { type: 'danger' });
            });
    };

    /* ========= 詳細 ========= */
    this.detailRoom = ref(null);
    this.detailRoomCarouselIndex = ref(0);
    this.carouselInterval = null;
    this.carouselDirection = 'next';

    this.startCarousel = () => {
        if (this.carouselInterval) {
            clearInterval(this.carouselInterval);
        }

        this.carouselInterval = setInterval(() => {
            if (!this.detailRoom.value || !this.detailRoom.value.Images) return;

            this.carouselDirection = 'next';
            const length = this.detailRoom.value.Images.length;
            this.detailRoomCarouselIndex.value =
                (this.detailRoomCarouselIndex.value + 1) % length;
        }, 5000);
    };

    this.stopCarousel = () => {
        if (this.carouselInterval) {
            clearInterval(this.carouselInterval);
            this.carouselInterval = null;
        }
    };

    this.hasDetailImages = computed(() => {
        return this.detailRoom.value &&
            Array.isArray(this.detailRoom.value.Images) &&
            this.detailRoom.value.Images.length > 0;
    });

    this.currentDetailImage = computed(() => {
        if (!this.hasDetailImages.value) return null;
        return this.detailRoom.value.Images[this.detailRoomCarouselIndex.value];
    });

    this.prevDetailImage = () => {
        if (!this.hasDetailImages.value) return;
        this.stopCarousel();
        this.carouselDirection = 'prev';
        const len = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value =
            (this.detailRoomCarouselIndex.value - 1 + len) % len;
        this.startCarousel();
    };

    this.nextDetailImage = () => {
        if (!this.hasDetailImages.value) return;
        this.stopCarousel();
        this.carouselDirection = 'next';
        const len = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value =
            (this.detailRoomCarouselIndex.value + 1) % len;
        this.startCarousel();
    };

    this.openDetail = (id) => {
        global.api.admin.roomdetail({ body: { Id: id } })
            .then(res => {
                this.detailRoom.value = res.data;
                this.detailRoomCarouselIndex.value = 0;

                const modal = new bootstrap.Modal(
                    document.getElementById('roomDetailModal')
                );
                modal.show();

                setTimeout(() => {
                    this.startCarousel();
                }, 300);

                this.loadTodaySchedule(id);
            })
            .catch(() => {
                addAlert('取得會議室詳情失敗', { type: 'danger' });
            });
    };

    this.nextCardImage = (item) => {
        if (!item.Images?.length) return;
        item._imgIndex = ((item._imgIndex || 0) + 1) % item.Images.length;
    };

    this.prevCardImage = (item) => {
        if (!item.Images?.length) return;
        const len = item.Images.length;
        item._imgIndex = ((item._imgIndex || 0) - 1 + len) % len;
    };

    this.goReserve = (item) => {
        const params = new URLSearchParams({
            roomId: item.Id,
            building: item.Building,
            floor: item.Floor,
            departmentId: item.DepartmentId,
        });
        // ✅ 如果在智慧搜尋模式且有選擇日期,自動帶入
        if (isSearchMode.value && smartSearch.date) {
            params.append('date', smartSearch.date);
            console.log('🗓️ 自動帶入搜尋日期:', smartSearch.date);
        }
        location.href = `/Conference/Create?${params.toString()}`;
    };

    /* ========= 智慧搜尋 ========= */
    this.performSmartSearch = async () => {
        try {
            console.log('🔍 開始智慧搜尋...');

            // 準備搜尋參數
            const searchParams = {
                date: smartSearch.date || null,
                minCapacity: smartSearch.minCapacity || null,
                equipmentTypes: smartSearch.equipmentTypes.length > 0
                    ? smartSearch.equipmentTypes.map(t => parseInt(t))
                    : null,
                keyword: smartSearch.keyword || null,
                building: smartSearch.building || null,
                departmentId: selectedDepartment.value || null
            };

            console.log('📤 搜尋參數:', searchParams);

            // 呼叫 API
            const res = await global.api.select.smartsearch({ body: searchParams });

            console.log('✅ 搜尋結果:', res.data);

            // 清空原本的列表
            this.list.splice(0);

            // 填入搜尋結果
            if (res.data && res.data.length > 0) {
                res.data.forEach(x => this.list.push(x));
            }

            // 切換到搜尋模式
            isSearchMode.value = true;

            // 關閉 Modal
            const modal = bootstrap.Modal.getInstance(
                document.getElementById('smartSearchModal')
            );
            if (modal) {
                modal.hide();
            }

            // 隱藏分頁
            if (this.page && this.page.data) {
                this.page.data.total = 0;
            }

            addAlert(`找到 ${res.data.length} 間符合條件的會議室`, {
                type: res.data.length > 0 ? 'success' : 'warning'
            });

        } catch (error) {
            console.error('❌ 智慧搜尋失敗:', error);
            addAlert('智慧搜尋失敗,請稍後再試', { type: 'danger' });
        }
    };

    this.clearSmartSearch = () => {
        console.log('🔄 清空搜尋條件');
        smartSearch.date = '';
        smartSearch.minCapacity = null;
        smartSearch.equipmentTypes = [];
        smartSearch.keyword = '';
        smartSearch.building = '';
    };

    this.clearSearchResults = () => {
        console.log('↩️ 返回總覽');

        // 清空搜尋條件
        this.clearSmartSearch();

        // 切換回正常模式
        isSearchMode.value = false;

        // 重新載入原本的列表
        this.getList({ page: 1, perPage: 6 });
    };
};

// ===== Room Popover =====
const roomPopover = reactive({
    visible: false,
    loading: false,
    data: null,
    top: 0,
    left: 0
});
const popoverCache = {};
let popoverHideTimer = null;

const showRoomPopover = async (item, event) => {
    clearTimeout(popoverHideTimer);

    const rect = event.currentTarget.getBoundingClientRect();
    roomPopover.top = rect.bottom - 300;
    roomPopover.left = rect.right - 300;

    // use cache
    if (popoverCache[item.Id]) {
        roomPopover.data = popoverCache[item.Id];
        roomPopover.visible = true;
        return;
    }

    roomPopover.data = null;
    roomPopover.loading = true;
    roomPopover.visible = true;

    try {
        const res = await global.api.admin.roomdetail({ body: { Id: item.Id } });
        popoverCache[item.Id] = res.data;
        roomPopover.data = res.data;
    } catch (e) {
        roomPopover.visible = false;
    } finally {
        roomPopover.loading = false;
    }
};

const hideRoomPopover = () => {
    popoverHideTimer = setTimeout(() => {
        roomPopover.visible = false;
    }, 200);
};

const keepRoomPopover = () => {
    clearTimeout(popoverHideTimer);
};

window.$config = {
    setup: () => new function () {
        this.room = room;
        this.roompage = ref(null);

        this.buildings = buildings;
        this.selectedBuilding = selectedBuilding;
        this.selectedFloor = selectedFloor;
        this.floorOptions = floorOptions;
        this.departments = departments;
        this.selectedDepartment = selectedDepartment;

        this.isAdmin = isAdmin;
        this.userDepartmentName = userDepartmentName;

        this.todaySchedule = room.todaySchedule;
        this.getStatusBadgeClass = room.getStatusBadgeClass;
        this.getStatusText = room.getStatusText;

        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;
        this.hasDetailImages = room.hasDetailImages;
        this.currentDetailImage = room.currentDetailImage;

        this.roomPopover = roomPopover;
        this.showRoomPopover = showRoomPopover;
        this.hideRoomPopover = hideRoomPopover;
        this.keepRoomPopover = keepRoomPopover;

        this.isDetailVideoFile = (filePath) => {
            if (!filePath) return false;
            const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
            return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
        };

        // ===== 智慧搜尋相關 =====
        this.smartSearch = smartSearch;
        this.isSearchMode = isSearchMode;
        this.performSmartSearch = room.performSmartSearch;
        this.clearSmartSearch = room.clearSmartSearch;
        this.clearSearchResults = room.clearSearchResults;

        onMounted(async () => {
            await loadCurrentUser();

            room.page = this.roompage.value;

            if (isAdmin.value) {
                console.log('✅ 是管理者,載入分院列表');
                room.loadDepartments();
                // ✅ 後端會自動過濾
                room.getList({ page: 1, perPage: 6 });
            } else {
                console.log('⚠️ 不是管理者,自動設定為自己的分院');
                if (userDepartmentId.value) {
                    room.query.departmentId = userDepartmentId.value;
                    selectedDepartment.value = userDepartmentId.value;

                    // ✅ 後端會自動過濾,不傳參數
                    room.loadBuildingsByDepartment();
                }
                room.getList({ page: 1, perPage: 6 });
            }

            watch(selectedDepartment, (departmentId) => {

                console.log('🏥 selectedDepartment =', departmentId);

                // 只判斷「有沒有值」
                if (!departmentId) {
                    room.query.departmentId = '';
                    room.query.building = '';
                    room.query.floor = '';

                    buildings.value = [];
                    selectedBuilding.value = '';
                    selectedFloor.value = '';

                    room.getList({ page: 1, perPage: 6 });
                    return;
                }

                // ✅ 有選分院
                room.query.departmentId = departmentId;
                room.query.building = '';
                room.query.floor = '';

                buildings.value = [];
                selectedBuilding.value = '';
                selectedFloor.value = '';

                room.loadBuildingsByDepartment();
                room.getList({ page: 1, perPage: 6 });
            });

            watch(selectedBuilding, (newBuilding) => {
                console.log('[watch selectedBuilding]', newBuilding);
                room.query.building = newBuilding || '';
                selectedFloor.value = '';
                room.query.floor = '';
                room.loadFloorsByBuilding(newBuilding);

                room.getList({ page: 1, perPage: 6 });
            });

            watch(selectedFloor, (newFloor) => {
                room.query.floor = newFloor || '';
                room.getList({ page: 1, perPage: 6 });
            });

            const detailModalElement = document.getElementById('roomDetailModal');
            if (detailModalElement) {
                detailModalElement.addEventListener('hidden.bs.modal', () => {
                    room.stopCarousel();
                    room.stopScheduleRefresh();
                });

                // ✅ 新增:監聽 Tab 切換事件
                detailModalElement.addEventListener('shown.bs.tab', (event) => {
                    const targetId = event.target.getAttribute('data-bs-target');

                    if (targetId === '#schedule') {
                        // 切換到今日時程 Tab,啟動自動重新整理
                        console.log('📅 切換到今日時程 Tab');
                        if (room.detailRoom.value?.Id) {
                            room.startScheduleRefresh(room.detailRoom.value.Id);
                        }
                    } else {
                        // 切換到其他 Tab,停止自動重新整理
                        room.stopScheduleRefresh();
                    }
                });
            }
        });
    }
};