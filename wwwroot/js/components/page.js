const { ref, reactive, computed, defineExpose } = Vue;

export default {
    props: ['per-page', 'perPageOption'],
    emits: ['reload'],
    setup: (props, { emit }) => new function () {
        this.total = ref(0);
        this.data = reactive({ page: 1, perPage: 5 });
        this.maxPage = computed(() => Math.max(1, Math.ceil(this.total.value / this.data.perPage)));

        const reload = () => {
            emit('reload', { page: this.data.page, perPage: this.data.perPage })
        }

        this.previous = {
            onClick: () => {
                this.data.page = Math.max(1, this.data.page - 1);
                reload();
            },
            disabled: computed(() => this.data.page == 1)
        }

        this.next = {
            onClick: () => {
                this.data.page = Math.min(this.maxPage.value, this.data.page + 1);
                reload();
            },
            disabled: computed(() => this.data.page == this.maxPage.value)
        }

        this.perPageOption = props.perPageOption || [5, 10, 20, 50, 100, 500];
        this.perPageChange = () => {
            if (this.data.page > this.maxPage.value) {
                this.data.page = this.maxPage.value;
            }
            reload();
        }

        this.pageChange = () => {
            reload();
        }

        this.index = computed(() => (this.data.page - 1) * this.data.perPage);

        this.setTotal = response => {
            this.total.value = response.headers.get('total');
            return Promise.resolve(response);
        }

        this.setHeaders = (config) => {
            if (config.headers) {
                Object.assign(config.headers, this.data);
            } else {
                config.headers = Object.assign({}, this.data);
            }
            return config;
        }

        this.go = (page) => {
            this.data.page = page;
        }

        //defineExpose({ index: this.index, setTotal: this.setTotal, setHeaders: this.setHeaders, go: this.go });
    },
    template: `<nav class='user-select-none' aria-label='Page navigation'><ul class='pagination justify-content-center mb-0'><li class='page-item'><button v-on:click='previous.onClick' :disabled='previous.disabled.value' class='page-link' aria-label='Previous'><span aria-hidden='true'>&laquo;</span></button></li><li class='page-item'><span class='page-link bg-none'><span v-text='total'></span> 筆，每頁<select v-model='data.perPage' v-on:change='perPageChange' class='page-select'><option v-for='item in perPageOption' :value='item' v-text='item'></option></select>筆，<span style='white-space:nowrap;'><select v-model='data.page' v-on:change='pageChange' class='page-select'><option v-for='n in maxPage' v-text='n' :value='n'></option></select>/<span v-text='maxPage'></span> 頁</span></span></li><li class='page-item'><button v-on:click='next.onClick' :disabled='next.disabled.value' class='page-link' aria-label='Next'><span aria-hidden='true'>&raquo;</span></button></li></ul></nav>`
}