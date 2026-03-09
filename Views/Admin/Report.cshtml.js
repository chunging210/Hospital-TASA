import global from '/global.js';
const { ref, reactive, onMounted, nextTick, watch } = Vue;

let reportpageRef = null;
const reportpage = ref(null);

const report = new function () {
    this.query = reactive({
        startDate: '',
        endDate: '',
        roomId: '',
        paymentMethod: '',  // 繳款方式
        departmentCode: ''  // 部門代碼（成本分攤用）
    });

    this.list = reactive([]);
    this.rooms = reactive([]);
    this.departmentCodes = reactive([]);  // 部門代碼列表
    this.loading = ref(false);

    // 加總資訊
    this.summary = reactive({
        totalCount: 0,
        grandTotal: 0,
        totalAttendees: 0
    });

    // 匯出欄位選擇（預設全選）
    this.exportColumns = reactive({
        bookingNo: true,
        borrowingUnit: true,
        conferenceName: true,
        dateRange: true,
        roomName: true,
        paymentMethod: true,
        attendees: true,
        amount: true
    });

    this.getList = async (pagination = false) => {
        try {
            this.loading.value = true;

            const queryParams = {};
            if (this.query.startDate) queryParams.StartDate = this.query.startDate;
            if (this.query.endDate) queryParams.EndDate = this.query.endDate;
            if (this.query.roomId) queryParams.RoomId = this.query.roomId;
            // 繳款方式
            if (this.query.paymentMethod) queryParams.PaymentMethod = this.query.paymentMethod;
            // 部門代碼（成本分攤用）
            if (this.query.departmentCode) queryParams.DepartmentCode = this.query.departmentCode;

            const options = { body: queryParams };

            const usePagination = pagination && reportpageRef;

            const request = usePagination
                ? global.api.report.list(reportpageRef.setHeaders(options))
                : global.api.report.list(options);

            const response = await request;

            if (usePagination) {
                reportpageRef.setTotal(response);
            }

            const data = response.data || [];
            this.list.splice(0, this.list.length, ...data);

            // 取得加總（不分頁的完整資料加總）
            await this.loadSummary();

        } catch (err) {
            console.error('載入報表失敗:', err);
            addAlert({
                message: `${err.status ?? ''} ${err.message ?? err}`,
                type: 'danger'
            });
        } finally {
            this.loading.value = false;
        }
    };

    this.loadSummary = async () => {
        try {
            const queryParams = {};
            if (this.query.startDate) queryParams.StartDate = this.query.startDate;
            if (this.query.endDate) queryParams.EndDate = this.query.endDate;
            if (this.query.roomId) queryParams.RoomId = this.query.roomId;
            if (this.query.paymentMethod) queryParams.PaymentMethod = this.query.paymentMethod;
            if (this.query.departmentCode) queryParams.DepartmentCode = this.query.departmentCode;

            const response = await global.api.report.summary({ body: queryParams });
            const data = response.data || response;

            this.summary.totalCount = data.TotalCount || 0;
            this.summary.grandTotal = data.GrandTotal || 0;
            this.summary.totalAttendees = data.TotalAttendees || 0;
        } catch (err) {
            console.error('載入加總失敗:', err);
        }
    };

    this.search = () => {
        if (reportpageRef) {
            reportpageRef.go(1);
        }
        this.getList(!!reportpageRef);
    };

    this.exportExcel = () => {
        const params = new URLSearchParams();
        if (this.query.startDate) params.append('StartDate', this.query.startDate);
        if (this.query.endDate) params.append('EndDate', this.query.endDate);
        if (this.query.roomId) params.append('RoomId', this.query.roomId);
        if (this.query.paymentMethod) params.append('PaymentMethod', this.query.paymentMethod);
        if (this.query.departmentCode) params.append('DepartmentCode', this.query.departmentCode);

        // 匯出欄位
        const cols = [];
        if (this.exportColumns.bookingNo) cols.push('bookingNo');
        if (this.exportColumns.borrowingUnit && this.query.paymentMethod === 'cost-sharing') cols.push('borrowingUnit');
        if (this.exportColumns.conferenceName) cols.push('conferenceName');
        if (this.exportColumns.dateRange) cols.push('dateRange');
        if (this.exportColumns.roomName) cols.push('roomName');
        if (this.exportColumns.paymentMethod) cols.push('paymentMethod');
        if (this.exportColumns.attendees) cols.push('attendees');
        if (this.exportColumns.amount) cols.push('amount');
        params.append('Columns', cols.join(','));

        window.location.href = `/api/report/export?${params.toString()}`;
    };

    this.loadRooms = async () => {
        try {
            const res = await global.api.select.room();
            const data = res.data || res || [];
            this.rooms.splice(0, this.rooms.length, ...data);
        } catch (err) {
            console.error('載入會議室失敗:', err);
        }
    };

    this.loadDepartmentCodes = async () => {
        try {
            const res = await global.api.report.departmentcodes();
            const data = res.data || res || [];
            this.departmentCodes.splice(0, this.departmentCodes.length, ...data);
        } catch (err) {
            console.error('載入部門代碼失敗:', err);
        }
    };
};

// 防抖函數
const debounce = (func, delay) => {
    let timeoutId;
    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => func.apply(null, args), delay);
    };
};

const debouncedSearch = debounce(() => {
    if (reportpageRef) {
        reportpageRef.go(1);
    }
    report.getList(!!reportpageRef);
}, 300);

window.$config = {
    setup: () => new function () {
        this.report = report;
        this.reportpage = reportpage;

        onMounted(async () => {
            await nextTick();
            reportpageRef = this.reportpage.value;

            await report.loadRooms();
            await report.loadDepartmentCodes();
            await report.getList(!!reportpageRef);

            // 監聽篩選條件變化，自動查詢
            watch(
                () => [
                    report.query.startDate,
                    report.query.endDate,
                    report.query.roomId,
                    report.query.paymentMethod,
                    report.query.departmentCode
                ],
                () => {
                    debouncedSearch();
                }
            );

            // 監聽繳款方式變化，當不是成本分攤時清空部門代碼
            watch(
                () => report.query.paymentMethod,
                (newVal) => {
                    if (newVal !== 'cost-sharing') {
                        report.query.departmentCode = '';
                    }
                }
            );
        });
    }
};
