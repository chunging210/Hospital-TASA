// Conference
import global from '/global.js';
const { ref, reactive, onMounted, watch } = Vue;

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

export const visitor = new function () {
    this.query = reactive({ Keyword: '', CarType: '' });
    this.list = reactive([]);
    this.page = {};
    this.modal = {};
    this.vm = reactive({});
    this.loading = ref(false);
    this.submitting = ref(false);
    this.manual = reactive([]);
    this.new = reactive({ CName: '', Email: '', CompanyName: '', Phone: '' });

    // ✅ 簡化 getList，就像 createby 那樣
    this.getList = (page) => {
        const queryParams = {};
        if (this.query.Keyword?.trim()) {
            queryParams.keyword = this.query.Keyword.trim();
        }
        if (this.query.CarType) {
            queryParams.carType = this.query.CarType;
        }

        // ✅ 只在需要時才加 List 和 Contains
        if (this.query.Contains === true) {
            // 「已選擇」時，傳送已選的訪客 ID
            queryParams.List = conference.vm.Guests;
            queryParams.Contains = true;
        }

        this.loading.value = true;

        if (page) {
            this.page.data.page = page;
        } else if (!this.page.data.page) {
            this.page.data.page = 1;
        }

        global.api.visitor.list(this.page.setHeaders({ body: queryParams }))
            .then(this.page.setTotal)
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(err => {
                addAlert({ message: `${err.status ?? ''} ${err.message ?? err}`, type: 'danger' });
            })
            .finally(() => {
                this.loading.value = false;
            });
    };

    this.addManual = () => {
        if (!this.new.CName?.trim()) {
            addAlert({ message: '請輸入姓名', type: 'warning' });
            return;
        }
        if (!this.new.Email?.trim()) {
            addAlert({ message: '請輸入 Email', type: 'warning' });
            return;
        }
        if (!this.new.CompanyName?.trim()) {
            addAlert({ message: '請輸入公司名稱', type: 'warning' });
            return;
        }
        if (!this.new.Phone?.trim()) {
            addAlert({ message: '請輸入電話', type: 'warning' });
            return;
        }

        if (!/^\d{10}$/.test(this.new.Phone)) {
            addAlert({ message: '電話需為 10 碼數字', type: 'warning' });
            return;
        }
        if (!/@/.test(this.new.Email)) {
            addAlert({ message: 'Email 格式不正確', type: 'warning' });
            return;
        }

        const guest = {
            CName: this.new.CName.trim(),
            Email: this.new.Email.trim(),
            CompanyName: this.new.CompanyName.trim(),
            Phone: this.new.Phone.trim()
        };
        this.manual.push(guest);
        conference.vm.GuestsManual.push(guest);

        Object.assign(this.new, {
            CName: '',
            Email: '',
            CompanyName: '',
            Phone: ''
        });

        this.getList(1);
        addAlert({ message: '新增成功', type: 'success' });
    };

    this.removeManual = (idx) => {
        const g = this.manual[idx];
        this.manual.splice(idx, 1);
        conference.vm.GuestsManual = conference.vm.GuestsManual.filter(x => x.Email !== g.Email);
    };
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
    Guests = [];  // ✅ 選中的訪客 IDs
    GuestsManual = [];  // ✅ 臨時訪客
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
    if (entity.Visitor) {
        entity.Guests = entity.Visitor.map(x => x.Id);
    }

    if (!entity.GuestsManual) {
        entity.GuestsManual = [];
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

    this.query = reactive({
        Start: new Date().format(),
        End: new Date().addDays(7).format(),
        RoomId: '',
        UserId: '',
        keyword: ''
    });

    this.list = reactive([]);
    this.loading = ref(false);

    // ✅ 統一的新 getList
    this.getList = async (pagination) => {
        try {
            this.loading.value = true;

            const options = { body: this.query };

            const request = pagination && conferencepageRef
                ? global.api.conference.list(
                    conferencepageRef.setHeaders(options)
                )
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

    // ✅ 所有 filter 都走這裡
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
        this.department = department;
        this.createby = createby;
        this.conference = conference;
        this.conferencepage = ref(null);
        this.conferenceoffcanvas = ref(null);
        this.template = template;
        this.mcu = mcu;
        this.schedule = schedule;
        this.schedulepage = ref(null);
        this.visitor = visitor;
        this.visitorpage = ref(null);

        watch(() => conference.query.keyword, () => {
            conference.onFilterChange();
        });

        onMounted(() => {
            me.getVM();
            room.getList();
            createby.getList();


            // ✅ 接 page ref
            conferencepageRef = this.conferencepage.value;

            // ✅ 第一次一定用 pagination
            conference.getList(true);
            schedule.page = this.schedulepage.value;
            visitor.page = this.visitorpage.value;

            visitor.getList();
        });
    }
}