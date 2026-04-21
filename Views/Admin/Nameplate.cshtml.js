import global from '/global.js';
const { ref, reactive, onMounted, nextTick } = Vue;

let pageRef = null;

const emptyForm = () => ({
    Id: null,
    DeviceType: 0,
    Name: '',
    Host: '',
    Port: null,
    Mac: '',
    DistributorId: null,
    IsEnabled: true,
});

const query        = reactive({ keyword: '', deviceType: '', isEnabled: '' });
const list         = ref([]);
const loading      = ref(false);
const saving       = ref(false);
const distributorOptions = ref([]);
const form         = reactive(emptyForm());
const nameplatepage = ref(null);
let modalInstance  = null;

const getList = async (pagination) => {
    loading.value = true;
    try {
        const options = {};
        if (query.keyword)          options.keyword    = query.keyword;
        if (query.deviceType !== '') options.deviceType = query.deviceType;
        if (query.isEnabled  !== '') options.isEnabled  = query.isEnabled;

        const usePagination = pagination && pageRef;
        const req = usePagination
            ? global.api.nameplate.nameplatelist(pageRef.setHeaders({ body: options }))
            : global.api.nameplate.nameplatelist({ body: options });

        const res = await req;
        if (usePagination) pageRef.setTotal(res);
        list.value = res.data || [];
    } catch (e) {
        console.error('載入失敗', e);
    } finally {
        loading.value = false;
    }
};

const loadDistributorOptions = async () => {
    try {
        const res = await global.api.nameplate.nameplateoptions();
        distributorOptions.value = res.data;
    } catch { /* 靜默 */ }
};

const openModal = async (id = null) => {
    Object.assign(form, emptyForm());
    if (id) {
        try {
            const res = await global.api.nameplate.nameplatedetail({ body: { id } });
            Object.assign(form, res.data);
        } catch {
            console.error('載入資料失敗');
            return;
        }
    }
    await loadDistributorOptions();
    modalInstance?.show();
};

const save = async () => {
    if (!form.Name?.trim()) {
        alert('請填寫名稱');
        return;
    }
    saving.value = true;
    try {
        if (form.Id) {
            await global.api.nameplate.nameplateupdate({ body: { ...form } });
        } else {
            await global.api.nameplate.nameplateinsert({ body: { ...form } });
        }
        modalInstance?.hide();
        if (pageRef) pageRef.go(1);
        getList(!!pageRef);
    } catch (e) {
        console.error('儲存失敗', e);
    } finally {
        saving.value = false;
    }
};

const deleteItem = async (id) => {
    if (!confirm('確定刪除此裝置？')) return;
    try {
        await global.api.nameplate.nameplatedelete({ body: { id } });
        getList(!!pageRef);
    } catch (e) {
        console.error('刪除失敗', e);
    }
};

window.$config = {
    setup() {
        onMounted(async () => {
            await nextTick();
            pageRef = nameplatepage.value;
            const el = document.getElementById('nameplateModal');
            if (el) modalInstance = new bootstrap.Modal(el);
            getList(!!pageRef);
        });

        return {
            query, list, loading, saving,
            form, distributorOptions, nameplatepage,
            getList, openModal, save, deleteItem,
        };
    }
};
