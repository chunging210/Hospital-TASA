// Admin/Equipment
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

class VM {
    Id = null;
    Name = '';
    Type = 3;
    Host = '';
    Port = null;
    Account = '';
    Password = '';
    IsEnabled = true;
}

const equipment = new function () {
    this.query = reactive({ keyword: '' });
    this.list = reactive([]);
    this.getList = () => {
        global.api.admin.equipmentlist({ body: this.query })
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
            global.api.admin.equipmentdetail({ body: { id } })
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
        const method = this.vm.Id ? global.api.admin.equipmentupdate : global.api.admin.equipmentinsert;
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
            global.api.admin.equipmentdelete({ body: { id } })
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
    setup: () => new function () {
        this.equipment = equipment;
        this.equipmentoffcanvas = ref(null);       

        onMounted(() => {
            equipment.getList();
            equipment.offcanvas = this.equipmentoffcanvas.value;
            //window.addEventListener('ctrls', () => authuser.save());
        });
    }
}