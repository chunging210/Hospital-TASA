import 'vue';
import '/js/extensions.js'
import apiMethods, { GET, POST, PUT, PATCH, DELETE } from '/js/apiMethods.js';
import globalalert from '/js/components/globalalert.js';
import navbar, { fn } from '/navbar.js';

const global = new function () {
    this.setting = apiMethods('/api/setting', { json: GET }).json();
    this.api = {
        auth: apiMethods('/api/auth', {
            login: POST,
            logout: POST,
            me: GET,
            register: POST,
        }),
        password: apiMethods('/api/password', {
            forgetmail: POST,
            forget: POST,
            changepw: POST,
        }),
        profiles: apiMethods('/api/profiles', {
            detail: GET,
            update: POST,
        }),
        profiles: apiMethods('/api/profiles', {
            detail: GET,
            update: POST,
        }),
        select: apiMethods('/api/select', {
            room: GET,
            roomlist: GET,
            role: GET,
            user: GET,
            userschedule: POST,
            department: GET,
            departmenttree: GET,
            conferencecreateby: GET,
            equipment: GET,
            ECS: GET,
            buildingsbydepartment: POST,
            floorsbybuilding: POST,
            roomsbyfloor: POST,
            roomslots: POST,
            equipmentbyroom: POST,
            roombyschedule: POST,
            costcenters: GET,
            smartsearch: POST
        }),
        calendar: apiMethods('/api/calendar', {
            list: GET,
            recent: GET,
        }),
        conference: apiMethods('/api/conference', {
            createdbylist: GET,
            list: GET,
            detail: GET,
            insert: POST,
            update: POST,
            delete: DELETE,
        }),
        reservations: apiMethods('/api/reservations', {
            // ===== 預約建立 =====
            createreservation: POST,

            // ===== 租借審核 (主任/管理者) =====
            reservationlist: GET,      // 租借審核列表
            approve: POST,             // 租借審核通過
            reject: POST,              // 租借審核拒絕

            // ===== 付款審核 (總務/管理者) =====
            paymentlist: GET,
            approvepayment: POST,
            rejectpayment: POST,

            // ===== 預約總覽 (查詢) =====
            list: GET,
            mylist: GET,

            // ===== 權限 =====
            permissions: GET,
            // ===== 其他 (舊版,待整理) =====
            pendingcheck: GET,
            cancel: POST,
            delete: POST,
            detail: POST,
            update: POST,
        }),
        payment: apiMethods('/api/payment', {
            uploadcounter: POST,
            transfer: POST,
            approve: POST,
            reject: POST
        }),
        conferencetemplate: apiMethods('/api/conferencetemplate', {
            list: GET,
            detail: GET,
            insert: POST,
            update: POST,
            delete: DELETE,
        }),
        seatsetting: apiMethods('/api/seatsetting', {
            list: GET,
            detail: GET,
            save: POST,
            uploadlogo: POST,
            delete: POST,
        }),
        visitor: apiMethods('/api/visitor', {
            list: GET,
            detail: GET,
            insert: POST,
            update: POST,
            delete: DELETE,
        }),

        admin: apiMethods('/api/admin', {
            userlist: GET,
            userdetail: GET,
            userinsert: POST,
            userupdate: POST,
            departmentlist: GET,
            departmentdetail: GET,
            departmentinsert: POST,
            departmentupdate: POST,
            departmentdelete: DELETE,
            roomlist: GET,
            roomdetail: GET,
            roominsert: POST,
            roomupdate: POST,
            roomdelete: DELETE,
            equipmentlist: GET,
            equipmentdetail: GET,
            equipmentinsert: POST,
            equipmentupdate: POST,
            equipmentdelete: DELETE,
            ecslist: GET,
            ecsdetail: GET,
            ecsinsert: POST,
            ecsupdate: POST,
            ecsdelete: DELETE,
            ecstest: GET,
            loginloglist: POST,
        }),
        adminwebex: apiMethods('/api/adminwebex', {
            list: GET,
            detail: GET,
            insert: POST,
            update: POST,
            delete: DELETE,
        }),
        sysconfig: apiMethods('/api/sysconfig', {
            registrationstatus: GET,
            registrationtoggle: POST,
            getall: GET,
            update: POST,
        }),
    }
}

export default global;

const { createApp } = Vue;
if (document.querySelector('globalalert')) {
    createApp(globalalert).mount("globalalert");
}
if (document.querySelector('header')) {
    fn.getName = () => cookies.get('n');
    fn.getExp = () => cookies.get('e');
    fn.logout = () => global.api.auth.logout().then(() => { location.href = '/api/auth/redirection'; });
    createApp(navbar).mount("header");
}