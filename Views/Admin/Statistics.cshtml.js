import global from '/global.js';

const { reactive, onMounted, nextTick } = Vue;

let chartTrend = null;
let chartRoom = null;
let chartDept = null;

const BLUE = '#4361ee';
const GRID = '#f0f0f0';
const HEATMAP_BASE = [232, 240, 254];
const HEATMAP_FULL = [67, 97, 238];

const state = reactive({
    loading: false,
    error: '',
    year: new Date().getFullYear(),
    month: 0,   // 0 = 全年
    years: (() => {
        const y = new Date().getFullYear();
        return [y, y - 1, y - 2, y - 3, y - 4];
    })(),
    data: null,
    heatmapMap: {},
    heatmapMax: 1,
    heatmapHours: Array.from({ length: 13 }, (_, i) => i + 8),
});

function heatmapValue(dow, hour) {
    return state.heatmapMap[`${dow}_${hour}`] || 0;
}

function heatmapColor(dow, hour) {
    const val = heatmapValue(dow, hour);
    if (val === 0) return '#f8f9fa';
    const ratio = Math.min(val / state.heatmapMax, 1);
    const r = Math.round(HEATMAP_BASE[0] + (HEATMAP_FULL[0] - HEATMAP_BASE[0]) * ratio);
    const g = Math.round(HEATMAP_BASE[1] + (HEATMAP_FULL[1] - HEATMAP_BASE[1]) * ratio);
    const b = Math.round(HEATMAP_BASE[2] + (HEATMAP_FULL[2] - HEATMAP_BASE[2]) * ratio);
    return `rgb(${r},${g},${b})`;
}

function destroyCharts() {
    if (chartTrend) { chartTrend.destroy(); chartTrend = null; }
    if (chartRoom)  { chartRoom.destroy();  chartRoom = null; }
    if (chartDept)  { chartDept.destroy();  chartDept = null; }
}

async function loadStats() {
    state.loading = true;
    state.error = '';
    state.data = null;
    destroyCharts();

    try {
        const res = await global.api.admin.statisticsusage({
            body: { year: state.year, month: state.month }
        });
        const d = res.data;
        state.data = d;

        state.heatmapMap = {};
        state.heatmapMax = 1;
        (d.Heatmap || []).forEach(h => {
            state.heatmapMap[`${h.DayOfWeek}_${h.Hour}`] = h.Count;
            if (h.Count > state.heatmapMax) state.heatmapMax = h.Count;
        });

        state.loading = false;   // 先關 loading，讓 v-if 渲染 canvas
        await nextTick();        // 等 Vue 把 canvas 放進 DOM
        renderTrendChart(d.Trend);
        renderRoomChart(d.ByRoom);
        renderDeptChart(d.ByDepartment);
    } catch (err) {
        console.error('[Statistics] 載入失敗', err);
        state.error = `載入失敗：${err?.message || JSON.stringify(err)}`;
        state.loading = false;
    }
}

function renderTrendChart(trend) {
    const ctx = document.getElementById('chartTrend');
    if (!ctx || !window.Chart || !trend || !trend.length) return;

    chartTrend = new Chart(ctx, {
        type: 'line',
        data: {
            labels: trend.map(t => t.Label),
            datasets: [{
                label: '使用率 (%)',
                data: trend.map(t => t.UsageRate),
                borderColor: BLUE,
                backgroundColor: 'rgba(67,97,238,0.08)',
                borderWidth: 2.5,
                pointBackgroundColor: BLUE,
                pointRadius: state.month === 0 ? 4 : 3,
                pointHoverRadius: 6,
                fill: true,
                tension: 0.35
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: c => ` 使用率：${c.parsed.y}%`,
                        afterLabel: c => {
                            const t = trend[c.dataIndex];
                            return ` 使用 ${t.UsedHours} 時 / 開放 ${t.AvailableHours} 時`;
                        }
                    }
                }
            },
            scales: {
                y: {
                    min: 0, max: 100,
                    ticks: { callback: v => v + '%' },
                    grid: { color: GRID }
                },
                x: { grid: { display: false } }
            }
        }
    });
}

function renderRoomChart(byRoom) {
    const ctx = document.getElementById('chartRoom');
    if (!ctx || !window.Chart || !byRoom || !byRoom.length) return;

    const sorted = [...byRoom].sort((a, b) => a.UsageRate - b.UsageRate);

    // 每間 36px，最少 200px
    ctx.style.height = Math.max(sorted.length * 36, 200) + 'px';

    chartRoom = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: sorted.map(r => r.RoomName),
            datasets: [
                {
                    label: '已使用',
                    data: sorted.map(r => r.UsedHours),
                    backgroundColor: 'rgba(67,97,238,0.85)',
                    borderRadius: 4,
                    borderSkipped: false
                },
                {
                    label: '剩餘可用',
                    data: sorted.map(r => Math.max(r.AvailableHours - r.UsedHours, 0)),
                    backgroundColor: 'rgba(67,97,238,0.15)',
                    borderRadius: 4,
                    borderSkipped: false
                }
            ]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            plugins: {
                tooltip: {
                    callbacks: {
                        afterTitle: c => `使用率 ${sorted[c[0].dataIndex].UsageRate}%`,
                        label: c => ` ${c.dataset.label}：${c.parsed.x} 時`
                    }
                }
            },
            scales: {
                x: { stacked: true, ticks: { callback: v => v + ' 時' }, grid: { color: GRID } },
                y: { stacked: true, grid: { display: false } }
            }
        }
    });
}

function renderDeptChart(byDept) {
    const ctx = document.getElementById('chartDept');
    if (!ctx || !window.Chart || !byDept || !byDept.length) return;

    const sorted = [...byDept].sort((a, b) => a.UsedHours - b.UsedHours);

    // 每間 36px，最少 200px
    ctx.style.height = Math.max(sorted.length * 36, 200) + 'px';
    const colors = sorted.map((_, i) =>
        `rgba(67,97,238,${(0.3 + 0.7 * i / Math.max(sorted.length - 1, 1)).toFixed(2)})`
    );

    chartDept = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: sorted.map(d => d.UnitName),
            datasets: [{
                label: '使用時數',
                data: sorted.map(d => d.UsedHours),
                backgroundColor: colors,
                borderRadius: 4,
                borderSkipped: false
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            plugins: {
                legend: { display: false },
                tooltip: { callbacks: { label: c => ` ${c.parsed.x} 時` } }
            },
            scales: {
                x: { ticks: { callback: v => v + ' 時' }, grid: { color: GRID } },
                y: { grid: { display: false } }
            }
        }
    });
}

window.$config = {
    setup: () => new function () {
        this.state = state;
        this.heatmapValue = heatmapValue;
        this.heatmapColor = heatmapColor;
        this.loadStats = loadStats;
        onMounted(() => loadStats());
    }
};
