// Views/Admin/SysConfig.cshtml.js
import global from '/global.js';
const { reactive, ref, onMounted } = Vue;

const sysConfig = new function () {
    this.isAdmin = ref(false);  // 是否為管理員
    this.departmentId = ref(null);  // 使用者的分院 ID

    this.settings = reactive({
        isRegistrationOpen: true,
        paymentDeadlineDays: 7,
        minAdvanceBookingDays: 7,
        managerEmail: ''
    });

    // 載入用戶資訊
    this.loadUserInfo = async () => {
        try {
            const res = await global.api.auth.me();
            this.isAdmin.value = res.data.IsAdmin || false;
            this.departmentId.value = res.data.DepartmentId || null;
        } catch (err) {
            console.error('❌ 無法取得使用者資訊:', err);
        }
    };

    // 載入設定 (GET /admin/api/sysconfig)
    this.loadSettings = () => {
        global.api.sysconfig.getall()
            .then((response) => {
                const data = response.data;
                console.log('[SysConfig] 載入的資料:', data);

                if (data) {
                    this.settings.isRegistrationOpen = data.GUEST_REGISTRATION === 'true';
                    this.settings.paymentDeadlineDays = parseInt(data.PAYMENT_DEADLINE_DAYS) || 7;
                    this.settings.minAdvanceBookingDays = parseInt(data.MIN_ADVANCE_BOOKING_DAYS) || 7;
                    this.settings.managerEmail = data.MANAGER_EMAIL || '';
                }
            })
            .catch(error => {
                console.error('[SysConfig] 載入設定失敗:', error);
                addAlert('載入設定失敗', { type: 'danger', click: error.download });
            });
    };

    // 儲存設定 (POST /api/sysconfig/update)
    this.saveSettings = () => {
        // 準備要更新的設定
        const configs = [];

        // 非 Admin 用戶需要傳遞 DepartmentId
        const departmentId = this.isAdmin.value ? null : this.departmentId.value;

        // 只有 Admin 才能修改「是否開啟訪客註冊」(全局設定)
        if (this.isAdmin.value) {
            configs.push({ configKey: 'GUEST_REGISTRATION', configValue: this.settings.isRegistrationOpen.toString() });
        }

        // 其他設定都可以修改
        configs.push(
            { configKey: 'PAYMENT_DEADLINE_DAYS', configValue: this.settings.paymentDeadlineDays.toString() },
            { configKey: 'MIN_ADVANCE_BOOKING_DAYS', configValue: this.settings.minAdvanceBookingDays.toString() },
            { configKey: 'MANAGER_EMAIL', configValue: this.settings.managerEmail }
        );

        console.log('[SysConfig] saveSettings() configs =', configs, 'departmentId =', departmentId);

        // 一次送出所有設定
        global.api.sysconfig.update({
            body: {
                configs: configs,
                departmentId: departmentId
            }
        })
            .then(() => {
                console.log('[SysConfig] 所有設定已儲存');
                addAlert('設定已儲存', { type: 'success' });
                this.loadSettings(); // 重新載入確認
            })
            .catch(error => {
                console.error('[SysConfig] 儲存失敗:', error);
                // 顯示後端回傳的錯誤訊息
                const errorMsg = error?.message || '儲存設定失敗';
                addAlert(errorMsg, { type: 'danger' });
            });
    };

    // 重設為預設值
    this.resetSettings = () => {
        if (!confirm('確定要重設為預設值嗎?')) return;

        this.settings.isRegistrationOpen = true;
        this.settings.paymentDeadlineDays = 7;
        this.settings.minAdvanceBookingDays = 14;
        this.settings.managerEmail = '';

        addAlert('已重設為預設值 (請記得儲存設定)', { type: 'info' });
    };
};

window.$config = {
    setup: () => new function () {
        this.settings = sysConfig.settings;
        this.isAdmin = sysConfig.isAdmin;
        this.saveSettings = sysConfig.saveSettings;
        this.resetSettings = sysConfig.resetSettings;

        onMounted(async () => {
            await sysConfig.loadUserInfo();
            sysConfig.loadSettings();
        });
    }
};