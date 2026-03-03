// Conference Create/Edit Page
import global from '/global.js';
const { ref, reactive, computed, onMounted, watch } = Vue;

window.$config = {
    setup: () => new function () {

        this.costCenters = ref([]);                    // 所有成本中心
        this.costCenterSearch = ref('');               // 搜尋關鍵字
        this.filteredCostCenters = ref([]);            // 過濾後的結果
        this.showCostCenterDropdown = ref(false);      // 是否顯示下拉選單
        this.selectedCostCenter = ref(null);           // 已選擇的成本中心


        // 載入成本中心列表
        this.loadCostCenters = async () => {
            try {
                const res = await global.api.select.costcenters();
                this.costCenters.value = (res.data || []).map(c => ({
                    code: c.Code,
                    name: c.Name
                }));
            } catch (err) {
                console.error('❌ 載入成本中心失敗:', err);
                addAlert('載入成本中心失敗', { type: 'danger' });
            }
        };

        this.validateCostCenter = (code) => {
            const exists = this.costCenters.value.some(c => c.code === code);
            if (!exists) {
                addAlert('無效的成本中心代碼', { type: 'warning' });
                return false;
            }
            return true;
        };

        // 過濾成本中心
        this.filterCostCenters = () => {
            const keyword = this.costCenterSearch.value.toLowerCase().trim();

            if (!keyword) {
                this.filteredCostCenters.value = this.costCenters.value; // ✅ 顯示全部
                return;
            }

            this.filteredCostCenters.value = this.costCenters.value.filter(center => {
                const codeMatch = center.code.toLowerCase().includes(keyword);
                const nameMatch = center.name.toLowerCase().includes(keyword);
                return codeMatch || nameMatch;
            }); // 限制最多顯示20筆
        };

        // 選擇成本中心
        this.selectCostCenter = (center) => {
            this.selectedCostCenter.value = center;
            this.form.departmentCode = center.code;
            this.costCenterSearch.value = `${center.code} - ${center.name}`;
            this.showCostCenterDropdown.value = false;
        };

        this.minAdvanceBookingDays = ref(7);

        // 國定假日資料 (格式: { "2025-01-01": true, "2025-02-08": false } - true=假日, false=補班日)
        this.holidayDates = ref({});

        // 載入國定假日資料
        this.loadHolidays = async () => {
            const currentYear = new Date().getFullYear();
            const years = [currentYear, currentYear + 1];

            for (const year of years) {
                try {
                    const res = await global.api.holiday.list(year);
                    if (res.data) {
                        res.data.forEach(h => {
                            if (h.IsEnabled) {
                                // IsWorkday=true 表示補班日(用平日價), IsWorkday=false 表示假日(用假日價)
                                this.holidayDates.value[h.Date] = !h.IsWorkday;
                            }
                        });
                    }
                } catch (err) {
                    console.warn(`載入 ${year} 年假日資料失敗:`, err);
                }
            }
            console.log('✅ 已載入國定假日:', Object.keys(this.holidayDates.value).length, '筆');
        };

        // 檢查日期是否為假日 (包含國定假日和週六日)
        this.checkIsHoliday = (dateStr) => {
            // 1. 先檢查國定假日/補班日
            if (this.holidayDates.value.hasOwnProperty(dateStr)) {
                return this.holidayDates.value[dateStr]; // true=假日價, false=平日價(補班日)
            }
            // 2. 沒有特別設定，判斷週六日
            const date = new Date(dateStr);
            const dayOfWeek = date.getDay();
            return dayOfWeek === 0 || dayOfWeek === 6;
        };

        // 最早可預約日期 computed
        this.minBookingDate = computed(() => {
            const today = new Date();
            today.setDate(today.getDate() + this.minAdvanceBookingDays.value);
            return today.toISOString().split('T')[0];
        });

        // 判斷選擇的日期是否為假日 - 單日用
        this.isHoliday = computed(() => {
            if (!this.form.startDate) return false;
            return this.checkIsHoliday(this.form.startDate);
        });

        // 計算日期範圍（統一使用 startDate/endDate）
        this.dateRange = computed(() => {
            if (!this.form.startDate) return [];

            // 如果沒有 endDate，預設與 startDate 相同（單日）
            const endDate = this.form.endDate || this.form.startDate;

            const dates = [];
            let current = new Date(this.form.startDate);
            const end = new Date(endDate);

            while (current <= end) {
                dates.push(current.toISOString().split('T')[0]);
                current.setDate(current.getDate() + 1);
            }
            return dates;
        });

        // 判斷是否為中間天（需要自動全選）
        this.isMiddleDay = (dateStr) => {
            const dates = this.dateRange.value;
            if (dates.length <= 2) return false;
            const idx = dates.indexOf(dateStr);
            return idx > 0 && idx < dates.length - 1;
        };

        // 判斷是否為首日
        this.isFirstDay = (dateStr) => {
            const dates = this.dateRange.value;
            return dates.length > 0 && dates[0] === dateStr;
        };

        // 判斷是否為末日
        this.isLastDay = (dateStr) => {
            const dates = this.dateRange.value;
            return dates.length > 0 && dates[dates.length - 1] === dateStr;
        };

        // 取得某日期的第一個時段
        this.getFirstSlotForDate = (dateStr) => {
            const slots = this.slotsByDate[dateStr] || [];
            if (slots.length === 0) return null;
            return slots.reduce((min, s) => s.StartTime < min.StartTime ? s : min, slots[0]);
        };

        // 取得某日期的最後一個時段
        this.getLastSlotForDate = (dateStr) => {
            const slots = this.slotsByDate[dateStr] || [];
            if (slots.length === 0) return null;
            return slots.reduce((max, s) => s.EndTime > max.EndTime ? s : max, slots[0]);
        };

        // 判斷某日期是否為假日 (跨日模式用)
        this.isHolidayForDate = (dateStr) => {
            return this.checkIsHoliday(dateStr);
        };

        // 格式化 Tab 日期顯示
        this.formatTabDate = (dateStr) => {
            const date = new Date(dateStr);
            const weekdays = ['日', '一', '二', '三', '四', '五', '六'];
            const month = date.getMonth() + 1;
            const day = date.getDate();
            const weekday = weekdays[date.getDay()];
            return `${month}/${day} (${weekday})`;
        };

        // 取得某日期的已選時段數量
        this.getSelectedCountForDate = (dateStr) => {
            const slots = this.selectedSlotsByDate[dateStr];
            return slots ? slots.length : 0;
        };

        // 取得時段的顯示價格（根據平日/假日）
        this.getSlotPrice = (slot) => {
            if (this.isHoliday.value && slot.HolidayPrice) {
                return slot.HolidayPrice;
            }
            return slot.Price;
        };

        this.currentUser = ref(null);
        this.isAdmin = ref(false);
        this.isInternalStaff = ref(false);

        this.showAgreementPDF = ref(false);          // 控制 PDF 彈窗顯示
        this.hasReadAgreement = ref(false);          // 是否已勾選同意
        this.agreementPdfUrl = ref('');
        this.canConfirmAgreement = ref(false);
        this.pdfCacheBuster = ref(Date.now());
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
            organizerUnit: '',
            chairman: '',
            expectedAttendees: null,  // ✅ 預計到達人數
            startDate: '',  // 開始日期
            endDate: '',    // 結束日期（單日時與 startDate 相同）
            meetingType: 'physical',
            departmentId: null,
            building: '',
            floor: '',
            roomId: null,
            initiatorId: '',
            attendees: [],
            selectedSlots: [],  // 新格式: [{ key: '09:00:00-10:00:00', isSetup: false }]
            selectedEquipment: [],
            selectedBooths: [],
            paymentMethod: '',
            departmentCode: '',
            attachments: [],
            parkingTicketPurchase: 0  // 停車券加購張數
        });

        // ========== 跨日預約相關 ==========
        // isMultiDay 改為 computed：開始日期 ≠ 結束日期 即為跨日
        this.isMultiDay = computed(() => {
            if (!this.form.startDate || !this.form.endDate) return false;
            return this.form.startDate !== this.form.endDate;
        });
        this.activeTab = ref('');      // 當前選中的日期 Tab
        this.slotsByDate = reactive({});        // 每天的時段資料 { '2025-01-20': [...], ... }
        this.selectedSlotsByDate = reactive({}); // 每天的已選時段 { '2025-01-20': [{key, isSetup}], ... }
        this.loadingSlots = reactive({});        // 每天的載入狀態
        this.middleDayErrors = reactive({});     // 中間天錯誤訊息

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
            console.log('🔴 關閉 PDF 彈窗');
            this.showAgreementPDF.value = false;
            this.canConfirmAgreement.value = false;
        };


        /* ====== PDF 載入完成 ====== */
        this.onPDFLoaded = () => {
            console.log('✅ PDF 載入完成');
        };

        this.onPdfIframeLoaded = () => {
            console.log('📄 PDF iframe 載入完成');

            // ✅ 延遲發送重置訊息,確保 PDF.js 初始化完成
            setTimeout(() => {
                const iframe = this.$refs.pdfIframe;
                if (iframe && iframe.contentWindow) {
                    iframe.contentWindow.postMessage({ type: 'RESET_SCROLL' }, '*');
                    console.log('📨 已發送 RESET_SCROLL 訊息');
                }
            }, 800);
        };



        this.confirmReadAgreement = () => {
            if (!this.hasOpenedAgreement.value) {
                console.warn('⚠️ 尚未開啟過 PDF');
                return;
            }

            console.log('✅ 確認已閱讀');
            this.hasReadAgreement.value = true;
            this.showAgreementPDF.value = false;
            addAlert('已確認閱讀使用聲明書', { type: 'success' });
        };

        this.pdfViewerUrl = computed(() => {
            const agreementPath = this.selectedRoom.value?.AgreementPath || '/files/agreement.pdf';
            return `/pdfjs/web/viewer.html?file=${encodeURIComponent(agreementPath)}&t=${this.pdfCacheBuster.value}`;
        });




        this.openAgreementPDF = () => {
            // 重置狀態
            this.canConfirmAgreement.value = false;
            this.hasOpenedAgreement.value = true;
            this.showAgreementPDF.value = false;

            // 清除 pdfjs 記住的捲動位置
            try { localStorage.removeItem('pdfjs.history'); } catch (e) {}

            this.pdfCacheBuster.value = Date.now();

            setTimeout(() => {
                this.showAgreementPDF.value = true;
            }, 50);
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
                this.isInternalStaff.value = this.currentUser.value.IsInternal || false;

                if (!this.isAdmin.value && this.currentUser.value.DepartmentId) {
                    this.form.departmentId = this.currentUser.value.DepartmentId;
                }

                console.log('✅ 使用者資訊載入完成:', {
                    name: this.currentUser.value.Name,
                    isAdmin: this.isAdmin.value,
                    isInternalStaff: this.isInternalStaff.value,
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
            // ✅ 檢查是否為免費會議室
            const room = this.selectedRoom.value;

            if (room && room.BookingSettings === 3) {  // BookingSettings.Free = 3
                return 0;
            }

            // ✅ 統一使用 selectedSlotsByDate 和 slotsByDate 計算（單日和跨日都適用）
            let totalCost = 0;
            for (const dateStr of this.dateRange.value) {
                const slots = this.selectedSlotsByDate[dateStr] || [];
                const dateSlots = this.slotsByDate[dateStr] || [];

                for (const slotInfo of slots) {
                    const slot = dateSlots.find(s => s.Key === slotInfo.key);
                    if (!slot) continue;

                    let price;
                    if (slotInfo.isSetup && slot.SetupPrice != null) {
                        price = slot.SetupPrice;
                    } else if (this.isHolidayForDate(dateStr) && slot.HolidayPrice) {
                        price = slot.HolidayPrice;
                    } else {
                        price = slot.Price;
                    }
                    totalCost += price;
                }
            }
            return totalCost;
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

        // 小計（會議室 + 設備 + 攤位，用於計算停車券贈送）
        this.subtotal = computed(() => {
            return this.roomCost.value + this.equipmentCost.value + this.boothCost.value;
        });

        // 檢查是否有任何已選時段（用於設備/攤位區塊顯示）
        this.hasAnySelectedSlots = computed(() => {
            for (const dateStr of this.dateRange.value) {
                const slots = this.selectedSlotsByDate[dateStr] || [];
                if (slots.length > 0) return true;
            }
            return false;
        });

        // ✅ 停車券單價（從會議室設定取得）
        this.parkingTicketUnitPrice = computed(() => {
            return this.selectedRoom.value?.ParkingTicketPrice || 100;
        });

        // 停車券贈送張數（每滿 5 萬送 30 張，僅啟用停車券時有效）
        this.freeTicketCount = computed(() => {
            if (!this.selectedRoom.value?.EnableParkingTicket) return 0;
            return Math.floor(this.subtotal.value / 50000) * 30;
        });

        // 停車券加購費用（使用會議室設定的單價）
        this.parkingTicketCost = computed(() => {
            if (!this.selectedRoom.value?.EnableParkingTicket) return 0;
            return (this.form.parkingTicketPurchase || 0) * this.parkingTicketUnitPrice.value;
        });

        // 停車券總張數（贈送 + 加購）
        this.totalTicketCount = computed(() => {
            if (!this.selectedRoom.value?.EnableParkingTicket) return 0;
            return this.freeTicketCount.value + (this.form.parkingTicketPurchase || 0);
        });

        this.totalAmount = computed(() => {
            return this.subtotal.value + this.parkingTicketCost.value;
        });

        /* ====== ✅ 確認彈窗相關功能 ====== */
        this.showConfirmationModal = () => {
            // 驗證
            if (!this.form.name.trim()) {
                addAlert('請填寫會議名稱', { type: 'warning' });
                return;
            }

            // 日期驗證（統一處理單日/多日）
            if (!this.form.startDate) {
                addAlert('請選擇開始日期', { type: 'warning' });
                return;
            }
            if (this.form.startDate < this.minBookingDate.value) {
                addAlert(`開始日期必須在 ${this.minAdvanceBookingDays.value} 天後（最早可選 ${this.minBookingDate.value}）`, { type: 'warning' });
                return;
            }

            // 檢查是否有錯誤（中間天被佔用、首日末日限制等）
            for (const dateStr of this.dateRange.value) {
                if (this.middleDayErrors[dateStr]) {
                    addAlert(this.middleDayErrors[dateStr], { type: 'danger' });
                    return;
                }
            }

            // 檢查是否有選擇時段
            let hasAnySlots = false;
            for (const dateStr of this.dateRange.value) {
                const slots = this.selectedSlotsByDate[dateStr] || [];
                if (slots.length > 0) {
                    hasAnySlots = true;
                    break;
                }
            }
            if (!hasAnySlots) {
                addAlert('請至少選擇一個時段', { type: 'warning' });
                return;
            }

            // ✅ 跨日模式額外驗證（開始日期 ≠ 結束日期）
            if (this.isMultiDay.value) {
                // 首日驗證：必須選到最後一個時段
                const firstDate = this.dateRange.value[0];
                const firstDateSlots = this.slotsByDate[firstDate] || [];
                if (firstDateSlots.length > 0) {
                    const sortedFirstDay = [...firstDateSlots].sort((a, b) => a.StartTime.localeCompare(b.StartTime));
                    const lastSlotOfFirstDay = sortedFirstDay[sortedFirstDay.length - 1];
                    const firstDaySelected = this.selectedSlotsByDate[firstDate] || [];
                    const hasLastSlot = firstDaySelected.some(s => s.key === lastSlotOfFirstDay.Key);
                    if (!hasLastSlot) {
                        addAlert(`首日 ${this.formatTabDate(firstDate)} 必須選擇到最後時段，才能連續到隔天`, { type: 'warning' });
                        return;
                    }
                }

                // 末日驗證：必須從第一個時段開始選
                const lastDate = this.dateRange.value[this.dateRange.value.length - 1];
                const lastDateSlots = this.slotsByDate[lastDate] || [];
                if (lastDateSlots.length > 0) {
                    const sortedLastDay = [...lastDateSlots].sort((a, b) => a.StartTime.localeCompare(b.StartTime));
                    const firstSlotOfLastDay = sortedLastDay[0];
                    const lastDaySelected = this.selectedSlotsByDate[lastDate] || [];
                    const hasFirstSlot = lastDaySelected.some(s => s.key === firstSlotOfLastDay.Key);
                    if (!hasFirstSlot) {
                        addAlert(`末日 ${this.formatTabDate(lastDate)} 必須從第一時段開始選，才能與前一天連續`, { type: 'warning' });
                        return;
                    }
                }
            }

            if (!this.form.roomId) {
                addAlert('請選擇會議室', { type: 'warning' });
                return;
            }
            if (!this.form.paymentMethod) {
                addAlert('請選擇付款方式', { type: 'warning' });
                return;
            }

            // ✅ 新增:如果選擇成本分攤,驗證成本中心代碼
            if (this.form.paymentMethod === 'cost-sharing') {
                if (!this.form.departmentCode) {
                    addAlert('請選擇成本中心代碼', { type: 'warning' });
                    return;
                }
                if (!this.validateCostCenter(this.form.departmentCode)) {
                    return;
                }
            }


            // ✅ 每次開啟確認彈窗，都重置所有聲明書狀態
            this.hasReadAgreement.value = false;
            this.hasOpenedAgreement.value = false;
            this.canConfirmAgreement.value = false;
            this.showAgreementPDF.value = false;
            this.pdfCacheBuster.value = Date.now();

            // 驗證通過,顯示彈窗
            this.showConfirmModal.value = true;
        };

        this.closeConfirmationModal = () => {
            this.showConfirmModal.value = false;
            this.showAgreementPDF.value = false;

            // ✅ 關閉時也重置所有聲明書狀態
            this.hasReadAgreement.value = false;
            this.hasOpenedAgreement.value = false;
            this.canConfirmAgreement.value = false;
            this.pdfCacheBuster.value = Date.now();
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

        // ✅ 取得選擇的時段文字（單日模式使用，統一使用 selectedSlotsByDate）
        this.getSelectedSlotsText = () => {
            if (!this.form.startDate) return '無';
            // 單日模式直接使用 getSelectedSlotsTextForDate
            return this.getSelectedSlotsTextForDate(this.form.startDate);
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
            const payload = {};

            // ✅ 如果有選擇分院,傳給後端
            if (this.form.departmentId) {
                payload.departmentId = this.form.departmentId;
            }

            console.log('📤 [ConferenceCreate - loadBuildingsByDepartment] payload:', payload);

            global.api.select.buildingsbydepartment({ body: payload })
                .then(res => {
                    console.log('✅ 大樓列表:', res.data);
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
            // 收集所有選擇的時段
            let allSlotKeys = [];
            for (const dateStr of this.dateRange.value) {
                const slots = this.selectedSlotsByDate[dateStr] || [];
                allSlotKeys = allSlotKeys.concat(slots.map(s => s.key));
            }

            if (!this.form.roomId || !this.form.startDate || allSlotKeys.length === 0) {
                console.warn('⏸ 條件不足,無法載入設備');
                return;
            }

            try {
                const body = {
                    roomId: this.form.roomId,
                    date: this.form.startDate,
                    slotKeys: allSlotKeys,
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
                        occupied: e.Occupied || false,
                        image: e.ImagePath || null
                    }));

                this.availableBooths.value = allData
                    .filter(e => e.TypeName === '攤位租借')
                    .map(e => ({
                        id: e.Id,
                        name: e.Name,
                        icon: 'bx-store',
                        description: e.ProductModel || '攤位',
                        price: e.RentalPrice,
                        occupied: e.Occupied || false,
                        image: e.ImagePath || null
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

        /* ====== 跨日模式：載入某天的時段 ====== */
        this.loadSlotsForDate = async (dateStr) => {
            if (!this.form.roomId) return;

            this.loadingSlots[dateStr] = true;
            delete this.middleDayErrors[dateStr];

            try {
                const res = await global.api.select.roomslots({
                    body: {
                        roomId: this.form.roomId,
                        date: dateStr,
                        excludeConferenceId: this.isEditMode.value ? this.editingReservationId.value : null
                    }
                });

                const slots = res.data || [];
                this.slotsByDate[dateStr] = slots;

                // 排序時段（依開始時間）
                slots.sort((a, b) => a.StartTime.localeCompare(b.StartTime));

                // 初始化已選時段
                if (!this.selectedSlotsByDate[dateStr]) {
                    this.selectedSlotsByDate[dateStr] = [];
                }

                // 跨日模式才需要檢查限制
                if (this.isMultiDay.value) {
                    // 中間天檢查：必須整天空閒，自動全選
                    if (this.isMiddleDay(dateStr)) {
                        const hasOccupied = slots.some(s => s.Occupied);
                        if (hasOccupied) {
                            this.middleDayErrors[dateStr] = `${this.formatTabDate(dateStr)} 已有其他預約，無法進行跨日預約`;
                            this.selectedSlotsByDate[dateStr] = [];
                        } else {
                            // 自動全選，預設為一般使用
                            this.selectedSlotsByDate[dateStr] = slots.map(s => ({
                                key: s.Key,
                                isSetup: false
                            }));
                        }
                    }
                    // 首日檢查：最後一個時段必須可用
                    else if (this.isFirstDay(dateStr)) {
                        const lastSlot = slots.length > 0 ? slots[slots.length - 1] : null;
                        if (lastSlot && lastSlot.Occupied) {
                            this.middleDayErrors[dateStr] = `${this.formatTabDate(dateStr)} 最後時段已被預約，無法作為跨日首日`;
                            this.selectedSlotsByDate[dateStr] = [];
                        }
                    }
                    // 末日檢查：第一個時段必須可用
                    else if (this.isLastDay(dateStr)) {
                        const firstSlot = slots.length > 0 ? slots[0] : null;
                        if (firstSlot && firstSlot.Occupied) {
                            this.middleDayErrors[dateStr] = `${this.formatTabDate(dateStr)} 第一時段已被預約，無法作為跨日末日`;
                            this.selectedSlotsByDate[dateStr] = [];
                        }
                    }
                }
                // 單日模式：無特殊限制

            } catch (err) {
                console.error(`❌ 載入 ${dateStr} 時段失敗:`, err);
                this.middleDayErrors[dateStr] = '載入時段失敗，請重試';
            } finally {
                this.loadingSlots[dateStr] = false;
            }
        };

        /* ====== 跨日模式：載入所有日期的時段 ====== */
        this.loadAllDateSlots = async () => {
            if (!this.form.roomId) return;

            // 清空舊資料
            Object.keys(this.slotsByDate).forEach(key => delete this.slotsByDate[key]);
            Object.keys(this.selectedSlotsByDate).forEach(key => delete this.selectedSlotsByDate[key]);
            Object.keys(this.loadingSlots).forEach(key => delete this.loadingSlots[key]);
            Object.keys(this.middleDayErrors).forEach(key => delete this.middleDayErrors[key]);

            // 設定第一個日期為 active tab
            if (this.dateRange.value.length > 0) {
                this.activeTab.value = this.dateRange.value[0];
            }

            // 載入所有日期的時段
            for (const dateStr of this.dateRange.value) {
                await this.loadSlotsForDate(dateStr);
            }
        };

        /* ====== 跨日模式：檢查時段是否已選（某日期） ====== */
        this.isSlotSelectedForDate = (dateStr, slot) => {
            const slots = this.selectedSlotsByDate[dateStr];
            return slots ? slots.some(s => s.key === slot.Key) : false;
        };

        /* ====== 跨日模式：檢查時段是否為場佈（某日期） ====== */
        this.isSlotSetupForDate = (dateStr, slot) => {
            const slots = this.selectedSlotsByDate[dateStr];
            if (!slots) return false;
            const found = slots.find(s => s.key === slot.Key);
            return found ? found.isSetup : false;
        };

        /* ====== 跨日模式：設定時段類型（某日期） ====== */
        this.setSlotTypeForDate = (dateStr, slot, isSetup) => {
            const slots = this.selectedSlotsByDate[dateStr];
            if (!slots) return;
            const found = slots.find(s => s.key === slot.Key);
            if (found) {
                found.isSetup = isSetup;
            }
        };

        /* ====== 跨日模式：取得時段顯示價格（某日期） ====== */
        this.getSlotDisplayPriceForDate = (dateStr, slot) => {
            const isSetup = this.isSlotSetupForDate(dateStr, slot);
            if (isSetup && slot.SetupPrice != null) {
                return slot.SetupPrice;
            }
            if (this.isHolidayForDate(dateStr) && slot.HolidayPrice) {
                return slot.HolidayPrice;
            }
            return slot.Price;
        };

        /* ====== 檢查時段是否可選（某日期） ====== */
        this.isSlotAvailableForDate = (dateStr, slot) => {
            if (slot.Occupied) return false;

            // 中間天不允許操作（已全選）- 只在跨日模式有意義
            if (this.isMultiDay.value && this.isMiddleDay(dateStr)) {
                return false;
            }

            const dateSlots = this.slotsByDate[dateStr] || [];
            if (dateSlots.length === 0) return false;

            // 排序後的時段
            const sortedSlots = [...dateSlots].sort((a, b) => a.StartTime.localeCompare(b.StartTime));
            const firstSlot = sortedSlots[0];
            const lastSlot = sortedSlots[sortedSlots.length - 1];

            const selectedSlotsList = this.selectedSlotsByDate[dateStr] || [];

            // 已選中的可以操作（取消選擇）
            if (this.isSlotSelectedForDate(dateStr, slot)) return true;

            // 跨日模式：首日只能從尾巴開始選（必須包含最後時段）
            if (this.isMultiDay.value && this.isFirstDay(dateStr) && !this.isLastDay(dateStr)) {
                if (selectedSlotsList.length === 0) {
                    return slot.Key === lastSlot.Key;
                }
                const selectedKeys = selectedSlotsList.map(s => s.key);
                const selectedSlotsData = sortedSlots.filter(s => selectedKeys.includes(s.Key));
                if (selectedSlotsData.length === 0) return slot.Key === lastSlot.Key;

                const minStart = selectedSlotsData.reduce((min, s) => s.StartTime < min ? s.StartTime : min, selectedSlotsData[0].StartTime);
                return slot.EndTime === minStart;
            }

            // 跨日模式：末日只能從頭開始選（必須包含第一時段）
            if (this.isMultiDay.value && this.isLastDay(dateStr) && !this.isFirstDay(dateStr)) {
                if (selectedSlotsList.length === 0) {
                    return slot.Key === firstSlot.Key;
                }
                const selectedKeys = selectedSlotsList.map(s => s.key);
                const selectedSlotsData = sortedSlots.filter(s => selectedKeys.includes(s.Key));
                if (selectedSlotsData.length === 0) return slot.Key === firstSlot.Key;

                const maxEnd = selectedSlotsData.reduce((max, s) => s.EndTime > max ? s.EndTime : max, selectedSlotsData[0].EndTime);
                return slot.StartTime === maxEnd;
            }

            // 單日模式 或 其他情況：使用原本的相鄰邏輯
            if (selectedSlotsList.length === 0) return true;

            const selectedKeys = selectedSlotsList.map(s => s.key);
            const selectedSlotsData = sortedSlots.filter(s => selectedKeys.includes(s.Key));
            if (selectedSlotsData.length === 0) return true;

            const minStart = selectedSlotsData.reduce((min, s) => s.StartTime < min ? s.StartTime : min, selectedSlotsData[0].StartTime);
            const maxEnd = selectedSlotsData.reduce((max, s) => s.EndTime > max ? s.EndTime : max, selectedSlotsData[0].EndTime);

            return slot.EndTime === minStart || slot.StartTime === maxEnd;
        };

        /* ====== 檢查時段是否在邊緣（某日期） ====== */
        this.isSlotAtEdgeForDate = (dateStr, slot) => {
            if (!this.isSlotSelectedForDate(dateStr, slot)) return false;

            const selectedSlotsList = this.selectedSlotsByDate[dateStr] || [];
            const dateSlots = this.slotsByDate[dateStr] || [];

            // 排序後的時段
            const sortedSlots = [...dateSlots].sort((a, b) => a.StartTime.localeCompare(b.StartTime));
            const firstSlotOfDay = sortedSlots[0];
            const lastSlotOfDay = sortedSlots[sortedSlots.length - 1];

            const selectedKeys = selectedSlotsList.map(s => s.key);
            const selectedSlotsData = sortedSlots.filter(s => selectedKeys.includes(s.Key));

            if (selectedSlotsData.length === 0) return false;

            const minStart = selectedSlotsData.reduce((min, s) => s.StartTime < min ? s.StartTime : min, selectedSlotsData[0].StartTime);
            const maxEnd = selectedSlotsData.reduce((max, s) => s.EndTime > max ? s.EndTime : max, selectedSlotsData[0].EndTime);

            // 跨日模式：首日只能從前面取消（不能取消最後時段）
            if (this.isMultiDay.value && this.isFirstDay(dateStr) && !this.isLastDay(dateStr)) {
                if (selectedSlotsData.length === 1) {
                    // 只選了一個，最後時段不能取消
                    if (slot.Key === lastSlotOfDay.Key) return false;
                    return true;
                }
                if (slot.Key === lastSlotOfDay.Key) return false;
                return slot.StartTime === minStart;
            }

            // 跨日模式：末日只能從後面取消（不能取消第一時段）
            if (this.isMultiDay.value && this.isLastDay(dateStr) && !this.isFirstDay(dateStr)) {
                if (selectedSlotsData.length === 1) {
                    // 只選了一個，第一時段不能取消
                    if (slot.Key === firstSlotOfDay.Key) return false;
                    return true;
                }
                if (slot.Key === firstSlotOfDay.Key) return false;
                return slot.EndTime === maxEnd;
            }

            // 單日模式：只有一個可直接取消，多個則兩邊都可取消
            if (selectedSlotsData.length === 1) return true;
            return slot.StartTime === minStart || slot.EndTime === maxEnd;
        };

        /* ====== 切換時段選擇（某日期） ====== */
        this.toggleTimeSlotForDate = (dateStr, slot) => {
            if (slot.Occupied) return;

            // 跨日模式：中間天不允許操作（已全選）
            if (this.isMultiDay.value && this.isMiddleDay(dateStr)) return;

            if (!this.selectedSlotsByDate[dateStr]) {
                this.selectedSlotsByDate[dateStr] = [];
            }

            const isSelected = this.isSlotSelectedForDate(dateStr, slot);

            if (isSelected) {
                if (this.isSlotAtEdgeForDate(dateStr, slot)) {
                    const idx = this.selectedSlotsByDate[dateStr].findIndex(s => s.key === slot.Key);
                    if (idx > -1) {
                        this.selectedSlotsByDate[dateStr].splice(idx, 1);
                    }
                }
            } else {
                if (this.isSlotAvailableForDate(dateStr, slot)) {
                    this.selectedSlotsByDate[dateStr].push({ key: slot.Key, isSetup: false });
                }
            }
        };

        /* ====== 跨日模式：取得某日期已選時段的文字描述 ====== */
        this.getSelectedSlotsTextForDate = (dateStr) => {
            const slots = this.selectedSlotsByDate[dateStr] || [];
            if (slots.length === 0) return '未選擇';

            const dateSlots = this.slotsByDate[dateStr] || [];
            const selectedSlots = dateSlots.filter(s => slots.some(sel => sel.key === s.Key));

            selectedSlots.sort((a, b) => a.StartTime.localeCompare(b.StartTime));

            return selectedSlots.map(slot => {
                const startTime = slot.StartTime.slice(0, 5);
                const endTime = slot.EndTime.slice(0, 5);
                const slotInfo = slots.find(s => s.key === slot.Key);
                const suffix = slotInfo?.isSetup ? ' [場佈]' : '';
                return `${startTime}-${endTime}${suffix}`;
            }).join(', ');
        };

        /* ====== 載入時段（統一使用 loadAllDateSlots） ====== */
        this.updateTimeSlots = async () => {
            if (!this.form.roomId || !this.form.startDate) {
                console.warn('⏸ 條件不足');
                return;
            }
            this.selectedRoom.value = this.rooms.value.find(r => r.Id === this.form.roomId) || null;
            await this.loadAllDateSlots();

            // ✅ 單日模式：同步更新 timeSlots（供費用計算和顯示使用）
            if (!this.isMultiDay.value && this.form.startDate) {
                this.timeSlots.value = this.slotsByDate[this.form.startDate] || [];
                console.log('✅ 單日模式：同步 timeSlots', this.timeSlots.value.length, '個時段');
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
            return this.form.selectedSlots.some(s => s.key === slot.Key);
        };

        // 檢查時段是否為場佈模式
        this.isSlotSetup = (slot) => {
            const found = this.form.selectedSlots.find(s => s.key === slot.Key);
            return found ? found.isSetup : false;
        };

        // 設定時段類型（一般/場佈）
        this.setSlotType = (slot, isSetup) => {
            const found = this.form.selectedSlots.find(s => s.key === slot.Key);
            if (found) {
                found.isSetup = isSetup;
            }
        };

        // 取得時段顯示價格（根據平日/假日/場佈）
        this.getSlotDisplayPrice = (slot) => {
            const isSetup = this.isSlotSetup(slot);
            if (isSetup && slot.SetupPrice != null) {
                return slot.SetupPrice;
            }
            return this.getSlotPrice(slot);
        };

        // 取得已選時段的起始和結束邊界
        this.getSelectedRange = () => {
            if (this.form.selectedSlots.length === 0) {
                return null;
            }

            const selectedKeys = this.form.selectedSlots.map(s => s.key);
            const selectedSlots = this.timeSlots.value.filter(slot =>
                selectedKeys.includes(slot.Key)
            );

            let minStart = null;
            let maxEnd = null;

            for (const slot of selectedSlots) {
                if (minStart === null || slot.StartTime < minStart) {
                    minStart = slot.StartTime;
                }
                if (maxEnd === null || slot.EndTime > maxEnd) {
                    maxEnd = slot.EndTime;
                }
            }

            return { minStart, maxEnd };
        };

        // 檢查時段是否與已選範圍相鄰
        this.isSlotAdjacent = (slot) => {
            const range = this.getSelectedRange();
            if (!range) return true; // 沒有選擇，全部可選

            // 時段的結束時間 = 已選範圍的開始時間，或時段的開始時間 = 已選範圍的結束時間
            return slot.EndTime === range.minStart || slot.StartTime === range.maxEnd;
        };

        // 檢查時段是否可以被選擇
        this.isSlotAvailable = (slot) => {
            if (slot.Occupied) return false;
            if (this.form.selectedSlots.length === 0) return true;
            if (this.isSlotSelected(slot)) return true; // 已選的可以取消
            return this.isSlotAdjacent(slot);
        };

        // 檢查時段是否在已選範圍的邊緣（可以取消選擇）
        this.isSlotAtEdge = (slot) => {
            if (!this.isSlotSelected(slot)) return false;
            if (this.form.selectedSlots.length === 1) return true; // 只有一個，當然是邊緣

            const range = this.getSelectedRange();
            if (!range) return false;

            // 是開頭或結尾
            return slot.StartTime === range.minStart || slot.EndTime === range.maxEnd;
        };

        this.toggleTimeSlot = (slot) => {
            if (slot.Occupied) return;

            const isSelected = this.isSlotSelected(slot);

            if (isSelected) {
                // 取消選擇：只能取消邊緣的時段
                if (this.isSlotAtEdge(slot)) {
                    const idx = this.form.selectedSlots.findIndex(s => s.key === slot.Key);
                    if (idx > -1) {
                        this.form.selectedSlots.splice(idx, 1);
                    }
                }
                // 如果不是邊緣，不做任何事（或可以顯示提示）
            } else {
                // 新增選擇：只能選相鄰的時段
                if (this.isSlotAvailable(slot)) {
                    this.form.selectedSlots.push({ key: slot.Key, isSetup: false });
                }
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
            if (!this.selectedRoom.value || !this.hasAnySelectedSlots.value) {
                return { hours: 0, minutes: 0 };
            }

            // 收集所有日期的已選時段
            let allSelectedSlots = [];
            for (const dateStr of this.dateRange.value) {
                const selectedKeys = (this.selectedSlotsByDate[dateStr] || []).map(s => s.key);
                const dateSlots = this.slotsByDate[dateStr] || [];
                const selectedSlots = dateSlots.filter(slot => selectedKeys.includes(slot.Key));
                allSelectedSlots = allSelectedSlots.concat(selectedSlots.map(s => ({ ...s, date: dateStr })));
            }

            if (!allSelectedSlots.length) {
                return { hours: 0, minutes: 0 };
            }

            // 計算總時長（跨日需要加總每天的時長）
            let totalSeconds = 0;
            for (const dateStr of this.dateRange.value) {
                const selectedKeys = (this.selectedSlotsByDate[dateStr] || []).map(s => s.key);
                const dateSlots = this.slotsByDate[dateStr] || [];
                const selectedSlots = dateSlots.filter(slot => selectedKeys.includes(slot.Key));

                if (selectedSlots.length === 0) continue;

                selectedSlots.sort((a, b) => a.StartTime.localeCompare(b.StartTime));
                const firstSlot = selectedSlots[0];
                const lastSlot = selectedSlots[selectedSlots.length - 1];

                const startTime = this.parseTime(firstSlot.StartTime);
                const endTime = this.parseTime(lastSlot.EndTime);
                totalSeconds += (endTime - startTime);
            }

            const totalMinutes = totalSeconds / 60;
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
                this.form.expectedAttendees = data.ExpectedAttendees || null;  // ✅ 預計到達人數
                this.form.organizerUnit = data.OrganizerUnit || '';
                this.form.chairman = data.Chairman || '';
                // 設定日期（支援跨日）
                this.form.startDate = data.StartDate || data.ReservationDate || '';
                this.form.endDate = data.EndDate || data.ReservationDate || '';
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

                // 處理時段資料 - 支援新舊格式，同步到 selectedSlotsByDate
                if (data.SlotInfos && Array.isArray(data.SlotInfos)) {
                    // 新格式：包含 isSetup 和 date 資訊
                    // 按日期分組
                    const slotsByDateMap = {};
                    data.SlotInfos.forEach(s => {
                        const slotDate = s.Date || s.date || this.form.startDate;
                        if (!slotsByDateMap[slotDate]) {
                            slotsByDateMap[slotDate] = [];
                        }
                        slotsByDateMap[slotDate].push({
                            key: s.Key || s.key,
                            isSetup: s.IsSetup || s.isSetup || false
                        });
                    });
                    // 設定到 selectedSlotsByDate
                    for (const [dateStr, slots] of Object.entries(slotsByDateMap)) {
                        this.selectedSlotsByDate[dateStr] = slots;
                    }
                    console.log('✅ 已載入時段 (新格式):', slotsByDateMap);
                } else if (data.SlotKeys && Array.isArray(data.SlotKeys)) {
                    // 舊格式：只有 key，預設為一般使用，歸到 startDate
                    const slots = data.SlotKeys.map(key => ({
                        key: key,
                        isSetup: false
                    }));
                    this.selectedSlotsByDate[this.form.startDate] = slots;
                    console.log('✅ 已載入時段 (舊格式):', slots);
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
                }, 1000);
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

            // ✅ 組裝 SlotInfos（統一從 selectedSlotsByDate 取得）
            let slotInfos = [];
            let slotKeys = [];

            for (const [dateStr, slots] of Object.entries(this.selectedSlotsByDate)) {
                for (const slot of slots) {
                    slotInfos.push({
                        key: slot.key,
                        isSetup: slot.isSetup,
                        date: dateStr  // 包含日期資訊
                    });
                    slotKeys.push(slot.key);
                }
            }

            const payload = {
                name: this.form.name,
                description: this.form.content,
                expectedAttendees: this.form.expectedAttendees || null,  // ✅ 預計到達人數
                organizerUnit: this.form.organizerUnit,
                chairman: this.form.chairman,
                usageType: 1,
                durationHH: this.calculateDuration().hours,
                durationSS: this.calculateDuration().minutes,
                // 日期欄位（統一使用 startDate/endDate）
                reservationDate: this.form.startDate,
                startDate: this.form.startDate,
                endDate: this.form.endDate || this.form.startDate,
                paymentMethod: this.form.paymentMethod,
                departmentCode: this.form.paymentMethod === 'cost-sharing' ? this.form.departmentCode : null,
                roomCost: this.roomCost.value,
                equipmentCost: this.equipmentCost.value,
                boothCost: this.boothCost.value,
                parkingTicketCount: this.totalTicketCount.value,
                parkingTicketCost: this.parkingTicketCost.value,
                totalAmount: this.totalAmount.value,
                roomId: this.form.roomId,
                slotKeys: slotKeys,  // 保持向下相容
                slotInfos: slotInfos,  // 新格式（含日期）
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
                    }, 1000);
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
            await this.loadCostCenters();

            // 載入系統設定（最早預約天數）
            try {
                const configRes = await global.api.sysconfig.getall();
                if (configRes.data) {
                    this.minAdvanceBookingDays.value = parseInt(configRes.data.MIN_ADVANCE_BOOKING_DAYS) || 7;
                }
            } catch (err) {
                console.error('載入系統設定失敗:', err);
            }

            // 載入國定假日資料
            await this.loadHolidays();

            // 點擊外部關閉下拉選單
            document.addEventListener('click', (e) => {
                if (!e.target.closest('.position-relative')) {
                    this.showCostCenterDropdown.value = false;
                }
            });


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
            const presetDate = params.get('date');
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

                if (presetDate) {
                    this.form.startDate = presetDate;
                    this.form.endDate = presetDate;
                    console.log('✅ 自動帶入搜尋日期:', presetDate);
                }

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

            // 監聽 selectedSlotsByDate 的變化（統一處理單日和跨日）
            watch(
                () => JSON.stringify(this.selectedSlotsByDate),
                (newVal, oldVal) => {
                    if (this.form.roomId && this.form.startDate && this.hasAnySelectedSlots.value) {
                        console.log('🔄 時段變更,重新檢查設備可用性');

                        this.form.selectedEquipment = [];
                        this.form.selectedBooths = [];

                        this.loadEquipmentByRoom();
                    }
                }
            );

            // ✅ 日期範圍變更時，重新載入時段（統一處理單日/多日）
            watch(
                () => [this.form.startDate, this.form.endDate],
                async ([newStart, newEnd], [oldStart, oldEnd]) => {
                    if (!this.form.roomId || !newStart) return;

                    // 清空舊資料
                    Object.keys(this.slotsByDate).forEach(key => delete this.slotsByDate[key]);
                    Object.keys(this.selectedSlotsByDate).forEach(key => delete this.selectedSlotsByDate[key]);
                    Object.keys(this.loadingSlots).forEach(key => delete this.loadingSlots[key]);
                    Object.keys(this.middleDayErrors).forEach(key => delete this.middleDayErrors[key]);
                    this.form.selectedSlots = [];
                    this.timeSlots.value = [];

                    // 載入新日期範圍的時段
                    await this.loadAllDateSlots();

                    // ✅ 單日模式：同步更新 timeSlots
                    if (!this.isMultiDay.value && newStart) {
                        this.timeSlots.value = this.slotsByDate[newStart] || [];
                    }
                },
                { deep: true }
            );

            // ✅ 會議室變更時，重新載入時段
            watch(
                () => this.form.roomId,
                async (roomId) => {
                    if (roomId && this.form.startDate) {
                        this.selectedRoom.value = this.rooms.value.find(r => r.Id === roomId) || null;
                        // 清空舊資料
                        Object.keys(this.slotsByDate).forEach(key => delete this.slotsByDate[key]);
                        Object.keys(this.selectedSlotsByDate).forEach(key => delete this.selectedSlotsByDate[key]);
                        Object.keys(this.loadingSlots).forEach(key => delete this.loadingSlots[key]);
                        Object.keys(this.middleDayErrors).forEach(key => delete this.middleDayErrors[key]);
                        this.form.selectedSlots = [];
                        this.timeSlots.value = [];
                        await this.loadAllDateSlots();

                        // ✅ 單日模式：同步更新 timeSlots
                        if (!this.isMultiDay.value && this.form.startDate) {
                            this.timeSlots.value = this.slotsByDate[this.form.startDate] || [];
                        }
                    }
                }
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