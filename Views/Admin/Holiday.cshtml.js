// Views/Admin/Holiday.cshtml.js
import global from '/global.js';
const { reactive, onMounted } = Vue;

const holidayModule = new function () {
    // 使用 reactive 而非 ref
    this.state = reactive({
        selectedYear: new Date().getFullYear(),
        uploading: false,
        syncing: false
    });

    this.list = reactive([]);

    // 年度選項
    this.yearOptions = (() => {
        const currentYear = new Date().getFullYear();
        return [currentYear - 1, currentYear, currentYear + 1, currentYear + 2];
    })();

    // 統計資訊
    this.getStats = () => {
        const holidays = this.list.filter(h => !h.IsWorkday && h.IsEnabled).length;
        const workdays = this.list.filter(h => h.IsWorkday && h.IsEnabled).length;
        return {
            holidays,
            workdays,
            total: holidays + workdays
        };
    };

    // 取得星期幾
    this.getDayOfWeek = (dateStr) => {
        const date = new Date(dateStr);
        const days = ['日', '一', '二', '三', '四', '五', '六'];
        return '週' + days[date.getDay()];
    };

    // 載入假日列表
    this.loadHolidays = async () => {
        try {
            const res = await global.api.holiday.list(this.state.selectedYear);
            this.list.length = 0;
            this.list.push(...res.data);
        } catch (err) {
            console.error('載入假日失敗:', err);
            addAlert('載入假日失敗', { type: 'danger' });
        }
    };

    // 同步政府假日
    this.syncHolidays = async () => {
        if (!confirm(`確定要同步 ${this.state.selectedYear} 年的政府國定假日嗎？\n現有資料將會被更新。`)) {
            return;
        }

        this.state.syncing = true;
        try {
            const res = await global.api.holiday.sync(this.state.selectedYear);
            addAlert(res.data.message, { type: 'success' });
            await this.loadHolidays();
        } catch (err) {
            console.error('同步失敗:', err);
            addAlert(err.message || '同步失敗，請確認該年度資料是否已發布', { type: 'danger' });
        } finally {
            this.state.syncing = false;
        }
    };

    // 上傳 JSON 檔案
    this.uploadFile = async (event) => {
        const file = event.target.files[0];
        if (!file) return;

        // 檢查副檔名
        if (!file.name.endsWith('.json')) {
            addAlert('請選擇 JSON 檔案', { type: 'warning' });
            event.target.value = '';
            return;
        }

        this.state.uploading = true;
        try {
            const formData = new FormData();
            formData.append('file', file);

            const res = await global.api.holiday.upload(formData);
            addAlert(res.data.message, { type: 'success' });
            await this.loadHolidays();
        } catch (err) {
            console.error('上傳失敗:', err);
            addAlert(err.message || '上傳失敗', { type: 'danger' });
        } finally {
            this.state.uploading = false;
            event.target.value = ''; // 清空 input，允許重複上傳同一檔案
        }
    };

    // 刪除假日
    this.deleteHoliday = async (id) => {
        if (!confirm('確定要刪除此假日嗎？')) return;

        try {
            await global.api.holiday.delete(id);
            addAlert('假日已刪除', { type: 'success' });
            await this.loadHolidays();
        } catch (err) {
            console.error('刪除失敗:', err);
            addAlert(err.message || '刪除失敗', { type: 'danger' });
        }
    };

    // 切換啟用狀態
    this.toggleEnabled = async (id) => {
        try {
            await global.api.holiday.toggle(id);
            addAlert('狀態已更新', { type: 'success' });
            await this.loadHolidays();
        } catch (err) {
            console.error('更新失敗:', err);
            addAlert(err.message || '更新失敗', { type: 'danger' });
        }
    };
};

window.$config = {
    setup: () => new function () {
        this.holiday = holidayModule;

        onMounted(async () => {
            await holidayModule.loadHolidays();
        });
    }
};
