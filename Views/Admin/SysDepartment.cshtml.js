// Admin/SysDepartment
import global from '/global.js';
const { ref, reactive, onMounted, computed, watch } = Vue;

class VM {
    Id = null;
    Parent = null;
    Name = '';
    IsEnabled = true;
}

const department = new function () {
    this.query = reactive({ keyword: '' });
    this.tree = reactive([]);
    this.getTree = () => {
        global.api.select.departmenttree()
            .then((response) => {
                copy(this.tree, response.data);
            })
            .catch(error => {
                addAlert('取得資料失敗', { type: 'danger', click: error.download });
            });
    }
    this.list = reactive([]);
    this.getList = () => {
        global.api.admin.departmentlist({ body: this.query })
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
            global.api.admin.departmentdetail({ body: { id } })
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
        const method = this.vm.Id ? global.api.admin.departmentupdate : global.api.admin.departmentinsert;
        method({ body: this.vm })
            .then((response) => {
                addAlert('操作成功');
                this.getTree();
                this.getList();
                this.offcanvas.hide();
            })
            .catch(error => {
                addAlert(error.details, { type: 'danger', click: error.download });
            });
    }
    this.delete = (id) => {
        if (confirm('確認刪除?')) {
            global.api.admin.departmentdelete({ body: { id } })
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
        this.department = department;
        this.departmentoffcanvas = ref(null);

        watch(() => department.query.keyword, () => {
            department.getList();
        });

        onMounted(() => {
            department.getTree();
            department.getList();
            department.offcanvas = this.departmentoffcanvas.value;
            //window.addEventListener('ctrls', () => authuser.save());
        });


    }
}