// Auth/Profiles
import global from '/global.js';
const { reactive, onMounted } = Vue;

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

window.$config = {
    setup: () => new function () {
        this.personal = personal;
        onMounted(() => {
            personal.getVM();
            window.addEventListener('ctrls', () => personal.save());
        });
    }
}