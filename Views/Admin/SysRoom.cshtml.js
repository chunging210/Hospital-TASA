import global from '/global.js';

// 模擬舊版的 room 物件結構
const room = {
    // 查詢參數
    query: {
        keyword: '',
        pageIndex: 1,
        pageSize: 6
    },
    list: [],
    total: 0,
    hasNextPage: false,
    offcanvas: null,
    editModal: null,
    detailModal: null,

    // 取得會議室列表
    getList: function () {
        console.log('getList 執行中...');
        global.api.admin.roomlist({ body: this.query })
            .then((response) => {
                console.log('API 回傳:', response);
                this.list = response.data?.items || response.data || [];
                this.total = response.data?.total || this.list.length;
                this.hasNextPage = (this.query.pageIndex * this.query.pageSize) < this.total;

                // 更新分頁UI
                document.getElementById('totalCount').textContent = this.total;
                document.getElementById('pageIndex').textContent = this.query.pageIndex;
                document.getElementById('prevPageBtn').classList.toggle('disabled', this.query.pageIndex <= 1);
                document.getElementById('nextPageBtn').classList.toggle('disabled', !this.hasNextPage);

                this.renderRoomGrid();
            })
            .catch(error => {
                console.error('getList 錯誤:', error);
                alert('取得資料失敗');
            });
    },

    // 搜尋
    search: function () {
        this.query.keyword = document.getElementById('searchKeyword').value;
        this.query.pageIndex = 1;
        this.getList();
    },

    // 前一頁
    previousPage: function () {
        if (this.query.pageIndex > 1) {
            this.query.pageIndex--;
            this.getList();
        }
    },

    // 下一頁
    nextPage: function () {
        if (this.hasNextPage) {
            this.query.pageIndex++;
            this.getList();
        }
    },

    // ===== 渲染卡片網格 =====
    renderRoomGrid: function () {
        const roomGrid = document.getElementById('room-grid');
        roomGrid.innerHTML = '';

        if (this.list.length === 0) {
            roomGrid.innerHTML = '<div style="grid-column: 1/-1; text-align: center; color: #999;">查無資料</div>';
            return;
        }

        this.list.forEach(roomItem => {
            const card = document.createElement('div');
            card.className = 'room-card';

            const statusClass = roomItem.Status === 'available' ? 'status-available' :
                roomItem.Status === 'occupied' ? 'status-occupied' : 'status-maintenance';
            const statusText = roomItem.Status === 'available' ? '可預約' :
                roomItem.Status === 'occupied' ? '使用中' : '維修中';

            card.innerHTML = `
                <div class="room-card-header">
                    <div class="room-name">${roomItem.Name}</div>
                    <div class="room-info">${roomItem.Building} ${roomItem.Floor}樓 | ${roomItem.Number}</div>
                </div>
                <div class="room-card-body">
                    <div class="room-preview" style="${roomItem.Image ? `background-image: url('${roomItem.Image}')` : ''}"></div>
                    <div class="room-details">
                        <div class="detail-row">
                            <span class="detail-label">容量</span>
                            <span class="detail-value">${roomItem.Capacity} 人</span>
                        </div>
                        <div class="detail-row">
                            <span class="detail-label">面積</span>
                            <span class="detail-value">${roomItem.Area} ㎡</span>
                        </div>
                        <div class="detail-row">
                            <span class="detail-label">使用狀態</span>
                            <span class="status-badge ${statusClass}">${statusText}</span>
                        </div>
                        <div class="detail-row">
                            <span class="detail-label">設備數量</span>
                            <span class="detail-value">${roomItem.EquipmentCount || 0} 項設備</span>
                        </div>
                        <div class="detail-row">
                            <span class="detail-label">異常設備</span>
                            <span class="detail-value ${roomItem.ErrorCount > 0 ? 'text-danger' : 'text-success'}">
                                ${roomItem.ErrorCount || 0} 項
                            </span>
                        </div>
                    </div>
                    <div class="action-buttons">
                        <button class="btn btn-view" onclick="room.viewRoomDetail('${roomItem.Id}')">
                            <i class="mdi mdi-eye"></i> 檢視
                        </button>
                        <button class="btn btn-edit" onclick="room.editRoom('${roomItem.Id}')">
                            <i class="mdi mdi-pencil"></i> 編輯
                        </button>
                        <button class="btn btn-danger" onclick="room.deleteRoom('${roomItem.Id}')">
                            <i class="mdi mdi-trash-can"></i> 刪除
                        </button>
                    </div>
                </div>
            `;

            roomGrid.appendChild(card);
        });
    },

    // ===== 新增/編輯會議室 =====
    getVM: function (id) {
        if (id) {
            // 編輯模式
            global.api.admin.roomdetail({ body: { id } })
                .then((response) => {
                    this.populateEditForm(response.data);
                    this.editModal.show();
                })
                .catch(error => {
                    console.error('取得資料失敗:', error);
                    alert('取得資料失敗');
                });
        } else {
            // 新增模式
            this.clearCreateForm();
            this.toggleFeeOptions();
            this.offcanvas.show();
        }
    },

    // 清空新增表單
    clearCreateForm: function () {
        document.getElementById('createRoomForm').reset();
        document.getElementById('create-capacity').value = '10';
        document.getElementById('create-area').value = '20';
        document.querySelector('input[name="feeType"][value="hourly"]').checked = true;
        this.generateHourlySlots();
        this.clearTimeSlotContainer();
    },

    // 填入編輯表單
    populateEditForm: function (data) {
        document.getElementById('roomEditForm').dataset.roomId = data.Id;
        document.getElementById('edit-name').value = data.Name || '';
        document.getElementById('edit-building').value = data.Building || '';
        document.getElementById('edit-floor').value = data.Floor || '';
        document.getElementById('edit-room-number').value = data.Number || '';
        document.getElementById('edit-status').value = data.Status || 'available';
        document.getElementById('edit-capacity').value = data.Capacity || '';
        document.getElementById('edit-area').value = data.Area || '';
        document.getElementById('edit-room-description').value = data.Description || '';

        // 設定收費方式
        const pricingType = data.PricingType || 'hourly';
        document.querySelector(`input[name="editFeeType"][value="${pricingType}"]`).checked = true;

        // 清空動態容器
        document.getElementById('editHourlySlotContainer').innerHTML = '';
        document.getElementById('editTimeSlotContainer').innerHTML = '';

        this.toggleEditFeeOptions();
    },

    // 切換收費方式（新增）
    toggleFeeOptions: function () {
        const hourlyOptions = document.getElementById('hourlyOptions');
        const slotOptions = document.getElementById('slotOptions');
        const feeType = document.querySelector('input[name="feeType"]:checked');

        if (!feeType) return;

        hourlyOptions.style.display = feeType.value === 'hourly' ? 'block' : 'none';
        slotOptions.style.display = feeType.value === 'slot' ? 'block' : 'none';

        if (feeType.value === 'hourly') {
            this.generateHourlySlots();
        } else if (feeType.value === 'slot') {

            this.clearTimeSlotContainer();
            this.generateCreateTimeSlotDefaults();  // 生成預設三個時段

        }
    },

    generateCreateTimeSlotDefaults: function () {
        const container = document.getElementById('timeSlotContainer');
        if (!container) return;

        const defaultSlots = [
            { name: '上午場', startTime: '09:00', endTime: '12:00', fee: 1000 },
            { name: '午餐場', startTime: '12:00', endTime: '14:00', fee: 800 },
            { name: '下午場', startTime: '14:00', endTime: '18:00', fee: 1200 }
        ];

        defaultSlots.forEach((slot) => {
            const timestamp = Date.now() + Math.random();
            const newSlot = document.createElement('div');
            newSlot.className = 'time-slot-item';
            newSlot.setAttribute('data-slot-id', `custom_${timestamp}`);
            newSlot.style.cssText = 'display: flex; align-items: center; gap: 10px; margin-bottom: 10px; padding: 10px; background: #fff; border: 1px solid #e9ecef; border-radius: 8px;';

            // ✅ 建立完整的時段 HTML
            newSlot.innerHTML = `
            <div class="form-check">
                <input class="form-check-input" type="checkbox" id="custom_${timestamp}" checked>
                <label class="form-check-label" for="custom_${timestamp}">開放</label>
            </div>
            <input type="text" class="form-control form-control-sm slot-name-input" 
                   value="${slot.name}" placeholder="時段名稱" style="width: 80px; max-width: 150px;">
            <input type="time" class="form-control form-control-sm time-input" 
                   value="${slot.startTime}" style="width: 110px;">
            <span>-</span>
            <input type="time" class="form-control form-control-sm time-input" 
                   value="${slot.endTime}" style="width: 110px;">
            <div class="input-group" style="width: 180px;">
                <span class="input-group-text">$</span>
                <input type="number" class="form-control form-control-sm fee-input" 
                       value="${slot.fee}" placeholder="費用" min="0">
                <span class="input-group-text">元</span>
            </div>
            <button type="button" class="btn btn-sm btn-outline-danger delete-btn"
                    onclick="room.removeTimeSlot(this)">刪除</button>
        `;

            container.appendChild(newSlot);
        });
    },

    // 切換收費方式（編輯）
    toggleEditFeeOptions: function () {
        const hourlyOptions = document.getElementById('editHourlyOptions');
        const slotOptions = document.getElementById('editSlotOptions');
        const feeType = document.querySelector('input[name="editFeeType"]:checked');

        if (!feeType) return;

        hourlyOptions.style.display = feeType.value === 'hourly' ? 'block' : 'none';
        slotOptions.style.display = feeType.value === 'period' ? 'block' : 'none';

        if (feeType.value === 'hourly') {
            this.generateHourlySlotsEdit();
        }
    },

    // ===== 小時制相關函數 =====
    generateHourlySlots: function () {
        const container = document.getElementById('hourlySlotContainer');
        if (!container) return;

        container.innerHTML = '';

        for (let hour = 8; hour < 20; hour++) {
            const startTime = `${hour.toString().padStart(2, '0')}:00`;
            const endTime = `${(hour + 1).toString().padStart(2, '0')}:00`;

            const slotDiv = document.createElement('div');
            slotDiv.className = 'mb-2';
            slotDiv.innerHTML = `
                <div class="d-flex align-items-center gap-2 p-2 bg-white rounded-2">
                    <div class="form-check">
                        <input class="form-check-input hourly-checkbox" type="checkbox" 
                               id="hour_${hour}" name="hourlySlots" value="${hour}">
                        <label class="form-check-label fw-600" for="hour_${hour}">
                            ${startTime} - ${endTime}
                        </label>
                    </div>
                    <div class="input-group ms-auto" style="width: 150px;">
                        <span class="input-group-text">$</span>
                        <input type="number" class="form-control form-control-sm" 
                               value="500" name="hourly_fee_${hour}" min="0">
                        <span class="input-group-text">元</span>
                    </div>
                </div>
            `;
            container.appendChild(slotDiv);
        }
    },

    generateHourlySlotsEdit: function () {
        const container = document.getElementById('editHourlySlotContainer');
        if (!container) return;

        container.innerHTML = '';

        for (let hour = 8; hour < 20; hour++) {
            const startTime = `${hour.toString().padStart(2, '0')}:00`;
            const endTime = `${(hour + 1).toString().padStart(2, '0')}:00`;

            const slotDiv = document.createElement('div');
            slotDiv.className = 'mb-2';
            slotDiv.innerHTML = `
                <div class="d-flex align-items-center gap-2 p-2 bg-white rounded-2">
                    <div class="form-check">
                        <input class="form-check-input hourly-checkbox" type="checkbox" 
                               id="edit_hour_${hour}" name="edit_hourlySlots" value="${hour}">
                        <label class="form-check-label fw-600" for="edit_hour_${hour}">
                            ${startTime} - ${endTime}
                        </label>
                    </div>
                    <div class="input-group ms-auto" style="width: 150px;">
                        <span class="input-group-text">$</span>
                        <input type="number" class="form-control form-control-sm" 
                               value="500" name="edit_hourly_fee_${hour}" min="0">
                        <span class="input-group-text">元</span>
                    </div>
                </div>
            `;
            container.appendChild(slotDiv);
        }
    },

    selectAllHours: function () {
        document.querySelectorAll('#hourlySlotContainer input[name="hourlySlots"]').forEach(cb => cb.checked = true);
    },

    deselectAllHours: function () {
        document.querySelectorAll('#hourlySlotContainer input[name="hourlySlots"]').forEach(cb => cb.checked = false);
    },

    selectAllHoursEdit: function () {
        document.querySelectorAll('#editHourlySlotContainer input[name="edit_hourlySlots"]').forEach(cb => cb.checked = true);
    },

    deselectAllHoursEdit: function () {
        document.querySelectorAll('#editHourlySlotContainer input[name="edit_hourlySlots"]').forEach(cb => cb.checked = false);
    },

    // ===== 時段制相關函數 =====
    addTimeSlot: function () {
        const container = document.getElementById('timeSlotContainer');
        if (!container) return;

        const timestamp = Date.now();
        const newSlot = document.createElement('div');
        newSlot.className = 'time-slot-item';
        newSlot.setAttribute('data-slot-id', `custom_${timestamp}`);

        newSlot.innerHTML = `
            <div class="form-check">
                <input class="form-check-input" type="checkbox" id="custom_${timestamp}" checked>
                <label class="form-check-label" for="custom_${timestamp}">開放</label>
            </div>
            <input type="text" class="form-control form-control-sm slot-name-input" 
                   value="自訂時段" placeholder="時段名稱" style="width: 80px; max-width: 150px;">
            <input type="time" class="form-control form-control-sm time-input" 
                   value="22:00" style="width: 110px;">
            <span>-</span>
            <input type="time" class="form-control form-control-sm time-input" 
                   value="23:00" style="width: 110px;">
            <div class="input-group" style="width: 180px;">
                <span class="input-group-text">$</span>
                <input type="number" class="form-control form-control-sm fee-input" 
                       placeholder="費用" min="0">
                <span class="input-group-text">元</span>
            </div>
            <button type="button" class="btn btn-sm btn-outline-danger delete-btn"
                    onclick="room.removeTimeSlot(this)">刪除</button>
        `;

        container.appendChild(newSlot);
    },

    addTimeSlotEdit: function () {
        const container = document.getElementById('editTimeSlotContainer');
        if (!container) return;

        const timestamp = Date.now();
        const newSlot = document.createElement('div');
        newSlot.className = 'time-slot-item';
        newSlot.setAttribute('data-slot-id', `edit_custom_${timestamp}`);

        newSlot.innerHTML = `
            <div class="form-check">
                <input class="form-check-input" type="checkbox" id="edit_custom_${timestamp}" checked>
                <label class="form-check-label" for="edit_custom_${timestamp}">開放</label>
            </div>
            <input type="text" class="form-control form-control-sm slot-name-input" 
                   value="自訂時段" placeholder="時段名稱" style="width: 80px; max-width: 150px;">
            <input type="time" class="form-control form-control-sm time-input" 
                   value="22:00" style="width: 110px;">
            <span>-</span>
            <input type="time" class="form-control form-control-sm time-input" 
                   value="23:00" style="width: 110px;">
            <div class="input-group" style="width: 180px;">
                <span class="input-group-text">$</span>
                <input type="number" class="form-control form-control-sm fee-input" 
                       placeholder="費用" min="0">
                <span class="input-group-text">元</span>
            </div>
            <button type="button" class="btn btn-sm btn-outline-danger delete-btn"
                    onclick="room.removeTimeSlot(this)">刪除</button>
        `;

        container.appendChild(newSlot);
    },

    removeTimeSlot: function (button) {
        const slotItem = button.closest('.time-slot-item');
        if (confirm('確認要刪除這個時段嗎？')) {
            slotItem.remove();
        }
    },

    clearTimeSlotContainer: function () {
        const container = document.getElementById('timeSlotContainer');
        if (container) container.innerHTML = '';
    },

    getTimeSlotSettings: function () {
        const slotItems = document.querySelectorAll('#timeSlotContainer [data-slot-id]');
        const settings = [];

        slotItems.forEach(item => {
            const checkbox = item.querySelector('input[type="checkbox"]');
            const nameInput = item.querySelector('.slot-name-input');
            const timeInputs = item.querySelectorAll('.time-input');
            const feeInput = item.querySelector('.fee-input');

            const setting = {
                name: nameInput ? nameInput.value || '未命名時段' : '未命名時段',
                startTime: timeInputs[0] ? timeInputs[0].value : '',
                endTime: timeInputs[1] ? timeInputs[1].value : '',
                fee: parseInt(feeInput ? feeInput.value : 0) || 0,
                enabled: checkbox ? checkbox.checked : false
            };

            settings.push(setting);
        });

        return settings;
    },

    validateTimeSlots: function () {
        const settings = this.getTimeSlotSettings();
        const errors = [];

        settings.forEach((setting, index) => {
            if (setting.startTime >= setting.endTime) {
                errors.push(`第${index + 1}個時段：結束時間必須晚於開始時間`);
            }

            if (setting.enabled && setting.fee <= 0) {
                errors.push(`第${index + 1}個時段：開放的時段必須設定費用`);
            }

            if (!setting.name.trim()) {
                errors.push(`第${index + 1}個時段：請輸入時段名稱`);
            }
        });

        return errors;
    },

    // ===== 租借權限 =====
    selectRentalOption: function (element, type) {
        const container = element.closest('.rental-options');
        if (container) {
            container.querySelectorAll('.rental-option').forEach(opt => {
                opt.style.border = '1px solid #e9ecef';
                opt.style.background = '#fff';
            });
        }
        element.style.border = '2px solid #007bff';
        element.style.background = '#f0f7ff';
        const input = document.getElementById('create-rental-type');
        if (input) input.value = type;
    },

    selectEditRentalOption: function (element, type) {
        const container = element.closest('.rental-options');
        if (container) {
            container.querySelectorAll('.rental-option').forEach(opt => {
                opt.style.border = '1px solid #e9ecef';
                opt.style.background = '#fff';
            });
        }
        element.style.border = '2px solid #007bff';
        element.style.background = '#f0f7ff';
        const input = document.getElementById('edit-rental-type');
        if (input) input.value = type;
    },

    // ===== 媒體上傳 =====
    handleMediaUpload: function (event) {
        const files = event.target.files;
        const container = document.getElementById('mediaGrid');
        if (!container || files.length === 0) return;

        const file = files[0];  // 只取第一個
        const reader = new FileReader();

        reader.onload = function (e) {
            const mediaItem = document.createElement('div');
            mediaItem.className = 'media-item';
            mediaItem.style.cssText = 'position: relative; height: 120px; border: 1px solid #eee; border-radius: 8px; overflow: hidden; background: #fff;';

            // 媒體內容
            if (file.type.startsWith('image/')) {
                mediaItem.innerHTML = `<img src="${e.target.result}" style="width: 100%; height: 100%; object-fit: cover;">`;
            } else if (file.type.startsWith('video/')) {
                mediaItem.innerHTML = `<video style="width: 100%; height: 100%; object-fit: cover;"><source src="${e.target.result}" type="${file.type}"></video>`;
            }

            // 刪除按鈕
            const deleteBtn = document.createElement('button');
            deleteBtn.type = 'button';
            deleteBtn.innerHTML = '&times;';
            deleteBtn.style.cssText = 'position: absolute; top: 6px; right: 6px; width: 28px; height: 28px; border: 0; border-radius: 50%; background: rgba(0, 0, 0, 0.6); color: #fff; cursor: pointer; z-index: 10; font-size: 18px; padding: 0;';

            // 重點：事件綁定正確做法
            deleteBtn.onclick = function (e) {
                e.stopPropagation();
                mediaItem.remove();
                document.getElementById('mediaUpload').value = '';
            };

            mediaItem.appendChild(deleteBtn);

            // 在上傳按鈕前插入（保留舊檔案）
            const uploadItem = container.querySelector('.upload-item');
            if (uploadItem) {
                container.insertBefore(mediaItem, uploadItem);
            } else {
                container.appendChild(mediaItem);
            }

            event.target.value = '';
        };

        reader.readAsDataURL(file);
    },

    getMediaInfo: function () {
        const container = document.getElementById('mediaGrid');
        if (!container) return [];
        const mediaItems = container.querySelectorAll('.media-item');
        const mediaInfo = [];
        mediaItems.forEach((item, index) => {
            const img = item.querySelector('img');
            const video = item.querySelector('video');
            const source = item.querySelector('source');
            if (img) {
                mediaInfo.push({
                    type: 'image',
                    src: img.src,
                    fileSize: img.src.length,
                    sortOrder: index
                });
            } else if (video && source) {
                mediaInfo.push({
                    type: 'video',
                    src: source.src,
                    fileSize: source.src.length,
                    sortOrder: index
                });
            }
        });
        return mediaInfo;
    },


    // ===== 詳情檢視 =====
    viewRoomDetail: function (roomId) {
        console.log('viewRoomDetail called with ID:', roomId);
        global.api.admin.roomdetail({ body: { id: roomId } })
            .then((response) => {
                this.populateDetailModal(response.data);
                this.detailModal.show();
            })
            .catch(error => {
                console.error('取得資料失敗:', error);
                alert('取得資料失敗');
            });
    },

    populateDetailModal: function (data) {
        document.getElementById('modal-room-name').textContent = data.Name || '';
        document.getElementById('modal-feature').textContent = data.Description || '';
        document.getElementById('modal-capacity').textContent = (data.Capacity || 0) + '人';
        document.getElementById('modal-area').textContent = (data.Area || 0) + '㎡';
        document.getElementById('modal-room-number').textContent = data.Number || '';
        document.getElementById('modal-location').textContent = `${data.Building || ''} ${data.Floor || ''}樓`;

        // 生成輪播
        this.generateImageCarousel(data);

        // 設備清單
        const equipmentList = document.getElementById('modal-equipment-list');
        equipmentList.innerHTML = '';
        if (data.Equipment && data.Equipment.length > 0) {
            data.Equipment.forEach(equipment => {
                const statusColor = equipment.Status === 'normal' ? 'text-success' : 'text-danger';
                const equipmentItem = document.createElement('div');
                equipmentItem.className = 'equipment-item';
                equipmentItem.innerHTML = `
                    <i class="mdi ${equipment.Icon || 'mdi-tools'} equipment-icon ${statusColor}"></i>
                    <div>
                        <div class="equipment-name">${equipment.Name || '設備'}</div>
                        <small class="${statusColor}">${equipment.Status === 'normal' ? '運作正常' : '異常'}</small>
                    </div>
                `;
                equipmentList.appendChild(equipmentItem);
            });
        }

        // 時程
        const scheduleList = document.getElementById('modal-schedule-list');
        scheduleList.innerHTML = '';
        if (data.Schedule && data.Schedule.length > 0) {
            data.Schedule.forEach(schedule => {
                const row = document.createElement('tr');
                const statusBadge = this.getScheduleStatusBadge(schedule.Status);
                row.innerHTML = `
                    <td>${schedule.Time || ''}</td>
                    <td>${schedule.MeetingName || ''}</td>
                    <td>${schedule.Organizer || ''}</td>
                    <td>${statusBadge}</td>
                `;
                scheduleList.appendChild(row);
            });
        }

        // 收費
        document.getElementById('modal-pricing-type').textContent = data.PricingType === 'hourly' ? '小時制' : '時段制';

        const pricingList = document.getElementById('modal-pricing-list');
        pricingList.innerHTML = '';
        if (data.Pricing && data.Pricing.length > 0) {
            data.Pricing.forEach(price => {
                const priceItem = document.createElement('div');
                priceItem.className = 'time-slot-item';
                priceItem.innerHTML = `
                    <span><i class="mdi mdi-clock-outline me-2"></i>${price.Time || price.StartTime + '-' + price.EndTime}</span>
                    <span class="fw-bold text-primary">$${price.Price || price.Fee} 元</span>
                `;
                pricingList.appendChild(priceItem);
            });
        }
    },

    generateImageCarousel: function (data) {
        const carouselInner = document.getElementById('carouselInner');
        const carouselIndicators = document.getElementById('carouselIndicators');

        carouselInner.innerHTML = '';
        carouselIndicators.innerHTML = '';

        const mediaList = data.Media && data.Media.length > 0 ? data.Media : [{ Type: 'image', Src: data.Image }];

        mediaList.forEach((media, index) => {
            const carouselItem = document.createElement('div');
            carouselItem.className = `carousel-item ${index === 0 ? 'active' : ''}`;

            if (media.Type === 'video') {
                carouselItem.innerHTML = `<video class="d-block w-100" controls style="height: 400px; object-fit: cover;"><source src="${media.Src}" type="video/mp4">您的瀏覽器不支援影片播放。</video>`;
            } else {
                carouselItem.innerHTML = `<img src="${media.Src || media.Image}" class="d-block w-100" alt="會議室媒體" style="height: 400px; object-fit: cover;">`;
            }

            carouselInner.appendChild(carouselItem);

            const indicator = document.createElement('button');
            indicator.type = 'button';
            indicator.setAttribute('data-bs-target', '#roomImageCarousel');
            indicator.setAttribute('data-bs-slide-to', index);
            if (index === 0) {
                indicator.className = 'active';
                indicator.setAttribute('aria-current', 'true');
            }
            carouselIndicators.appendChild(indicator);
        });
    },

    getScheduleStatusBadge: function (status) {
        const badgeMap = {
            'completed': '<span class="badge bg-success">已完成</span>',
            'ongoing': '<span class="badge bg-danger">進行中</span>',
            'pending': '<span class="badge bg-warning">待開始</span>',
            'available': '<span class="badge bg-info">可預約</span>',
            'maintenance': '<span class="badge bg-secondary">維修中</span>'
        };
        return badgeMap[status] || status;
    },

    // ===== 刪除會議室 =====
    deleteRoom: function (roomId) {
        if (!confirm('確認要刪除這個會議室嗎？此操作無法復原。')) {
            return;
        }

        global.api.admin.roomdelete({ body: { id: roomId } })
            .then((response) => {
                alert('刪除成功');
                this.getList();
            })
            .catch(error => {
                console.error('刪除失敗:', error);
                alert('刪除失敗，請稍後再試');
            });
    },

    // ===== 編輯會議室 =====
    editRoom: function (roomId) {
        console.log('editRoom called with ID:', roomId);
        global.api.admin.roomdetail({ body: { id: roomId } })
            .then((response) => {
                this.populateEditForm(response.data);
                this.editModal.show();
            })
            .catch(error => {
                console.error('取得資料失敗:', error);
                alert('取得資料失敗');
            });
    },

    saveEditRoomChanges: function () {

        const feeType = document.querySelector('input[name="feeType"]:checked');

        // 收集收費詳情
        let pricingDetails = [];

        if (feeType.value === 'hourly') {
            // 小時制
            for (let hour = 8; hour < 20; hour++) {
                const checkbox = document.getElementById(`hour_${hour}`);
                const feeInput = document.querySelector(`input[name="hourly_fee_${hour}"]`);

                if (checkbox.checked) {
                    pricingDetails.push({
                        name: `${hour}:00 - ${hour + 1}:00`,
                        startTime: `${hour}:00`,
                        endTime: `${hour + 1}:00`,
                        price: parseFloat(feeInput.value) || 0,
                        enabled: true
                    });
                }
            }
        } else if (feeType.value === 'slot') {
            // 時段制
            const slotItems = document.querySelectorAll('#timeSlotContainer [data-slot-id]');
            slotItems.forEach(item => {
                const checkbox = item.querySelector('input[type="checkbox"]');
                const nameInput = item.querySelector('.slot-name-input');
                const timeInputs = item.querySelectorAll('.time-input');
                const feeInput = item.querySelector('.fee-input');

                if (checkbox.checked) {
                    pricingDetails.push({
                        name: nameInput.value || '未命名時段',
                        startTime: timeInputs[0].value,
                        endTime: timeInputs[1].value,
                        price: parseFloat(feeInput.value) || 0,
                        enabled: true
                    });
                }
            });
        }

        const form = document.getElementById('roomEditForm');


        // 收集表單資料
        const formData = {
            Id: form.dataset.roomId,
            Name: document.getElementById('edit-name').value,
            Building: document.getElementById('edit-building').value,
            Floor: document.getElementById('edit-floor').value,
            Number: document.getElementById('edit-room-number').value,
            Capacity: parseInt(document.getElementById('edit-capacity').value),
            Area: parseFloat(document.getElementById('edit-area').value),
            Description: document.getElementById('edit-room-description').value,
            PricingType: feeType ? feeType.value : 'hourly',
            IsEnabled: document.getElementById('editRefundEnabled') ? document.getElementById('editRefundEnabled').checked : true,
            Images: this.getMediaInfo(),
            Status: document.getElementById('edit-status').value,
            BookingSettings: document.getElementById('edit-rental-type').value

        };

        // 發送更新
        global.api.admin.roomupdate({ body: formData })
            .then((response) => {
                alert('更新成功');
                this.editModal.hide();
                this.getList();
            })
            .catch(error => {
                alert('更新失敗');
                console.error(error);
            });
    },

    // ===== 新增會議室 =====
    save: function () {
        // 驗證收費設定
        const feeType = document.querySelector('input[name="feeType"]:checked');

        // 收集收費詳情
        let pricingDetails = [];
        const imageFiles = Array.from(document.getElementById('mediaUpload').files);

        if (feeType.value === 'hourly') {
            // 小時制
            for (let hour = 8; hour < 20; hour++) {
                const checkbox = document.getElementById(`hour_${hour}`);
                const feeInput = document.querySelector(`input[name="hourly_fee_${hour}"]`);

                if (checkbox.checked) {
                    pricingDetails.push({
                        name: `${hour}:00 - ${hour + 1}:00`,
                        startTime: `${hour}:00`,
                        endTime: `${hour + 1}:00`,
                        price: parseFloat(feeInput.value) || 0,
                        enabled: true
                    });
                }
            }
        } else if (feeType.value === 'slot') {
            // 時段制
            const slotItems = document.querySelectorAll('#timeSlotContainer [data-slot-id]');
            slotItems.forEach(item => {
                const checkbox = item.querySelector('input[type="checkbox"]');
                const nameInput = item.querySelector('.slot-name-input');
                const timeInputs = item.querySelectorAll('.time-input');
                const feeInput = item.querySelector('.fee-input');

                if (checkbox.checked) {
                    pricingDetails.push({
                        name: nameInput.value || '未命名時段',
                        startTime: timeInputs[0].value,
                        endTime: timeInputs[1].value,
                        price: parseFloat(feeInput.value) || 0,
                        enabled: true
                    });
                }
            });
        }


        // 收集表單資料
        const formData = {
            Name: document.getElementById('create-name').value,
            Building: document.getElementById('create-building').value,
            Floor: document.getElementById('create-floor').value,
            Number: document.getElementById('create-room-number').value,
            Capacity: parseInt(document.getElementById('create-capacity').value),
            Area: parseFloat(document.getElementById('create-area').value),
            Description: document.getElementById('create-room-description').value,
            PricingType: feeType ? feeType.value : 'hourly',
            IsEnabled: document.getElementById('refundEnabled') ? document.getElementById('refundEnabled').checked : true,
            Images: this.getMediaInfo(),
            Status: 'available',
            BookingSettings: document.getElementById('create-rental-type').value || 'in',
            PricingDetails: pricingDetails

        };

        console.log('【新增】傳送給後端的資料:', formData);

        // 發送新增
        global.api.admin.roominsert({ body: formData })
            .then((response) => {
                console.log('【新增】後端回應:', response);
                alert('新增成功');
                this.offcanvas.hide();
                this.getList();
            })
            .catch(error => {
                alert('新增失敗');
                console.log('【新增】錯誤:', error);
                console.error(error);
            });
    }
};

// 初始化
document.addEventListener('DOMContentLoaded', function () {
    console.log('DOMContentLoaded - 初始化開始');

    // 初始化 Bootstrap 元件
    const offcanvasEl = document.getElementById('offcanvasRoomCreate');
    const editModalEl = document.getElementById('roomEditModal');
    const detailModalEl = document.getElementById('roomDetailModal');

    if (offcanvasEl) {
        room.offcanvas = new bootstrap.Offcanvas(offcanvasEl);
        console.log('Offcanvas 已初始化');
    } else {
        console.error('找不到 offcanvasRoomCreate 元素');
    }

    if (editModalEl) {
        room.editModal = new bootstrap.Modal(editModalEl);
        console.log('Edit Modal 已初始化');
    } else {
        console.error('找不到 roomEditModal 元素');
    }

    if (detailModalEl) {
        room.detailModal = new bootstrap.Modal(detailModalEl);
        console.log('Detail Modal 已初始化');
    } else {
        console.error('找不到 roomDetailModal 元素');
    }

    // 搜尋功能
    const searchInput = document.getElementById('searchKeyword');
    if (searchInput) {
        searchInput.addEventListener('keyup', function (e) {
            if (e.key === 'Enter') {
                room.search();
            }
        });
    }

    // 載入列表
    room.getList();
    console.log('初始化完成');
});

// 暴露到全局
window.room = room;
