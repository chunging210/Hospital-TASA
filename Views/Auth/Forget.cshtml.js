import global from '/global.js';

const { reactive } = Vue;

const tokenId = document.getElementById('tokenId')?.value;

window.$config = {
    components: {},
    setup: () => new function () {
        this.vm = reactive({ Password: '', ConfirmPassword: '' });
        this.isExpiredRedirect = new URLSearchParams(location.search).get('reason') === 'expired';

        // 密碼規則驗證
        this.isValidPassword = (password) => {
            if (password.length < 8) return false;
            let categories = 0;
            if (/[a-z]/.test(password)) categories++;
            if (/[A-Z]/.test(password)) categories++;
            if (/\d/.test(password)) categories++;
            if (/[@$!%*?&\-_#^]/.test(password)) categories++;
            return categories >= 3;
        };
        this.passwordRuleMessage = '密碼須至少 8 個字元，並包含大寫字母、小寫字母、數字、特殊符號（@$!%*?&-_#^）中的任意三種';

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
                        fetch('/api/auth/logout', { method: 'POST' }).finally(() => {
                            location.href = '/';
                        });
                    }, 1500);
                })
                .catch(error => {
                    addAlert(error.details || error.message || '重設密碼失敗', { type: 'danger', click: error.download });
                });
        };

        this.copyyear = new Date().getFullYear();
    }
}
