const { ref, reactive, computed, onMounted } = Vue;

window.$config = {
    setup: () => new function () {
        const self = this;

        const HOUR_START = 8;
        const HOUR_END   = 21;
        this.hours = Array.from({ length: HOUR_END - HOUR_START }, (_, i) => HOUR_START + i);

        // ── 狀態 ──
        this.loading            = ref(false);
        this.viewMode           = ref('day');   // 'day' | 'week'
        this.selectedDate       = ref('');
        this.selectedDepartment = ref('');
        this.selectedBuilding   = ref('');
        this.departments        = ref([]);
        this.buildings          = ref([]);
        this.data               = ref(null);    // 日視圖資料
        this.rangeData          = ref(null);    // 週視圖資料
        this.expandedBuildings  = reactive({});

        // 格式化日期為 yyyy-MM-dd（本地時間）
        const formatDate = (date) => {
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            return `${year}-${month}-${day}`;
        };

        const today = new Date();
        this.selectedDate.value = formatDate(today);

        // ── 工具 ──
        this.pad = h => String(h).padStart(2, '0');

        this.shortName = fullName => {
            const parts = fullName.trim().split(/\s+/);
            return parts[parts.length - 1] || fullName;
        };

        this.countRooms = building =>
            building.Floors.reduce((s, f) => s + f.Rooms.length, 0);

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

        // ── Gantt block 建立（日視圖用）──
        const toDecimal = t => {
            const [h, m] = t.split(':').map(Number);
            return h + m / 60;
        };

        this.buildBlocks = slots => {
            if (!slots?.length) return [];
            const map = {};
            slots.forEach(slot => {
                const s = Math.floor(toDecimal(slot.StartTime));
                const e = Math.floor(toDecimal(slot.EndTime));
                for (let h = s; h < e; h++) map[h] = slot;
            });
            const blocks = [];
            let i = HOUR_START;
            while (i < HOUR_END) {
                const slot = map[i];
                if (!slot) {
                    let span = 1;
                    while (i + span < HOUR_END && !map[i + span]) span++;
                    blocks.push({ type: 'empty', hour: i, span });
                    i += span;
                } else {
                    let span = 1;
                    while (i + span < HOUR_END && map[i + span]?.Key === slot.Key) span++;
                    blocks.push({
                        key:       `${slot.Key}-${i}`,
                        type:      slot.Occupied ? 'occupied' : 'available',
                        hour:      i, span,
                        // label:     slot.Occupied ? (slot.Name || '已預約') : '可預約',
                        startTime: slot.StartTime,
                        endTime:   slot.EndTime,
                    });
                    i += span;
                }
            }
            return blocks;
        };

        this.blockStyle = block => {
            const total = HOUR_END - HOUR_START;
            const left  = ((block.hour - HOUR_START) / total) * 100;
            const width = (block.span / total) * 100;
            return { left: `calc(${left}% + 2px)`, width: `calc(${width}% - 4px)` };
        };

        this.toggleBuilding = name => {
            self.expandedBuildings[name] = !self.expandedBuildings[name];
        };

        // ── API ──
        const loadDepartments = async () => {
            try {
                const res = await fetch('/api/public/departments');
                if (res.ok) {
                    self.departments.value = await res.json();
                    if (self.departments.value.length > 0 && !self.selectedDepartment.value) {
                        const taipei = self.departments.value.find(d => d.Name.includes('臺北'));
                        self.selectedDepartment.value = taipei?.Id ?? self.departments.value[0].Id;
                    }
                }
            } catch (err) { console.error(err); }
        };

        const loadBuildings = async () => {
            try {
                let url = '/api/public/buildings';
                if (self.selectedDepartment.value)
                    url += `?departmentId=${self.selectedDepartment.value}`;
                const res = await fetch(url);
                if (res.ok) self.buildings.value = await res.json();
            } catch (err) { console.error(err); }
        };

        this.onDepartmentChange = async () => {
            self.selectedBuilding.value = '';
            await loadBuildings();
            await self.loadAvailability();
        };

        this.loadAvailability = async () => {
            self.loading.value = true;
            try {
                if (self.viewMode.value === 'day') {
                    // 單日
                    const res = await fetch('/api/public/availability', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({
                            date:         self.selectedDate.value,
                            departmentId: self.selectedDepartment.value || null,
                            building:     self.selectedBuilding.value   || null
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
                            startDate:    start,
                            endDate:      end,
                            departmentId: self.selectedDepartment.value || null,
                            building:     self.selectedBuilding.value   || null
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

        onMounted(async () => {
            await loadDepartments();
            await loadBuildings();
            await self.loadAvailability();
        });
    }
};