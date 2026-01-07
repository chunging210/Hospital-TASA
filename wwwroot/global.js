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
            buildingsbydepartment: GET,
            floorsbybuilding: POST,
            roomsbyfloor: POST,
            roomslots: POST,
            equipmentbyroom: POST,
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