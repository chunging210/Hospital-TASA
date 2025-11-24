import 'vue';
const { ref } = Vue;

const pad0 = (v) => {
    return v.toString().padStart(2, '0');
}

export const fn = {
    getName: () => "",
    getExp: () => 0,
    logout: () => location.href = '/'
}

export default {
    setup: () => new function () {
        this.name = ref(fn.getName());
        this.countdown = ref('00:00');
        setInterval(() => {
            this.name.value = fn.getName();
            if (this.name.value) {
                const s = Math.round(Math.max(0, fn.getExp() - new Date().getTime()) / 1000);
                if (s > 0) {
                    const SS = s % 60;
                    const HH = (s - SS) / 60;
                    this.countdown.value = `${pad0(HH)}:${pad0(SS)}`;
                    return;
                }
            }
            this.logout();
        }, 250);

        this.logout = fn.logout;

        //this.lang = ref(htmlLang.get());
        //this.SUPPORT_LOCALES = SUPPORT_LOCALES;
        //this.setLang = htmlLang.set;
    }
}