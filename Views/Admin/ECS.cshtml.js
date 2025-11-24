// Admin/ECS
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

class VM {
    Id = null;
    Name = '';
    RoomId = '';
    Macro = '';
    Equipment = [];
    IsEnabled = true;
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

export const equipment = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.equipment()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

const ecs = new function () {
    this.query = reactive({ keyword: '' });
    this.list = reactive([]);
    this.getList = () => {
        global.api.admin.ecslist({ body: this.query })
            .then((response) => {
                copy(this.list, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }
    this.offcanvas = {}
    this.vm = reactive(new VM());
    this.getVM = (id) => {
        if (id) {
            global.api.admin.ecsdetail({ body: { id } })
                .then((response) => {
                    copy(this.vm, response.data);
                    this.offcanvas.show();
                })
                .catch(error => {
                    addAlert('取得資料失敗', { type: 'danger', click: error.download });
                });
        } else {
            copy(this.vm, new VM());
            this.offcanvas.show();
        }
    }
    this.save = () => {
        const method = this.vm.Id ? global.api.admin.ecsupdate : global.api.admin.ecsinsert;
        method({ body: this.vm })
            .then((response) => {
                addAlert('操作成功');
                this.getList();
                this.offcanvas.hide();
            })
            .catch(error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }
    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.ecsdelete({ body: { id } })
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
    this.test = (id) => {
        global.api.admin.ecstest({ body: { id } })
            .then((response) => {
                addAlert('已發送');
            })
            .catch(error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }
}

window.$config = {
    setup: () => new function () {
        this.room = room;
        this.equipment = equipment;
        this.ecs = ecs;
        this.ecsoffcanvas = ref(null);

        onMounted(() => {
            room.getList();
            equipment.getList();
            ecs.getList();
            ecs.offcanvas = this.ecsoffcanvas.value;
            //window.addEventListener('ctrls', () => authuser.save());
        });
    }
}