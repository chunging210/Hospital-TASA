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
            roomId: null,

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

            this.form.roomId = null;
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
        this.loadEquipmentByRoom = async () => {
            try {
                const roomId = this.form.roomId;  // ✅ 直接讀取 reactive 的值

                console.log('🔄 loadEquipmentByRoom - roomId:', roomId);

                const body = {};
                if (roomId) {
                    body.RoomId = roomId;
                }

                console.log('📤 send body:', body);

                const res = await global.api.select.equipmentbyroom({
                    body
                });

                // ✅ 檢查回傳的資料結構
                console.log('✅ API 回傳:', res);

                let allData = [];

                // 如果 res.data 是陣列，直接使用
                if (Array.isArray(res.data)) {
                    allData = res.data;
                }
                // 如果是物件（EquipmentGroupVM），合併 Shared 和 ByRoom
                else if (res.data && typeof res.data === 'object') {
                    const shared = res.data.Shared || [];
                    const byRoom = res.data.ByRoom || {};

                    // 合併共用設備和該房間的設備
                    allData = [
                        ...shared,
                        ...Object.values(byRoom).flat()
                    ];
                }

                console.log('📊 整理後的設備列表:', allData);

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

                console.log('✅ 設備:', this.availableEquipment.value);
                console.log('✅ 攤位:', this.availableBooths.value);

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

                // 🔍 【新增 DEBUG】
                if (this.timeSlots.value.length > 0) {
                    console.log('🔍 第一個時段完整結構:');
                    console.log(this.timeSlots.value[0]);
                }

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

        // ✅ 新增：計算時段持續時間
        this.calculateDuration = () => {
            if (!this.selectedRoom.value || !this.form.selectedSlots.length) {
                return { hours: 0, minutes: 0 };
            }

            // 取得選中的所有時段
            const selectedSlots = this.timeSlots.value.filter(slot =>
                this.form.selectedSlots.includes(slot.Key)
            );

            if (!selectedSlots.length) {
                return { hours: 0, minutes: 0 };
            }

            // 按開始時間排序
            selectedSlots.sort((a, b) => a.StartTime.localeCompare(b.StartTime));

            // 取第一個時段的開始時間和最後一個時段的結束時間
            const firstSlot = selectedSlots[0];
            const lastSlot = selectedSlots[selectedSlots.length - 1];

            const startTime = this.parseTime(firstSlot.StartTime);
            const endTime = this.parseTime(lastSlot.EndTime);

            // 計算時間差（秒轉換為分鐘）
            const totalMinutes = (endTime - startTime) / 60;
            const hours = Math.floor(totalMinutes / 60);
            const minutes = totalMinutes % 60;

            return {
                hours: Math.max(0, hours),
                minutes: Math.max(0, Math.round(minutes))
            };
        };

        // ✅ 輔助方法：解析時間字串為秒數
        this.parseTime = (timeStr) => {
            // 時間格式: "09:00" 或 "09:00:00"
            if (!timeStr) return 0;

            const parts = timeStr.split(':').map(Number);
            const hours = parts[0] || 0;
            const minutes = parts[1] || 0;
            const seconds = parts[2] || 0;

            return hours * 3600 + minutes * 60 + seconds;
        };

        this.submitBooking = () => {
            console.log('🟢 submitBooking 開始執行');

            // ===== 驗證 =====
            if (!this.form.name.trim()) {
                addAlert('請填寫會議名稱', { type: 'warning' });
                return;
            }
            if (!this.form.date) {
                addAlert('請選擇會議日期', { type: 'warning' });
                return;
            }
            if (!this.form.roomId) {
                addAlert('請選擇會議室', { type: 'warning' });
                return;
            }
            if (!this.form.selectedSlots.length) {
                addAlert('請選擇時段', { type: 'warning' });
                return;
            }
            if (!this.form.paymentMethod) {
                addAlert('請選擇付款方式', { type: 'warning' });
                return;
            }

            console.log('✅ 所有驗證通過');

            // ===== 準備發送資料 =====
            const payload = {
                // Conference 基本資訊
                name: this.form.name,
                description: this.form.content,
                usageType: 1,  // 實體會議
                durationHH: this.calculateDuration().hours,
                durationSS: this.calculateDuration().minutes,
                reservationDate: this.form.date,
                // 付款
                paymentMethod: this.form.paymentMethod,
                departmentCode: this.form.paymentMethod === 'cost-sharing' ? this.form.departmentCode : null,
                roomCost: this.roomCost.value,
                equipmentCost: this.equipmentCost.value,
                boothCost: this.boothCost.value,
                totalAmount: this.totalAmount.value,

                // 會議室時段
                roomId: this.form.roomId,
                // ✅ 【重要】轉換 Proxy Array 成普通陣列
                slotKeys: [...this.form.selectedSlots],

                // 設備和攤位
                // ✅ 【重要】轉換 Proxy Array 成普通陣列
                equipmentIds: [...this.form.selectedEquipment],
                boothIds: [...this.form.selectedBooths],

                // 參與者
                attendeeIds: [this.initiatorId.value]
            };

            console.log('📤 payload:', JSON.stringify(payload));

            // ✅ 改為呼叫新的 createreservation endpoint
            global.api.reservations.createreservation({ body: payload })
                .then(res => {
                    console.log('%c✅ 預約成功！', 'color: #00aa00; font-weight: bold; font-size: 14px;');
                    console.log('預約ID:', res);

                    addAlert('預約已送出，請等待管理者審核！', { type: 'success' });

                    // 延遲後重導到預約清單
                    setTimeout(() => {
                        window.location.href = '/reservationoverview';
                    }, 2000);
                })
                .catch(err => {
                    console.error('%c❌ 預約失敗！', 'color: #aa0000; font-weight: bold; font-size: 14px;');
                    console.error('錯誤:', err);
                    addAlert('預約失敗：' + (err.message || '未知錯誤'), { type: 'danger' });
                });
        };

        /* ===============================
         * mounted
         * =============================== */
        onMounted(async () => {
            // 先載入分院列表
            this.loadDepartments();

            // ✅ 第一次載入共用設備（form.roomId 為 null）
            await this.loadEquipmentByRoom();

            // 檢查 URL 參數
            const params = new URLSearchParams(location.search);
            const presetRoomId = params.get('roomId');
            const presetBuilding = params.get('building');
            const presetFloor = params.get('floor');
            const presetDepartmentId = params.get('departmentId');

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

                this.form.roomId = presetRoomId;  // ✅ 設定 roomId
                this.selectedRoom.value =
                    this.rooms.value.find(r => r.Id === presetRoomId) || null;

                await this.updateTimeSlots();
                // ✅ 現在 form.roomId 已設定，直接呼叫（會自動讀取 form.roomId）
                await this.loadEquipmentByRoom();

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
                        this.form.roomId = null;
                        this.rooms.value = [];
                        this.timeSlots.value = [];
                        this.form.selectedSlots = [];
                        return;
                    }

                    this.form.building = '';
                    this.form.floor = '';
                    this.form.roomId = null;
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
                        this.form.roomId = null;
                        this.rooms.value = [];
                        this.timeSlots.value = [];
                        this.form.selectedSlots = [];
                        return;
                    }

                    this.form.floor = '';
                    this.form.roomId = null;
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
                        this.form.roomId = null;
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
                    if (!roomId) return;
                    console.log('🔄 roomId changed:', roomId);
                    // ✅ roomId 已改變，直接呼叫（會自動讀取最新的 form.roomId）
                    this.loadEquipmentByRoom();
                    this.updateTimeSlots();
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