/*
 * extensions
 */
if (!Object.prototype.clear) {
    Object.defineProperty(Object.prototype, 'clear', {
        configurable: true,
        enumerable: false,
        writable: true,
        value: function () {
            for (let property in this) {
                delete this[property]
            }
            return this;
        }
    })
}

if (!String.prototype.format) {
    String.prototype.format = function (pattern = 'yyyy-mm-dd') {
        try {
            return new Date(this).format(pattern)
        } catch {
            return ''
        }
    }
}

if (!Date.prototype.format) {
    Date.prototype.format = function (pattern = 'yyyy-mm-dd') {
        const year = this.getFullYear().toString();
        const month = (this.getMonth() + 1).toString().padStart(2, '0');
        const day = this.getDate().toString().padStart(2, '0');
        const hour = this.getHours().toString().padStart(2, '0');
        const minute = this.getMinutes().toString().padStart(2, '0');
        const second = this.getSeconds().toString().padStart(2, '0');
        const millisecond = this.getMilliseconds().toString().padStart(3, '0');

        const replacements = {
            'yyyy': year,
            'yy': year.slice(-2),                   // 簡寫年份
            'mm': month,
            'm': parseInt(month, 10).toString(),    // 不補零的月份
            'dd': day,
            'd': parseInt(day, 10).toString(),      // 不補零的日期
            'HH': hour,
            'H': parseInt(hour, 10).toString(),     // 不補零的小時
            'MM': minute,
            'M': parseInt(minute, 10).toString(),   // 不補零的分鐘
            'SS': second,
            'S': parseInt(second, 10).toString(),   // 不補零的秒
            'MS': millisecond,
        }

        let formattedString = pattern;
        for (const placeholder in replacements) {
            formattedString = formattedString.replace(new RegExp(placeholder, 'g'), replacements[placeholder]);
        }

        return formattedString;
    }
}

if (!Date.prototype.addDays) {
    Date.prototype.addDays = function (days) {
        const result = new Date(this);
        result.setDate(result.getDate() + days);
        return result;
    }
}

if (!Array.prototype.remove) {
    Array.prototype.remove = function () {
        let what, a = arguments, L = a.length, ax;
        while (L && this.length) {
            what = a[--L];
            while ((ax = this.indexOf(what)) !== -1) {
                this.splice(ax, 1);
            }
        }
        return this;
    }
}

if (!Number.prototype.currency) {
    Number.prototype.currency = function () {
        try {
            let parts = this.toString().split('.');
            parts[0] = parts[0].replace(/\B(?=(\d{3})+(?!\d))/g, '.');
            return parts.join('.');
        } catch {
            return this
        }
    }
}

window.copy = (target, sources) => {
    if (target.__v_isRef) {
        target = target.value;
    }
    let type = Object.prototype.toString.call(target);
    if (type == '[object Object]') {
        for (var property in target) {
            delete target[property]
        }
        Object.assign(target, sources);
    } else if (type == '[object Array]') {
        target.splice(0);
        sources.forEach(x => target.push(x));
    } else {
        target = sources;
    }
}

window.getSearch = (name) => {
    const urlParams = new URLSearchParams(location.search);
    return urlParams.get(name);
}

window.getMessage = (error) => {
    let message = error.details;
    if (typeof error.details === "object") {
        message = Object.keys(error.details).map(x => x + error.details[x].join()).join();
    }
    return message;
}

/*
 * 防止錯誤訊息出現在 console 中
 */
window.addEventListener('unhandledrejection', (event) => {
    event.preventDefault();
});

/*
 * Ctrl + S
 */
const globalHandleCtrlS = (event) => {
    const isCtrlOrCmd = event.ctrlKey || event.metaKey;
    if (isCtrlOrCmd && event.key.toLowerCase() === 's') {
        event.preventDefault();
        const ctrlsEvent = new CustomEvent('ctrls');
        window.dispatchEvent(ctrlsEvent);
    }
}
window.addEventListener('keydown', globalHandleCtrlS);

//cookies
(function () {
    const isStandardBrowserEnv = (() => { let e; return ("undefined" == typeof navigator || "ReactNative" !== (e = navigator.product) && "NativeScript" !== e && "NS" !== e) && ("undefined" != typeof window && "undefined" != typeof document) })(), typeOfTest = e => t => typeof t === e; window.cookies = isStandardBrowserEnv ? function e() { return { set: function e(t, n, o, i, r, u) { const s = []; s.push(t + "=" + encodeURIComponent(n)), typeOfTest("number")(o) && s.push("expires=" + new Date(o).toGMTString()), typeOfTest("string")(i) && s.push("path=" + i), typeOfTest("string")(r) && s.push("domain=" + r), !0 === u && s.push("secure"), document.cookie = s.join("; ") }, get: function e(t) { const n = document.cookie.match(RegExp("(^|;\\s*)(" + t + ")=([^;]*)")); return n ? decodeURIComponent(n[3]) : null }, remove: function e(t) { this.write(t, "", Date.now() - 864e5) } } }() : { set: function e() { }, get: function e() { return null }, remove: function e() { } }
})();