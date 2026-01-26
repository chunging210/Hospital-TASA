// Admin/SysRoomCalendar
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

const floorOptions = computed(() => {
    const b = buildings.value.find(
        x => x.Building === selectedBuilding.value
    );
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

    this.list = reactive([]);
    this.page = {};

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


    /* ====== 載入樓層（根據分院+大樓） ====== */
    this.loadFloorsByBuilding = (building) => {
        if (!building || !selectedDepartment.value) return;

        const deptId = selectedDepartment.value || userDepartmentId.value;
        if (!deptId) return;

        global.api.select.floorsbybuilding({
            body: {
                departmentId: deptId,
                building: building
            }
        })
            .then(res => {
                const buildingItem = buildings.value.find(
                    b => b.Building === building
                );

                if (buildingItem) {
                    buildingItem.Floors = (res.data || []).map(f => f.Name);
                }
            })
            .catch(() => {
                addAlert('取得樓層列表失敗', { type: 'danger' });
            });
    };

    /* ====== 載入大樓（根據分院） ====== */
    this.loadBuildingsByDepartment = (departmentId) => {
        global.api.select.buildingsbydepartment({
            body: {
                departmentId: departmentId
            }
        })
            .then(res => {
                buildings.value = res.data || [];
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

        const req = this.page.setHeaders({
            body: rawQuery  // ⭐ 改成 body 而不是 params
        });

        global.api.select.roomlist(req)
            .then(this.page.setTotal)
            .then(res => {
                this.list.splice(0);
                res.data.forEach(x => this.list.push(x));
            })
            .catch((error) => {
                console.error('API 錯誤:', error.response?.data || error.message);
                addAlert(`取得會議室失敗`, { type: 'danger' });
            });
    };

    /* ========= 詳細 ========= */
    this.detailRoom = ref(null);
    this.detailRoomCarouselIndex = ref(0);
    this.carouselInterval = null;  // 輪播計時器
    this.carouselDirection = 'next';  // 輪播方向
    // ✅ 啟動自動輪播
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
        }, 5000);  // 5 秒換一張
    };

    // ✅ 停止自動輪播
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

    // ✅ 修改：下一張（加上停止/重啟輪播）
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

        location.href = `/Conference/Create?${params.toString()}`;
    };
};

window.$config = {
    setup: () => new function () {
        this.room = room;
        this.roompage = ref(null);

        /* expose 給 cshtml */
        this.buildings = buildings;
        this.selectedBuilding = selectedBuilding;
        this.selectedFloor = selectedFloor;
        this.floorOptions = floorOptions;
        this.departments = departments;  // ✅ 加上分院
        this.selectedDepartment = selectedDepartment;  // ✅ 加上分院選擇

        this.isAdmin = isAdmin;
        this.userDepartmentName = userDepartmentName;

        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;
        this.hasDetailImages = room.hasDetailImages;
        this.currentDetailImage = room.currentDetailImage;

        this.isDetailVideoFile = (filePath) => {
            if (!filePath) return false;
            const videoExtensions = ['.mp4', '.webm', '.ogv', '.mov', '.avi', '.mkv'];
            return videoExtensions.some(ext => filePath.toLowerCase().endsWith(ext));
        };

        onMounted(async () => {

            await loadCurrentUser();

            room.loadDepartments();

            room.page = this.roompage.value;

            if (isAdmin.value) {
                console.log('✅ 是管理者,載入分院列表');
                room.loadDepartments();
                // 管理者預設顯示所有會議室
                room.getList({ page: 1, perPage: 6 });
            } else {
                console.log('⚠️ 不是管理者,自動設定為自己的分院');
                // 非管理者自動設定為自己的分院
                if (userDepartmentId.value) {
                    room.query.departmentId = userDepartmentId.value;
                    selectedDepartment.value = userDepartmentId.value;

                    // 載入該分院的大樓列表
                    room.loadBuildingsByDepartment(userDepartmentId.value);
                }
                // 載入該分院的會議室
                room.getList({ page: 1, perPage: 6 });
            }


            watch(selectedDepartment, (departmentId) => {

                if (!isAdmin.value) return;

                if (typeof departmentId !== 'string' || !departmentId) {
                    // 清空所有條件
                    room.query.departmentId = '';
                    room.query.building = '';
                    room.query.floor = '';

                    buildings.value = [];
                    selectedBuilding.value = '';
                    selectedFloor.value = '';

                    // ⭐ 重新抓「全部會議室」
                    room.getList({ page: 1, perPage: 6 });
                    return;
                }

                room.query.departmentId = departmentId;
                room.query.building = '';
                room.query.floor = '';
                selectedBuilding.value = '';
                selectedFloor.value = '';

                room.loadBuildingsByDepartment(departmentId);
                room.getList({ page: 1, perPage: 6 });
            });

            watch(selectedBuilding, (newBuilding) => {
                console.log('[watch selectedBuilding]', newBuilding);
                room.query.building = newBuilding || '';
                selectedFloor.value = '';  // ⭐ 自動清空樓層
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
                });
            }
        });
    }
};
