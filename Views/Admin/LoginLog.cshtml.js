import global from '/global.js';
const { ref, reactive, onMounted, watch } = Vue;

let currentSearchController = null;
let loginlogpageRef = null;

const loginlog = new function () {
    // ✅ Tab 列表
    this.tabs = [
        { text: '登入日誌', value: 'login_' },
        { text: '新增帳號日誌', value: 'user_register_' },
        { text: '修改帳號日誌', value: 'user_update' },
    ];

    this.selectedTab = ref('login_');

    // ✅ 查詢條件
    this.query = reactive({
        keyword: '',
        startDate: '',
        endDate: '',
        infoType: 'login_'
    });

    // ✅ 資料列表
    this.list = reactive([]);

    // ✅ 參考 visitor.js 的寫法 - 完整的 getList
    this.getList = async (pagination) => {
        try {
            if (currentSearchController) {
                currentSearchController.abort();
            }

            currentSearchController = new AbortController();

            const queryParams = {};
            if (this.query.keyword && this.query.keyword.trim()) {
                queryParams.keyword = this.query.keyword.trim();
            }
            if (this.query.startDate) {
                queryParams.startDate = this.query.startDate;
            }
            if (this.query.endDate) {
                queryParams.endDate = this.query.endDate;
            }
            if (this.query.infoType) {
                queryParams.infoType = this.query.infoType;
            }

            const options = { body: queryParams, signal: currentSearchController.signal };

            // ✅ 參考 visitor 的寫法
            const request = pagination && loginlogpageRef
                ? global.api.admin.loginloglist(loginlogpageRef.setHeaders(options))
                : global.api.admin.loginloglist(options);

            const response = await request;

            console.log('🔍 API Response:', response);

            // ✅ 參考 visitor 的寫法 - 設定分頁總數
            if (pagination && loginlogpageRef) {
                loginlogpageRef.setTotal(response);
            }

            let data = response;
            if (response && response.data && Array.isArray(response.data)) {
                data = response.data;
            }
            if (!Array.isArray(data)) {
                data = [];
            }

            console.log('📦 資料數量:', data.length);

            // 確保日期是 Date 物件
            data.forEach(x => {
                if (x.LoginTime && typeof x.LoginTime === 'string') {
                    x.LoginTime = new Date(x.LoginTime);
                }
            });

            this.list.splice(0, this.list.length, ...data);

            console.log('✅ list 現在有:', this.list.length, '筆');
        } catch (err) {
            if (err.name === 'AbortError') return;
            console.error('❌ 錯誤:', err);
            addAlert({ message: `${err.status ?? ''} ${err.message ?? err}`, type: 'danger' });
        } finally {
            currentSearchController = null;
        }
    };

    // ✅ 切換 tab
    this.selectTab = (value) => {
        this.selectedTab.value = value;
        this.query.infoType = value;
        console.log('🔍 切換 Tab，準備查詢:', this.query);
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(true);
    };

    // ✅ 搜尋
    this.search = () => {
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(true);
    };

    // ✅ 清除搜尋
    this.clearSearch = () => {
        this.query.keyword = '';
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(true);
    };

    // ✅ 清除日期篩選
    this.clearDateFilter = () => {
        this.query.startDate = '';
        this.query.endDate = '';
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(true);
    };
}

// ✅ Debounce 搜尋
const debounce = (func, delay) => {
    let timeoutId;
    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => func.apply(null, args), delay);
    };
};

const debouncedSearch = debounce(() => {
    if (loginlogpageRef) {
        loginlogpageRef.go(1);
    }
    loginlog.getList(true);
}, 300);

window.$config = {
    setup: () => new function () {
        this.loginlog = loginlog;
        this.loginlogpage = ref(null);

        onMounted(() => {
            console.log('🚀 登入日誌頁面 onMounted 開始');

            // ✅ 初始化日期範圍
            const endDate = new Date();
            const startDate = new Date();
            startDate.setDate(endDate.getDate() - 15);

            this.loginlog.query.startDate = startDate.toISOString().split('T')[0];
            this.loginlog.query.endDate = endDate.toISOString().split('T')[0];

            // ✅ 初始化分頁參考
            loginlogpageRef = this.loginlogpage.value;

            // ✅ 初始載入
            console.log('📅 查詢日期:', this.loginlog.query.startDate, '-', this.loginlog.query.endDate);
            this.loginlog.getList(true);

            // ✅ Watch 關鍵字搜尋
            watch(() => loginlog.query.keyword, (newValue) => {
                if (newValue.trim() === '') {
                    if (loginlogpageRef) {
                        loginlogpageRef.go(1);
                    }
                    loginlog.getList(true);
                    return;
                }
                debouncedSearch();
            });
        });
    }
};