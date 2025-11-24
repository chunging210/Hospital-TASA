import 'vue';
import offcanvas from 'offcanvas';
import page from 'page';

const { createApp } = Vue;

if (window.$config) {
    const $app = createApp(window.$config);
    $app.component('offcanvas', offcanvas);
    $app.component('page', page);
    $app.mount("#app");
}