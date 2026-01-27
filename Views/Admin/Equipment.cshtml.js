// Admin/Equipment - Global Query Filter 版本
import global from '/global.js';
const { ref, reactive, onMounted, watch } = Vue;

class VM {
    Id = null;
    Name = '';
    ProductModel = '';
    Type = '';
    RoomId = null;
    Building = '';
    Floor = '';
    RentalPrice = 0;
    Host = '';
    Port = null;
    Mac = '';
    Serial = null;
    Account = '';
    Password = '';
    IsEnabled = true;
    DepartmentId = '';
}

// ========= 全域狀態 =========
let currentUser = null;
const isAdmin = ref(false);
const userDepartmentId = ref(null);
const userDepartmentName = ref('');
const isEditing = ref(false);
const departments = ref([]);
const selectedDepartment = ref('');

// ========= 設備管理物件 =========
const equipment = new function () {
    this.query = reactive({
        keyword: '',
        type: ''
    });

    this.list = reactive([]);
    this.buildings = reactive([]);
    this.floorOptions = reactive([]);
    this.roomOptions = reactive([]);
    this.vm = reactive(new VM());

    // ========= 取得列表 =========
    this.getList = () => {
        global.api.admin.equipmentlist({ body: this.query })
            .then(response => copy(this.list, response.data))
            .catch(error => addAlert('取得資料失敗', { type: 'danger', click: error.download }));
    };

    // ========= 級聯選單 - 大樓變更 =========
    this.onBuildingChange = () => {
        this.floorOptions.splice(0);
        this.roomOptions.splice(0);
        this.vm.Floor = '';
        this.vm.RoomId = null;

        if (!this.vm.Building) return;

        // ✅ 後端 Global Query Filter 會自動過濾
        global.api.select.floorsbybuilding({
            body: { building: this.vm.Building }
        }).then(res => copy(this.floorOptions, res.data || []));
    };

    // ========= 級聯選單 - 樓層變更 =========
    this.onFloorChange = () => {
        this.vm.RoomId = null;

        if (!this.vm.Building || !this.vm.Floor) {
            this.roomOptions.splice(0);
            return;
        }

        // ✅ 後端 Global Query Filter 會自動過濾
        global.api.select.roomsbyfloor({
            body: {
                Building: this.vm.Building,
                Floor: this.vm.Floor
            }
        })
            .then(response => {
                if (Array.isArray(response.data)) {
                    copy(this.roomOptions, response.data);
                }
            })
            .catch(error => {
                addAlert('取得會議室失敗', { type: 'danger', click: error.download });
                this.roomOptions.splice(0);
            });
    };

    // ========= 清空級聯欄位 =========
    this.clearCascadeFields = () => {
        this.buildings.splice(0);
        this.floorOptions.splice(0);
        this.roomOptions.splice(0);
        this.vm.Building = '';
        this.vm.Floor = '';
        this.vm.RoomId = null;
    };

    // ========= 打開 Modal (新增或編輯) =========
    this.getVM = async (id) => {
        if (!id) {
            // ===== 新增模式 =====
            isEditing.value = false;
            copy(this.vm, new VM());

            selectedDepartment.value = isAdmin.value ? '' : userDepartmentId.value;

            // ✅ 後端會自動過濾
            if (selectedDepartment.value) {
                try {
                    const bRes = await global.api.select.buildingsbydepartment();
                    copy(this.buildings, bRes.data || []);
                } catch (err) {
                    console.error('❌ 載入大樓失敗:', err);
                }
            } else {
                this.clearCascadeFields();
            }
            return;
        }

        // ===== 編輯模式 =====
        isEditing.value = true;

        try {
            const res = await global.api.admin.equipmentdetail({ body: { id } });
            copy(this.vm, res.data);

            const departmentId = this.vm.DepartmentId;
            selectedDepartment.value = departmentId || '';

            if (departmentId) {
                // ✅ 後端會自動過濾
                const bRes = await global.api.select.buildingsbydepartment();
                copy(this.buildings, bRes.data || []);

                if (this.vm.Building) {
                    const fRes = await global.api.select.floorsbybuilding({
                        body: { building: this.vm.Building }
                    });
                    copy(this.floorOptions, fRes.data || []);

                    if (this.vm.Floor) {
                        const rRes = await global.api.select.roomsbyfloor({
                            body: {
                                Building: this.vm.Building,
                                Floor: this.vm.Floor
                            }
                        });
                        copy(this.roomOptions, rRes.data || []);
                    }
                }
            } else {
                this.clearCascadeFields();
            }

            isEditing.value = false;
        } catch (error) {
            console.error('❌ 載入設備詳細資料失敗:', error);
            addAlert('載入設備詳細資料失敗', { type: 'danger' });
            isEditing.value = false;
        }
    };

    // ========= 驗證 =========
    this.validate = () => {
        if (!this.vm.Name || this.vm.Name.trim() === '') {
            addAlert('產品名稱為必填', { type: 'warning' });
            return false;
        }

        if (!this.vm.Type || this.vm.Type === '') {
            addAlert('產品類型為必填', { type: 'warning' });
            return false;
        }

        const deptId = selectedDepartment.value || userDepartmentId.value;
        if (!deptId) {
            addAlert('請選擇分院', { type: 'warning' });
            return false;
        }

        // 如果選了大樓,就必須選到會議室
        if (this.vm.Building && this.vm.Building !== '') {
            if (!this.vm.Floor || this.vm.Floor === '') {
                addAlert('已選擇大樓,請選擇樓層', { type: 'warning' });
                return false;
            }

            if (!this.vm.RoomId) {
                addAlert('已選擇樓層,請選擇會議室', { type: 'warning' });
                return false;
            }
        }

        // 公有設備(8)/攤位租借(9) 必須有租借金額
        if ([8, 9].includes(Number(this.vm.Type)) && this.vm.RentalPrice <= 0) {
            addAlert('公有設備和攤位租借必須設定租借金額', { type: 'warning' });
            return false;
        }

        // 檢核重複
        const type = Number(this.vm.Type);
        const id = this.vm.Id;

        if (type === 9) {
            const isDuplicate = this.list.some(item =>
                item.Name === this.vm.Name && item.Id !== id
            );
            if (isDuplicate) {
                addAlert('此攤位名稱已存在', { type: 'warning' });
                return false;
            }
        } else if ([1, 2, 3, 4, 8].includes(type)) {
            if (!this.vm.ProductModel || this.vm.ProductModel.trim() === '') {
                addAlert('產品型號為必填', { type: 'warning' });
                return false;
            }

            const isDuplicate = this.list.some(item =>
                item.ProductModel === this.vm.ProductModel && item.Id !== id
            );
            if (isDuplicate) {
                addAlert('此產品型號已存在', { type: 'warning' });
                return false;
            }
        }

        return true;
    };

    // ========= 載入分院列表 =========
    this.loadDepartments = () => {
        global.api.select.department({ excludeTaipei: true })
            .then(res => departments.value = res.data || [])
            .catch(() => addAlert('取得分院失敗', { type: 'danger' }));
    };

    // ========= 保存 =========
    this.save = () => {
        if (!this.validate()) return;

        const method = this.vm.Id ? global.api.admin.equipmentupdate : global.api.admin.equipmentinsert;

        const body = {
            ...this.vm,
            Type: Number(this.vm.Type),
            DepartmentId: selectedDepartment.value || userDepartmentId.value || this.vm.DepartmentId
        };

        method({ body })
            .then(() => {
                addAlert(this.vm.Id ? '編輯成功' : '新增成功');
                this.getList();
                const modalElement = document.querySelector('#equipmentModal');
                const modal = window.bootstrap?.Modal?.getInstance(modalElement);
                if (modal) modal.hide();
            })
            .catch(error => {
                addAlert(error.details || '操作失敗', { type: 'danger', click: error.download });
            });
    };

    // ========= 刪除 =========
    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.equipmentdelete({ body: { id } })
                .then(() => {
                    addAlert('刪除成功');
                    this.getList();
                })
                .catch(error => {
                    addAlert(getMessage(error) || '刪除失敗', { type: 'danger', click: error.download });
                });
        }
    };
};

// ========= 載入使用者資訊 =========
const loadCurrentUser = async () => {
    try {
        const userRes = await global.api.auth.me();
        currentUser = userRes.data;
        isAdmin.value = currentUser.IsAdmin || false;
        userDepartmentId.value = currentUser.DepartmentId;
        userDepartmentName.value = currentUser.DepartmentName || '';

        console.log('✅ 使用者資訊:', {
            name: currentUser.Name,
            isAdmin: isAdmin.value,
            departmentId: userDepartmentId.value,
            departmentName: userDepartmentName.value
        });
    } catch (err) {
        console.error('❌ 無法取得使用者資訊:', err);
    }
};

// ========= Vue 配置 =========
window.$config = {
    setup() {
        // 監聽分院變更
        watch(selectedDepartment, (departmentId) => {
            if (isEditing.value) return;

            equipment.buildings.splice(0);
            equipment.floorOptions.splice(0);
            equipment.roomOptions.splice(0);
            equipment.vm.Building = '';
            equipment.vm.Floor = '';
            equipment.vm.RoomId = null;

            if (!departmentId) return;

            // ✅ 後端會自動過濾
            global.api.select.buildingsbydepartment()
                .then(res => copy(equipment.buildings, res.data || []));
        });

        onMounted(async () => {
            await loadCurrentUser();

            // 只有管理者才載入所有分院
            if (isAdmin.value) {
                console.log('✅ 是管理者,載入分院列表');
                equipment.loadDepartments();
            } else {
                console.log('⚠️ 不是管理者,跳過載入分院列表');
            }

            equipment.getList();
        });

        return {
            equipment,
            departments,
            selectedDepartment,
            isAdmin,
            userDepartmentName
        };
    }
};