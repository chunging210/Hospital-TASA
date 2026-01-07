// Conference Create Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch, toRaw } = Vue;

/* ===============================
 * 主畫面 ViewModel
 * =============================== */
window.$config = {
    setup: () => new function () {

        /* ========= 基本資料 ========= */
        this.initiatorName = ref('');
        this.initiatorId = ref('');
        this.availableEquipment = ref([]);
        this.availableBooths = ref([]);

        this.form = reactive({
            name: '',
            content: '',
            date: '',
            meetingType: 'physical',

            departmentId: null,
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
        this.departments = ref([]);
        this.buildings = ref([]);
        this.rooms = ref([]);
        this.selectedRoom = ref(null);
        this.timeSlots = ref([]);

        /* ========= 計算樓層選項 ========= */
        this.availableFloors = computed(() => {
            const b = this.buildings.value.find(
                x => x.Building === this.form.building
            );
            return b ? b.Floors : [];
        });

        this.filteredRooms = computed(() => this.rooms.value);

        /* ========= 費用計算 ========= */
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

        this.totalAmount = computed(() => {
            return this.roomCost.value + this.equipmentCost.value + this.boothCost.value;
        });

        /* ====== 載入分院 ====== */
        this.loadDepartments = () => {
            global.api.select.department()
                .then(res => {
                    this.departments.value = res.data || [];
                })
                .catch(() => {
                    addAlert('取得分院列表失敗', { type: 'danger' });
                });
        };

        /* ====== 載入大樓（根據分院） ====== */
        this.loadBuildingsByDepartment = (departmentId) => {
            if (!departmentId) {
                this.buildings.value = [];
                return;
            }

            global.api.select.buildingsbydepartment({
                body: {
                    departmentId: departmentId
                }
            })
                .then(res => {
                    this.buildings.value = res.data || [];
                })
                .catch(() => {
                    addAlert('取得大樓列表失敗', { type: 'danger' });
                });
        };

        /* ====== 載入樓層（根據分院+大樓） ====== */
        this.loadFloorsByBuilding = (building) => {
            if (!building || !this.form.departmentId) {
                return;
            }

            global.api.select.floorsbybuilding({
                body: {
                    departmentId: this.form.departmentId,
                    building: building
                }
            })
                .then(res => {
                    const buildingItem = this.buildings.value.find(
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

        /* ====== 載入會議室（根據樓層） ====== */
        this.loadRoomsByFloor = async () => {
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
                console.log('✅ 成功載入會議室:', this.rooms.value);

            } catch (error) {
                console.error('❌ 失敗:', error);
            }
        };

        /* ========= 設備和攤位 ========= */
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
                console.log('✅ 設備資料:', res);

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
                console.error('❌ 錯誤:', err);
            }
        };

        /* ========= 時段 ========= */
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

        this.toggleEquipment = (equipmentId) => {
            const idx = this.form.selectedEquipment.indexOf(equipmentId);
            if (idx > -1) {
                this.form.selectedEquipment.splice(idx, 1);
            } else {
                this.form.selectedEquipment.push(equipmentId);
            }
        };

        this.toggleBooth = (boothId) => {
            const idx = this.form.selectedBooths.indexOf(boothId);
            if (idx > -1) {
                this.form.selectedBooths.splice(idx, 1);
            } else {
                this.form.selectedBooths.push(boothId);
            }
        };

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

        /* ===============================
         * mounted
         * =============================== */
        onMounted(async () => {
            // 先載入分院列表
            this.loadDepartments();

            // 檢查 URL 參數
            const params = new URLSearchParams(location.search);
            const presetRoomId = params.get('roomId');
            const presetBuilding = params.get('building');
            const presetFloor = params.get('floor');
            const presetDepartmentId = params.get('departmentId');


            console.log('📌 預設參數', {
                presetRoomId,
                presetBuilding,
                presetFloor,
                presetDepartmentId
            });

            // 載入使用者資訊
            try {
                const userRes = await global.api.auth.me();
                const currentUser = userRes.data;

                console.log('✅ 目前登入使用者:', currentUser);

                this.initiatorName.value = currentUser.Name || '未知使用者';
                this.initiatorId.value = currentUser.Id || '';

                this.form.initiatorId = this.initiatorId.value;
                this.form.attendees = [this.initiatorId.value];

            } catch (err) {
                console.error('❌ 無法取得使用者資訊:', err);
                this.initiatorName.value = '未知使用者';
                this.initiatorId.value = '';
            }

            // 如果從「立即預約」進來，自動填入資料
            if (presetRoomId && presetBuilding && presetFloor && presetDepartmentId) {


                this.form.departmentId = presetDepartmentId;  // ✅ 先設定分院
                await this.loadBuildingsByDepartment(presetDepartmentId);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.building = presetBuilding;
                this.loadFloorsByBuilding(presetBuilding);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.floor = presetFloor;
                await this.loadRoomsByFloor();
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.roomId = presetRoomId;
                this.selectedRoom.value =
                    this.rooms.value.find(r => r.Id === presetRoomId) || null;
                await this.updateTimeSlots();
                console.log('✅ 自動選好會議室', this.selectedRoom.value);
            }

            // Watch 監聽選擇變化
            watch(
                () => this.form.departmentId,
                (departmentId) => {
                    if (!departmentId) {
                        this.buildings.value = [];
                        this.form.building = '';
                        this.form.floor = '';
                        this.form.roomId = '';
                        this.rooms.value = [];
                        this.timeSlots.value = [];
                        this.form.selectedSlots = [];
                        return;
                    }

                    this.form.building = '';
                    this.form.floor = '';
                    this.form.roomId = '';
                    this.rooms.value = [];
                    this.timeSlots.value = [];
                    this.form.selectedSlots = [];

                    this.loadBuildingsByDepartment(departmentId);
                }
            );

            watch(
                () => this.form.building,
                (building) => {
                    if (!building) {
                        this.form.floor = '';
                        this.form.roomId = '';
                        this.rooms.value = [];
                        this.timeSlots.value = [];
                        this.form.selectedSlots = [];
                        return;
                    }

                    this.form.floor = '';
                    this.form.roomId = '';
                    this.rooms.value = [];
                    this.timeSlots.value = [];
                    this.form.selectedSlots = [];

                    this.loadFloorsByBuilding(building);
                }
            );

            watch(
                () => this.form.floor,
                (floor) => {
                    if (!floor) {
                        this.form.roomId = '';
                        this.rooms.value = [];
                        this.timeSlots.value = [];
                        this.form.selectedSlots = [];
                        return;
                    }

                    this.loadRoomsByFloor();
                }
            );

            watch(
                () => this.form.roomId,
                (roomId) => {
                    console.log('🔄 roomId changed:', roomId);
                    this.loadEquipmentByRoom(roomId);
                    this.updateTimeSlots();  // ✅ 加上這行
                }
            );

            watch(
                () => this.form.date,
                () => {
                    this.updateTimeSlots();
                }
            );
        });
    }
};