import 'vue';
// import 'bootstrap';
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
            delegate: GET,
            saveDelegate: { method: POST, path: 'delegate' },
            removeDelegate: { method: POST, path: 'delegate/remove' },
            blockedPeriods: { method: GET, path: 'delegate/blocked-periods' },
        }),
        select: {
            // 保留原有的 apiMethods
            ...apiMethods('/api/select', {
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
                roomslotsrange: POST,
                equipmentbyroom: POST,
                smallbooths: GET,
                roombyschedule: POST,
                costcenters: GET,
                smartsearch: POST,
                internaluser: GET,
                unitnames: GET,
                calendarday: { method: POST, path: 'calendar/day' },
                calendarweek: { method: POST, path: 'calendar/week' },
                calendarmonth: { method: POST, path: 'calendar/month' }
            }),
        },
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
            createrecurring: POST,       // 循環預約（僅院內人員）

            // ===== 租借審核 (主任/管理者) =====
            reservationlist: GET,      // 租借審核列表
            approve: POST,             // 租借審核通過
            reject: POST,              // 租借審核拒絕
            fasttrack: POST,           // 決行（直接通過所有剩餘關卡）
            bulkapprove: POST,         // 批量核准
            bulkreject: POST,          // 批量拒絕

            // ===== 付款審核 (總務/管理者) =====
            paymentlist: GET,
            approvepayment: POST,
            rejectpayment: POST,
            orderdetail: (id) => fetch(`/api/reservations/orderdetail/${id}`).then(r => r.ok ? r.json().then(data => ({ data })) : Promise.reject(r)),

            // ===== 預約總覽 (查詢) =====
            list: GET,
            mylist: GET,
            detailview: (id) => fetch(`/api/reservations/detailview/${id}`).then(r => r.ok ? r.json().then(data => ({ data })) : Promise.reject(r)),

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
            reject: POST,
            createDraft: { method: POST, path: 'create-draft' },
            myDrafts: { method: GET, path: 'my-drafts' },
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

        nameplate: apiMethods('/api/admin', {
            nameplatelist:    GET,
            nameplatedetail:  GET,
            nameplateinsert:  POST,
            nameplateupdate:  POST,
            nameplatedelete:  DELETE,
            nameplateoptions: GET,
        }),
        admin: apiMethods('/api/admin', {
            userlist: GET,
            userdetail: GET,
            userinsert: POST,
            userupdate: POST,
            userreject: POST,
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
            roommoveup: POST,
            roommovedown: POST,
            roominitsequence: POST,
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
            costcentermanagerlist: GET,
            costcentermanagerdetail: GET,
            costcentermanagerinsert: POST,
            costcentermanagerupdate: POST,
            costcentermanagerdelete: DELETE,
            statisticsusage: GET,
        }),
        adminwebex: apiMethods('/api/adminwebex', {
            list: GET,
            detail: GET,
            insert: POST,
            update: POST,
            delete: DELETE,
        }),
        sysconfig: {
            ...apiMethods('/api/sysconfig', {
                registrationstatus: GET,
                registrationtoggle: POST,
                getall: GET,
                getglobal: GET,
                update: POST,
            }),
            getbydepartment: (params) => fetch(`/api/sysconfig/getbydepartment/${params.departmentId}`).then(r => r.ok ? r.json().then(data => ({ data })) : Promise.reject(r)),
        },
        holiday: {
            list: (year) => fetch(`/api/holiday/list/${year}`).then(r => r.ok ? r.json().then(data => ({ data })) : Promise.reject(r)),
            years: () => fetch('/api/holiday/years').then(r => r.ok ? r.json().then(data => ({ data })) : Promise.reject(r)),
            sync: (year) => fetch(`/api/holiday/sync/${year}`, { method: 'POST' }).then(r => r.ok ? r.json().then(data => ({ data })) : r.json().then(e => Promise.reject(e))),
            upload: (formData) => fetch('/api/holiday/upload', { method: 'POST', body: formData }).then(r => r.ok ? r.json().then(data => ({ data })) : r.json().then(e => Promise.reject(e))),
            delete: (id) => fetch(`/api/holiday/${id}`, { method: 'DELETE' }).then(r => r.ok ? r.json().then(data => ({ data })) : r.json().then(e => Promise.reject(e))),
            toggle: (id) => fetch(`/api/holiday/toggle/${id}`, { method: 'POST' }).then(r => r.ok ? r.json().then(data => ({ data })) : r.json().then(e => Promise.reject(e))),
            check: (date) => fetch(`/api/holiday/check/${date}`).then(r => r.ok ? r.json().then(data => ({ data })) : Promise.reject(r)),
        },
        report: apiMethods('/api/report', {
            list: GET,
            summary: GET,
            departmentcodes: GET,
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
    fn.logout = () => global.api.auth.logout().then(() => { sessionStorage.removeItem('announcement_dismissed'); location.href = '/api/auth/redirection'; });
    createApp(navbar).mount("header");
}