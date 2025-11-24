// ConferenceTemplate
import global from '/global.js';
const { ref, reactive, onMounted } = Vue;

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

export const department = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.department()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
    this.tree = reactive([]);
    this.getTree = () => {
        global.api.select.departmenttree()
            .then((response) => {
                copy(this.tree, response.data);
            });
    }
}

class VM {
    Id = null;
    Name = '';
    UsageType = '1';
    MCU = mcu.first();
    Recording = true;
    DurationHH = 1;
    DurationSS = 0;
    //RepeatType = '-';
    //RepeatOn = [];
    //rOption = 'count';
    Room = [];
    Ecs = [];
    User = [];
    Department = [];
    Host = null;
    Recorder = null;
}

export const mcu = new function () {
    this.list = reactive([]);
    global.setting
        .then((response) => {
            if (response.data.Webex) {
                this.list.push(7);
            }
        })
    this.first = () => {
        return this.list.length > 0 ? this.list[0] : '';
    }
}

export const schedule = new function () {
    this.query = reactive({ DepartmentId: null, Contains: null, Keyword: '' });
    this.list = reactive([]);
    this.page = {}
    this.getList = (page) => {
        this.query.ScheduleDate = new Date(0);
        this.query.List = [...conference.vm.User, conference.vm.Host, conference.vm.Recorder].filter(x => x != null);
        this.page.data.page = page || this.page.data.page;
        global.api.select.userschedule(this.page.setHeaders({ body: this.query }))
            .then(this.page.setTotal)
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

const toVM = (entity) => {
    if (entity.Room) {
        entity.Room = entity.Room.map(x => x.Id);
    }
    if (entity.User) {
        let host = entity.User.filter(x => x.IsHost);
        entity.Host = host != null && host.length > 0 ? host[0].Id : null;
        let recorder = entity.User.filter(x => x.IsRecorder);
        entity.Recorder = recorder != null && recorder.length > 0 ? recorder[0].Id : null;
        entity.User = entity.User.filter(x => x.IsAttendees).map(x => x.Id);
    }
    if (entity.Department) {
        entity.Department = entity.Department.map(x => x.Id);
    }
    return entity;
}

export const conference = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.conferencetemplate.list()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
    this.offcanvas = {}
    this.vm = reactive(new VM());
    this.show = () => {
        schedule.getList(1);
        this.offcanvas.show();
    }
    this.getVM = (id) => {
        if (id) {
            global.api.conferencetemplate.detail({ body: { id } })
                .then((response) => {
                    copy(this.vm, toVM(response.data));
                    this.show();
                })
                .catch(error => {
                    addAlert(getMessage(error), { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            this.show();
        }
    }
    this.ecsShow = (roomid) => this.vm.Room.includes(roomid);
    this.save = () => {
        let body = { ...conference.vm };
        let edit = body.Id ? global.api.conferencetemplate.update : global.api.conferencetemplate.insert;
        return edit({ body })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                conference.offcanvas.hide();
            })
            .catch(error => {
                addAlert(getMessage(error), { type: 'danger', click: error.download });
            });
    }
    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.conferencetemplate.delete({ body: { id } })
                .then((response) => {
                    alert('操作成功');
                    this.getList();
                })
                .catch(error => {
                    addAlert(getMessage(error), { type: 'danger', click: error.download });
                });
        }
    }
}

window.$config = {
    components: {},
    setup: () => new function () {
        this.me = me;
        this.room = room;
        this.department = department;
        this.conference = conference;
        this.conferenceoffcanvas = ref(null);
        this.mcu = mcu;
        this.schedule = schedule;
        this.schedulepage = ref(null);

        onMounted(() => {
            me.getVM();
            room.getList();
            department.getList();
            department.getTree();
            conference.getList();
            conference.offcanvas = this.conferenceoffcanvas.value;
            schedule.page = this.schedulepage.value;
        });
    }
}