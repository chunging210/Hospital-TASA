// Views/Announcement/Manage.cshtml.js
import global from '/global.js';
import 'bootstrap';
const { reactive, ref, onMounted } = Vue;

let quill = null;

window.$config = {
    setup() {
        // ── State ──────────────────────────────────────
        const tab = ref('announcement');
        const loading = ref(true);
        const saving = ref(false);
        const uploading = ref(false);
        const list = reactive([]);
        const links = reactive([]);
        const pendingFiles = ref([]);
        const form = reactive({ id: null, title: '', content: '', isPinned: false, isDefaultExpanded: false, isActive: true, endDate: '', attachments: [] });
        const linkForm = reactive({ id: null, title: '', url: '' });

        // ── 公告 ──────────────────────────────────────
        const loadList = async () => {
            loading.value = true;
            const res = await fetch('/api/announcement/managelist').then(r => r.json());
            copy(list, res);
            loading.value = false;
        };

        const openCreate = () => {
            Object.assign(form, { id: null, title: '', content: '', isPinned: false, isDefaultExpanded: false, isActive: true, endDate: '', attachments: [] });
            pendingFiles.value = [];
            showAnnModal();
        };

        const openEdit = async (id) => {
            pendingFiles.value = [];
            const res = await fetch(`/api/announcement/detail?id=${id}`).then(r => r.json());
            Object.assign(form, {
                id: res.Id,
                title: res.Title,
                content: res.Content,
                isPinned: res.IsPinned,
                isDefaultExpanded: res.IsDefaultExpanded,
                isActive: res.IsActive,
                endDate: res.EndDate || '',
                attachments: (res.Attachments || []).map(a => ({
                    id: a.Id, fileName: a.FileName, fileType: a.FileType, fileSize: a.FileSize, url: a.Url
                }))
            });
            showAnnModal();
        };

        const showAnnModal = () => {
            const el = document.getElementById('annModal');
            const modal = bootstrap.Modal.getOrCreateInstance(el);
            modal.show();
            el.addEventListener('shown.bs.modal', () => {
                if (!quill) {
                    quill = new Quill('#quillEditor', { theme: 'snow' });
                }
                quill.root.innerHTML = form.content || '';
            }, { once: true });
        };

        const saveAnn = async () => {
            if (!form.title) return addAlert('請填寫標題', { type: 'warning' });
            form.content = quill ? quill.root.innerHTML : '';
            if (!form.content || form.content === '<p><br></p>') return addAlert('請填寫內容', { type: 'warning' });

            saving.value = true;
            const url = form.id ? '/api/announcement/update' : '/api/announcement/insert';
            const res = await fetch(url, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ...form })
            });

            if (!res.ok) {
                saving.value = false;
                return addAlert('儲存失敗，請重試', { type: 'danger' });
            }

            const data = await res.json().catch(() => null);
            const wasNew = !form.id;
            if (wasNew && data?.id) form.id = data.id;

            // 新增時，把待上傳附件一次上傳
            if (wasNew && pendingFiles.value.length > 0) {
                for (const file of pendingFiles.value) {
                    const fd = new FormData();
                    fd.append('file', file);
                    await fetch(`/api/announcement/uploadattachment?announcementId=${form.id}`, {
                        method: 'POST', body: fd
                    });
                }
                pendingFiles.value = [];
            }

            saving.value = false;
            bootstrap.Modal.getInstance(document.getElementById('annModal'))?.hide();
            addAlert('儲存成功', { type: 'success' });
            loadList();
        };

        const deleteAnn = async (id, title) => {
            if (!confirm(`確定刪除公告「${title}」？`)) return;
            const res = await fetch('/api/announcement/delete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id })
            });
            if (res.ok) {
                addAlert('已刪除', { type: 'success' });
                loadList();
            }
        };

        // 選檔：新增模式暫存，編輯模式直接上傳
        const uploadFile = async (e) => {
            const file = e.target.files[0];
            if (!file) return;
            e.target.value = '';

            if (!form.id) {
                pendingFiles.value.push(file);
                return;
            }

            const fd = new FormData();
            fd.append('file', file);
            uploading.value = true;
            const res = await fetch(`/api/announcement/uploadattachment?announcementId=${form.id}`, {
                method: 'POST', body: fd
            });
            uploading.value = false;
            if (res.ok) {
                const a = await res.json();
                form.attachments.push({ id: a.Id, fileName: a.FileName, fileType: a.FileType, fileSize: a.FileSize, url: a.Url });
            } else {
                const err = await res.text().catch(() => '上傳失敗');
                addAlert(err || '上傳失敗', { type: 'danger' });
            }
        };

        const removePending = (idx) => pendingFiles.value.splice(idx, 1);

        const fileIcon = (ext) => {
            if (!ext) return 'attach_file';
            const t = ext.toLowerCase();
            if (t === 'pdf') return 'picture_as_pdf';
            if (['jpg','jpeg','png','gif'].includes(t)) return 'image';
            if (['doc','docx'].includes(t)) return 'description';
            if (['xls','xlsx'].includes(t)) return 'table_chart';
            if (['ppt','pptx'].includes(t)) return 'slideshow';
            if (['zip','rar'].includes(t)) return 'folder_zip';
            return 'attach_file';
        };

        const deleteAttachment = async (attId) => {
            if (!confirm('確定刪除此附件？')) return;
            await fetch('/api/announcement/deleteattachment', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id: attId })
            });
            const idx = form.attachments.findIndex(a => a.id === attId);
            if (idx !== -1) form.attachments.splice(idx, 1);
        };

        // ── 超連結 ───────────────────────────────────
        const loadLinks = async () => {
            const res = await fetch('/api/announcement/quicklinksmanage').then(r => r.json());
            copy(links, res);
        };

        const openLinkCreate = () => {
            Object.assign(linkForm, { id: null, title: '', url: '', isActive: true });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('linkModal')).show();
        };

        const openLinkEdit = (id, title, url, isActive) => {
            Object.assign(linkForm, { id, title, url, isActive });
            bootstrap.Modal.getOrCreateInstance(document.getElementById('linkModal')).show();
        };

        const saveLink = async () => {
            if (!linkForm.title) return addAlert('請填寫顯示文字', { type: 'warning' });
            if (!linkForm.url) return addAlert('請填寫網址', { type: 'warning' });
            saving.value = true;
            const apiUrl = linkForm.id ? '/api/announcement/quicklinkupdate' : '/api/announcement/quicklinkinsert';
            const res = await fetch(apiUrl, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ ...linkForm })
            });
            saving.value = false;
            if (res.ok) {
                bootstrap.Modal.getInstance(document.getElementById('linkModal'))?.hide();
                addAlert('儲存成功', { type: 'success' });
                loadLinks();
            }
        };

        const deleteLink = async (id, title) => {
            if (!confirm(`確定刪除連結「${title}」？`)) return;
            await fetch('/api/announcement/quicklinkdelete', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ id })
            });
            addAlert('已刪除', { type: 'success' });
            loadLinks();
        };

        // ── 初始化 ─────────────────────────────────────
        const initSortable = () => {
            const el = document.getElementById('linkSortable');
            if (!el || !window.Sortable) return;
            Sortable.create(el, {
                handle: '.link-drag-handle',
                animation: 150,
                onEnd: async () => {
                    const ids = [...el.querySelectorAll('tr[data-id]')]
                        .map(tr => tr.getAttribute('data-id'));
                    await fetch('/api/announcement/quicklinkreorder', {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify(ids)
                    });
                }
            });
        };

        onMounted(() => {
            loadList();
            loadLinks().then(() => initSortable());
        });

        return {
            tab, loading, saving, uploading,
            list, links, form, linkForm, pendingFiles,
            openCreate, openEdit, saveAnn, deleteAnn,
            uploadFile, removePending, deleteAttachment,
            openLinkCreate, openLinkEdit, saveLink, deleteLink,
            fileIcon,
        };
    }
};
