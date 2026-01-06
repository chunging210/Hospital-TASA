// Admin/AuthUser.cshtml
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

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
    this.list = [{ text: '管理者', value: 'IsAdmin' }, { text: '一般使用者', value: 'IsNormal' }, { text: '一般職員', value: 'IsStaff' }];
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
    this.offcanvas = {}
    this.vm = reactive(new VM());
    this.getVM = (id) => {
        if (id) {
            global.api.admin.userdetail({ body: { id } })
                .then((response) => {
                    copy(this.vm, response.data);
                    this.offcanvas.show();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            this.offcanvas.show();
        }
    }
    this.save = () => {
        const method = this.vm.Id ? global.api.admin.userupdate : global.api.admin.userinsert;
        method({ body: this.vm })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                this.offcanvas.hide();
            })
            .catch(error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }
}

// ✅ 新增：系統設定物件
const sysConfig = new function () {
    this.isRegistrationOpen = ref(false);  // 用 ref

    this.getRegistrationStatus = () => {
        console.log('🔍 取得註冊開關狀態...');
        global.api.sysconfig.registrationstatus()
            .then((response) => {
                console.log('✅ API 回傳:', response);
                // 正確取得 API 回傳的值
                const isOpen = response.data?.isOpen ?? false;
                this.isRegistrationOpen.value = isOpen;
                console.log('📝 註冊已' + (isOpen ? '開放' : '關閉'));
            })
            .catch(error => {
                console.error('❌ 取得註冊狀態失敗:', error);
                this.isRegistrationOpen.value = false;
            });
    };

    // 切換功能 - 保持原來的邏輯
    this.toggleRegistration = (event) => {
        const newValue = event.target.checked;  // 直接從 checkbox 取值
        console.log('🔀 切換註冊開關:', newValue);

        global.api.sysconfig.registrationtoggle({ body: { isOpen: newValue } })
            .then((response) => {
                console.log('✅ 設定已更新:', response);
                // ✅ 成功後才更新狀態
                this.isRegistrationOpen.value = newValue;
                addAlert(response.message || '設定已更新', { type: 'success' });

                // 通知其他頁面
                window.dispatchEvent(new CustomEvent('registrationStatusChanged', {
                    detail: { isOpen: newValue }
                }));
            })
            .catch(error => {
                console.error('❌ 更新失敗:', error);
                addAlert(error.message || '更新失敗', { type: 'danger' });
                // ❌ 失敗時不改變狀態，checkbox 會自動恢復原值
            });
    };
}

window.$config = {
    setup: () => new function () {
        this.tabs = tabs;
        this.department = department;
        this.role = role;
        this.authuser = authuser;
        this.authuseroffcanvas = ref(null);
        this.staffList = computed(() => authuser.list.filter(x => x.IsStaff));
        this.tabData = computed(() => authuser.list.filter(x => x[tabs.select.value]));
        this.sysConfig = sysConfig;


        onMounted(() => {
            console.log('🚀 onMounted 開始');
            department.gettree();
            role.getList();
            authuser.getList();
            authuser.offcanvas = this.authuseroffcanvas.value;
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