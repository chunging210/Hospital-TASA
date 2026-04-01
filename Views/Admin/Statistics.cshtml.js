import global from '/global.js';

const { reactive, onMounted, nextTick } = Vue;

let chartTrend = null;
let chartRoom = null;
let chartRoomPie = null;
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
    useCustomRange: false,
    customStart: '',
    customEnd: '',
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
    if (chartTrend)   { chartTrend.destroy();   chartTrend = null; }
    if (chartRoom)    { chartRoom.destroy();     chartRoom = null; }
    if (chartRoomPie) { chartRoomPie.destroy();  chartRoomPie = null; }
    if (chartDept)    { chartDept.destroy();     chartDept = null; }
}

async function loadStats() {
    state.loading = true;
    state.error = '';
    state.data = null;
    destroyCharts();

    try {
        const params = state.useCustomRange && state.customStart && state.customEnd
            ? { startDate: state.customStart, endDate: state.customEnd }
            : { year: state.year, month: state.month };
        const res = await global.api.admin.statisticsusage({ body: params });
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
        renderRoomPieChart(d.ByRoom);
        renderDeptChart(d.ByDepartment);
    } catch (err) {
        console.error('[Statistics] 載入失敗', err);
        state.error = `載入失敗：${err?.message || JSON.stringify(err)}`;
        state.loading = false;
    }
}

function downloadBlob(content, filename) {
    const blob = new Blob(['\uFEFF' + content], { type: 'text/csv;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);
}

function downloadChartPng(canvasId, name) {
    const canvas = document.getElementById(canvasId);
    if (!canvas) return;
    const tmp = document.createElement('canvas');
    tmp.width = canvas.width;
    tmp.height = canvas.height;
    const ctx = tmp.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, tmp.width, tmp.height);
    ctx.drawImage(canvas, 0, 0);
    const link = document.createElement('a');
    link.download = `${name}_${state.data?.Kpi?.Period || ''}.png`;
    link.href = tmp.toDataURL('image/png');
    link.click();
}

function downloadTrendCsv() {
    const rows = ['期間,使用率,使用時數,開放時數,預約次數,費用'];
    (state.data?.Trend || []).forEach(t =>
        rows.push(`${t.Label},${t.UsageRate}%,${t.UsedHours},${t.AvailableHours},${t.BookingCount},${t.Revenue}`)
    );
    downloadBlob(rows.join('\n'), `趨勢_${state.data?.Kpi?.Period || ''}.csv`);
}

function downloadRoomCsv() {
    const rows = ['會議室,預約次數,使用時數,開放時數,使用率'];
    (state.data?.ByRoom || []).forEach(r =>
        rows.push(`${r.RoomName},${r.BookingCount},${r.UsedHours},${r.AvailableHours},${r.UsageRate}%`)
    );
    downloadBlob(rows.join('\n'), `各會議室_${state.data?.Kpi?.Period || ''}.csv`);
}

function downloadDeptCsv() {
    const rows = ['單位,預約次數,使用時數,費用,使用率'];
    (state.data?.ByDepartment || []).forEach(d =>
        rows.push(`${d.UnitName},${d.BookingCount},${d.UsedHours},${d.Revenue},${d.UsageRate}%`)
    );
    downloadBlob(rows.join('\n'), `各成本中心_${state.data?.Kpi?.Period || ''}.csv`);
}

function buildParams() {
    return state.useCustomRange && state.customStart && state.customEnd
        ? `startDate=${state.customStart}&endDate=${state.customEnd}`
        : `year=${state.year}&month=${state.month}`;
}

function exportCsv() {
    window.open(`/api/admin/statisticsexport?${buildParams()}`, '_blank');
}

function exportRaw() {
    window.open(`/api/admin/statisticsrawexport?${buildParams()}`, '_blank');
}

function renderTrendChart(trend) {
    const ctx = document.getElementById('chartTrend');
    if (!ctx || !window.Chart || !trend || !trend.length) return;

    const isMonthly = state.month === 0 && !state.useCustomRange;

    chartTrend = new Chart(ctx, {
        data: {
            labels: trend.map(t => t.Label),
            datasets: [
                {
                    type: 'line',
                    label: '使用率 (%)',
                    data: trend.map(t => t.UsageRate),
                    borderColor: BLUE,
                    backgroundColor: 'rgba(67,97,238,0.08)',
                    borderWidth: 2.5,
                    pointBackgroundColor: BLUE,
                    pointRadius: isMonthly ? 4 : 3,
                    pointHoverRadius: 6,
                    fill: true,
                    tension: 0.35,
                    yAxisID: 'yRate',
                    order: 1
                },
                {
                    type: 'bar',
                    label: '預約次數',
                    data: trend.map(t => t.BookingCount),
                    backgroundColor: 'rgba(67,97,238,0.18)',
                    borderRadius: 3,
                    yAxisID: 'yCount',
                    order: 2
                }
            ]
        },
        options: {
            responsive: true,
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: { display: true, labels: { boxWidth: 12, font: { size: 12 } } },
                tooltip: {
                    callbacks: {
                        afterBody: items => {
                            const t = trend[items[0].dataIndex];
                            return [`使用 ${t.UsedHours} 時 / 開放 ${t.AvailableHours} 時`, `費用 ${t.Revenue} 元`];
                        }
                    }
                }
            },
            scales: {
                yRate: {
                    type: 'linear', position: 'left',
                    min: 0, max: 100,
                    ticks: { callback: v => v + '%' },
                    grid: { color: GRID }
                },
                yCount: {
                    type: 'linear', position: 'right',
                    min: 0,
                    ticks: { stepSize: 1, callback: v => v + ' 次' },
                    grid: { display: false }
                },
                x: { grid: { display: false } }
            }
        }
    });
}

function renderRoomPieChart(byRoom) {
    const ctx = document.getElementById('chartRoomPie');
    if (!ctx || !window.Chart || !byRoom || !byRoom.length) return;

    const data = byRoom.filter(r => r.UsedHours > 0);
    if (!data.length) return;

    const palette = data.map((_, i) =>
        `rgba(67,97,238,${(0.25 + 0.75 * i / Math.max(data.length - 1, 1)).toFixed(2)})`
    );

    chartRoomPie = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: data.map(r => r.RoomName),
            datasets: [{
                data: data.map(r => r.UsedHours),
                backgroundColor: palette,
                borderWidth: 2,
                borderColor: '#fff'
            }]
        },
        options: {
            responsive: true,
            plugins: {
                legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } },
                tooltip: {
                    callbacks: {
                        label: c => ` ${c.label}：${c.parsed} 時 (${c.dataset.data.reduce((a, b) => a + b, 0) > 0 ? Math.round(c.parsed / c.dataset.data.reduce((a, b) => a + b, 0) * 100) : 0}%)`
                    }
                }
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
                    label: '總時數',
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
                        afterTitle: c => `使用率 ${sorted[c[0].dataIndex].UsageRate}%  預約 ${sorted[c[0].dataIndex].BookingCount} 次`,
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
                tooltip: {
                    callbacks: {
                        label: c => ` ${c.parsed.x} 時`,
                        afterLabel: c => {
                            const d = sorted[c.dataIndex];
                            return [` 預約 ${d.BookingCount} 次`, ` 費用 ${d.Revenue} 元`, ` 使用率 ${d.UsageRate}%`];
                        }
                    }
                }
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
        this.exportCsv = exportCsv;
        this.exportRaw = exportRaw;
        this.downloadChartPng = downloadChartPng;
        this.downloadTrendCsv = downloadTrendCsv;
        this.downloadRoomCsv = downloadRoomCsv;
        this.downloadDeptCsv = downloadDeptCsv;
        onMounted(() => loadStats());
    }
};
