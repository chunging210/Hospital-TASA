// Admin/SysRoomCalendar
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

const room = new function () {

    this.query = reactive({
        keyword: '',
        isEnabled: true
    });

    this.list = reactive([]);
    this.page = {};

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

    /* ========= 列表 ========= */
    this.getList = (payload) => {
        if (payload?.page) this.page.data.page = payload.page;
        if (payload?.perPage) this.page.data.perPage = payload.perPage;

        global.api.select.roomlist(
            this.page.setHeaders({
                params: this.query
            })
        )
            .then(this.page.setTotal)
            .then(res => {
                this.list.splice(0);
                res.data.forEach(x => this.list.push(x));
            })
            .catch(() => {
                addAlert('取得會議室失敗', { type: 'danger' });
            });
    };

    /* ========= 詳細 ========= */
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
        this.detailRoom = room.detailRoom;
        this.detailRoomCarouselIndex = room.detailRoomCarouselIndex;
        this.hasDetailImages = room.hasDetailImages;
        this.currentDetailImage = room.currentDetailImage;

        onMounted(() => {
            room.page = this.roompage.value;
            room.getList({ page: 1, perPage: 6 });
        });
    }
};
