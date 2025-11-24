// Conference
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

export const createby = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.conferencecreateby()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

class VM {
    Id = null;
    Name = '';
    UsageType = '1';
    MCU = mcu.first();
    Recording = true;
    StartTime = new Date().format('yyyy-mm-dd HH:MM');
    StartNow = false;
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

const toVM = (entity) => {
    if (entity.StartTime) {
        entity.StartTime = new Date(entity.StartTime).format('yyyy-mm-dd HH:MM');
    }
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

export const template = new function () {
    this.list = reactive({});
    this.getList = () => {
        global.api.conferencetemplate.list()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
    this.select = ref('');
    this.clone = (id) => {
        global.api.conferencetemplate.detail({ body: { id: this.select.value } })
            .then((response) => {
                let starttime = conference.vm.StartTime;
                copy(conference.vm, toVM(response.data));
                conference.vm.Id = null;
                conference.vm.CreateBy = me.vm.Name;
                conference.vm.StartTime = starttime;
                this.select.value = '';
                addAlert('已套用範本');
            });
    }
    this.save = () => {
        let body = { ...conference.vm }
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
}

export const mcu = new function () {
    this.list = reactive([]);
    global.setting
        .then((response) => {
            if (response.data.UCNS.Webex) {
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
        if (conference.vm.StartNow || conference.vm.StartTime) {
            this.query.ScheduleDate = conference.vm.StartNow ? new Date() : new Date(conference.vm.StartTime);
            this.query.List = [...conference.vm.User, conference.vm.Host, conference.vm.Recorder].filter(x => x != null);
            this.page.data.page = page || this.page.data.page;
            global.api.select.userschedule(this.page.setHeaders({ body: this.query }))
                .then(this.page.setTotal)
                .then((response) => {
                    copy(this.list, response.data);
                });
        }
    }
    this.format = (item) => {
        return item.map(x => `${new Date(x.StartTime).format('HH:MM')}~${new Date(x.EndTime).format('HH:MM')}`).join(',');
    }
}

export const conference = new function () {
    this.query = reactive({ Start: new Date().format(), End: new Date().addDays(7).format(), RoomId: '', DepartmentId: '', UserId: '' });
    this.list = reactive([]);
    this.getList = () => {
        global.api.conference.list({ body: this.query })
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
        template.getList();
        if (id) {
            global.api.conference.detail({ body: { id } })
                .then((response) => {
                    if (new Date(response.data.StartTime) < new Date()) {
                        addAlert('會議已開始，無法編輯', { type: 'danger' });
                    } else {
                        copy(this.vm, toVM(response.data));
                        this.show();
                    }
                })
                .catch(error => {
                    addAlert(getMessage(error), { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            this.vm.CreateBy = me.vm.Name;
            this.vm.User.push(me.vm.Id);
            this.show();
        }
    }
    this.ecsShow = (roomid) => this.vm.Room.includes(roomid);
    this.saved = () => { }
    this.save = () => {
        let body = { ...this.vm }
        body.StartTime = new Date(body.StartTime);
        let edit = this.vm.Id ? global.api.conference.update : global.api.conference.insert;
        return edit({ body })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                this.offcanvas.hide();
            })
            .then(this.saved)
            .catch(error => {
                addAlert(getMessage(error), { type: 'danger', click: error.download });
            });
    }
    this.copy = (id) => {
        template.getList();
        global.api.conference.detail({ body: { id } })
            .then((response) => {
                copy(this.vm, toVM(response.data));
                this.vm.Id = null;
                this.vm.CreateBy = me.vm.Name;
                this.vm.MCU = this.vm.MCU || 7;
                this.vm.Recording = this.vm.UsageType == 2 ? this.vm.Recording : true;
                this.vm.StartTime = new Date().format('yyyy-mm-dd HH:MM');
                this.show();
            });
    }
    this.deleted = () => { }
    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.conference.delete({ body: { id } })
                .then((response) => {
                    addAlert('操作成功');
                    this.getList();
                })
                .then(this.deleted)
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
        this.createby = createby;
        this.conference = conference;
        this.conferenceoffcanvas = ref(null);
        this.template = template;
        this.mcu = mcu;
        this.schedule = schedule;
        this.schedulepage = ref(null);

        onMounted(() => {
            me.getVM();
            room.getList();
            department.getList();
            department.getTree();
            createby.getList();
            conference.getList();
            conference.offcanvas = this.conferenceoffcanvas.value;
            schedule.page = this.schedulepage.value;
        });
    }
}