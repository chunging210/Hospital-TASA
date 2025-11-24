// Calendar
import '/js/fullcalendar/index.global.js';
import '/js/fullcalendar/locales-all.global.js';
import VSelect from 'vselect';
import global from '/global.js';
import { me, room, department, template, mcu, schedule, conference } from '/Views/Conference/Index.cshtml.js';
const { ref, reactive, onMounted, watch } = Vue;

export const user = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.user()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

export const recent = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.calendar.recent()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

export const calendar = new function () {
    this.fc = {};
    this.config = {
        schedulerLicenseKey: 'CC-Attribution-NonCommercial-NoDerivatives',
        locale: 'zh-tw',
        headerToolbar: { left: 'prev,next today dayGridMonth,timeGridWeek,timeGridDay', right: 'title' },
        initialView: 'dayGridMonth',
        contentHeight: 'auto',
        navLinks: true,
        eventTimeFormat: { hour: '2-digit', minute: '2-digit', hour12: false },
        slotDuration: { hours: 0.25 },
        slotLabelInterval: { hours: 0.25 },
        slotMinWidth: 150,

        //selectable: false,
        //selectMirror: true, 
        //viewDidMount: function (info) {
        //    const isWeekOrDayView = info.view.type === 'timeGridWeek' || info.view.type === 'timeGridDay';
        //    info.view.calendar.setOption('selectable', isWeekOrDayView);
        //},
        //select: function (selectInfo) {
        //    // 這裡可以讓使用者輸入事件名稱
        //    let title = prompt('請輸入會議名稱:')
        //    let calendarApi = selectInfo.view.calendar

        //    calendarApi.unselect() // 取消目前時段的選取

        //    if (title) {
        //        calendarApi.addEvent({
        //            id: Date.now().toString(), // 建議使用更穩定的 ID
        //            title,
        //            start: selectInfo.startStr,
        //            end: selectInfo.endStr,
        //            allDay: selectInfo.allDay
        //        })
        //    }
        //}
    }
    this.query = reactive({ localtion: [], user: [], department: [], keyword: '' });
    this.list = reactive([]);
    this.dataMap = (item) => {
        return {
            title: item.Title,
            start: item.Start,
            end: item.End,
            extendedProps: item.ExtendedProps,
            classNames: item.ExtendedProps.Self ? ['bg-danger'] : []
        };
    };
    this.dataFilter = (item) => {
        let result = true;
        if (this.query.keyword) {
            result &= item.title.toLowerCase().includes(this.query.keyword.toLowerCase());
        }
        if (this.query.localtion.length > 0) {
            if (item.extendedProps.Room.length == 0) {
                result &= false;
            } else {
                let matched = false;
                for (const r of item.extendedProps.Room) {
                    matched = matched || this.query.localtion.includes(r);
                }
                result &= matched;
            }
        }
        if (this.query.user.length > 0) {
            if (item.extendedProps.User.length == 0) {
                result &= false;
            } else {
                let matched = false;
                for (const u of item.extendedProps.User) {
                    matched = matched || this.query.user.includes(u);
                }
                result &= matched;
            }
        }
        if (this.query.department.length > 0) {
            if (item.extendedProps.Department.length == 0) {
                result &= false;
            } else {
                let matched = false;
                for (const d of item.extendedProps.Department) {
                    matched = matched || this.query.department.includes(d);
                }
                result &= matched;
            }
        }
        return result;
    };
    this.getList = () => {
        global.api.calendar.list({ body: this.query })
            .then(response => {
                this.list.splice(0);
                response.data.map(this.dataMap).forEach(x => this.list.push(x));
                this.fc.setOption('events', this.list.filter(this.dataFilter));
            })
            .catch(error => {
                addAlert(getMessage(error), { type: 'danger', click: error.download });
            });
        this.offcanvas.hide();
    }
    this.offcanvas = {}
    this.vm = reactive({ Room: [], User: [], Department: [], StartTime: new Date(), EndTime: new Date() });
    this.getVM = (id) => {

    }
}

conference.saved = () => {
    calendar.getList();
    recent.getList();
}
conference.deleted = () => {
    calendar.getList();
    recent.getList();
}

window.$config = {
    components: { VSelect },
    setup: () => new function () {
        this.me = me;
        this.room = room;
        this.user = user;
        this.department = department;
        this.conference = conference;
        this.conferenceoffcanvas = ref(null);
        this.template = template;
        this.mcu = mcu;
        this.schedule = schedule;
        this.schedulepage = ref(null);

        this.recent = recent;

        this.calendar = calendar;
        this.calendarfc = ref(null);
        this.calendaroffcanvas = ref(null);

        watch(
            () => [calendar.query.start, calendar.query.end], calendar.getList
        );

        watch(
            () => [calendar.query.keyword, calendar.query.localtion, calendar.query.user, calendar.query.department],
            () => {
                calendar.fc.setOption('events', calendar.list.filter(calendar.dataFilter));
            }
        );

        const datesSet = (info) => {
            const { start, end } = info;
            if (calendar.query.start instanceof Date && calendar.query.end instanceof Date) {
                const queryDates = { start: calendar.query.start.getTime(), end: calendar.query.end.getTime() };
                const newDates = { start: start.getTime(), end: end.getTime() };
                if (queryDates.start <= newDates.start && newDates.end <= queryDates.end) {
                    return;
                }
            }
            Object.assign(calendar.query, { start, end });
        }

        const eventClick = (info) => {
            const id = info.event.extendedProps.Id;
            global.api.conference.detail({ body: { id } })
                .then(response => {
                    copy(calendar.vm, response.data);
                    calendar.offcanvas.show();
                })
                .catch(error => {
                    addAlert(getMessage(error), { type: 'danger', click: error.download });
                });
        }

        const dayCellContent = (arg) => {
            return { html: arg.dayNumberText.replace('日', '') };
        }

        const dateClick = (info) => {
            conference.getVM();
            conference.vm.StartTime = info.dateStr.format('yyyy-mm-dd 00:00');
        }

        onMounted(() => {
            me.getVM();
            room.getList();
            user.getList();
            department.getList();
            department.getTree();
            recent.getList();
            conference.offcanvas = this.conferenceoffcanvas.value;
            schedule.page = this.schedulepage.value;

            calendar.fc = new FullCalendar.Calendar(this.calendarfc.value, { ...calendar.config, datesSet, eventClick, dayCellContent, dateClick });
            calendar.fc.render();
            calendar.offcanvas = this.calendaroffcanvas.value;
        });
    }
};