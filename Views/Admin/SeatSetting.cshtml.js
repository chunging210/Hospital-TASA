// Views/Admin/SeatSetting.cshtml.js
import global from '/global.js';
const { ref, reactive, onMounted } = Vue;

const seatSetting = new function () {
    this.settings = reactive({
        id: null,
        logoPath: null,
        fontSizeSmall: 14,
        fontSizeMedium: 28,
        fontSizeLarge: 32,
        isEnabled: true
    });

    this.loadSettings = () => {
        fetch('/api/seatsetting/detail')
            .then(response => {
                if (!response.ok) throw new Error('API 回應失敗');
                return response.json();
            })
            .then(data => {
                console.log('載入的資料:', data);
                if (data) {
                    // ✅ 使用 PascalCase (大寫開頭)
                    this.settings.id = data.Id;
                    this.settings.logoPath = data.LogoPath;
                    this.settings.fontSizeSmall = data.FontSizeSmall || 14;
                    this.settings.fontSizeMedium = data.FontSizeMedium || 28;
                    this.settings.fontSizeLarge = data.FontSizeLarge || 32;
                    this.settings.isEnabled = data.IsEnabled ?? true;
                }
            })
            .catch(error => {
                console.error('載入設定失敗:', error);
                addAlert('載入設定失敗', { type: 'danger' });
            });
    };

    this.saveSettings = () => {
        fetch('/api/seatsetting/save', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                logoPath: this.settings.logoPath,
                fontSizeSmall: this.settings.fontSizeSmall,
                fontSizeMedium: this.settings.fontSizeMedium,
                fontSizeLarge: this.settings.fontSizeLarge,
                isEnabled: this.settings.isEnabled
            })
        })
            .then(response => {
                if (!response.ok) throw new Error('儲存失敗');
                return response.json();
            })
            .then(result => {
                this.settings.id = result.Id;  // ✅ 大寫 I
                addAlert('設定已儲存');
                this.loadSettings();
            })
            .catch(error => {
                console.error('儲存失敗:', error);
                addAlert('儲存設定失敗', { type: 'danger' });
            });
    };

    this.handleFileUpload = (event) => {
        const file = event.target.files[0];
        if (!file) return;

        if (file.size > 5 * 1024 * 1024) {
            addAlert('檔案大小不可超過 5MB', { type: 'danger' });
            return;
        }

        const formData = new FormData();
        formData.append('file', file);

        fetch('/api/seatsetting/upload-logo', {
            method: 'POST',
            body: formData
        })
            .then(async response => {
                const text = await response.text();
                let data = null;
                try {
                    data = text ? JSON.parse(text) : null;
                } catch {
                    // 不是 JSON 就照原樣印
                }

                console.log('[SeatSetting] upload-logo response status =', response.status);
                console.log('[SeatSetting] upload-logo raw body =', text);

                if (!response.ok) {
                    const msg = (data && (data.message || data.error)) || '上傳失敗';
                    addAlert(msg, { type: 'danger' });
                    // 這裡直接 return，不再 throw，避免只看到「Error: 上傳失敗」
                    return;
                }

                if (data && (data.Path || data.path)) {
                    const logoPath = data.Path || data.path;
                    this.settings.logoPath = logoPath;
                    addAlert('Logo 上傳成功', { type: 'success' });
                } else {
                    addAlert('上傳失敗：未返回檔案路徑', { type: 'danger' });
                }
            })
            .catch(error => {
                console.error('[SeatSetting] 上傳失敗（JS error）:', error);
                addAlert('上傳失敗', { type: 'danger' });
            });
    };


    this.deleteLogo = () => {
        if (!confirm('確定要刪除 Logo 嗎？')) return;
        this.settings.logoPath = null;
        addAlert('Logo 已刪除（請記得儲存設定）', { type: 'info' });
    };

    this.resetSettings = () => {
        if (!confirm('確定要重設為預設值嗎？')) return;
        this.settings.logoPath = null;
        this.settings.fontSizeSmall = 14;
        this.settings.fontSizeMedium = 28;
        this.settings.fontSizeLarge = 32;
        this.settings.isEnabled = true;
        addAlert('已重設為預設值（請記得儲存設定）', { type: 'info' });
    };
}

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
}