// Views/Announcement/Index.cshtml.js
import '/global.js';
import 'bootstrap';
const { reactive, ref, computed, onMounted } = Vue;

window.$config = {
    setup() {
        const loading = ref(true);
        const list = reactive([]);
        const quickLinks = reactive([]);
        const expanded = reactive({});

        const pinnedList = computed(() => list.filter(x => x.IsPinned));
        const normalList = computed(() => list.filter(x => !x.IsPinned));

        const toggle = (id) => {
            if (expanded[id]) delete expanded[id];
            else expanded[id] = true;
        };

        const load = async () => {
            loading.value = true;
            const [listRes, linkRes] = await Promise.all([
                fetch('/api/announcement/list').then(r => r.json()),
                fetch('/api/announcement/quicklinks').then(r => r.json()),
            ]);
            copy(list, listRes);
            copy(quickLinks, linkRes);
            // 將 IsDefaultExpanded = true 的公告預設展開
            listRes.forEach(item => {
                if (item.IsDefaultExpanded) expanded[item.Id] = true;
            });
            loading.value = false;
        };

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

        onMounted(() => {
            load();
        });

        return { loading, list, quickLinks, pinnedList, normalList, expanded, toggle, fileIcon };
    }
};
