import global from '/global.js';

const { reactive } = Vue;

const tokenId = document.getElementById('tokenId')?.value;

window.$config = {
    components: {},
    setup: () => new function () {
        this.vm = reactive({ Password: '', ConfirmPassword: '' });

        this.resetPassword = () => {
            if (this.vm.Password !== this.vm.ConfirmPassword) {
                addAlert('兩次輸入的密碼不一致', { type: 'warning' });
                return;
            }
            if (!this.vm.Password) {
                addAlert('請輸入新密碼', { type: 'warning' });
                return;
            }

            global.api.password.forget({
                body: {
                    Id: tokenId,
                    Password: this.vm.Password
                }
            })
                .then(() => {
                    addAlert('密碼已重設，請重新登入', { type: 'success' });
                    setTimeout(() => {
                        location.href = '/';
                    }, 1500);
                })
                .catch(error => {
                    addAlert(error.details || error.message || '重設密碼失敗', { type: 'danger', click: error.download });
                });
        };

        this.copyyear = new Date().getFullYear();
    }
}
