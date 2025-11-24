// Auth
import global from '/global.js';

const { ref, reactive } = Vue;

class VM {
    Account = ''; Password = ''; Captcha = '';
}

const captcha = new function () {
    this.url = '/api/captcha';
    this.refresh = () => `${captcha.url}?${new Date().getTime()}`;
}

window.$config = {
    components: {},
    setup: () => new function () {
        this.vm = reactive(new VM());

        this.captchaSrc = ref(captcha.url);
        this.refreshCaptcha = () => {
            this.captchaSrc.value = captcha.refresh();
        }
        this.upper = () => {
            this.vm.Captcha = this.vm.Captcha.toUpperCase();
        }

        this.pwType = ref('password');
        this.chaangePwType = () => {
            this.pwType.value = this.pwType.value == 'password' ? 'text' : 'password';
        }

        this.auth = () => {
            global.api.auth.login({ body: this.vm })
                .then(response => {
                    location.href = '/api/auth/redirection';
                })
                .catch(error => {
                    addAlert('登入失敗', { type: 'danger', click: error.download });
                    this.refreshCaptcha();
                });
        }

        this.copyyear = new Date().getFullYear();
    }
}