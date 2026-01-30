import global from '/global.js';
const { ref, reactive, onMounted, watch, nextTick } = Vue;  // ✅ 加入 nextTick

let currentSearchController = null;
let loginlogpageRef = null;
const loginlogpage = ref(null);  // ✅ 移到這裡宣告

const loginlog = new function () {
    this.tabs = [
        { text: '登入日誌', value: 'login_' },
        { text: '新增帳號日誌', value: 'user_register_' },
        { text: '修改帳號日誌', value: 'user_update' },
    ];

    this.selectedTab = ref('login_');

    this.query = reactive({
        keyword: '',
        startDate: '',
        endDate: '',
        infoType: 'login_'
    });

    this.list = reactive([]);
    this.loading = ref(false);

    // ✅ 標準 getList 寫法
    this.getList = async (pagination = false) => {
        try {
            console.log('🔍 getList - pagination:', pagination, 'pageRef:', !!loginlogpageRef);
            
            if (currentSearchController) {
                currentSearchController.abort();
            }

            this.loading.value = true;
            currentSearchController = new AbortController();

            const queryParams = {};
            if (this.query.keyword?.trim()) {
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

            const options = {
                body: queryParams,
                signal: currentSearchController.signal
            };

            // ✅ 標準寫法
            const usePagination = pagination && loginlogpageRef;
            
            const request = usePagination
                ? global.api.admin.loginloglist(loginlogpageRef.setHeaders(options))
                : global.api.admin.loginloglist(options);

            const response = await request;

            if (usePagination) {
                loginlogpageRef.setTotal(response);
            }

            let data = response.data || [];

            data.forEach(x => {
                if (x.LoginTime && typeof x.LoginTime === 'string') {
                    x.LoginTime = new Date(x.LoginTime);
                }
            });

            this.list.splice(0, this.list.length, ...data);
            
            console.log('✅ 登入日誌載入完成,共', data.length, '筆');

        } catch (err) {
            if (err.name === 'AbortError') return;
            console.error('❌ 錯誤:', err);
            addAlert({
                message: `${err.status ?? ''} ${err.message ?? err}`,
                type: 'danger'
            });
        } finally {
            this.loading.value = false;
            currentSearchController = null;
        }
    };

    this.selectTab = (value) => {
        this.selectedTab.value = value;
        this.query.infoType = value;
        console.log('🔍 切換 Tab:', value);
        
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(!!loginlogpageRef);
    };

    this.search = () => {
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(!!loginlogpageRef);
    };

    this.clearSearch = () => {
        this.query.keyword = '';
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(!!loginlogpageRef);
    };

    this.clearDateFilter = () => {
        this.query.startDate = '';
        this.query.endDate = '';
        if (loginlogpageRef) {
            loginlogpageRef.go(1);
        }
        this.getList(!!loginlogpageRef);
    };
}

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
    loginlog.getList(!!loginlogpageRef);
}, 300);

window.$config = {
    setup: () => new function () {
        this.loginlog = loginlog;
        this.loginlogpage = loginlogpage;  // ✅ 使用全域的 ref

        // ✅ 改良版 onMounted
        onMounted(async () => {
            console.log('🚀 登入日誌頁面已掛載');

            // 1️⃣ 初始化日期範圍
            const endDate = new Date();
            const startDate = new Date();
            startDate.setDate(endDate.getDate() - 15);

            this.loginlog.query.startDate = startDate.toISOString().split('T')[0];
            this.loginlog.query.endDate = endDate.toISOString().split('T')[0];

            // 2️⃣ 等待 DOM 渲染完成
            await nextTick();

            // 3️⃣ 綁定分頁元件 ref
            loginlogpageRef = this.loginlogpage.value;
            
            console.log('📌 分頁元件綁定:', !!loginlogpageRef);

            // 4️⃣ 載入資料
            await this.loginlog.getList(!!loginlogpageRef);

            // 5️⃣ 設定 Watch
            watch(() => loginlog.query.keyword, (newValue) => {
                if (newValue.trim() === '') {
                    if (loginlogpageRef) {
                        loginlogpageRef.go(1);
                    }
                    loginlog.getList(!!loginlogpageRef);
                    return;
                }
                debouncedSearch();
            });
        });
    }
};