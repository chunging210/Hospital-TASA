// Admin/AuthUser.cshtml
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

class VM {
    Id = null;
    Name = '';
    Account = '';
    Email = '';
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
                    copy(this.vm, response.data);
                    this.showModal();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
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
}

window.$config = {
    setup: () => new function () {
        this.tabs = tabs;
        this.department = department;
        this.role = role;
        this.authuser = authuser;
        this.staffList = computed(() => authuser.list.filter(x => x.IsStaff));
        this.tabData = computed(() => authuser.list.filter(x => x[tabs.select.value]));

        watch(() => authuser.query.keyword, () => {
            authuser.getList(1);
        });

        onMounted(() => {
            console.log('🚀 onMounted 開始');
            department.gettree();
            role.getList();
            authuser.getList();
            sysConfig.getRegistrationStatus();

            window.addEventListener('registrationStatusChanged', (event) => {
                console.log('👀 監聽到註冊狀態變化:', event.detail);
                sysConfig.isRegistrationOpen.value = event.detail.isOpen;
            });
            // ✅ 延遲 log，確認資料已載入
            setTimeout(() => {
                console.log('⏱️ 1 秒後 - authuser.list 筆數:', authuser.list.length);
                console.log('⏱️ 1 秒後 - 第一筆資料:', authuser.list[0]);
                console.log('⏱️ 1 秒後 - IsStaff 欄位:', authuser.list[0]?.IsStaff);
                console.log('⏱️ 1 秒後 - 一般職員總數:', authuser.list.filter(x => x.IsStaff).length);
            }, 1000);
        });
    }
}