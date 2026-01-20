// Views/Admin/SysConfig.cshtml.js
import global from '/global.js';
const { reactive, onMounted } = Vue;

const sysConfig = new function () {
    this.settings = reactive({
        isRegistrationOpen: true,
        paymentDeadlineDays: 7,
        minAdvanceBookingDays: 7,
        managerEmail: '',
        autoRelease: true
    });

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
                    this.settings.autoRelease = data.AUTO_RELEASE_AFTER_DEADLINE === 'true';
                }
            })
            .catch(error => {
                console.error('[SysConfig] 載入設定失敗:', error);
                addAlert('載入設定失敗', { type: 'danger', click: error.download });
            });
    };

    // 儲存設定 (POST /admin/api/sysconfig)
    this.saveSettings = () => {
        // 準備要更新的設定
        const configs = [
            { configKey: 'GUEST_REGISTRATION', configValue: this.settings.isRegistrationOpen.toString() },
            { configKey: 'PAYMENT_DEADLINE_DAYS', configValue: this.settings.paymentDeadlineDays.toString() },
            { configKey: 'MIN_ADVANCE_BOOKING_DAYS', configValue: this.settings.minAdvanceBookingDays.toString() },
            { configKey: 'MANAGER_EMAIL', configValue: this.settings.managerEmail },
            { configKey: 'AUTO_RELEASE_AFTER_DEADLINE', configValue: this.settings.autoRelease.toString() }
        ];

        console.log('[SysConfig] saveSettings() configs =', configs);

        // 逐一更新設定
        let updatePromises = configs.map(config => {
            return global.api.sysconfig.update({
                body: {
                    configKey: config.configKey,
                    configValue: config.configValue
                }
            });
        });

        Promise.all(updatePromises)
            .then(() => {
                console.log('[SysConfig] 所有設定已儲存');
                addAlert('設定已儲存', { type: 'success' });
                this.loadSettings(); // 重新載入確認
            })
            .catch(error => {
                console.error('[SysConfig] 儲存失敗:', error);
                addAlert('儲存設定失敗', { type: 'danger', click: error.download });
            });
    };

    // 重設為預設值
    this.resetSettings = () => {
        if (!confirm('確定要重設為預設值嗎?')) return;

        this.settings.isRegistrationOpen = true;
        this.settings.paymentDeadlineDays = 7;
        this.settings.minAdvanceBookingDays = 7;
        this.settings.managerEmail = '';
        this.settings.autoRelease = true;

        addAlert('已重設為預設值 (請記得儲存設定)', { type: 'info' });
    };
};

window.$config = {
    setup: () => new function () {
        this.settings = sysConfig.settings;
        this.saveSettings = sysConfig.saveSettings;
        this.resetSettings = sysConfig.resetSettings;

        onMounted(() => {
            sysConfig.loadSettings();
        });
    }
};