import { useAuthStore } from "@/stores/auth";
import { ApiError } from "@/types";

const apiUrl = import.meta.env.VITE_API_URL;
const apiVersion = 'v1';

type Query = Record<string, unknown>;
type Headers = Record<string, string>

interface RequestOptions {
    method: 'GET' | 'POST';
    url: string;
    query?: Query;
    body?: unknown;
    headers?: Headers;
};

function buildUrl(url: string, query: Query = {}): string {
    const qs = new URLSearchParams(
        Object.entries(query).map(([key, value]) => [key, String(value)])
    ).toString();
    return `${apiUrl}/${apiVersion}/${url}${qs ? `?${qs}` : ''}`;
}

// One attempt. Headers are rebuilt every time so a retry picks up the new access token.
async function send(options: RequestOptions): Promise<Response> {
    const headers: Headers = { ...options.headers };

    if (options.body !== undefined) {
        headers['Content-Type'] = 'application/json';
    }

    const token = useAuthStore().accessToken;
    if (token) {
        headers['Authorization'] = `Bearer ${token}`;
    }

    if (options.url.startsWith('auth/')) {
        headers['X-Token-Delivery'] = 'cookie';
    }

    return fetch(buildUrl(options.url, options.query), {
        method: options.method,
        mode: 'cors',
        credentials: 'include',
        headers,
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
    });
}

async function request<T>(options: RequestOptions): Promise<T> {
    let res = await send(options);

    // Expired access token: refresh once and retry once. No loops.
    // /auth/* calls are excluded so a failed login or refresh can't trigger a refresh.
    if (res.status === 401 && !options.url.startsWith('auth/')) {
        const auth = useAuthStore();

        if (await auth.refresh()) {
            res = await send(options);
        }
    }

    if (!res.ok) {
        const errorBody = await res.json().catch(() => null);
        throw new ApiError(res.status, errorBody?.message ?? `HTTP ${res.status}`);
    }

    // Some endpoints (e.g. logout) answer 204 with no body.
    const text = await res.text();
    return (text ? JSON.parse(text) : undefined) as T;
}

export const get = <T = unknown>(url: string, query: Query = {}, headers?: Headers) =>
    request<T>({ method: 'GET', url, query, headers });

export const post = <T = unknown>(url: string, body?: unknown, query: Query = {}, headers?: Headers) =>
    request<T>({ method: 'POST', url, body, query, headers });