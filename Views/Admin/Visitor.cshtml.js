// visitor
import global from '/global.js';
const { ref, reactive, onMounted, watch } = Vue;

export const department = new function () {
    this.list = reactive([]);
    this.getList = () => {
        global.api.select.department()
            .then((response) => {
                copy(this.list, response.data);
            });
    }
}

class VM {
    Id = null;
    CName = '';
    EName = '';
    CompanyName = '';
    JobTitle = '';
    Phone = '';
    Email = '';
    LicensePlate = '';
    CarType = '';
    IsEnabled = true;
}

const getCarType = (carType) => {
    const carTypeMap = {
        'motorcycle': '機車',
        'car': '汽車',
        'truck_bus': '貨車/大客車',
        'other': '其他車種'
    };
    return carTypeMap[carType] || carType;
};

const debounce = (func, delay) => {
    let timeoutId;
    return (...args) => {
        clearTimeout(timeoutId);
        timeoutId = setTimeout(() => func.apply(null, args), delay);
    };
};

let currentSearchController = null;
let visitorpageRef = null;

export const visitor = new function () {
    this.query = reactive({ Keyword: '', CarType: '' });
    this.list = reactive([]);
    this.modal = {};
    this.vm = reactive(new VM());
    this.loading = ref(false);
    this.submitting = ref(false);

    // ✅ 正確的 visitor.getList - 完全參考 UCMS3 的 table.get
    this.getList = async (pagination) => {
        try {
            if (currentSearchController) {
                currentSearchController.abort();
            }

            this.loading.value = true;
            currentSearchController = new AbortController();

            const queryParams = {};
            if (this.query.Keyword && this.query.Keyword.trim()) {
                queryParams.keyword = this.query.Keyword.trim();
            }
            if (this.query.CarType) {
                queryParams.carType = this.query.CarType;
            }

            const options = { body: queryParams, signal: currentSearchController.signal };

            // ✅ 參考 UCMS3 的寫法
            const request = pagination && visitorpageRef
                ? global.api.visitor.list(visitorpageRef.setHeaders(options))
                : global.api.visitor.list(options);

            const response = await request;

            console.log('後端回傳的資料:', response.data);

            // ✅ 參考 UCMS3 的寫法
            if (pagination && visitorpageRef) {
                visitorpageRef.setTotal(response);
            }

            if (Array.isArray(response.data)) {
                this.list.splice(0, this.list.length, ...response.data);
            } else {
                this.list.splice(0, this.list.length);
            }

        } catch (err) {
            if (err.name === 'AbortError') return;
            addAlert({ message: `${err.status ?? ''} ${err.message ?? err}`, type: 'danger' });
        } finally {
            this.loading.value = false;
            currentSearchController = null;
        }
    };

    this.getVM = (id) => {
        if (!id) {
            window.copy(this.vm, new VM());
            this.modal.show();
            return;
        }
        global.api.visitor.detail({ body: { id } })
            .then(res => { window.copy(this.vm, res.data); this.modal.show(); })
            .catch(err => addAlert({ message: `${err.status} - ${err.message}`, type: 'danger' }));
    };

    this.save = async () => {
        if (this.submitting.value) return;
        if (!this.vm.CName?.trim()) {
            addAlert({ message: '中文名必填', type: 'warning' });
            return;
        }

        this.submitting.value = true;
        try {
            const body = { ...this.vm };
            const call = this.vm.Id ? global.api.visitor.update : global.api.visitor.insert;
            await call({ body });
            addAlert({ message: this.vm.Id ? '更新成功' : '新增成功', type: 'success' });
            this.modal.hide();
            await this.getList(true);
        } catch (err) {
            console.log(err.details)
            if (err.details && typeof err.details === 'string') {
                const errorLines = err.details.split(',').map(e => e.trim()).filter(e => e);
                const errorMessage = errorLines.join('\n');
                addAlert({ message: errorMessage, type: 'danger' });
            } else {
                addAlert({ message: `${err.status ?? ''} ${err.message ?? err}`, type: 'danger' });
            }
        } finally {
            this.submitting.value = false;
        }
    };

    this.delete = (id) => {
        if (!confirm('確認刪除?')) return;
        global.api.visitor.delete({ body: { id } })
            .then(() => {
                addAlert({ message: '刪除成功', type: 'success' });
                this.getList(true);
            })
            .catch(err => addAlert({ message: `${err.status} - ${err.message}`, type: 'danger' }));
    };
}

const debouncedSearch = debounce(() => {
    if (visitorpageRef) {
        visitorpageRef.go(1);
    }
    visitor.getList(true);
}, 300);

window.$config = {
    setup: () => new function () {
        this.visitor = visitor;
        this.department = department;
        this.visitormodal = ref(null);
        this.visitorpage = ref(null);
        this.loading = visitor.loading;
        this.getCarType = getCarType;

        this.search = () => {
            if (visitorpageRef) {
                visitorpageRef.go(1);
            }
            visitor.getList(true);
        };

        this.clearSearch = () => {
            visitor.query.Keyword = '';
            if (visitorpageRef) {
                visitorpageRef.go(1);
            }
            visitor.getList(true);
        };

        watch(() => visitor.query.Keyword, (newValue) => {
            if (newValue.trim() === '') {
                if (visitorpageRef) {
                    visitorpageRef.go(1);
                }
                visitor.getList(true);
                return;
            }
            debouncedSearch();
        });

        watch(() => visitor.query.CarType, () => {
            if (visitorpageRef) {
                visitorpageRef.go(1);
            }
            visitor.getList(true);
        });


        onMounted(() => {
            // ✅ 在 onMounted 時初始化 Modal（而不是在 class 定義時）
            visitor.modal = new bootstrap.Modal(this.visitormodal.value);
            visitorpageRef = this.visitorpage.value;

            // ✅ 初始化：載入部門和訪客列表
            department.getList();
            visitor.getList(true);
        });
    }
};