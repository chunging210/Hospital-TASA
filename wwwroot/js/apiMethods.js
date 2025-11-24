/*
 * GET,POST,PUT,PATCH,DELETE,apiMethods
 */
export const GET = 'GET', POST = 'POST', PUT = 'PUT', PATCH = 'PATCH', DELETE = 'DELETE';

const toISODateString = (object) => {
    for (const [k, v] of Object.entries(object)) {
        if (v instanceof Date) {
            object[k] = v.toISOString();
        } else if (v && typeof v === 'object') {
            object[k] = toISODateString(v);
        }
    }
    return object;
}

const responseParser = async (response) => {
    const contentType = response.headers.get('Content-Type');
    if (!contentType) {
        return await response.text();
    }
    if (contentType.includes('application/json')) {
        return await response.json();
    }
    if (contentType.includes('text/plain') || contentType.includes('text/html')) {
        return await response.text();
    }
    if (contentType.includes('application/octet-stream') || contentType.includes('application/vnd')) {
        return await response.blob();
    }
}

const download = (filename, content) => {
    const blob = new Blob([JSON.stringify(content, null, 2)], { type: 'application/json' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    URL.revokeObjectURL(link.href);
}

const api = async (method, url, options = {}) => {
    Object.assign(options, { method });
    options.headers = options.headers || {}
    if (options.body && typeof options.body === 'object') {
        let bodyCopy = '';
        if (Array.isArray(options.body)) {
            bodyCopy = [...options.body];
        } else {
            bodyCopy = toISODateString({ ...options.body });
        }
        switch (method) {
            case GET: case DELETE:
                url += (url.includes('?') ? '&' : '?') + new URLSearchParams(bodyCopy).toString();
                delete options.body;
                break;
            case POST: case PUT: case PATCH:
                Object.assign(options.headers, { 'Content-Type': 'application/json' });
                options.body = JSON.stringify(bodyCopy);
                break;
        }
    }
    return fetch(url, options)
        .then(async response => {
            if (response.ok) {
                response.data = await responseParser(response);
                return Promise.resolve(response);
            } else {
                try {
                    response = await response.json();
                } catch {
                    response = { message: '', details: {} }
                }
                response.download = () => download('api error', { url, options, response });
                return Promise.reject(response);
            }
        });
}

export default (baseUrl, actions) => {
    const methods = {}
    for (const [k, v] of Object.entries(actions)) {
        switch (typeof v) {
            case 'string': methods[k] = o => api(v, `${baseUrl}/${k}`, o); break;
            case 'function': methods[k] = v; break;
            case 'object':
                if (v && v.method && v.path) {
                    methods[k] = o => api(v.method, `${baseUrl}/${v.path}`, o);
                }
                break;
        }
    }
    return methods;
}