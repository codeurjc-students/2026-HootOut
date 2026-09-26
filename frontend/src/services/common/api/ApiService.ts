const apiUrl = import.meta.env.VITE_API_URL;
const apiVersion = 'v1';

type APIService_get = <T = unknown >(url: string, parameters: Record<string, unknown>) => Promise<T>;
type APIService_post = <T = unknown>(url: string, body: any) => Promise<T>;

const get: APIService_get = async function APIService_get(url, parameters = {}) {

    const headers = {
    };

    const query = new URLSearchParams(Object.entries(parameters).map(([key, value]) => [key, String(value)])).toString();

    const request: Promise<Response> = fetch(`${apiUrl}/${apiVersion}/${url}${query ? `?${query}` : ''}`, {
        method: 'GET',
        mode: 'cors',
        headers: headers
    });

    const res: Response = await request;
    if (!res.ok) {
        const errorBody = await res.json().catch(() => null);
        throw new Error(errorBody?.message ?? `HTTP ${res.status}`);
    }

    return res.json();
}

const post: APIService_post = async function (url, body) {
    const headers = {
        'Content-Type': 'application/json'
    };

    const request = fetch(`${apiUrl}/${apiVersion}/${url}`, {
        method: 'POST',
        mode: 'cors',
        headers: headers,
        body: JSON.stringify(body)
    });

    const res = await request;

    if (!res.ok) {
        const errorBody = await res.json().catch(() => null);
        throw new Error(errorBody?.message ?? `HTTP ${res.status}`);
    }

    return res.json();
}

export { get, post }