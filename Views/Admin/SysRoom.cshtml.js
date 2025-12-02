import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

class VM {
    id = null;
    name = '';
    description = '';
    isEnabled = true;
    capacity = 50;
    area = 80;
    building = '';
    floor = '';
    number = '';
    status = 'available';
    pricingType = 'hourly';
    bookingSettings = { type: 'internal' };
}

const room = new function () {
    this.query = reactive({
        keyword: '',
        pageIndex: 1,
        pageSize: 6
    });
    this.list = reactive([]);
    this.total = 0;
    this.hasNextPage = false;
    this.offcanvas = null;
    this.vm = reactive(new VM());

    this.getList = () => {
        console.log('getList 執行中...');
        global.api.admin.roomlist({ body: this.query })
            .then((response) => {
                console.log('API 回傳:', response);
                console.log('response.data:', response.data);

                // 確保資料正確賦值
                const data = response.data?.items || response.data || [];
                console.log('要填入的資料:', data);

                this.list.length = 0; // 清空舊資料
                this.list.push(...data); // 加入新資料

                console.log('list 更新後:', this.list);
                this.total = response.data?.total || this.list.length;
                this.hasNextPage = (this.query.pageIndex * this.query.pageSize) < this.total;
            })
            .catch(error => {
                console.error('getList 錯誤:', error);
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }

    this.getVM = (id) => {
        if (id) {
            global.api.admin.roomdetail({ body: { id } })
                .then((response) => {
                    copy(this.vm, response.data);
                    // 初始化收費設定 UI
                    setTimeout(() => {
                        this.togglePricingType();
                    }, 100);
                    if (this.offcanvas) this.offcanvas.show();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            // 初始化新增時的收費設定 UI
            setTimeout(() => {
                this.togglePricingType();
            }, 100);
            if (this.offcanvas) this.offcanvas.show();
        }
    }

    this.save = () => {
        // 驗證收費設定
        if (this.vm.pricingType === 'period') {
            const errors = this.validateTimeSlots();
            if (errors.length > 0) {
                addAlert('設定有誤：\n' + errors.join('\n'), { type: 'danger' });
                return;
            }
        }

        const method = this.vm.id ? global.api.admin.roomupdate : global.api.admin.roominsert;
        method({ body: this.vm })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                if (this.offcanvas) this.offcanvas.hide();
            })
            .catch(error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }

    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.roomdelete({ body: { id } })
                .then((response) => {
                    addAlert('操作成功');
                    this.getList();
                })
                .catch(error => {
                    addAlert(getMessage(error), { type: 'danger', click: error.download });
                });
        }
    }

    this.viewDetail = (id) => {
        console.log('檢視會議室詳情:', id);
    }

    this.previousPage = () => {
        if (this.query.pageIndex > 1) {
            this.query.pageIndex--;
            this.getList();
        }
    }

    this.nextPage = () => {
        if (this.hasNextPage) {
            this.query.pageIndex++;
            this.getList();
        }
    }

    // ===== 小時制相關函數 =====
    this.generateHourlySlots = () => {
        const container = document.getElementById('hourlySlotContainer');
        if (!container) return;

        container.innerHTML = '';

        // 從早上8點到晚上8點 (20:00)
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
    }

    this.selectAllHours = () => {
        const checkboxes = document.querySelectorAll('#hourlySlotContainer input[name="hourlySlots"]');
        checkboxes.forEach(checkbox => {
            checkbox.checked = true;
        });
    }

    this.deselectAllHours = () => {
        const checkboxes = document.querySelectorAll('#hourlySlotContainer input[name="hourlySlots"]');
        checkboxes.forEach(checkbox => {
            checkbox.checked = false;
        });
    }

    this.getHourlySettings = () => {
        const hourlySettings = [];
        const checkboxes = document.querySelectorAll('#hourlySlotContainer input[name="hourlySlots"]:checked');

        checkboxes.forEach(checkbox => {
            const hour = checkbox.value;
            const feeInput = document.querySelector(`input[name="hourly_fee_${hour}"]`);
            const fee = feeInput ? feeInput.value : 0;

            hourlySettings.push({
                hour: parseInt(hour),
                startTime: `${hour.padStart(2, '0')}:00`,
                endTime: `${(parseInt(hour) + 1).toString().padStart(2, '0')}:00`,
                fee: parseInt(fee) || 0,
                enabled: true
            });
        });

        return hourlySettings;
    }

    // ===== 時段制相關函數 =====
    this.addTimeSlot = () => {
        const container = document.getElementById('timeSlotContainer');
        if (!container) return;

        const timestamp = Date.now();
        const newSlot = document.createElement('div');
        newSlot.className = 'p-3 border-start border-4 border-primary rounded-2 bg-white mb-3';
        newSlot.setAttribute('data-slot-id', `custom_${timestamp}`);

        newSlot.innerHTML = `
        <div class="d-flex align-items-center gap-3">
            <div class="form-check mt-1">
                <input class="form-check-input" type="checkbox" id="custom_${timestamp}" checked>
                <label class="form-check-label fw-600" for="custom_${timestamp}">開放</label>
            </div>
            
            <input type="text" class="form-control form-control-sm slot-name-input rounded-2" 
                   value="自訂時段" placeholder="時段名稱" style="max-width: 100px;">
            
            <input type="time" class="form-control form-control-sm time-input rounded-2" 
                   value="22:00" style="max-width: 120px;">
            
            <span>-</span>
            
            <input type="time" class="form-control form-control-sm time-input rounded-2" 
                   value="23:00" style="max-width: 120px;">
            
            <div class="input-group input-group-sm" style="max-width: 140px;">
                <span class="input-group-text">$</span>
                <input type="number" class="form-control form-control-sm fee-input rounded-2" 
                       placeholder="費用" min="0">
                <span class="input-group-text">元</span>
            </div>
            
            <button type="button" class="btn btn-sm btn-outline-danger rounded-2 ms-auto" 
                    data-slot-id="custom_${timestamp}">
                <i class="mdi mdi-trash-can-outline"></i>刪除
            </button>
        </div>
    `;

        container.appendChild(newSlot);

        // 綁定刪除按鈕事件
        const deleteBtn = newSlot.querySelector('button');
        deleteBtn.addEventListener('click', () => {
            newSlot.remove();
        });
    }

    this.getTimeSlotSettings = () => {
        const slotItems = document.querySelectorAll('#timeSlotContainer [data-slot-id]');
        const settings = [];

        slotItems.forEach(item => {
            const checkbox = item.querySelector('input[type="checkbox"]');
            const nameInput = item.querySelector('.slot-name-input');
            const timeInputs = item.querySelectorAll('.time-input');
            const feeInput = item.querySelector('.fee-input');
            const slotId = item.getAttribute('data-slot-id');

            const setting = {
                id: slotId,
                name: nameInput ? nameInput.value || '未命名時段' : '未命名時段',
                startTime: timeInputs[0] ? timeInputs[0].value : '',
                endTime: timeInputs[1] ? timeInputs[1].value : '',
                fee: parseInt(feeInput ? feeInput.value : 0) || 0,
                enabled: checkbox ? checkbox.checked : false
            };

            settings.push(setting);
        });

        return settings;
    }

    this.validateTimeSlots = () => {
        const settings = this.getTimeSlotSettings();
        const errors = [];

        settings.forEach((setting, index) => {
            // 檢查時間設定
            if (setting.startTime >= setting.endTime) {
                errors.push(`第${index + 1}個時段：結束時間必須晚於開始時間`);
            }

            // 檢查費用設定
            if (setting.enabled && setting.fee <= 0) {
                errors.push(`第${index + 1}個時段：開放的時段必須設定費用`);
            }

            // 檢查時段名稱
            if (!setting.name.trim()) {
                errors.push(`第${index + 1}個時段：請輸入時段名稱`);
            }
        });

        return errors;
    }

    this.togglePricingType = () => {
        const pricingType = this.vm.pricingType;

        if (pricingType === 'hourly') {
            this.generateHourlySlots();
        } else if (pricingType === 'period') {
            const container = document.getElementById('timeSlotContainer');
            if (container && container.children.length === 0) {
                this.addDefaultTimeSlots();
            }
        }
    }

    this.addDefaultTimeSlots = () => {
        const defaults = [
            { name: '上午時段', startTime: '09:00', endTime: '12:00', fee: '' },
            { name: '下午時段', startTime: '13:00', endTime: '17:00', fee: '' },
            { name: '晚上時段', startTime: '18:00', endTime: '21:00', fee: '' }
        ];

        const container = document.getElementById('timeSlotContainer');
        if (!container) return;

        defaults.forEach((slot, index) => {
            const timestamp = `default_${index}_${Date.now()}`;
            const slotDiv = document.createElement('div');
            slotDiv.className = 'p-3 border-start border-4 border-primary rounded-2 bg-white mb-3';
            slotDiv.setAttribute('data-slot-id', timestamp);

            slotDiv.innerHTML = `
            <div class="d-flex align-items-center gap-3">
                <div class="form-check mt-1">
                    <input class="form-check-input" type="checkbox" id="${timestamp}" checked>
                    <label class="form-check-label fw-600" for="${timestamp}">開放</label>
                </div>
                
                <input type="text" class="form-control form-control-sm slot-name-input rounded-2" 
                       value="${slot.name}" placeholder="時段名稱" style="max-width: 100px;">
                
                <input type="time" class="form-control form-control-sm time-input rounded-2" 
                       value="${slot.startTime}" style="max-width: 120px;">
                
                <span>-</span>
                
                <input type="time" class="form-control form-control-sm time-input rounded-2" 
                       value="${slot.endTime}" style="max-width: 120px;">
                
                <div class="input-group input-group-sm" style="max-width: 140px;">
                    <span class="input-group-text">$</span>
                    <input type="number" class="form-control form-control-sm fee-input rounded-2" 
                           placeholder="費用" min="0">
                    <span class="input-group-text">元</span>
                </div>
                
                <button type="button" class="btn btn-sm btn-outline-danger rounded-2 ms-auto delete-slot-btn">
                    <i class="mdi mdi-trash-can-outline"></i>刪除
                </button>
            </div>
        `;

            container.appendChild(slotDiv);

            // 綁定刪除按鈕事件
            const deleteBtn = slotDiv.querySelector('.delete-slot-btn');
            deleteBtn.addEventListener('click', () => {
                slotDiv.remove();
            });
        });
    }
}

window.$config = {
    setup: () => {
        onMounted(() => {
            room.getList();
            // 初始化 Bootstrap offcanvas
            const offcanvasElement = document.getElementById('offcanvasRoomEdit');
            if (offcanvasElement) {
                room.offcanvas = new bootstrap.Offcanvas(offcanvasElement);
            }
        });

        // 監聽 pricingType 變化
        watch(() => room.vm.pricingType, (newVal) => {
            setTimeout(() => {
                room.togglePricingType();
            }, 100);
        });

        return {
            room: room
        }
    }
}

window.room = room;