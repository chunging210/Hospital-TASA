// Views/Admin/SeatSetting.cshtml.js
import global from '/global.js';
const { reactive, onMounted } = Vue;

const seatSetting = new function () {
    this.settings = reactive({
        id: null,
        logoPath: null,
        fontSizeSmall: 14,
        fontSizeMedium: 28,
        fontSizeLarge: 32,
        isEnabled: true
    });

    // 取得設定（GET /api/seatsetting/detail）
    this.loadSettings = () => {
        global.api.seatsetting.detail()
            .then((response) => {
                const data = response.data;
                console.log('[SeatSetting] 載入的資料:', data);

                if (data) {
                    this.settings.id = data.Id ?? data.id ?? null;
                    this.settings.logoPath = data.LogoPath ?? data.logoPath ?? null;
                    this.settings.fontSizeSmall = data.FontSizeSmall ?? data.fontSizeSmall ?? 14;
                    this.settings.fontSizeMedium = data.FontSizeMedium ?? data.fontSizeMedium ?? 28;
                    this.settings.fontSizeLarge = data.FontSizeLarge ?? data.fontSizeLarge ?? 32;
                    this.settings.isEnabled = data.IsEnabled ?? data.isEnabled ?? true;
                }
            })
            .catch(error => {
                console.error('[SeatSetting] 載入設定失敗:', error);
                addAlert('載入設定失敗', { type: 'danger', click: error.download });
            });
    };

    // 儲存設定（POST /api/seatsetting/save）
    this.saveSettings = () => {
        const body = {
            logoPath: this.settings.logoPath,
            fontSizeSmall: this.settings.fontSizeSmall,
            fontSizeMedium: this.settings.fontSizeMedium,
            fontSizeLarge: this.settings.fontSizeLarge,
            isEnabled: this.settings.isEnabled
        };

        console.log('[SeatSetting] saveSettings() body =', body);

        global.api.seatsetting.save({ body })
            .then((response) => {
                console.log('[SeatSetting] saveSettings() response =', response);
                const data = response.data;
                this.settings.id = data.Id ?? data.id ?? null;
                addAlert('設定已儲存');
                this.loadSettings();
            })
            .catch(error => {
                console.error('[SeatSetting] 儲存失敗:', error);
                addAlert('儲存設定失敗', { type: 'danger', click: error.download });
            });
    };

    // 上傳 Logo（POST /api/seatsetting/upload-logo）
    this.handleFileUpload = (event) => {
        const file = event.target.files[0];
        if (!file) return;

        if (file.size > 5 * 1024 * 1024) {
            addAlert('檔案大小不可超過 5MB', { type: 'danger' });
            event.target.value = '';
            return;
        }

        const formData = new FormData();
        formData.append('file', file);

        console.log('[SeatSetting] handleFileUpload() file =', file.name, file.size);

        // 因為 key 有 dash，要用 ['upload-logo']
        global.api.seatsetting.uploadlogo({ body: formData })
            .then((response) => {
                console.log('[SeatSetting] upload-logo response =', response);
                const data = response.data;
                const logoPath = data.Path ?? data.path ?? null;

                if (logoPath) {
                    this.settings.logoPath = logoPath;
                    addAlert('Logo 上傳成功', { type: 'success' });
                } else {
                    addAlert('上傳失敗：未返回檔案路徑', { type: 'danger' });
                }
            })
            .catch(error => {
                console.error('[SeatSetting] 上傳失敗:', error);
                addAlert('上傳失敗', { type: 'danger', click: error.download });
            })
            .finally(() => {
                // 重置 input，避免同一個檔案無法再次觸發 change
                event.target.value = '';
            });
    };

    // 刪除 Logo（只改前端，真正刪檔案要不要做看你需求）
    this.deleteLogo = () => {
        if (!confirm('確定要刪除 Logo 嗎？')) return;
        this.settings.logoPath = null;
        addAlert('Logo 已刪除（請記得儲存設定）', { type: 'info' });
    };

    // 重設為預設值
    this.resetSettings = () => {
        if (!confirm('確定要重設為預設值嗎？')) return;

        this.settings.logoPath = null;
        this.settings.fontSizeSmall = 14;
        this.settings.fontSizeMedium = 28;
        this.settings.fontSizeLarge = 32;
        this.settings.isEnabled = true;

        addAlert('已重設為預設值（請記得儲存設定）', { type: 'info' });
    };
};

window.$config = {
    setup: () => new function () {
        this.settings = seatSetting.settings;
        this.saveSettings = seatSetting.saveSettings;
        this.handleFileUpload = seatSetting.handleFileUpload;
        this.deleteLogo = seatSetting.deleteLogo;
        this.resetSettings = seatSetting.resetSettings;

        onMounted(() => {
            seatSetting.loadSettings();
        });
    }
};
