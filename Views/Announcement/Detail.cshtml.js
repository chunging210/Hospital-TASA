// Views/Announcement/Detail.cshtml.js
import global from '/global.js';
const { ref, onMounted } = Vue;

window.$config = {
    setup() {
        const loading = ref(true);
        const item = ref(null);

        const announcementId = document.getElementById('announcementId')?.value;

        const load = async () => {
            if (!announcementId) { loading.value = false; return; }
            const res = await fetch(`/api/announcement/detail?id=${announcementId}`);
            if (res.ok) item.value = await res.json();
            loading.value = false;
        };

        onMounted(() => { load(); });

        return { loading, item };
    }
};
