// Auth/Profiles
import global from '/global.js';
const { reactive, computed, onMounted } = Vue;

class VM { Id = null; Name = ''; Email = ''; Password = ''; Password2 = ''; }

const personal = new function () {
    this.vm = reactive(new VM());
    // 取得個人資料
    this.getVM = () => {
        global.api.profiles.detail()
            .then((response) => {
                copy(this.vm, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }
    this.save = () => {
        if (this.vm.Password && this.vm.Password !== this.vm.Password2) {
            addAlert('密碼與確認密碼不相符', { type: 'danger' });
            return;
        }
        global.api.profiles.update({ body: this.vm })
            .then(() => {
                addAlert('資料已更新');
                // 清空密碼欄位
                personal.vm.Password = '';
                personal.vm.Password2 = '';
                // 重新取得資料
                personal.getVM();
            })
            .catch(async error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }
    this.resetPassword = () => {
        this.vm.Password = '';
        this.vm.Password2 = '';
    }
}

class DelegateVM { Id = null; DelegateUserId = ''; DelegateUserName = ''; StartDate = ''; EndDate = ''; IsEnabled = true; }

const delegateSection = new function () {
    this.vm = reactive(new DelegateVM());
    this.users = reactive([]);
    this.isActive = computed(() => {
        if (!delegateSection.vm.StartDate || !delegateSection.vm.EndDate) return false;
        const today = new Date().toISOString().slice(0, 10);
        return delegateSection.vm.StartDate <= today && delegateSection.vm.EndDate >= today;
    });

    this.load = () => {
        global.api.profiles.delegate()
            .then(response => {
                if (response.data) {
                    copy(delegateSection.vm, response.data);
                }
            })
            .catch(() => { });

        const departmentId = document.getElementById('userDepartmentId')?.value || '';
        const currentUserId = document.getElementById('userId')?.value || '';
        global.api.select.internaluser({ body: { departmentId } })
            .then(response => {
                const filtered = response.data.filter(u => u.Id !== currentUserId);
                delegateSection.users.splice(0, delegateSection.users.length, ...filtered);
            })
            .catch(() => { });
    };

    this.save = () => {
        if (!delegateSection.vm.DelegateUserId) {
            addAlert('請選擇代理人', { type: 'danger' });
            return;
        }
        if (!delegateSection.vm.StartDate || !delegateSection.vm.EndDate) {
            addAlert('請設定委派日期範圍', { type: 'danger' });
            return;
        }
        if (delegateSection.vm.StartDate > delegateSection.vm.EndDate) {
            addAlert('開始日期不得晚於結束日期', { type: 'danger' });
            return;
        }
        global.api.profiles.saveDelegate({ body: delegateSection.vm })
            .then(() => {
                addAlert('委派設定已儲存');
                delegateSection.load();
            })
            .catch(error => {
                addAlert(error.details || '儲存失敗', { type: 'danger' });
            });
    };

    this.remove = () => {
        if (!confirm('確定要取消委派嗎？')) return;
        global.api.profiles.removeDelegate()
            .then(() => {
                addAlert('委派已取消');
                Object.assign(delegateSection.vm, new DelegateVM());
            })
            .catch(error => {
                addAlert(error.details || '取消失敗', { type: 'danger' });
            });
    };
}

window.$config = {
    setup: () => new function () {
        this.personal = personal;
        this.delegate = delegateSection;
        onMounted(() => {
            personal.getVM();
            delegateSection.load();
            window.addEventListener('ctrls', () => personal.save());
        });
    }
}