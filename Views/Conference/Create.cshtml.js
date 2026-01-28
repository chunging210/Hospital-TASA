// Conference Create/Edit Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch } = Vue;

window.$config = {
    setup: () => new function () {

        this.currentUser = ref(null);
        this.isAdmin = ref(false);

        this.showAgreementPDF = ref(false);          // 控制 PDF 彈窗顯示
        this.hasReadAgreement = ref(false);          // 是否已勾選同意
        this.agreementPdfUrl = ref('');
        this.canConfirmAgreement = ref(false);
        this.hasOpenedAgreement = ref(false);
        /* ========= 編輯模式相關 ========= */
        this.isEditMode = ref(false);
        this.editingReservationId = ref(null);

        /* ========= ✅ 確認彈窗相關 ========= */
        this.showConfirmModal = ref(false);

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
            departmentCode: '',
            attachments: []
        });

        /* ========= 附件管理 ========= */
        this.agendaFile = ref(null);
        this.documentFiles = ref([]);
        this.agendaInput = ref(null);
        this.documentInput = ref(null);

        this.formatFileSize = (bytes) => {
            if (bytes === 0) return '0 Bytes';
            const k = 1024;
            const sizes = ['Bytes', 'KB', 'MB', 'GB'];
            const i = Math.floor(Math.log(bytes) / Math.log(k));
            return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
        };

        this.closeAgreementPDF = () => {
            this.showAgreementPDF.value = false;
        };

        /* ====== PDF 載入完成 ====== */
        this.onPDFLoaded = () => {
            console.log('✅ PDF 載入完成');
        };

        /* ====== 確認已閱讀完畢 ====== */
        this.confirmReadAgreement = () => {
            if (!this.hasOpenedAgreement.value) {
                return;
            }

            this.hasReadAgreement.value = true;
            this.showAgreementPDF.value = false;

            addAlert('已確認閱讀使用聲明書', { type: 'success' });
        };

        this.pdfViewerUrl = computed(() =>
            `/pdfjs/web/viewer.html?file=${encodeURIComponent('/files/agreement.pdf')}`
        );

        this.openAgreementPDF = () => {
            this.hasOpenedAgreement.value = true;
            this.showAgreementPDF.value = true;
        };

        this.validateFile = (file, maxSizeMB = 10) => {
            const maxSize = maxSizeMB * 1024 * 1024;
            if (file.size > maxSize) {
                addAlert(`檔案 ${file.name} 超過 ${maxSizeMB}MB 限制`, { type: 'warning' });
                return false;
            }

            const allowedTypes = [
                'application/pdf',
                'application/msword',
                'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
                'application/vnd.ms-excel',
                'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
                'application/vnd.ms-powerpoint',
                'application/vnd.openxmlformats-officedocument.presentationml.presentation'
            ];

            if (!allowedTypes.includes(file.type)) {
                addAlert(`檔案 ${file.name} 格式不支援`, { type: 'warning' });
                return false;
            }

            return true;
        };

        this.fileToBase64 = (file) => {
            return new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => {
                    const base64 = reader.result.split(',')[1];
                    resolve(base64);
                };
                reader.onerror = reject;
                reader.readAsDataURL(file);
            });
        };

        /* ====== 議程表上傳 ====== */
        this.triggerAgendaUpload = () => {
            this.agendaInput.value?.click();
        };

        this.handleAgendaSelect = (event) => {
            const file = event.target.files?.[0];
            if (file && this.validateFile(file)) {
                this.agendaFile.value = file;
            }
            event.target.value = '';
        };

        this.handleAgendaDrop = (event) => {
            const file = event.dataTransfer.files?.[0];
            if (file && this.validateFile(file)) {
                this.agendaFile.value = file;
            }
        };

        this.removeAgendaFile = () => {
            this.agendaFile.value = null;
        };

        /* ====== 會議文件上傳 ====== */
        this.triggerDocumentUpload = () => {
            this.documentInput.value?.click();
        };

        this.handleDocumentSelect = (event) => {
            const files = Array.from(event.target.files || []);
            files.forEach(file => {
                if (this.validateFile(file)) {
                    this.documentFiles.value.push(file);
                }
            });
            event.target.value = '';
        };

        this.handleDocumentDrop = (event) => {
            const files = Array.from(event.dataTransfer.files || []);
            files.forEach(file => {
                if (this.validateFile(file)) {
                    this.documentFiles.value.push(file);
                }
            });
        };

        this.removeDocumentFile = (index) => {
            this.documentFiles.value.splice(index, 1);
        };

        /* ========= 會議室資料 ========= */
        this.departments = ref([]);
        this.buildings = ref([]);
        this.rooms = ref([]);
        this.selectedRoom = ref(null);
        this.timeSlots = ref([]);

        this.loadCurrentUser = async () => {
            try {
                const userRes = await global.api.auth.me();
                this.currentUser.value = userRes.data;

                this.initiatorName.value = this.currentUser.value.Name || '未知使用者';
                this.initiatorId.value = this.currentUser.value.Id || '';
                this.form.initiatorId = this.initiatorId.value;
                this.form.attendees = [this.initiatorId.value];

                this.isAdmin.value = this.currentUser.value.IsAdmin || false;

                if (!this.isAdmin.value && this.currentUser.value.DepartmentId) {
                    this.form.departmentId = this.currentUser.value.DepartmentId;
                }

                console.log('✅ 使用者資訊載入完成:', {
                    name: this.currentUser.value.Name,
                    isAdmin: this.isAdmin.value,
                    departmentId: this.currentUser.value.DepartmentId,
                    departmentName: this.currentUser.value.DepartmentName
                });

            } catch (err) {
                console.error('❌ 無法取得使用者資訊:', err);
                addAlert('無法取得使用者資訊', { type: 'danger' });
            }
        };

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

        /* ====== ✅ 確認彈窗相關功能 ====== */
        this.showConfirmationModal = () => {
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
            // ✅ 重置聲明書狀態
            this.hasReadAgreement.value = false;
            // 驗證通過,顯示彈窗
            this.showConfirmModal.value = true;
        };

        this.closeConfirmationModal = () => {

            this.showConfirmModal.value = false;
            this.showAgreementPDF.value = false;

            // ✅ 重置聲明書狀態
            this.hasReadAgreement.value = false;
        };

        // ✅ 格式化日期顯示
        this.formatDate = (dateStr) => {
            if (!dateStr) return '';
            const date = new Date(dateStr);
            return date.toLocaleDateString('zh-TW', {
                year: 'numeric',
                month: '2-digit',
                day: '2-digit',
                weekday: 'long'
            });
        };

        // ✅ 取得會議室完整名稱
        this.getRoomFullName = () => {
            const dept = this.departments.value.find(d => d.Id === this.form.departmentId);
            const room = this.rooms.value.find(r => r.Id === this.form.roomId);

            if (this.isAdmin.value && dept) {
                return `${dept.Name} - ${this.form.building} ${this.form.floor} ${room?.Name || ''}`;
            }
            return `${this.form.building} ${this.form.floor} ${room?.Name || ''}`;
        };

        // ✅ 取得選擇的時段文字
        this.getSelectedSlotsText = () => {
            const selectedSlots = this.timeSlots.value.filter(slot =>
                this.form.selectedSlots.includes(slot.Key)
            );

            if (selectedSlots.length === 0) return '無';

            selectedSlots.sort((a, b) => a.StartTime.localeCompare(b.StartTime));

            return selectedSlots.map(slot => {
                const startTime = slot.StartTime.slice(0, 5);
                const endTime = slot.EndTime.slice(0, 5);
                return `${slot.Name || ''} (${startTime} - ${endTime})`;
            }).join(', ');
        };

        // ✅ 取得設備名稱
        this.getEquipmentName = (equipmentId) => {
            const equipment = this.availableEquipment.value.find(e => e.id === equipmentId);
            return equipment ? `${equipment.name} (+$${equipment.price}/天)` : '未知設備';
        };

        // ✅ 取得攤位名稱
        this.getBoothName = (boothId) => {
            const booth = this.availableBooths.value.find(b => b.id === boothId);
            return booth ? `${booth.name} (+$${booth.price}/天)` : '未知攤位';
        };

        // ✅ 取得付款方式文字
        this.getPaymentMethodText = () => {
            const methods = {
                'transfer': '銀行匯款',
                'cost-sharing': '成本分攤',
                'cash': '現金付款'
            };
            return methods[this.form.paymentMethod] || '未選擇';
        };

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
        this.loadBuildingsByDepartment = () => {
            global.api.select.buildingsbydepartment()
                .then(res => {
                    this.buildings.value = res.data || [];
                })
                .catch(() => {
                    addAlert('取得大樓列表失敗', { type: 'danger' });
                });
        };

        /* ====== 載入樓層 ====== */
        this.loadFloorsByBuilding = (building) => {
            if (!building) return;

            global.api.select.floorsbybuilding({
                body: { building: building }
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
            if (!this.form.roomId || !this.form.date || !this.form.selectedSlots.length) {
                console.warn('⏸ 條件不足,無法載入設備');
                return;
            }

            try {
                const body = {
                    roomId: this.form.roomId,
                    date: this.form.date,
                    slotKeys: this.form.selectedSlots,
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

        /* ====== 載入預約資料(編輯模式) ====== */
        this.loadReservationData = async (reservationNo) => {
            try {
                console.log('🔄 載入預約資料:', reservationNo);

                const res = await global.api.reservations.detail({
                    body: { reservationNo: reservationNo }
                });

                const data = res.data;
                console.log('✅ 預約資料:', data);

                this.form.name = data.ConferenceName || '';
                this.form.content = data.Description || '';
                this.form.date = data.ReservationDate || '';
                this.form.paymentMethod = data.PaymentMethod || '';
                this.form.departmentCode = data.DepartmentCode || '';

                if (data.Attachments && Array.isArray(data.Attachments)) {
                    console.log('📎 載入附件:', data.Attachments);

                    const agenda = data.Attachments.find(a => a.Type === 1);
                    if (agenda) {
                        this.agendaFile.value = {
                            name: agenda.FileName,
                            size: agenda.FileSize || 0,
                            path: agenda.FilePath,
                            id: agenda.Id,
                            isExisting: true
                        };
                        console.log('✅ 議程表:', this.agendaFile.value);
                    }

                    const documents = data.Attachments.filter(a => a.Type === 2);
                    if (documents.length > 0) {
                        this.documentFiles.value = documents.map(doc => ({
                            name: doc.FileName,
                            size: doc.FileSize || 0,
                            path: doc.FilePath,
                            id: doc.Id,
                            isExisting: true
                        }));
                        console.log('✅ 會議文件:', this.documentFiles.value);
                    }
                }

                this.form.departmentId = data.DepartmentId;
                await this.loadBuildingsByDepartment();
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.building = data.Building;
                this.loadFloorsByBuilding(data.Building);
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.floor = data.Floor;
                await this.loadRoomsByFloor();
                await new Promise(resolve => setTimeout(resolve, 300));

                this.form.roomId = data.RoomId;
                this.selectedRoom.value = this.rooms.value.find(r => r.Id === data.RoomId) || null;

                await this.updateTimeSlots();
                await new Promise(resolve => setTimeout(resolve, 300));

                if (data.SlotKeys && Array.isArray(data.SlotKeys)) {
                    this.form.selectedSlots = data.SlotKeys;
                }

                await this.loadEquipmentByRoom();
                await new Promise(resolve => setTimeout(resolve, 300));

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
        this.submitBooking = async () => {
            console.log('🟢 submitBooking 開始執行');


            // ✅ 檢查是否已同意聲明書
            if (!this.hasReadAgreement.value) {
                addAlert('請先閱讀並同意使用聲明書', { type: 'warning' });
                return;
            }

            // ✅ 關閉確認彈窗
            this.showConfirmModal.value = false;

            const attachments = [];

            try {
                if (this.agendaFile.value) {
                    const base64 = await this.fileToBase64(this.agendaFile.value);
                    attachments.push({
                        type: 1,
                        fileName: this.agendaFile.value.name,
                        base64Data: base64
                    });
                }

                for (const file of this.documentFiles.value) {
                    const base64 = await this.fileToBase64(file);
                    attachments.push({
                        type: 2,
                        fileName: file.name,
                        base64Data: base64
                    });
                }
            } catch (err) {
                console.error('❌ 檔案轉換失敗:', err);
                addAlert('檔案處理失敗,請重試', { type: 'danger' });
                return;
            }

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
                attendeeIds: [this.initiatorId.value],
                attachments: attachments
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
                        ? '預約已更新,請等待管理者審核!'
                        : '預約已送出,請等待管理者審核!';

                    console.log('%c✅ 操作成功!', 'color: #00aa00; font-weight: bold; font-size: 14px;');
                    addAlert(successMsg, { type: 'success' });

                    setTimeout(() => {
                        window.location.href = '/reservationoverview';
                    }, 2000);
                })
                .catch(err => {
                    const errorMsg = this.isEditMode.value ? '更新預約失敗' : '新增預約失敗';
                    console.error('%c❌ 操作失敗!', 'color: #aa0000; font-weight: bold; font-size: 14px;');
                    addAlert(`${errorMsg}:${err.message || '未知錯誤'}`, { type: 'danger' });
                });
        };

        /* ====== Mounted ====== */
        onMounted(async () => {

            await this.loadCurrentUser();

            if (this.isAdmin.value) {
                this.loadDepartments();
            }

            await this.loadEquipmentByRoom();

            const params = new URLSearchParams(location.search);
            const editId = params.get('id');
            const presetRoomId = params.get('roomId');
            const presetBuilding = params.get('building');
            const presetFloor = params.get('floor');
            const presetDepartmentId = params.get('departmentId');

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

            if (editId) {
                console.log('📝 進入編輯模式');
                this.isEditMode.value = true;
                this.editingReservationId.value = editId;
                await this.loadReservationData(editId);
            } else if (presetRoomId && presetBuilding && presetFloor && presetDepartmentId) {
                this.form.departmentId = presetDepartmentId;
                await this.loadBuildingsByDepartment();
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
            } else if (!this.isAdmin.value && this.form.departmentId) {
                await this.loadBuildingsByDepartment(this.form.departmentId);
            }

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

                    this.loadBuildingsByDepartment();
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
                    if (this.form.roomId) {
                        this.loadEquipmentByRoom();
                    }
                }
            );

            watch(
                () => [...this.form.selectedSlots],
                (newSlots, oldSlots) => {
                    if (this.form.roomId && this.form.date && newSlots.length > 0) {
                        console.log('🔄 時段變更,重新檢查設備可用性');

                        this.form.selectedEquipment = [];
                        this.form.selectedBooths = [];

                        this.loadEquipmentByRoom();
                    }
                },
                { deep: true }
            );

            window.addEventListener('message', (event) => {
                if (event.data?.type === 'PDF_REACHED_BOTTOM') {
                    this.canConfirmAgreement.value = true;
                    console.log('✅ PDF 已滑到底');
                }
            });
        });
    }
};