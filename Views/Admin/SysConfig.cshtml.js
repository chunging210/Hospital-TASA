// Views/Admin/SysConfig.cshtml.js
import global from '/global.js';
const { reactive, ref, onMounted } = Vue;

const sysConfig = new function () {
    this.isAdmin = ref(false);  // 是否為管理員
    this.isGlobalAdmin = ref(false);  // 是否為全院管理者（Admin + 無分院）
    this.departmentId = ref(null);  // 使用者的分院 ID
    this.selectedDepartmentId = ref(null);  // Admin 選擇查看的分院
    this.departments = reactive([]);  // 分院列表

    this.settings = reactive({
        isRegistrationOpen: true,
        paymentDeadlineDays: 7,
        minAdvanceBookingDays: 7,
        managerEmail: '',
        passwordLockoutAttempts: 5,
        passwordLockoutMinutes: 30,
        passwordExpiryDays: 0,
        passwordExpiryWarningDays: 7,
        passwordHistoryCount: 3,
        transferBankName: '',
        transferBankCode: '',
        transferAccount: '',
        transferAccountName: '',
        cashPaymentInfo: '',
        announcementTitle: '',
        announcementContent: '',
    });

    // 載入用戶資訊
    this.loadUserInfo = async () => {
        try {
            const res = await global.api.auth.me();
            this.isAdmin.value = res.data.IsAdmin || false;
            this.departmentId.value = res.data.DepartmentId || null;
            // 全院管理者 = Admin 且無分院
            this.isGlobalAdmin.value = this.isAdmin.value && !this.departmentId.value;

            // 分院管理者預設選擇自己的分院
            if (!this.isGlobalAdmin.value && this.departmentId.value) {
                this.selectedDepartmentId.value = this.departmentId.value;
            }
        } catch (err) {
            console.error('❌ 無法取得使用者資訊:', err);
        }
    };

    // 載入分院列表（Admin 用）
    this.loadDepartments = async () => {
        try {
            const res = await global.api.select.department();
            copy(this.departments, res.data || []);
        } catch (err) {
            console.error('❌ 無法取得分院列表:', err);
        }
    };

    // 切換分院時載入該分院的設定
    this.onDepartmentChange = () => {
        this.loadSettings();
    };

    // 載入設定
    this.loadSettings = () => {
        // 決定要載入哪個分院的設定
        let apiCall;
        if (this.isAdmin.value && this.selectedDepartmentId.value) {
            // Admin 查看特定分院
            apiCall = global.api.sysconfig.getbydepartment({ departmentId: this.selectedDepartmentId.value });
        } else if (this.isAdmin.value && this.selectedDepartmentId.value === null) {
            // Admin 查看全局設定
            apiCall = global.api.sysconfig.getglobal();
        } else {
            // 一般用戶
            apiCall = global.api.sysconfig.getall();
        }

        apiCall
            .then((response) => {
                const data = response.data;
                console.log('[SysConfig] 載入的資料:', data);

                if (data) {
                    this.settings.isRegistrationOpen = data.GUEST_REGISTRATION === 'true';
                    this.settings.paymentDeadlineDays = parseInt(data.PAYMENT_DEADLINE_DAYS) || 7;
                    this.settings.minAdvanceBookingDays = parseInt(data.MIN_ADVANCE_BOOKING_DAYS) || 7;
                    this.settings.managerEmail = data.MANAGER_EMAIL || '';
                    this.settings.passwordLockoutAttempts = parseInt(data.PASSWORD_LOCKOUT_ATTEMPTS ?? '5');
                    this.settings.passwordLockoutMinutes = parseInt(data.PASSWORD_LOCKOUT_MINUTES ?? '30');
                    this.settings.passwordExpiryDays = parseInt(data.PASSWORD_EXPIRY_DAYS ?? '0');
                    this.settings.passwordExpiryWarningDays = parseInt(data.PASSWORD_EXPIRY_WARNING_DAYS ?? '7');
                    this.settings.passwordHistoryCount = parseInt(data.PASSWORD_HISTORY_COUNT ?? '3');
                    this.settings.transferBankName = data.TRANSFER_BANK_NAME || '';
                    this.settings.transferBankCode = data.TRANSFER_BANK_CODE || '';
                    this.settings.transferAccount = data.TRANSFER_ACCOUNT || '';
                    this.settings.transferAccountName = data.TRANSFER_ACCOUNT_NAME || '';
                    this.settings.cashPaymentInfo = data.CASH_PAYMENT_INFO || '';
                    this.settings.announcementTitle = data.ANNOUNCEMENT_TITLE || '';
                    this.settings.announcementContent = data.ANNOUNCEMENT_CONTENT || '';
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

        // 決定要儲存到哪個分院
        let departmentId;
        if (this.isAdmin.value) {
            // Admin: 根據選擇的分院決定
            departmentId = this.selectedDepartmentId.value;
        } else {
            // 非 Admin: 使用自己的分院
            departmentId = this.departmentId.value;
        }

        // 只有 Admin 在編輯全局設定時才能修改「是否開啟訪客註冊」、密碼安全政策
        if (this.isAdmin.value && this.selectedDepartmentId.value === null) {
            configs.push({ configKey: 'GUEST_REGISTRATION', configValue: this.settings.isRegistrationOpen.toString() });
            configs.push(
                { configKey: 'PASSWORD_LOCKOUT_ATTEMPTS', configValue: this.settings.passwordLockoutAttempts.toString() },
                { configKey: 'PASSWORD_LOCKOUT_MINUTES', configValue: this.settings.passwordLockoutMinutes.toString() },
                { configKey: 'PASSWORD_EXPIRY_DAYS', configValue: this.settings.passwordExpiryDays.toString() },
                { configKey: 'PASSWORD_EXPIRY_WARNING_DAYS', configValue: this.settings.passwordExpiryWarningDays.toString() },
                { configKey: 'PASSWORD_HISTORY_COUNT', configValue: this.settings.passwordHistoryCount.toString() },
            );
        }

        // 公告：全院 Admin 和分院 Admin 都可以設定
        if (this.isAdmin.value) {
            configs.push(
                { configKey: 'ANNOUNCEMENT_TITLE', configValue: this.settings.announcementTitle },
                { configKey: 'ANNOUNCEMENT_CONTENT', configValue: this.settings.announcementContent },
            );
        }

        // 其他設定都可以修改
        configs.push(
            { configKey: 'PAYMENT_DEADLINE_DAYS', configValue: this.settings.paymentDeadlineDays.toString() },
            { configKey: 'MIN_ADVANCE_BOOKING_DAYS', configValue: this.settings.minAdvanceBookingDays.toString() },
            { configKey: 'MANAGER_EMAIL', configValue: this.settings.managerEmail },
            { configKey: 'TRANSFER_BANK_NAME', configValue: this.settings.transferBankName },
            { configKey: 'TRANSFER_BANK_CODE', configValue: this.settings.transferBankCode },
            { configKey: 'TRANSFER_ACCOUNT', configValue: this.settings.transferAccount },
            { configKey: 'TRANSFER_ACCOUNT_NAME', configValue: this.settings.transferAccountName },
            { configKey: 'CASH_PAYMENT_INFO', configValue: this.settings.cashPaymentInfo },
        );

        const targetName = departmentId ? this.departments.find(d => d.Id === departmentId)?.Name : '全局';
        console.log('[SysConfig] saveSettings() configs =', configs, 'departmentId =', departmentId, '(' + targetName + ')');

        // 一次送出所有設定
        global.api.sysconfig.update({
            body: {
                configs: configs,
                departmentId: departmentId
            }
        })
            .then(() => {
                console.log('[SysConfig] 所有設定已儲存');
                addAlert(`${targetName}設定已儲存`, { type: 'success' });
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
        this.settings.passwordLockoutAttempts = 5;
        this.settings.passwordLockoutMinutes = 30;
        this.settings.passwordExpiryDays = 0;
        this.settings.passwordExpiryWarningDays = 7;
        this.settings.passwordHistoryCount = 3;
        this.settings.transferBankName = '';
        this.settings.transferBankCode = '';
        this.settings.transferAccount = '';
        this.settings.transferAccountName = '';
        this.settings.cashPaymentInfo = '';
        this.settings.announcementTitle = '';
        this.settings.announcementContent = '';

        addAlert('已重設為預設值 (請記得儲存設定)', { type: 'info' });
    };
};

window.$config = {
    setup: () => new function () {
        this.settings = sysConfig.settings;
        this.isAdmin = sysConfig.isAdmin;
        this.isGlobalAdmin = sysConfig.isGlobalAdmin;
        this.selectedDepartmentId = sysConfig.selectedDepartmentId;
        this.departments = sysConfig.departments;
        this.onDepartmentChange = sysConfig.onDepartmentChange;
        this.saveSettings = sysConfig.saveSettings;
        this.resetSettings = sysConfig.resetSettings;

        onMounted(async () => {
            await sysConfig.loadUserInfo();
            // 只有全院管理者才載入分院列表
            if (sysConfig.isGlobalAdmin.value) {
                await sysConfig.loadDepartments();
            }
            sysConfig.loadSettings();
        });
    }
};