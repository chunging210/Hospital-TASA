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