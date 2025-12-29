// Conference Create Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch } = Vue;

/* ===============================
 * 主畫面 ViewModel
 * =============================== */
window.$config = {
    setup: () => new function () {

        /* ========= 基本資料 ========= */
        this.initiatorName = ref(''); // 之後可接 me.vm.Name
        this.initiatorId = ref('');
        this.availableEquipment = ref([]);
        this.availableBooths = ref([]);

        this.form = reactive({
            name: '',
            content: '',
            date: '',
            meetingType: 'physical',

            building: '',
            floor: '',
            roomId: '',

            initiatorId: '',
            attendees: [],

            selectedSlots: [],

            selectedEquipment: [],
            selectedBooths: [],

            paymentMethod: '',
            departmentCode: ''
        });

        /* ========= 會議室資料 ========= */
        this.buildings = ref([]);          // API buildingfloors
        this.rooms = ref([]);              // 展平後 rooms
        this.selectedRoom = ref(null);
        this.timeSlots = ref([]);

        /* ========= 設備 / 攤位 ========= */

        this.availableFloors = computed(() => {
            const b = this.buildings.value.find(
                x => x.Building === this.form.building
            );
            return b ? b.Floors : [];
        });


        this.submitBooking = () => {
            if (!this.form.name || !this.form.date || !this.form.roomId) {
                alert('請填寫完整會議資訊');
                return;
            }

            const payload = {
                ...this.form,
                roomCost: this.roomCost.value,
                equipmentCost: this.equipmentCost.value,
                boothCost: this.boothCost.value,
                totalAmount: this.totalAmount.value
            };

            console.log('送出資料', payload);
            // 呼叫後端 API
            global.api.conference.create({ body: payload })
                .then(res => {
                    alert('預約成功');
                    // 重導到預約清單
                })
                .catch(err => {
                    alert('預約失敗：' + err.message);
                });
        };

        this.loadEquipmentByRoom = async (roomId = null) => {
            try {

                const body = {};

                if (roomId) {
                    body.roomId = roomId;
                }

                const res = await global.api.select.equipmentbyroom({
                    body
                });

                const allData = res.data;
                console.log('✅ 回傳資料:', res);
                // ✅ 分離設備和攤位
                this.availableEquipment.value = allData
                    .filter(e => e.TypeName !== '攤位租借')
                    .map(e => ({
                        id: e.Id,
                        name: e.Name,
                        icon: 'bx-cog',
                        description: e.ProductModel || '設備',
                        price: e.RentalPrice
                    }));

                this.availableBooths.value = allData
                    .filter(e => e.TypeName === '攤位租借')
                    .map(e => ({
                        id: e.Id,
                        name: e.Name,
                        icon: 'bx-store',
                        description: e.ProductModel || '攤位',
                        price: e.RentalPrice
                    }));


            } catch (err) {
                console.error('❌ 錯誤:', err);  // ← 這樣才能看到錯誤
            }

        };

        /* ===============================
         * computed
         * =============================== */
        this.filteredRooms = computed(() => this.rooms.value);

        this.roomCost = computed(() => {
            if (!this.form.selectedSlots.length) return 0;

            return this.timeSlots.value
                .filter(slot => this.form.selectedSlots.includes(slot.Key))
                .reduce((sum, slot) => sum + slot.Price, 0);
        });

        this.equipmentCost = computed(() => {
            return this.form.selectedEquipment.reduce((sum, id) => {
                const equipment = this.availableEquipment.value.find(e => e.id === id);
                return sum + (equipment ? equipment.price : 0);
            }, 0);
        });

        this.boothCost = computed(() => {
            return this.form.selectedBooths.reduce((sum, id) => {
                const booth = this.availableBooths.value.find(e => e.id === id);
                return sum + (booth ? booth.price : 0);
            }, 0);
        });

        this.toggleEquipment = (equipmentId) => {
            const idx = this.form.selectedEquipment.indexOf(equipmentId);
            if (idx > -1) {
                this.form.selectedEquipment.splice(idx, 1);
            } else {
                this.form.selectedEquipment.push(equipmentId);
            }
        };

        /* ===============================
         * methods（對齊畫面）
         * =============================== */

        this.updateTimeSlots = async () => {
            console.group('🟦 updateTimeSlots Debug');

            console.log('form.roomId =', this.form.roomId);
            console.log('form.date   =', this.form.date);

            if (!this.form.roomId || !this.form.date) {
                console.warn('⏸ 條件不足，等待 roomId + date');
                console.groupEnd();
                return;
            }

            this.selectedRoom.value =
                this.rooms.value.find(r => r.Id === this.form.roomId) || null;

            console.log('selectedRoom =', this.selectedRoom.value);

            this.form.selectedSlots = [];
            this.timeSlots.value = [];

            const payload = {
                roomId: this.form.roomId,
                date: this.form.date
            };
            console.log('➡️ request payload =', payload);

            try {
                const res = await global.api.select.roomslots({
                    body: payload
                });

                console.log('✅ API data =', res.data);
                this.timeSlots.value = res.data || [];

            } catch (err) {
                console.error('🔥 roomslots API error', err);
            } finally {
                console.groupEnd();
            }

        };

        this.displayedSlots = computed(() => {
            const room = this.selectedRoom.value;
            if (!room) return [];

            return this.timeSlots.value.map(slot => ({
                ...slot,
                displayLabel:
                    room.PricingType === 0
                        ? `${slot.StartTime} - ${slot.EndTime}`
                        : slot.Name
            }));
        });

        this.isSlotSelected = (slot) => {
            return this.form.selectedSlots.includes(slot.Key);
        };

        this.toggleTimeSlot = (slot) => {
            if (slot.Occupied) return;

            const idx = this.form.selectedSlots.indexOf(slot.Key);
            if (idx > -1) {
                this.form.selectedSlots.splice(idx, 1);
            } else {
                this.form.selectedSlots.push(slot.Key);
            }
        };

        this.updateTotal = () => { };

        this.submitBooking = () => {
            if (!this.form.name || !this.form.date || !this.form.roomId) {
                alert('請填寫完整會議資訊');
                return;
            }

            const payload = {
                ...this.form,
                roomCost: this.roomCost.value,
                equipmentCost: this.equipmentCost.value,
                boothCost: this.boothCost.value,
                totalAmount: this.totalAmount.value
            };

            console.log('送出資料', payload);
            alert('預約成功（示意）');
        };

        this.onBuildingChange = () => {
            this.form.floor = '';
            this.form.roomId = '';
            this.timeSlots.value = [];
            this.form.selectedSlots = [];
        };

        this.onFloorChange = async () => {
            if (!this.form.building || !this.form.floor) return;

            this.form.roomId = '';
            this.rooms.value = [];
            this.timeSlots.value = [];
            this.form.selectedSlots = [];

            try {
                const res = await global.api.select.roomsbyfloor({
                    body: {
                        building: this.form.building,
                        floor: this.form.floor
                    }
                });

                this.rooms.value = res.data || [];
                console.log('✅ 成功:', this.rooms.value);

            } catch (error) {
                console.error('❌ 失敗:', error);
            }
        };

        this.toggleEquipment = (price) => {
            const idx = this.form.selectedEquipment.indexOf(price);
            if (idx > -1) {
                this.form.selectedEquipment.splice(idx, 1);
            } else {
                this.form.selectedEquipment.push(price);
            }
        };

        this.toggleBooth = (price) => {
            const idx = this.form.selectedBooths.indexOf(price);
            if (idx > -1) {
                this.form.selectedBooths.splice(idx, 1);
            } else {
                this.form.selectedBooths.push(price);
            }
        };

        watch(
            () => this.form.roomId,
            (roomId) => {
                console.log('🔄 roomId changed:', roomId);

                // roomId === '' → 不帶參數
                // roomId === guid → 帶 roomId
                this.loadEquipmentByRoom(roomId);
            },
            { immediate: true } // ⭐ 一進畫面就會跑一次（共用設備）
        );


        /* ===============================
         * mounted
         * =============================== */
        onMounted(async () => {
            const params = new URLSearchParams(location.search);

            const presetRoomId = params.get('roomId');
            const presetBuilding = params.get('building');
            const presetFloor = params.get('floor');
            const presetDate = params.get('date');

            if (presetDate) {
                this.form.date = presetDate;
            }
            console.log('📌 預設參數', {
                presetRoomId,
                presetBuilding,
                presetFloor
            });
            // 先載入大樓 / 樓層資料
            const bfRes = await global.api.select.buildingfloors();
            this.buildings.value = bfRes.data || [];
            await this.updateTimeSlots();


            // 如果是從「立即預約」進來
            if (presetRoomId && presetBuilding && presetFloor) {

                // 1️⃣ 設定大樓
                this.form.building = presetBuilding;

                // 2️⃣ 設定樓層
                this.form.floor = presetFloor;

                // 3️⃣ 撈該樓層會議室
                const roomRes = await global.api.select.roomsbyfloor({
                    body: {
                        building: presetBuilding,
                        floor: presetFloor
                    }
                });
                this.rooms.value = roomRes.data || [];

                // 4️⃣ 選中會議室
                this.form.roomId = presetRoomId;
                this.selectedRoom.value =
                    this.rooms.value.find(r => r.Id === presetRoomId) || null;
                await this.updateTimeSlots();
                console.log('✅ 自動選好會議室', this.selectedRoom.value);
            }



            try {
                const userRes = await global.api.auth.me();
                const currentUser = userRes.data;

                console.log('✅ 目前登入使用者:', currentUser);

                // ✅ 修正：直接使用 API 返回的欄位名稱
                this.initiatorName.value = currentUser.Name || '未知使用者';
                this.initiatorId.value = currentUser.Id || '';

                // ✅ 設定表單
                this.form.initiatorId = this.initiatorId.value;
                this.form.attendees = [this.initiatorId.value];

            } catch (err) {
                console.error('❌ 無法取得使用者資訊:', err);
                this.initiatorName.value = '未知使用者';
                this.initiatorId.value = '';
            }
        });
    }
};
