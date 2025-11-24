import 'vue';
const { ref } = Vue;

export default {
    setup: () => new function () {
        this.alerts = ref([]);
        this.removeAlert = (e) => {
            this.alerts.value.remove(e);
        }
        window.addAlert = (error, options = { type: 'success', click: null }) => {
            if (typeof error === 'object' && error.message) {
                options.message = error.message;
            }
            else {
                options.message = error;
            }
            this.alerts.value.push(options);
            setTimeout(() => this.removeAlert(options), 5000);
        }
    },
    template: `
<div class="global-alert">
    <div v-for="(alert, index) in alerts" :key="index" :class="['alert', \`alert-$\{alert.type\}\`, 'alert-dismissible', 'fade', 'show']" role="alert">
        <span @click="alert.click&&alert.click()" class="d-block text-break" style="max-width:350px;">\{\{alert.message\}\}</span>
        <button @click="removeAlert(alert)" class="btn-close"></button>
    </div>
</div>`
}