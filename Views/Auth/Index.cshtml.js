// Auth
import global from '/global.js';

const { ref, reactive, onMounted } = Vue;

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
    captcha = '';
}

const captcha = new function () {
    this.url = '/api/captcha';
    this.refresh = () => `${captcha.url}?${new Date().getTime()}`;
}

const sysConfig = new function () {
    // ❌ 原本是 true
    this.isRegistrationOpen = ref(false);

    // ✅ 新增：是否已載入完成
    this.isRegistrationLoaded = ref(false);

    this.getRegistrationStatus = () => {
        console.log('🔍 取得註冊開關狀態...');

        global.api.sysconfig.registrationstatus()
            .then((response) => {
                console.log('✅ 註冊狀態:', response);
                console.log('API response.data.isOpen =', response.data?.isOpen);
                // 只有明確 true 才開放
                this.isRegistrationOpen.value = response.data?.isOpen === true;
            })
            .catch(error => {
                console.error('❌ 取得註冊狀態失敗:', error);

                // 失敗時維持關閉（安全）
                this.isRegistrationOpen.value = false;
            })
            .finally(() => {
                // ⭐ 關鍵：標記已載入
                this.isRegistrationLoaded.value = true;
            });
    };
};

window.$config = {
    components: {},
    setup: () => new function () {
        this.vm = reactive(new VM());
        this.departments = ref([]);
        this.captchaSrc = ref(captcha.url);
        this.registerCaptchaSrc = ref(captcha.url);
        this.sysConfig = sysConfig;
        this.isRegistrationOpen = sysConfig.isRegistrationOpen;
        this.isRegistrationLoaded = sysConfig.isRegistrationLoaded;

        this.refreshCaptcha = () => {
            this.captchaSrc.value = captcha.refresh();
        }
        this.refreshRegisterCaptcha = () => {
            this.registerCaptchaSrc.value = captcha.refresh();
        }
        this.upperCaptcha = () => {
            this.guestForm.captcha = this.guestForm.captcha.toUpperCase();
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
                    addAlert(error.details || '登入失敗', { type: 'danger', click: error.download });
                    this.refreshCaptcha();
                });
        }

        this.onClickRegister = () => {
            // 開啟時重新產生驗證碼
            this.refreshRegisterCaptcha();
            this.guestForm.captcha = '';
            const modal = new bootstrap.Modal(
                document.getElementById('guestRegisterModal')
            );
            modal.show();
        };

        /* ===== Forget Password ===== */
        this.forgetForm = reactive({ Account: '' });

        this.onClickForget = () => {
            const modal = new bootstrap.Modal(
                document.getElementById('forgetPasswordModal')
            );
            modal.show();
        };

        this.sendForgetMail = () => {
            global.api.password.forgetmail({ body: this.forgetForm })
                .then(() => {
                    addAlert('已發送重置連結至您的信箱', { type: 'success' });
                    const modal = bootstrap.Modal.getInstance(
                        document.getElementById('forgetPasswordModal')
                    );
                    modal?.hide();
                    this.forgetForm.Account = '';
                })
                .catch(error => {
                    addAlert(error.details || error.message || '發送失敗，請確認帳號是否正確', { type: 'danger', click: error.download });
                });
        };

        /* ===== Register ===== */
        this.guestForm = reactive(new RegisterVM());
        this.sysConfig = sysConfig;

        // 密碼規則驗證
        this.isValidPassword = (password) => {
            const regexPattern = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{10,}$/;
            return regexPattern.test(password);
        };
        this.passwordRuleMessage = '密碼須至少 10 個字元，並包含大寫字母、小寫字母、數字及特殊字元（@$!%*?&）';

        this.registerGuest = () => {

            // ✅ 前端基本檢查
            if (this.guestForm.password !== this.guestForm.confirmPassword) {
                addAlert('兩次輸入的密碼不一致', { type: 'warning' });
                return;
            }

            // 密碼規則檢查
            if (!this.isValidPassword(this.guestForm.password)) {
                addAlert(this.passwordRuleMessage, { type: 'warning' });
                return;
            }

            // if (!this.guestForm.agreeTerms) {
            //     addAlert('請先同意服務條款與隱私權政策', { type: 'warning' });
            //     return;
            // }

            // 驗證碼檢查
            if (!this.guestForm.captcha || this.guestForm.captcha.length < 4) {
                addAlert('請輸入驗證碼', { type: 'warning' });
                return;
            }

            const payload = {
                Name: this.guestForm.name,
                Email: this.guestForm.email,
                Password: this.guestForm.password,
                ConfirmPassword: this.guestForm.confirmPassword,
                DepartmentId: this.guestForm.departmentId,
                Captcha: this.guestForm.captcha
            };

            global.api.auth.register({ body: payload })
                .then(() => {
                    addAlert('註冊成功，待管理者審核後使用帳號登入', { type: 'success' });

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
                    // 重新產生驗證碼
                    this.refreshRegisterCaptcha();
                    this.guestForm.captcha = '';
                });
        };

        global.api.select.department({ body: { excludeTaipei: true } })
            .then(res => {
                console.log('res:', res);
                console.log('res.data:', res.data);
                this.departments.value = res.data;  // ✅ 用 res.data，不是 res
            })
            .catch(() => {
                addAlert('無法取得分院資料', { type: 'danger' });
            });


        onMounted(() => {
            console.log('🚀 登入頁面 onMounted 開始');

            // ✅ 取得註冊開關狀態
            sysConfig.getRegistrationStatus();

            // ✅ 監聽管理員頁面的註冊狀態變化
            // window.addEventListener('registrationStatusChanged', (event) => {
            //     console.log('👀 監聽到註冊狀態變化:', event.detail);
            //     sysConfig.isRegistrationOpen.value = event.detail.isOpen;
            // });
        });

        this.copyyear = new Date().getFullYear();
    }
}