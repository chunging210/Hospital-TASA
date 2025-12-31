// Auth
import global from '/global.js';

const { ref, reactive } = Vue;

class VM {
    Account = ''; Password = ''; Captcha = '';
}

class RegisterVM {
    name = '';
    email = '';
    password = '';
    confirmPassword = '';
    hospital = ''; // 空字串 = 一般會員
    // agreeTerms = false;
    departmentId = null;
}

const captcha = new function () {
    this.url = '/api/captcha';
    this.refresh = () => `${captcha.url}?${new Date().getTime()}`;
}

window.$config = {
    components: {},
    setup: () => new function () {
        this.vm = reactive(new VM());
        this.departments = ref([]);
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

        /* ===== Register ===== */
        this.guestForm = reactive(new RegisterVM());

        this.registerGuest = () => {

            // ✅ 前端基本檢查
            if (this.guestForm.password !== this.guestForm.confirmPassword) {
                addAlert('兩次輸入的密碼不一致', { type: 'warning' });
                return;
            }

            // if (!this.guestForm.agreeTerms) {
            //     addAlert('請先同意服務條款與隱私權政策', { type: 'warning' });
            //     return;
            // }

            const payload = {
                Name: this.guestForm.name,
                Email: this.guestForm.email,
                Password: this.guestForm.password,
                ConfirmPassword: this.guestForm.confirmPassword,
                DepartmentId: this.guestForm.departmentId
            };

            global.api.auth.register({ body: payload })
                .then(() => {
                    addAlert('註冊成功，請使用帳號登入', { type: 'success' });

                    const modal = bootstrap.Modal.getInstance(
                        document.getElementById('guestRegisterModal')
                    );
                    modal?.hide();

                    Object.assign(this.guestForm, new RegisterVM());
                })
                .catch(error => {
                    addAlert(error.message || '註冊失敗', {
                        type: 'danger',
                        click: error.download
                    });
                });
        };

        global.api.select.department()
            .then(res => {
                console.log('res:', res);
                console.log('res.data:', res.data);
                this.departments.value = res.data;  // ✅ 用 res.data，不是 res
            })
            .catch(() => {
                addAlert('無法取得分院資料', { type: 'danger' });
            });


        this.copyyear = new Date().getFullYear();
    }
}