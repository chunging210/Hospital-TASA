(() => {
    let importmap = {
        "imports": {
            "bootstrap": "/js/bootstrap/bootstrap.bundle.min.js?v5.3.0",
            "vue": "/js/vue/vue.global.js?v3.5.13",
            "offcanvas": "/js/components/offcanvas.js",
            "page": "/js/components/page.js",
            "vselect": "/js/vselect/min.js?v4.0.0b6",
        }
    }

    let dveimportmap = {

    }

    window.development = false;
    const meta = document.querySelector('meta[name="environment"]');
    if (meta) {
        window.development = meta.getAttribute('content') == 'dev';
    }

    if (!window.development) {
        console.info = () => { }
        Object.assign(importmap.imports, dveimportmap);
    }

    const script = document.createElement('script');
    Object.assign(script, { type: 'importmap', textContent: JSON.stringify(importmap) });
    document.head.appendChild(script);
})();
