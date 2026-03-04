// Public Availability Page
const { ref, reactive, onMounted } = Vue;

window.$config = {
    setup: () => new function () {
        // 狀態
        this.loading = ref(false);
        this.selectedDate = ref('');
        this.selectedDepartment = ref('');
        this.selectedBuilding = ref('');
        this.departments = ref([]);
        this.buildings = ref([]);
        this.data = ref(null);
        this.expandedBuildings = reactive({});

        // 初始化日期為今天
        const today = new Date();
        this.selectedDate.value = today.toISOString().split('T')[0];

        // 載入分院列表
        this.loadDepartments = async () => {
            try {
                const res = await fetch('/api/public/departments');
                if (res.ok) {
                    this.departments.value = await res.json();
                    // 預設選擇台北總院，若找不到則選第一個
                    if (this.departments.value.length > 0 && !this.selectedDepartment.value) {
                        const taipei = this.departments.value.find(d => d.Name.includes('臺北'));
                        this.selectedDepartment.value = taipei ? taipei.Id : this.departments.value[0].Id;
                    }
                }
            } catch (err) {
                console.error('載入分院失敗:', err);
            }
        };

        // 載入大樓列表
        this.loadBuildings = async () => {
            try {
                let url = '/api/public/buildings';
                if (this.selectedDepartment.value) {
                    url += `?departmentId=${this.selectedDepartment.value}`;
                }
                const res = await fetch(url);
                if (res.ok) {
                    this.buildings.value = await res.json();
                }
            } catch (err) {
                console.error('載入大樓失敗:', err);
            }
        };

        // 分院變更時
        this.onDepartmentChange = async () => {
            this.selectedBuilding.value = '';
            await this.loadBuildings();
            await this.loadAvailability();
        };

        // 切換大樓展開/收合
        this.toggleBuilding = (buildingName) => {
            this.expandedBuildings[buildingName] = !this.expandedBuildings[buildingName];
        };

        // 載入空檔資料
        this.loadAvailability = async () => {
            this.loading.value = true;
            try {
                const res = await fetch('/api/public/availability', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({
                        date: this.selectedDate.value,
                        departmentId: this.selectedDepartment.value || null,
                        building: this.selectedBuilding.value || null
                    })
                });

                if (res.ok) {
                    this.data.value = await res.json();

                    // 預設展開所有大樓
                    if (this.data.value && this.data.value.buildings) {
                        this.data.value.buildings.forEach(b => {
                            if (this.expandedBuildings[b.Building] === undefined) {
                                this.expandedBuildings[b.Building] = true;
                            }
                        });
                    }
                }
            } catch (err) {
                console.error('載入空檔失敗:', err);
            } finally {
                this.loading.value = false;
            }
        };

        // 初始化
        onMounted(async () => {
            await this.loadDepartments();
            // loadDepartments 會自動選擇第一個分院
            if (this.selectedDepartment.value) {
                await this.loadBuildings();
                await this.loadAvailability();
            }
        });
    }
};
