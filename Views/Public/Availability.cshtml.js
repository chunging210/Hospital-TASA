// Public Availability Page
const { ref, reactive, computed, onMounted } = Vue;

window.$config = {
    setup: () => new function () {
        const self = this;

        // ── 狀態 ──
        this.loading = ref(false);
        this.viewMode = ref('day');   // 'day' | 'week'
        this.selectedDate = ref('');
        this.selectedDepartment = ref('');
        this.selectedBuilding = ref('');
        this.departments = ref([]);
        this.buildings = ref([]);
        this.data = ref(null);        // 日視圖資料
        this.rangeData = ref(null);   // 週視圖資料
        this.expandedBuildings = reactive({});

        // 格式化日期為 yyyy-MM-dd（本地時間）
        const formatDate = (date) => {
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        };

        // 初始化日期為今天
        const today = new Date();
        this.selectedDate.value = formatDate(today);

        // ── 工具函數 ──
        this.formatDateLabel = dateStr => {
            const d = new Date(dateStr + 'T00:00:00');
            const weekDays = ['日','一','二','三','四','五','六'];
            return `${d.getMonth()+1}/${d.getDate()}(${weekDays[d.getDay()]})`;
        };

        this.isToday = dateStr => {
            return dateStr === formatDate(today);
        };

        this.isWeekend = dateStr => {
            const d = new Date(dateStr + 'T00:00:00');
            return d.getDay() === 0 || d.getDay() === 6;
        };

        // 計算週的開始結束日期
        const getRangeDates = () => {
            const base = new Date(self.selectedDate.value + 'T00:00:00');
            const day = base.getDay();
            const mon = new Date(base);
            mon.setDate(base.getDate() - (day === 0 ? 6 : day - 1));
            const sun = new Date(mon);
            sun.setDate(mon.getDate() + 6);
            return {
                start: formatDate(mon),
                end:   formatDate(sun)
            };
        };

        // ── 視圖切換 ──
        this.switchView = async mode => {
            self.viewMode.value = mode;
            await self.loadAvailability();
        };

        // ── 週視圖點擊某天：切換到日視圖 ──
        this.drillToDay = async dateStr => {
            self.selectedDate.value = dateStr;
            self.viewMode.value = 'day';
            await self.loadAvailability();
        };

        // ── 日/週導航 ──
        this.navigating = ref(false);
        this.navigate = async direction => {
            // 防止重複點擊
            if (self.navigating.value || self.loading.value) return;
            self.navigating.value = true;

            try {
                const base = new Date(self.selectedDate.value + 'T00:00:00');
                if (self.viewMode.value === 'week') {
                    base.setDate(base.getDate() + direction * 7);
                } else {
                    base.setDate(base.getDate() + direction);
                }
                self.selectedDate.value = formatDate(base);
                await self.loadAvailability();
            } finally {
                self.navigating.value = false;
            }
        };

        // ── 目前顯示的標題 ──
        this.viewTitle = computed(() => {
            const base = new Date(self.selectedDate.value + 'T00:00:00');
            if (self.viewMode.value === 'day') {
                const weekDays = ['星期日','星期一','星期二','星期三','星期四','星期五','星期六'];
                return `${base.getFullYear()}/${base.getMonth()+1}/${base.getDate()} ${weekDays[base.getDay()]}`;
            }
            // week
            const { start, end } = getRangeDates();
            const s = new Date(start + 'T00:00:00');
            const e = new Date(end   + 'T00:00:00');
            return `${s.getMonth()+1}/${s.getDate()} – ${e.getMonth()+1}/${e.getDate()}`;
        });

        // 切換大樓展開/收合
        this.toggleBuilding = (buildingName) => {
            self.expandedBuildings[buildingName] = !self.expandedBuildings[buildingName];
        };

        // 載入分院列表
        this.loadDepartments = async () => {
            try {
                const res = await fetch('/api/public/departments');
                if (res.ok) {
                    self.departments.value = await res.json();
                    if (self.departments.value.length > 0 && !self.selectedDepartment.value) {
                        const taipei = self.departments.value.find(d => d.Name.includes('臺北'));
                        self.selectedDepartment.value = taipei ? taipei.Id : self.departments.value[0].Id;
                    }
                }
            } catch (err) {
                console.error('載入分院失敗:', err);
            }
        };

        // 載入大樓列表
        this.loadBuildings = async () => {
            try {
                let url = '/api/public/buildings';
                if (self.selectedDepartment.value) {
                    url += `?departmentId=${self.selectedDepartment.value}`;
                }
                const res = await fetch(url);
                if (res.ok) {
                    self.buildings.value = await res.json();
                }
            } catch (err) {
                console.error('載入大樓失敗:', err);
            }
        };

        // 分院變更時
        this.onDepartmentChange = async () => {
            self.selectedBuilding.value = '';
            await self.loadBuildings();
            await self.loadAvailability();
        };

        // 載入空檔資料
        this.loadAvailability = async () => {
            self.loading.value = true;
            try {
                if (self.viewMode.value === 'day') {
                    // 單日
                    const res = await fetch('/api/public/availability', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            date: self.selectedDate.value,
                            departmentId: self.selectedDepartment.value || null,
                            building: self.selectedBuilding.value || null
                        })
                    });

                    if (res.ok) {
                        self.data.value = await res.json();
                        self.data.value?.buildings?.forEach(b => {
                            if (self.expandedBuildings[b.Building] === undefined)
                                self.expandedBuildings[b.Building] = true;
                        });
                    }
                } else {
                    // 週
                    const { start, end } = getRangeDates();
                    const res = await fetch('/api/public/availability/range', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            startDate: start,
                            endDate: end,
                            departmentId: self.selectedDepartment.value || null,
                            building: self.selectedBuilding.value || null
                        })
                    });

                    if (res.ok) {
                        self.rangeData.value = await res.json();
                        self.rangeData.value?.buildings?.forEach(b => {
                            if (self.expandedBuildings[b.Building] === undefined)
                                self.expandedBuildings[b.Building] = true;
                        });
                    }
                }
            } catch (err) {
                console.error('載入失敗:', err);
            } finally {
                self.loading.value = false;
            }
        };

        // 初始化
        onMounted(async () => {
            await self.loadDepartments();
            if (self.selectedDepartment.value) {
                await self.loadBuildings();
                await self.loadAvailability();
            }
        });
    }
};
