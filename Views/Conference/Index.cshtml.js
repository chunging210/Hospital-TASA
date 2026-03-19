// Conference
import global from '/global.js';
const { ref, reactive, onMounted, watch, computed } = Vue;

let conferencepageRef = null;

export const me = new function () {
    this.vm = reactive({});
    this.getVM = () => {
        return global.api.auth.me()
            .then((response) => {
                copy(this.vm, response.data);
            });
    }
}

export const room = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.room()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

export const building = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.buildingsbydepartment({ body: {} })
            .then((response) => {
                if (response && response.data) {
                    copy(this.list, response.data);
                }
            })
            .catch((err) => {
                console.error('載入大樓列表失敗:', err);
            });
    }
}

export const conference = new function () {

    this.query = reactive({
        Start: new Date().format(),
        End: new Date().addDays(30).format(),
        RoomId: '',
        Building: '',
        keyword: ''
    });

    this.list = reactive([]);
    this.loading = ref(false);

    // ========== 視圖控制 ==========
    this.viewMode = ref('list'); // list, day, week, month
    this.calendarLoading = ref(false);
    this.selectedDate = ref(new Date());
    this.selectedMonth = ref(new Date());

    // ========== 單一會議室的時段資料 ==========
    this.roomSlots = reactive([]);      // 日視圖：當天的時段
    this.weekSlots = reactive([]);      // 週視圖：時段 x 7天

    // ========== 月視圖資料 ==========
    this.monthViewData = reactive({
        Year: 0,
        Month: 0,
        Weeks: []
    });

    // 格式化日期
    const formatDate = (date) => {
        const d = new Date(date);
        const year = d.getFullYear();
        const month = String(d.getMonth() + 1).padStart(2, '0');
        const day = String(d.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    };

    // 日期標題
    this.dateTitle = computed(() => {
        const d = new Date(this.selectedDate.value);
        const weekDays = ['日', '一', '二', '三', '四', '五', '六'];

        if (this.viewMode.value === 'day') {
            return `${formatDate(d)} (週${weekDays[d.getDay()]})`;
        } else if (this.viewMode.value === 'week') {
            // 計算週一
            const day = d.getDay();
            const diff = day === 0 ? -6 : 1 - day;
            const weekStart = new Date(d);
            weekStart.setDate(d.getDate() + diff);
            const weekEnd = new Date(weekStart);
            weekEnd.setDate(weekStart.getDate() + 6);
            return `${formatDate(weekStart)} ~ ${formatDate(weekEnd)}`;
        } else if (this.viewMode.value === 'month') {
            return `${d.getFullYear()} 年 ${d.getMonth() + 1} 月`;
        }
        return '';
    });

    // 週日期陣列
    this.weekDays = computed(() => {
        const d = new Date(this.selectedDate.value);
        const day = d.getDay();
        const diff = day === 0 ? -6 : 1 - day;
        const weekStart = new Date(d);
        weekStart.setDate(d.getDate() + diff);

        const days = [];
        const weekDayNames = ['一', '二', '三', '四', '五', '六', '日'];
        const today = formatDate(new Date());

        for (let i = 0; i < 7; i++) {
            const date = new Date(weekStart);
            date.setDate(weekStart.getDate() + i);
            const dateStr = formatDate(date);
            days.push({
                date: dateStr,
                label: `週${weekDayNames[i]}`,
                dateShort: `${date.getMonth() + 1}/${date.getDate()}`,
                isToday: dateStr === today,
                isWeekend: i >= 5
            });
        }
        return days;
    });

    // ========== 視圖切換 ==========
    this.setViewMode = (mode) => {
        this.viewMode.value = mode;
        if (mode === 'list') {
            this.getList(true);
        } else if (this.query.RoomId) {
            this.loadCalendarData();
        }
    };

    // ========== 大樓/會議室選擇 ==========
    this.onBuildingChange = () => {
        this.query.RoomId = '';
        this.viewMode.value = 'list';
        conferencepageRef?.go(1);
        this.getList(true);
    };

    this.onRoomChange = () => {
        if (this.query.RoomId) {
            // 選了會議室，載入日曆資料
            if (this.viewMode.value !== 'list') {
                this.loadCalendarData();
            }
        } else {
            // 清除會議室選擇，回到列表
            this.viewMode.value = 'list';
        }
        conferencepageRef?.go(1);
        this.getList(true);
    };

    // ========== 日期導航 ==========
    this.navigate = (direction) => {
        const d = new Date(this.selectedDate.value);
        if (this.viewMode.value === 'day') {
            d.setDate(d.getDate() + direction);
        } else if (this.viewMode.value === 'week') {
            d.setDate(d.getDate() + direction * 7);
        } else if (this.viewMode.value === 'month') {
            d.setMonth(d.getMonth() + direction);
        }
        this.selectedDate.value = d;
        this.selectedMonth.value = d;
        this.loadCalendarData();
    };

    this.goToToday = () => {
        this.selectedDate.value = new Date();
        this.selectedMonth.value = new Date();
        this.loadCalendarData();
    };

    this.goToDate = (dateStr) => {
        this.selectedDate.value = new Date(dateStr);
        this.viewMode.value = 'day';
        this.loadCalendarData();
    };

    // 日期選擇器用
    this.formatSelectedDate = () => {
        return formatDate(this.selectedDate.value);
    };

    this.onDatePickerChange = (event) => {
        const dateStr = event.target.value;
        if (dateStr) {
            this.selectedDate.value = new Date(dateStr);
            this.selectedMonth.value = new Date(dateStr);
            this.loadCalendarData();
        }
    };

    // ========== 載入日曆資料 ==========
    this.loadCalendarData = async () => {
        if (!this.query.RoomId) return;

        this.calendarLoading.value = true;
        try {
            if (this.viewMode.value === 'day') {
                await this.loadDayView();
            } else if (this.viewMode.value === 'week') {
                await this.loadWeekView();
            } else if (this.viewMode.value === 'month') {
                await this.loadMonthView();
            }
        } catch (err) {
            console.error('載入日曆失敗:', err);
            addAlert('載入失敗', { type: 'danger' });
        } finally {
            this.calendarLoading.value = false;
        }
    };

    // 日視圖：取得該會議室當天的時段
    this.loadDayView = async () => {
        const dateStr = formatDate(this.selectedDate.value);
        const response = await global.api.select.roomslots({
            body: {
                RoomId: this.query.RoomId,
                Date: dateStr
            }
        });
        console.log('roomSlots:', response.data);
        copy(this.roomSlots, response.data || []);
    };

    // 週視圖：取得該會議室一週的時段
    this.loadWeekView = async () => {
        const days = this.weekDays.value;
        const roomId = this.query.RoomId;

        // 取得每天的資料
        const weekData = [];
        for (let i = 0; i < 7; i++) {
            const resp = await global.api.select.roomslots({
                body: { RoomId: roomId, Date: days[i].date }
            });
            weekData.push(resp.data || []);
        }

        // 用第一天的時段作為模板
        const slotTemplates = weekData[0] || [];

        // 組裝週視圖資料
        const result = [];
        for (let s = 0; s < slotTemplates.length; s++) {
            const slot = slotTemplates[s];
            const dayList = [];
            for (let d = 0; d < 7; d++) {
                const daySlots = weekData[d] || [];
                const matched = daySlots.find(x => x.Key === slot.Key);
                dayList.push({
                    Occupied: matched ? matched.Occupied : false,
                    ConferenceName: matched ? matched.ConferenceName || '' : ''
                });
            }
            result.push({
                TimeLabel: slot.TimeLabel || '',
                DayList: dayList
            });
        }

        // 清空並重新填入
        this.weekSlots.length = 0;
        result.forEach(r => this.weekSlots.push(r));
    };

    // 月視圖：取得該會議室一個月的預約統計
    this.loadMonthView = async () => {
        const response = await global.api.select.calendarmonth({
            body: {
                Date: formatDate(this.selectedMonth.value),
                RoomId: this.query.RoomId
            }
        });
        if (response.data) {
            Object.assign(this.monthViewData, response.data);
        }
    };

    // ========== 列表資料 ==========
    this.getList = async (pagination) => {
        try {
            this.loading.value = true;

            const options = { body: this.query };

            const request = pagination && conferencepageRef
                ? global.api.conference.list(conferencepageRef.setHeaders(options))
                : global.api.conference.list(options);

            const response = await request;

            if (pagination && conferencepageRef) {
                conferencepageRef.setTotal(response);
            }

            copy(this.list, response.data || []);

        } catch (err) {
            addAlert('取得會議列表失敗', { type: 'danger' });
        } finally {
            this.loading.value = false;
        }
    };

    this.onFilterChange = () => {
        conferencepageRef?.go(1);
        this.getList(true);
    };
};

window.$config = {
    components: {},
    setup: () => new function () {
        this.me = me;
        this.room = room;
        this.building = building;
        this.conference = conference;
        this.conferencepage = ref(null);

        watch(() => conference.query.keyword, () => {
            conference.onFilterChange();
        });

        watch(() => conference.query.Start, () => {
            conference.onFilterChange();
        });

        watch(() => conference.query.End, () => {
            conference.onFilterChange();
        });

        onMounted(() => {
            me.getVM();
            room.getList();
            building.getList();

            conferencepageRef = this.conferencepage.value;
            conference.getList(true);
        });
    }
}
