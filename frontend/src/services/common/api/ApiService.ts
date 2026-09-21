const apiUrl = import.meta.env.VITE_API_URL;
const apiVersion = 'v1';

const get = async function <T = unknown>(url: string, parameters: Record<string, unknown> = {}): Promise<T> {

    const headers = {
    };

    const query = new URLSearchParams(Object.entries(parameters).map(([key, value]) => [key, String(value)])).toString();

    const request: Promise<Response> = fetch(`${apiUrl}/${apiVersion}/${url}${query}`, {
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

const post = async function <T = unknown>(url: string, body: any): Promise<T> {
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