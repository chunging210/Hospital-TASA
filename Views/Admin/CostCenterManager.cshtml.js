// Views/Admin/CostCenterManager.cshtml.js
import global from '/global.js';
const { reactive, ref, onMounted, watch } = Vue;
const debounce = (fn, delay = 300) => { let t; return (...args) => { clearTimeout(t); t = setTimeout(() => fn(...args), delay); }; };

class VM {
    Id = null;
    CostCenterCode = '';
    DepartmentId = null;
    ManagerId = null;
}

// 全域狀態
const isAdmin = ref(false);
const isGlobalAdmin = ref(false);  // 全院管理者（Admin + 無分院）
const userDepartmentId = ref(null);
const userDepartmentName = ref('');

const department = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.department()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

const costCenter = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.costcenters()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

const internalUser = new function () {
    this.list = reactive([]);
    this.getList = (departmentId) => {
        if (!departmentId) {
            this.list.length = 0;
            return;
        }
        global.api.select.internaluser({ body: { departmentId } })
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

const query = reactive({
    departmentId: null,
    costCenterCode: null,
    keyword: ''
});

const manager = new function () {
    this.list = reactive([]);
    this.getList = () => {
        const params = {};
        if (query.departmentId) params.departmentId = query.departmentId;
        if (query.costCenterCode) params.costCenterCode = query.costCenterCode;
        if (query.keyword) params.keyword = query.keyword;

        global.api.admin.costcentermanagerlist({ body: params })
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }

    this.vm = reactive(new VM());

    this.showModal = () => {
        const modalEl = document.querySelector('#managerModal');
        if (modalEl) {
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        }
    }

    this.getVM = (id) => {
        if (id) {
            global.api.admin.costcentermanagerdetail({ body: { id } })
                .then((response) => {
                    copy(this.vm, response.data);
                    // 載入該分院的內部人員
                    if (this.vm.DepartmentId) {
                        internalUser.getList(this.vm.DepartmentId);
                    }
                    this.showModal();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            // 分院管理者新增時自動使用自己的分院
            if (!isGlobalAdmin.value && userDepartmentId.value) {
                this.vm.DepartmentId = userDepartmentId.value;
                internalUser.getList(userDepartmentId.value);
            } else {
                internalUser.list.length = 0;
            }
        }
    }

    this.onDepartmentChange = () => {
        this.vm.ManagerId = null;
        internalUser.getList(this.vm.DepartmentId);
    }

    this.save = () => {
        // 驗證
        if (!this.vm.DepartmentId) {
            addAlert('請選擇分院', { type: 'warning' });
            return;
        }
        if (!this.vm.CostCenterCode) {
            addAlert('請選擇成本代碼', { type: 'warning' });
            return;
        }
        if (!this.vm.ManagerId) {
            addAlert('請選擇主管', { type: 'warning' });
            return;
        }

        const method = this.vm.Id
            ? global.api.admin.costcentermanagerupdate
            : global.api.admin.costcentermanagerinsert;

        method({ body: this.vm })
            .then(() => {
                addAlert('操作成功');
                this.getList();
                const modalEl = document.querySelector('#managerModal');
                const modal = window.bootstrap?.Modal?.getInstance(modalEl);
                if (modal) modal.hide();
            })
            .catch(error => {
                addAlert(error.message || error.details || '操作失敗', { type: 'danger', click: error.download });
            });
    }

    this.delete = (id) => {
        if (!confirm('確定要刪除此設定嗎？')) return;

        global.api.admin.costcentermanagerdelete({ body: { id } })
            .then(() => {
                addAlert('刪除成功');
                this.getList();
            })
            .catch(error => {
                addAlert(error.message || error.details || '刪除失敗', { type: 'danger', click: error.download });
            });
    }
}

// 載入使用者資訊
const loadCurrentUser = async () => {
    try {
        const userRes = await global.api.auth.me();
        isAdmin.value = userRes.data.IsAdmin || false;
        userDepartmentId.value = userRes.data.DepartmentId || null;
        userDepartmentName.value = userRes.data.DepartmentName || '';
        // 全院管理者 = Admin 且無分院
        isGlobalAdmin.value = isAdmin.value && !userDepartmentId.value;

        console.log('✅ 使用者資訊:', {
            isAdmin: isAdmin.value,
            isGlobalAdmin: isGlobalAdmin.value,
            departmentId: userDepartmentId.value,
            departmentName: userDepartmentName.value
        });
    } catch (err) {
        console.error('❌ 無法取得使用者資訊:', err);
    }
};

window.$config = {
    setup: () => new function () {
        this.department = department;
        this.costCenter = costCenter;
        this.internalUser = internalUser;
        this.query = query;
        this.manager = manager;
        this.isAdmin = isAdmin;
        this.isGlobalAdmin = isGlobalAdmin;
        this.userDepartmentId = userDepartmentId;
        this.userDepartmentName = userDepartmentName;

        watch(() => query.keyword, debounce(() => {
            manager.getList();
        }));

        onMounted(async () => {
            // 1️⃣ 載入使用者資訊
            await loadCurrentUser();

            // 2️⃣ 只有全院管理者才載入分院列表
            if (isGlobalAdmin.value) {
                department.getList();
            }

            // 3️⃣ 分院管理者預設篩選自己分院
            if (!isGlobalAdmin.value && userDepartmentId.value) {
                query.departmentId = userDepartmentId.value;
            }

            costCenter.getList();
            manager.getList();
        });
    }
}
