import 'vue';
import '/js/bootstrap/bootstrap.bundle.min.js';

const { ref, onMounted } = Vue;

export default {
    setup: () => new function () {
        this.el = ref(null);
        onMounted(() => {
            const bsOffcanvas = new bootstrap.Offcanvas(this.el.value);
            ['toggle', 'show', 'hide'].forEach(x => {
                this[x] = () => bsOffcanvas[x]();
            });
        });
    },
    template: `
<div ref="el" class="offcanvas offcanvas-end" tabindex="-1">
    <div class="offcanvas-header">
        <slot name="header"></slot>
        <button class="btn-close" data-bs-dismiss="offcanvas"></button>
    </div>
    <div class="offcanvas-body vstack gap-3">
        <slot></slot>
    </div>
    <div class="offcanvas-footer p-3">
        <slot name="footer"></slot>
    </div>
</div>`
}