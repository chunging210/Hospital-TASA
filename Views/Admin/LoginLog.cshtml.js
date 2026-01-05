// Admin/LoginLog.cshtml.js - 最簡單版本
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

const loginlog = new function () {
    // ✅ Tab 列表
    this.tabs = [
        { text: '登入日誌', value: 'login_' },      // ✅ 以 login_ 開頭
        { text: '新增帳號日誌', value: 'user_register_' },  // ✅ 以 user_insert_ 開頭
        { text: '修改帳號日誌', value: 'user_update' },  // ✅ 以 user_delete_ 開頭
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

    // ✅ 分頁相關
    this.currentPage = ref(1);
    this.pageSize = ref(10);
    this.totalPages = computed(() => {
        return Math.ceil(this.list.length / this.pageSize.value);
    });

    // ✅ 簡單過濾 - 只做分頁
    this.filteredList = computed(() => {
        const startIdx = (this.currentPage.value - 1) * this.pageSize.value;
        const endIdx = startIdx + this.pageSize.value;
        return this.list.slice(startIdx, endIdx);
    });

    this.searchByDate = () => {
        console.log('📅 搜尋日期範圍:', this.query.startDate, '到', this.query.endDate);

        // 驗證日期
        if (this.query.startDate && this.query.endDate) {
            const startDate = new Date(this.query.startDate);
            const endDate = new Date(this.query.endDate);

            if (startDate > endDate) {
                addAlert('開始日期不能大於結束日期', { type: 'warning' });
                return;
            }
        }

        this.currentPage.value = 1;
        this.getList();
    };

    // ✅ 清除日期篩選
    this.clearDateFilter = () => {
        console.log('🗑️ 清除日期篩選');
        this.query.startDate = '';
        this.query.endDate = '';
        this.currentPage.value = 1;
        this.getList();
    };

    // ✅ 清除關鍵字篩選
    this.clearKeywordFilter = () => {
        console.log('🗑️ 清除關鍵字篩選');
        this.query.keyword = '';
        this.currentPage.value = 1;
        this.getList();
    };


    // ✅ 取得資料
    this.getList = () => {
        console.log('🔍 發送查詢:', this.query);

        global.api.admin.loginloglist({ body: this.query })
            .then((response) => {
                console.log('🔍 API Response:', response);

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
                this.currentPage.value = 1;

                console.log('✅ list 現在有:', this.list.length, '筆');
            })
            .catch(error => {
                console.error('❌ 錯誤:', error);
                addAlert('取得資料失敗', { type: 'danger' });
            });
    };

    // ✅ 切換 tab
    this.selectTab = (value) => {
        this.selectedTab.value = value;
        this.query.infoType = value;
        this.currentPage.value = 1;
        console.log('🔍 切換 Tab，準備查詢:', this.query);  // ✅ 加這行
        this.getList();
    };

    // ✅ 分頁方法
    this.previousPage = () => {
        if (this.currentPage.value > 1) {
            this.currentPage.value--;
        }
    };

    this.nextPage = () => {
        if (this.currentPage.value < this.totalPages.value) {
            this.currentPage.value++;
        }
    };

    
};

window.$config = {
    setup: () => new function () {
        this.loginlog = loginlog;

        onMounted(() => {
            console.log('🚀 登入日誌頁面 onMounted 開始');

            const endDate = new Date();
            const startDate = new Date();
            startDate.setDate(endDate.getDate() - 15);

            this.loginlog.query.startDate = startDate.toISOString().split('T')[0];
            this.loginlog.query.endDate = endDate.toISOString().split('T')[0];

            console.log('📅 查詢日期:', this.loginlog.query.startDate, '-', this.loginlog.query.endDate);

            this.loginlog.getList();
        });
    }
};