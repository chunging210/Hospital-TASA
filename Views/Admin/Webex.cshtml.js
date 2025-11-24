// Admin/webex
import global from '/global.js';
const { ref, reactive, onMounted, computed } = Vue;

class VM {
    Id = null;
    Name = '';
    Client_id = '';
    Client_secret = '';
    Access_token = '';
    Refresh_token = '';
    IsEnabled = true;
}

const webex = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.adminwebex.list()
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
            global.api.adminwebex.detail({ body: { id } })
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
        const method = this.vm.Id ? global.api.adminwebex.update : global.api.adminwebex.insert;
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
            global.api.adminwebex.delete({ body: { id } })
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
    this.getToken = (item) => {
        let params = {
            client_id: item.Client_id,
            response_type: 'code',
            redirect_uri: `${window.location.origin}/webex`,
            scope: 'meeting:recordings_read meeting:schedules_read meeting:schedules_write spark:all',
            state: item.Id,
        };
        console.log(new URLSearchParams(params).toString())
        window.reload = this.getList;
        window.open('https://webexapis.com/v1/authorize?' + new URLSearchParams(params).toString());

    }
}

window.$config = {
    setup: () => new function () {
        this.webex = webex;
        this.webexoffcanvas = ref(null);

        onMounted(() => {
            webex.getList();
            webex.offcanvas = this.webexoffcanvas.value;
            //window.addEventListener('ctrls', () => authuser.save());
        });
    }
}