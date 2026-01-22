// Conference Create/Edit Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch } = Vue;

window.$config = {
    setup: () => new function () {

        /* ========= 編輯模式相關 ========= */
        this.isEditMode = ref(false);
        this.editingReservationId = ref(null);

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
            const b = this.buildings.value.find(x => x.Building === this.form.building);
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

        /* ====== 載入大樓 ====== */
        this.loadBuildingsByDepartment = (departmentId) => {
            if (!departmentId) {
                this.buildings.value = [];
                return;
            }

            global.api.select.buildingsbydepartment({
                body: { departmentId: departmentId }
            })
                .then(res => {
                    this.buildings.value = res.data || [];
                })
                .catch(() => {
                    addAlert('取得大樓列表失敗', { type: 'danger' });
                });
        };

        /* ====== 載入樓層 ====== */
        this.loadFloorsByBuilding = (building) => {
            if (!building || !this.form.departmentId) return;

            global.api.select.floorsbybuilding({
                body: {
                    departmentId: this.form.departmentId,
                    building: building
                }
            })
                .then(res => {
                    const buildingItem = this.buildings.value.find(b => b.Building === building);
                    if (buildingItem) {
                        buildingItem.Floors = (res.data || []).map(f => f.Name);
                    }
                })
                .catch(() => {
                    addAlert('取得樓層列表失敗', { type: 'danger' });
                });
        };

        /* ====== 載入會議室 ====== */
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

        /* ====== 載入設備和攤位 ====== */
        this.loadEquipmentByRoom = async () => {
            // ✅ 條件判斷
            if (!this.form.roomId || !this.form.date || !this.form.selectedSlots.length) {
                console.warn('⏸ 條件不足,無法載入設備');
                return;
            }

            try {
                const body = {
                    roomId: this.form.roomId,
                    date: this.form.date,
                    slotKeys: this.form.selectedSlots,  // ✅ 傳送所有選定的時段
                    excludeConferenceId: this.isEditMode.value ? this.editingReservationId.value : null
                };

                console.log('📤 發送請求:', JSON.stringify(body));
                const res = await global.api.select.equipmentbyroom({ body });

                let allData = [];
                if (Array.isArray(res.data)) {
                    allData = res.data;
                } else if (res.data && typeof res.data === 'object') {
                    const shared = res.data.Shared || [];
                    const byRoom = res.data.ByRoom || {};
                    allData = [...shared, ...Object.values(byRoom).flat()];
                }

                // ✅ 後端已經標記好 Occupied 狀態
                this.availableEquipment.value = allData
                    .filter(e => e.TypeName !== '攤位租借')
                    .map(e => ({
                        id: e.Id,
                        name: e.Name,
                        icon: 'bx-cog',
                        description: e.ProductModel || '設備',
                        price: e.RentalPrice,
                        occupied: e.Occupied || false
                    }));

                this.availableBooths.value = allData
                    .filter(e => e.TypeName === '攤位租借')
                    .map(e => ({
                        id: e.Id,
                        name: e.Name,
                        icon: 'bx-store',
                        description: e.ProductModel || '攤位',
                        price: e.RentalPrice,
                        occupied: e.Occupied || false
                    }));

                console.log('✅ 設備:', this.availableEquipment.value);
                console.log('✅ 攤位:', this.availableBooths.value);

                // ✅ 過濾掉已選但現在變成 occupied 的設備
                this.form.selectedEquipment = this.form.selectedEquipment.filter(id => {
                    const equipment = this.availableEquipment.value.find(e => e.id === id);
                    return equipment && !equipment.occupied;
                });

                this.form.selectedBooths = this.form.selectedBooths.filter(id => {
                    const booth = this.availableBooths.value.find(b => b.id === id);
                    return booth && !booth.occupied;
                });

            } catch (err) {
                console.error('❌ 錯誤:', err);
            }
        };

        /* ====== 載入時段 ====== */
        this.updateTimeSlots = async () => {
            console.group('🟦 updateTimeSlots Debug');

            if (!this.form.roomId || !this.form.date) {
                console.warn('⏸ 條件不足');
                console.groupEnd();
                return;
            }

            this.selectedRoom.value = this.rooms.value.find(r => r.Id === this.form.roomId) || null;
            this.form.selectedSlots = [];
            this.timeSlots.value = [];

            const dateStr = this.form.date instanceof Date
                ? this.form.date.toISOString().split('T')[0]
                : this.form.date;

            const payload = {
                roomId: this.form.roomId,
                date: dateStr,
                excludeConferenceId: this.isEditMode.value ? this.editingReservationId.value : null
            };

            try {
                const res = await global.api.select.roomslots({ body: payload });
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
                displayLabel: room.PricingType === 0
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
            const equipment = this.availableEquipment.value.find(e => e.id === equipmentId);

            if (equipment && equipment.occupied) {
                addAlert(`${equipment.name} 在選定時段已被借用`, { type: 'warning' });
                return;
            }

            const idx = this.form.selectedEquipment.indexOf(equipmentId);
            if (idx > -1) {
                this.form.selectedEquipment.splice(idx, 1);
            } else {
                this.form.selectedEquipment.push(equipmentId);
            }
        };

        this.toggleBooth = (boothId) => {
            const booth = this.availableBooths.value.find(b => b.id === boothId);

            if (booth && booth.occupied) {
                addAlert(`${booth.name} 在選定時段已被借用`, { type: 'warning' });
                return;
            }

            const idx = this.form.selectedBooths.indexOf(boothId);
            if (idx > -1) {
                this.form.selectedBooths.splice(idx, 1);
            } else {
                this.form.selectedBooths.push(boothId);
            }
        };


        this.calculateDuration = () => {
            if (!this.selectedRoom.value || !this.form.selectedSlots.length) {
                return { hours: 0, minutes: 0 };
            }

            const selectedSlots = this.timeSlots.value.filter(slot =>
                this.form.selectedSlots.includes(slot.Key)
            );

            if (!selectedSlots.length) {
                return { hours: 0, minutes: 0 };
            }

            selectedSlots.sort((a, b) => a.StartTime.localeCompare(b.StartTime));

            const firstSlot = selectedSlots[0];
            const lastSlot = selectedSlots[selectedSlots.length - 1];

            const startTime = this.parseTime(firstSlot.StartTime);
            const endTime = this.parseTime(lastSlot.EndTime);

            const totalMinutes = (endTime - startTime) / 60;
            const hours = Math.floor(totalMinutes / 60);
            const minutes = totalMinutes % 60;

            return {
                hours: Math.max(0, hours),
                minutes: Math.max(0, Math.round(minutes))
            };
        };

        this.parseTime = (timeStr) => {
            if (!timeStr) return 0;
            const parts = timeStr.split(':').map(Number);
            const hours = parts[0] || 0;
            const minutes = parts[1] || 0;
            const seconds = parts[2] || 0;
            return hours * 3600 + minutes * 60 + seconds;
        };

        /* ====== 載入預約資料（編輯模式） ====== */
        this.loadReservationData = async (reservationNo) => {
            try {
                console.log('🔄 載入預約資料:', reservationNo);

                const res = await global.api.reservations.detail({
                    body: { reservationNo: reservationNo }
                });

                const data = res.data;
                console.log('✅ 預約資料:', data);

                // 填入基本資訊
                this.form.name = data.ConferenceName || '';
                this.form.content = data.Description || '';
                this.form.date = data.ReservationDate || '';
                this.form.paymentMethod = data.PaymentMethod || '';
                this.form.departmentCode = data.DepartmentCode || '';

                // 填入會議室資訊
                this.form.departmentId = data.DepartmentId;
                await this.loadBuildingsByDepartment(data.DepartmentId);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.building = data.Building;
                this.loadFloorsByBuilding(data.Building);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.floor = data.Floor;
                await this.loadRoomsByFloor();
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.roomId = data.RoomId;
                this.selectedRoom.value = this.rooms.value.find(r => r.Id === data.RoomId) || null;

                // 載入時段
                await this.updateTimeSlots();
                await new Promise(resolve => setTimeout(resolve, 300));


                if (data.SlotKeys && Array.isArray(data.SlotKeys)) {

                    // 直接用 API 回傳的值
                    this.form.selectedSlots = data.SlotKeys;
                }
                // 載入設備
                await this.loadEquipmentByRoom();
                await new Promise(resolve => setTimeout(resolve, 300));

                // 選取已預約的設備和攤位
                if (data.EquipmentIds && Array.isArray(data.EquipmentIds)) {
                    this.form.selectedEquipment = [...data.EquipmentIds];
                }
                if (data.BoothIds && Array.isArray(data.BoothIds)) {
                    this.form.selectedBooths = [...data.BoothIds];
                }

                console.log('✅ 預約資料載入完成');

            } catch (err) {
                console.error('❌ 載入預約資料失敗:', err);
                addAlert('載入預約資料失敗', { type: 'danger' });
                setTimeout(() => {
                    window.location.href = '/reservationoverview';
                }, 2000);
            }
        };

        /* ====== 提交預約 ====== */
        this.submitBooking = () => {
            console.log('🟢 submitBooking 開始執行');

            // 驗證
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

            const payload = {
                name: this.form.name,
                description: this.form.content,
                usageType: 1,
                durationHH: this.calculateDuration().hours,
                durationSS: this.calculateDuration().minutes,
                reservationDate: this.form.date,
                paymentMethod: this.form.paymentMethod,
                departmentCode: this.form.paymentMethod === 'cost-sharing' ? this.form.departmentCode : null,
                roomCost: this.roomCost.value,
                equipmentCost: this.equipmentCost.value,
                boothCost: this.boothCost.value,
                totalAmount: this.totalAmount.value,
                roomId: this.form.roomId,
                slotKeys: [...this.form.selectedSlots],
                equipmentIds: [...this.form.selectedEquipment],
                boothIds: [...this.form.selectedBooths],
                attendeeIds: [this.initiatorId.value]
            };

            if (this.isEditMode.value) {
                payload.reservationNo = this.editingReservationId.value;
            }

            console.log('📤 payload:', JSON.stringify(payload));

            const apiCall = this.isEditMode.value
                ? global.api.reservations.update({ body: payload })
                : global.api.reservations.createreservation({ body: payload });

            apiCall
                .then(res => {
                    const successMsg = this.isEditMode.value
                        ? '預約已更新，請等待管理者審核！'
                        : '預約已送出，請等待管理者審核！';

                    console.log('%c✅ 操作成功！', 'color: #00aa00; font-weight: bold; font-size: 14px;');
                    addAlert(successMsg, { type: 'success' });

                    setTimeout(() => {
                        window.location.href = '/reservationoverview';
                    }, 2000);
                })
                .catch(err => {
                    const errorMsg = this.isEditMode.value ? '更新預約失敗' : '新增預約失敗';
                    console.error('%c❌ 操作失敗！', 'color: #aa0000; font-weight: bold; font-size: 14px;');
                    addAlert(`${errorMsg}：${err.message || '未知錯誤'}`, { type: 'danger' });
                });
        };

        /* ====== Mounted ====== */
        onMounted(async () => {
            this.loadDepartments();
            await this.loadEquipmentByRoom();

            const params = new URLSearchParams(location.search);
            const editId = params.get('id');
            const presetRoomId = params.get('roomId');
            const presetBuilding = params.get('building');
            const presetFloor = params.get('floor');
            const presetDepartmentId = params.get('departmentId');

            // 載入使用者資訊
            try {
                const userRes = await global.api.auth.me();
                const currentUser = userRes.data;
                this.initiatorName.value = currentUser.Name || '未知使用者';
                this.initiatorId.value = currentUser.Id || '';
                this.form.initiatorId = this.initiatorId.value;
                this.form.attendees = [this.initiatorId.value];
            } catch (err) {
                console.error('❌ 無法取得使用者資訊:', err);
                this.initiatorName.value = '未知使用者';
            }

            // 編輯模式
            if (editId) {
                console.log('📝 進入編輯模式');
                this.isEditMode.value = true;
                this.editingReservationId.value = editId;
                await this.loadReservationData(editId);
            }
            // 從「立即預約」進來
            else if (presetRoomId && presetBuilding && presetFloor && presetDepartmentId) {
                this.form.departmentId = presetDepartmentId;
                await this.loadBuildingsByDepartment(presetDepartmentId);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.building = presetBuilding;
                this.loadFloorsByBuilding(presetBuilding);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.floor = presetFloor;
                await this.loadRoomsByFloor();
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.roomId = presetRoomId;
                this.selectedRoom.value = this.rooms.value.find(r => r.Id === presetRoomId) || null;

                await this.updateTimeSlots();
                await this.loadEquipmentByRoom();

                console.log('✅ 自動選好會議室', this.selectedRoom.value);
            }

            // Watch 監聽
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
                    this.loadEquipmentByRoom();
                    this.updateTimeSlots();
                }
            );

            watch(
                () => this.form.date,
                () => {
                    this.updateTimeSlots();
                    // ✅ 日期改變時也要重新檢查設備
                    if (this.form.roomId) {
                        this.loadEquipmentByRoom();
                    }
                }
            );

            watch(
                () => [...this.form.selectedSlots],  // 使用展開運算子建立新陣列,確保能偵測到變化
                (newSlots, oldSlots) => {
                    // 只有在有選會議室的情況下才重新載入設備
                    if (this.form.roomId && this.form.date && newSlots.length > 0) {
                        console.log('🔄 時段變更,重新檢查設備可用性');

                        // ✅ 重新載入設備前,先清空已選設備
                        // 避免使用者選到已被佔用的設備
                        this.form.selectedEquipment = [];
                        this.form.selectedBooths = [];

                        this.loadEquipmentByRoom();
                    }
                },
                { deep: true }  // 深度監聽陣列內容變化
            );
        });
    }
};