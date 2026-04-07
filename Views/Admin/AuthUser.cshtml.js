// Admin/AuthUser.cshtml
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

class VM {
    Id = null;
    Name = '';
    Account = '';
    Email = '';
    DepartmentId = null;
    UnitName = '';  // 部門
    Role = [];
    IsEnabled = true;
}

const tabs = new function () {
    this.select = ref('IsAdmin');
    this.list = [{ text: '管理者', value: 'IsAdmin' }, { text: '主任', value: 'IsDirector' }, { text: '總務', value: 'IsAccountant' }, { text: '一般使用者', value: 'IsNormal' }, { text: '一般職員', value: 'IsStaff' }];
    this.click = (value) => {
        this.select.value = value;
    }
}

const department = new function () {
    this.tree = reactive([]);
    this.gettree = () => {
        global.api.select.departmenttree()
            .then((response) => {
                copy(this.tree, response.data);
            });
    }
}

const role = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.role()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

const costCenter = new function () {
    this.list = reactive([]);
    this.search = ref('');
    this.filtered = ref([]);
    this.showDropdown = ref(false);

    this.getList = () => {
        global.api.select.costcenters()
            .then((response) => {
                copy(this.list, response.data);
                this.filtered.value = response.data;
            });
    }

    this.filter = () => {
        const keyword = this.search.value.toLowerCase().trim();
        if (!keyword) {
            this.filtered.value = this.list;
            return;
        }
        this.filtered.value = this.list.filter(item => {
            const codeMatch = item.Code?.toLowerCase().includes(keyword);
            const nameMatch = item.Name?.toLowerCase().includes(keyword);
            return codeMatch || nameMatch;
        });
    }

    this.select = (item) => {
        authuser.vm.UnitName = item.Name;
        this.search.value = item.Name;
        this.showDropdown.value = false;
    }

    this.onFocus = () => {
        this.showDropdown.value = true;
        this.filtered.value = this.list;
    }

    // 點擊外部區域關閉下拉選單
    this.closeDropdown = () => {
        this.showDropdown.value = false;
    }

    // 當編輯時，設定搜尋框的值
    this.setSearchFromVM = () => {
        this.search.value = authuser.vm.UnitName || '';
    }

    // 初始化：點擊外部區域關閉下拉選單
    this.initClickOutside = () => {
        document.addEventListener('click', (e) => {
            const container = document.querySelector('.unit-dropdown-container');
            if (container && !container.contains(e.target)) {
                this.showDropdown.value = false;
            }
        });
    }
}

const authuser = new function () {
    this.query = reactive({ keyword: '' });
    this.list = reactive([]);
    this.getList = () => {
        global.api.admin.userlist({ body: this.query })
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }
    this.vm = reactive(new VM());
    this.showModal = () => {
        const modalEl = document.querySelector('#authUserModal');
        if (modalEl) {
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        }
    }
    this.getVM = (id) => {
        if (id) {
            global.api.admin.userdetail({ body: { id } })
                .then((response) => {
                    console.log('API 回傳:', response.data);
                    console.log('DepartmentId:', response.data.DepartmentId);
                    copy(this.vm, response.data);
                    console.log('vm.DepartmentId:', this.vm.DepartmentId);
                    console.log('department.tree:', department.tree);
                    costCenter.setSearchFromVM();  // 設定部門搜尋框的值
                    this.showModal();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            costCenter.search.value = '';  // 清空搜尋框
        }
    }
    this.save = () => {
        const method = this.vm.Id ? global.api.admin.userupdate : global.api.admin.userinsert;
        method({ body: this.vm })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                const modalEl = document.querySelector('#authUserModal');
                const modal = window.bootstrap?.Modal?.getInstance(modalEl);
                if (modal) modal.hide();
            })
            .catch(error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }

    // 拒絕帳號相關
    this.rejectVM = reactive({ userId: null, userName: '', reason: '' });
    this.showRejectModal = (item) => {
        this.rejectVM.userId = item.Id;
        this.rejectVM.userName = item.Name;
        this.rejectVM.reason = '';
        const modalEl = document.querySelector('#rejectUserModal');
        if (modalEl) {
            const modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        }
    }
    this.confirmReject = () => {
        global.api.admin.userreject({ body: { userId: this.rejectVM.userId, reason: this.rejectVM.reason } })
            .then(() => {
                addAlert('已拒絕該帳號申請');
                this.getList();
                const modalEl = document.querySelector('#rejectUserModal');
                const modal = window.bootstrap?.Modal?.getInstance(modalEl);
                if (modal) modal.hide();
            })
            .catch(error => {
                addAlert(error.details || '操作失敗', { type: 'danger', click: error.download });
            });
    }
}

window.$config = {
    setup: () => new function () {
        this.tabs = tabs;
        this.department = department;
        this.role = role;
        this.costCenter = costCenter;
        this.authuser = authuser;
        this.departmentFilter = ref(null);
        this.unitFilterInput = ref('');
        this.unitFilterDropdown = ref(false);
        this.unitFilterFiltered = computed(() => {
            const kw = this.unitFilterInput.value.toLowerCase().trim();
            if (!kw) return costCenter.list;
            return costCenter.list.filter(x => x.Name.toLowerCase().includes(kw));
        });
        this.staffList = computed(() => authuser.list.filter(x => x.IsStaff));
        const departmentFilter = this.departmentFilter;
        const unitFilterInput = this.unitFilterInput;
        this.tabData = computed(() => authuser.list.filter(x => {
            if (!x[tabs.select.value]) return false;
            if (departmentFilter.value && x.DepartmentId?.toLowerCase() !== departmentFilter.value?.toLowerCase()) return false;
            const kw = unitFilterInput.value.trim();
            if (kw && !x.UnitName?.toLowerCase().includes(kw.toLowerCase())) return false;
            return true;
        }));

        watch(() => authuser.query.keyword, () => {
            authuser.getList(1);
        });

        const unitFilterDropdown = this.unitFilterDropdown;
        onMounted(() => {
            department.gettree();
            role.getList();
            costCenter.getList();
            costCenter.initClickOutside();
            authuser.getList();
            document.addEventListener('click', (e) => {
                const container = document.querySelector('.unit-filter-container');
                if (container && !container.contains(e.target)) {
                    unitFilterDropdown.value = false;
                }
            });
        });
    }
}