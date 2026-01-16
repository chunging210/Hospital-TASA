// Reservation Review - 審核預約
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

class ReservationVM {
    id = null;
    bookingNo = '';
    applicantName = '';
    date = '';
    time = '';
    roomName = '';
    totalAmount = 0;
    status = '';
}

const reservation = new function () {
    // ========= 查詢參數 =========
    this.query = reactive({
        keyword: '',
        status: ''
    });

    this.list = reactive([]);

    // ========= 審核表單 =========
    this.vm = reactive({
        result: 'approve',
        rejectReason: '',
        discountType: 'none',
        discountPercent: 10,
        discountAmount: 0,
        discountReason: ''
    });

    this.currentReview = reactive({});

    // ========= 價格明細 =========
    this.pricing = reactive({
        place: 0,
        equipment: 0,
        booth: 0,
        discount: 0,
        final: 0
    });

    // ========= 取得待審核列表 =========
    this.getList = () => {
        console.log('🔍 開始呼叫 API...');

        this.query = {
            reservationStatus: 1
        };

        global.api.reservations.list({
            body: this.query
        })
            .then((response) => {
                if (Array.isArray(response.data)) {
                    console.log(response.data);
                    const mapped = response.data.map(x => ({
                        id: x.Id,
                        bookingNo: x.BookingNo,
                        applicantName: x.ApplicantName,
                        date: x.Date,
                        time: x.Time,
                        roomName: x.RoomName,
                        totalAmount: x.TotalAmount,
                        status: x.Status,
                        // ✅ 修正: 把 Slots 也 mapping 成小寫
                        slots: (x.Slots || []).map(s => ({
                            id: s.Id,
                            slotDate: s.SlotDate,
                            startTime: s.StartTime,
                            endTime: s.EndTime,
                            slotStatus: s.SlotStatus
                        }))
                    }));

                    copy(this.list, mapped);
                }
            })
            .catch(error => {
                console.error('❌ API 呼叫失敗:', error);
                addAlert('取得預約列表失敗', { type: 'danger', click: error.download });
            });
    };

    // ========= 開啟審核抽屜 =========
    // ✅ 改成 async/await
    this.openReview = (item) => {
        console.log('📋 開啟審核,item:', item);
        copy(this.currentReview, item);

        // ✅ 直接使用 item 裡的 slots,不用再呼叫 API!
        console.log('✅ 時段明細:', item.slots);

        // 重設審核表單
        this.vm.result = 'approve';
        this.vm.rejectReason = '';
        this.vm.discountType = 'none';
        this.vm.discountPercent = 10;
        this.vm.discountAmount = 0;
        this.vm.discountReason = '';

        // 計算價格
        this.calculatePricing();
    };

    // ========= 計算價格明細 =========
    this.calculatePricing = () => {
        const base = this.currentReview.totalAmount || 0;
        let discount = 0;

        if (this.vm.discountType === 'percent') {
            discount = Math.round(base * (this.vm.discountPercent / 100));
        } else if (this.vm.discountType === 'amount') {
            discount = this.vm.discountAmount;
        } else if (this.vm.discountType === 'free') {
            discount = base;
        }

        this.pricing.place = Math.round(base * 0.8);
        this.pricing.equipment = Math.round(base * 0.1);
        this.pricing.booth = Math.round(base * 0.1);
        this.pricing.discount = discount;
        this.pricing.final = Math.max(0, base - discount);
    };

    this.closeDrawer = () => {
        const modalElement = document.getElementById('reviewDrawer');
        const offcanvas = window.bootstrap?.Offcanvas?.getInstance(modalElement);
        if (offcanvas) {
            offcanvas.hide();
        }
    };

    // ========= 驗證 =========
    this.validate = () => {
        if (this.vm.result === 'approve') {
            if (this.vm.discountType === 'free' && !this.vm.discountReason.trim()) {
                addAlert('免單必須填寫原因', { type: 'warning' });
                return false;
            }
        }

        if (this.vm.result === 'reject') {
            if (!this.vm.rejectReason.trim()) {
                addAlert('拒絕必須填寫原因', { type: 'warning' });
                return false;
            }
        }

        return true;
    };

    // ========= 送出審核 =========
    this.submitReview = async () => {
        console.log('🚀 開始送出審核');
        console.log('📋 當前審核資料:', this.currentReview);
        console.log('📝 表單資料:', this.vm);
        console.log('💰 價格明細:', this.pricing);

        // 驗證
        if (!this.validate()) {
            console.log('❌ 驗證失敗');
            return;
        }

        console.log('✅ 驗證通過');

        if (this.vm.result === 'approve') {
            console.log('👍 執行審核通過流程');
            await this.approveReservation();
        } else {
            console.log('👎 執行審核拒絕流程');
            await this.rejectReservation();
        }

        console.log('✅ 審核流程完成');
    };

    // ========= 審核通過 =========
    this.approveReservation = async () => {
        try {
            const res = await global.api.reservations.approve({
                body: {
                    conferenceId: this.currentReview.id,  // ✅ 小寫 id
                    discountAmount: this.pricing.discount  // ✅ 傳折扣金額
                }
            });

            addAlert('審核通過!', { type: 'success' });
            this.getList();
            this.closeDrawer();
        } catch (err) {
            console.error('審核失敗:', err);
            addAlert('審核失敗：' + (err.message || '未知錯誤'), { type: 'danger' });
        }
    };

    // ========= 審核拒絕 =========

    // ========= 審核拒絕 =========
    this.rejectReservation = async () => {
        try {
            const res = await global.api.reservations.reject({
                body: {
                    conferenceId: this.currentReview.id,  // ✅ 小寫 id
                    reason: this.vm.rejectReason
                }
            });

            addAlert('已拒絕預約!', { type: 'success' });
            this.getList();
            this.closeDrawer();
        } catch (err) {
            console.error('拒絕失敗:', err);
            addAlert('拒絕失敗：' + (err.message || '未知錯誤'), { type: 'danger' });
        }
    };
};

// ========= Vue Setup =====
window.$config = {
    setup() {
        watch(() => reservation.vm.discountType, () => {
            reservation.calculatePricing();
        });

        watch(() => reservation.vm.discountPercent, () => {
            reservation.calculatePricing();
        });

        watch(() => reservation.vm.discountAmount, () => {
            reservation.calculatePricing();
        });

        // 初始化
        onMounted(() => {
            console.log('🚀 Vue 組件已掛載，開始載入列表');
            reservation.getList();
        });

        return {
            reservation
        };
    }
};