// Admin/SysRoomCalendar
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch, toRaw } = Vue;

const buildings = ref([]);
const selectedBuilding = ref('');
const selectedFloor = ref('');

const floorOptions = computed(() => {
    const b = buildings.value.find(
        x => x.Building === selectedBuilding.value
    );
    return b ? b.Floors : [];
});

const room = new function () {

    this.query = reactive({
        keyword: '',
        building: '',
        floor: '',
        isEnabled: true
    });

    this.list = reactive([]);
    this.page = {};

    /* ====== 載入大樓/樓層 ====== */
    this.loadBuildingFloors = () => {
        global.api.select.buildingfloors()
            .then(res => {
                buildings.value = res.data || [];
            })
            .catch(() => {
                addAlert('取得大樓樓層失敗', { type: 'danger' });
            });
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
        const len = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value =
            (this.detailRoomCarouselIndex.value - 1 + len) % len;
    };

    this.nextDetailImage = () => {
        if (!this.hasDetailImages.value) return;
        const len = this.detailRoom.value.Images.length;
        this.detailRoomCarouselIndex.value =
            (this.detailRoomCarouselIndex.value + 1) % len;
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

        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;
        this.hasDetailImages = room.hasDetailImages;
        this.currentDetailImage = room.currentDetailImage;
        onMounted(() => {
            room.loadBuildingFloors();

            room.page = this.roompage.value;
            room.getList({ page: 1, perPage: 6 });

            watch(selectedBuilding, (newBuilding) => {
                room.query.building = newBuilding || '';
                selectedFloor.value = '';  // ⭐ 自動清空樓層
                room.query.floor = '';
                room.getList({ page: 1, perPage: 6 });
            });

            watch(selectedFloor, (newFloor) => {
                room.query.floor = newFloor || '';
                room.getList({ page: 1, perPage: 6 });
            });
        });
    }
};
