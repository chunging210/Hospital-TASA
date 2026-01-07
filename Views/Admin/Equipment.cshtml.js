// Admin/Equipment - 修正級聯邏輯
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

class VM {
    Id = null;
    Name = '';
    ProductModel = '';
    Type = '';
    RoomId = null;
    Building = '';
    Floor = '';              // ✅ 樓層
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
const isEditing = ref(false);
const departments = ref([]);
const selectedDepartment = ref('');

const equipment = new function () {
    // ========= 查詢參數 =========
    this.query = reactive({
        keyword: '',
        type: ''
    });

    this.list = reactive([]);
    this.buildings = reactive([]);        // 大樓列表
    this.floorOptions = reactive([]);     // 樓層列表
    this.roomOptions = reactive([]);      // 會議室列表

    // ========= 取得列表 =========
    this.getList = () => {
        global.api.admin.equipmentlist({ body: this.query })
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    };


    // ========= 大樓變更時取得樓層 =========
    this.onBuildingChange = () => {
        this.floorOptions.splice(0);
        this.roomOptions.splice(0);
        this.vm.Floor = '';
        this.vm.RoomId = null;

        if (!this.vm.Building) return;

        global.api.select.floorsbybuilding({
            body: {
                departmentId: selectedDepartment.value,
                building: this.vm.Building
            }
        }).then(res => {
            copy(this.floorOptions, res.data || []);
        });
    };

    // ========= 樓層變更時取得會議室 =========
    this.onFloorChange = () => {
        // 清空會議室選擇
        this.vm.RoomId = null;

        if (!this.vm.Building || !this.vm.Floor) {
            this.roomOptions.splice(0);
            return;
        }

        // 調用 roomsbyfloor API 取得該大樓/樓層的會議室
        global.api.select.roomsbyfloor({ body: { Building: this.vm.Building, Floor: this.vm.Floor } })
            .then((response) => {
                if (Array.isArray(response.data)) {
                    copy(this.roomOptions, response.data);
                    console.log('✅ 會議室列表已更新:', this.roomOptions);
                }
            })
            .catch(error => {
                addAlert('取得會議室失敗', { type: 'danger', click: error.download });
                this.roomOptions.splice(0);
            });
    };

    // ========= VM 初始化 =========
    this.vm = reactive(new VM());

    // ========= 打開 Modal (新增或編輯) =========
    this.getVM = async (id) => {

        if (!id) {
            // 新增
            isEditing.value = false;
            copy(this.vm, new VM());
            selectedDepartment.value = '';
            return;
        }

        // ===== 編輯 =====
        isEditing.value = true;

        const res = await global.api.admin.equipmentdetail({ body: { id } });
        copy(this.vm, res.data);

        // 同步分院（不觸發清空）
        selectedDepartment.value = this.vm.DepartmentId;

        // 載入大樓
        const bRes = await global.api.select.buildingsbydepartment({
            body: { departmentId: selectedDepartment.value }
        });
        copy(this.buildings, bRes.data || []);

        // 載樓層
        const fRes = await global.api.select.floorsbybuilding({
            body: {
                departmentId: selectedDepartment.value,
                building: this.vm.Building
            }
        });
        copy(this.floorOptions, fRes.data || []);

        // 載會議室
        const rRes = await global.api.select.roomsbyfloor({
            body: {
                Building: this.vm.Building,
                Floor: this.vm.Floor
            }
        });
        copy(this.roomOptions, rRes.data || []);

        isEditing.value = false;
    };



    // ========= 驗證 =========
    this.validate = () => {
        // 1️⃣ 產品名稱必填
        if (!this.vm.Name || this.vm.Name.trim() === '') {
            addAlert('產品名稱為必填', { type: 'warning' });
            return false;
        }

        // 2️⃣ 產品類型必填
        if (!this.vm.Type || this.vm.Type === '') {
            addAlert('產品類型為必填', { type: 'warning' });
            return false;
        }

        if (selectedDepartment.value) {

            if (!this.vm.Building || this.vm.Building === '') {
                addAlert('已選擇分院，請選擇大樓', { type: 'warning' });
                return false;
            }

            if (!this.vm.Floor || this.vm.Floor === '') {
                addAlert('已選擇分院，請選擇樓層', { type: 'warning' });
                return false;
            }

            if (!this.vm.RoomId) {
                addAlert('已選擇分院，請選擇會議室', { type: 'warning' });
                return false;
            }
        }

        // 4️⃣ 公有設備(8)/攤位租借(9) 必須有租借金額
        if ([8, 9].includes(Number(this.vm.Type)) && this.vm.RentalPrice <= 0) {
            addAlert('公有設備和攤位租借必須設定租借金額', { type: 'warning' });
            return false;
        }

        // 5️⃣ 檢核重複（根據設備類型檢核不同欄位）
        const type = Number(this.vm.Type);
        const id = this.vm.Id;

        if (type === 9) {
            // 攤位租借(9)：檢核名稱
            const isDuplicate = this.list.some(item =>
                item.Name === this.vm.Name && item.Id !== id
            );
            if (isDuplicate) {
                addAlert('此攤位名稱已存在', { type: 'warning' });
                return false;
            }
        } else if ([1, 2, 3, 4, 8].includes(type)) {
            // 其他類型(1,2,3,4,8)：檢核型號
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

    this.loadDepartments = () => {
        global.api.select.department()
            .then(res => {
                departments.value = res.data || [];
            })
            .catch(() => {
                addAlert('取得分院失敗', { type: 'danger' });
            });
    };

    // ========= 保存 =========
    this.save = () => {
        if (!this.validate()) return;

        const method = this.vm.Id ? global.api.admin.equipmentupdate : global.api.admin.equipmentinsert;

        const body = {
            ...this.vm,
            Type: Number(this.vm.Type)
        };

        method({ body })
            .then((response) => {
                addAlert(this.vm.Id ? '編輯成功' : '新增成功');
                this.getList();
                const modalElement = document.querySelector('#equipmentModal');
                const modal = window.bootstrap?.Modal?.getInstance(modalElement);
                if (modal) {
                    modal.hide();
                }
            })
            .catch(error => {
                addAlert(error.details || '操作失敗', { type: 'danger', click: error.download });
            });
    };

    // ========= 刪除 =========
    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.equipmentdelete({ body: { id } })
                .then((response) => {
                    addAlert('刪除成功');
                    this.getList();
                })
                .catch(error => {
                    addAlert(getMessage(error) || '刪除失敗', { type: 'danger', click: error.download });
                });
        }
    };
};

window.$config = {
    setup() {
        watch(selectedDepartment, (departmentId) => {

            if (isEditing.value) return;

            equipment.buildings.splice(0);
            equipment.floorOptions.splice(0);
            equipment.roomOptions.splice(0);

            equipment.vm.Building = '';
            equipment.vm.Floor = '';
            equipment.vm.RoomId = null;

            if (!departmentId) return;

            global.api.select.buildingsbydepartment({
                body: { departmentId }
            }).then(res => {
                copy(equipment.buildings, res.data || []);
            });
        });

        onMounted(() => {
            equipment.loadDepartments();
            equipment.getList();
        });

        return {
            equipment,
            departments,
            selectedDepartment
        };
    }
};