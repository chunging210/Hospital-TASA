import global from '/global.js';

const { reactive } = Vue;

const tokenId = document.getElementById('tokenId')?.value;

window.$config = {
    components: {},
    setup: () => new function () {
        this.vm = reactive({ Password: '', ConfirmPassword: '' });

        // 密碼規則驗證
        this.isValidPassword = (password) => {
            const regexPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{10,}$/;
            return regexPattern.test(password);
        };
        this.passwordRuleMessage = '密碼須至少 10 個字元，並包含大寫字母、小寫字母、數字及特殊字元（@$!%*?&）';

        this.resetPassword = () => {
            if (this.vm.Password !== this.vm.ConfirmPassword) {
                addAlert('兩次輸入的密碼不一致', { type: 'warning' });
                return;
            }
            if (!this.vm.Password) {
                addAlert('請輸入新密碼', { type: 'warning' });
                return;
            }
            // 密碼規則檢查
            if (!this.isValidPassword(this.vm.Password)) {
                addAlert(this.passwordRuleMessage, { type: 'warning' });
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
