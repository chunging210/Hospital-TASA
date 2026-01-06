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
        this.sysConfig = sysConfig;
        this.isRegistrationOpen = sysConfig.isRegistrationOpen;
        this.isRegistrationLoaded = sysConfig.isRegistrationLoaded;

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

        this.onClickRegister = () => {
            const modal = new bootstrap.Modal(
                document.getElementById('guestRegisterModal')
            );
            modal.show();
        };

        /* ===== Register ===== */
        this.guestForm = reactive(new RegisterVM());
        this.sysConfig = sysConfig;
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