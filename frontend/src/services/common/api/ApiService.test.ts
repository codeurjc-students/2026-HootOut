import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { createPinia, setActivePinia } from 'pinia';
import { get, post } from './ApiService';
import { useAuthStore } from '@/stores/auth';
import { ApiError } from '@/types';

const mockFetch = vi.fn();

// The service reads success bodies with text() and error bodies with json().
function mockResponse(status: number, body?: unknown) {
    const text = body === undefined ? '' : JSON.stringify(body);
    return {
        ok: status >= 200 && status < 300,
        status,
        text: async () => text,
        json: async () => JSON.parse(text),
    };
}

function invalidJsonResponse(status: number) {
    return {
        ok: false,
        status,
        text: async () => 'not json',
        json: async () => { throw new Error('invalid json'); },
    };
}

const urlOfCall = (n: number) => mockFetch.mock.calls[n]?.[0] as string;
const initOfCall = (n: number) => mockFetch.mock.calls[n]?.[1] as RequestInit;
const headersOfCall = (n: number) => initOfCall(n).headers as Record<string, string>;

describe('ApiService', () => {
    beforeEach(() => {
        // The service reads the access token from the auth store, which needs an active Pinia.
        setActivePinia(createPinia());
        vi.stubGlobal('fetch', mockFetch);
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        mockFetch.mockReset();
    });

    describe('get', () => {
        it('calls fetch with method GET, mode cors and credentials include', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, { foo: 'bar' }));

            await get('resource', {});

            expect(mockFetch).toHaveBeenCalledWith(
                expect.any(String),
                expect.objectContaining({ method: 'GET', mode: 'cors', credentials: 'include' })
            );
        });

        it('builds the url including apiVersion and the given path', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, []));

            await get('resource', {});

            expect(urlOfCall(0)).toContain('/v1/resource');
        });

        it('does not append "?" to the url when no parameters are given', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, []));

            await get('resource', {});

            expect(urlOfCall(0)).not.toContain('?');
        });

        it('serializes parameters as a query string', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, []));

            await get('resource', { id: 1, active: true });

            expect(urlOfCall(0)).toContain('?');
            expect(urlOfCall(0)).toContain('id=1');
            expect(urlOfCall(0)).toContain('active=true');
        });

        it('does not send a Content-Type header, since there is no body', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, []));

            await get('resource', {});

            expect(headersOfCall(0)['Content-Type']).toBeUndefined();
            expect(initOfCall(0).body).toBeUndefined();
        });

        it('sends the extra headers it is given', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, []));

            await get('resource', {}, { 'X-Custom': '1' });

            expect(headersOfCall(0)['X-Custom']).toBe('1');
        });

        it('returns the parsed json body when the response is ok', async () => {
            const payload = { data: [1, 2, 3] };
            mockFetch.mockResolvedValueOnce(mockResponse(200, payload));

            const result = await get('resource', {});

            expect(result).toEqual(payload);
        });

        it('returns undefined when the response has no body (204)', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(204));

            const result = await get('resource', {});

            expect(result).toBeUndefined();
        });

        it('throws an error with the body message when the response is not ok', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(404, { message: 'Not found' }));

            await expect(get('resource', {})).rejects.toThrow('Not found');
        });

        it('throws an ApiError carrying the http status', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(404, { message: 'Not found' }));

            const error = await get('resource', {}).catch((e) => e);

            expect(error).toBeInstanceOf(ApiError);
            expect((error as ApiError).status).toBe(404);
        });

        it('throws an error with the status when the error body is not valid json', async () => {
            mockFetch.mockResolvedValueOnce(invalidJsonResponse(500));

            await expect(get('resource', {})).rejects.toThrow('HTTP 500');
        });
    });

    describe('post', () => {
        it('calls fetch with method POST, mode cors, credentials include and json content-type', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            await post('resource', { foo: 'bar' });

            expect(mockFetch).toHaveBeenCalledWith(
                expect.any(String),
                expect.objectContaining({
                    method: 'POST',
                    mode: 'cors',
                    credentials: 'include',
                    headers: expect.objectContaining({ 'Content-Type': 'application/json' }),
                })
            );
        });

        it('serializes the body as JSON', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            const body = { username: 'test1' };
            await post('resource', body);

            expect(initOfCall(0).body).toBe(JSON.stringify(body));
        });

        it('sends no body and no Content-Type when called without a body', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(204));

            await post('resource');

            expect(initOfCall(0).body).toBeUndefined();
            expect(headersOfCall(0)['Content-Type']).toBeUndefined();
        });

        it('builds the url including apiVersion and the given path', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            await post('resource', {});

            expect(urlOfCall(0)).toContain('/v1/resource');
        });

        it('returns the parsed json body when the response is ok', async () => {
            const payload = { uid: 1 };
            mockFetch.mockResolvedValueOnce(mockResponse(200, payload));

            const result = await post('resource', {});

            expect(result).toEqual(payload);
        });

        it('returns undefined when the response has no body (204)', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(204));

            const result = await post('resource', {});

            expect(result).toBeUndefined();
        });

        it('throws an error with the body message when the response is not ok', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(400, { message: 'Invalid data' }));

            await expect(post('resource', {})).rejects.toThrow('Invalid data');
        });

        it('throws an error with the status when the error body is not valid json', async () => {
            mockFetch.mockResolvedValueOnce(invalidJsonResponse(500));

            await expect(post('resource', {})).rejects.toThrow('HTTP 500');
        });
    });

    describe('authentication headers', () => {
        it('sends the access token as a Bearer token when there is one', async () => {
            useAuthStore().accessToken = 'my-token';
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            await get('resource', {});

            expect(headersOfCall(0)['Authorization']).toBe('Bearer my-token');
        });

        it('does not send an Authorization header when there is no access token', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            await get('resource', {});

            expect(headersOfCall(0)['Authorization']).toBeUndefined();
        });

        it('asks for cookie delivery of the refresh token on auth/ endpoints', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            await post('auth/login', { username: 'u', password: 'p' });

            expect(headersOfCall(0)['X-Token-Delivery']).toBe('cookie');
        });

        it('does not ask for cookie delivery on other endpoints', async () => {
            mockFetch.mockResolvedValueOnce(mockResponse(200, {}));

            await get('users/me', {});

            expect(headersOfCall(0)['X-Token-Delivery']).toBeUndefined();
        });
    });

    describe('expired access token (401)', () => {
        it('refreshes once and retries the request with the new token', async () => {
            const auth = useAuthStore();
            auth.accessToken = 'old-token';
            mockFetch
                .mockResolvedValueOnce(mockResponse(401))                                // original request
                .mockResolvedValueOnce(mockResponse(200, { accessToken: 'new-token' })) // auth/refresh
                .mockResolvedValueOnce(mockResponse(200, { username: 'test1' }));        // retry

            const result = await get('users/me', {});

            expect(result).toEqual({ username: 'test1' });
            expect(mockFetch).toHaveBeenCalledTimes(3);
            expect(urlOfCall(1)).toContain('/v1/auth/refresh');
            expect(urlOfCall(2)).toContain('/v1/users/me');
            expect(headersOfCall(2)['Authorization']).toBe('Bearer new-token');
            expect(auth.accessToken).toBe('new-token');
        });

        it('throws the original 401 and clears the session when the server rejects the refresh', async () => {
            const auth = useAuthStore();
            auth.accessToken = 'old-token';
            mockFetch
                .mockResolvedValueOnce(mockResponse(401))
                .mockResolvedValueOnce(mockResponse(400, { message: 'invalid refresh token' }));

            const error = await get('users/me', {}).catch((e) => e);

            expect(error).toBeInstanceOf(ApiError);
            expect((error as ApiError).status).toBe(401);
            expect(mockFetch).toHaveBeenCalledTimes(2); // no retry
            expect(auth.accessToken).toBeNull();
        });

        it('keeps the session when the refresh fails with a network error', async () => {
            const auth = useAuthStore();
            auth.accessToken = 'old-token';
            mockFetch
                .mockResolvedValueOnce(mockResponse(401))
                .mockRejectedValueOnce(new TypeError('Failed to fetch'));

            const error = await get('users/me', {}).catch((e) => e);

            expect((error as ApiError).status).toBe(401);
            expect(auth.accessToken).toBe('old-token');
        });

        it('retries only once when the retried request is rejected again', async () => {
            useAuthStore().accessToken = 'old-token';
            mockFetch
                .mockResolvedValueOnce(mockResponse(401))
                .mockResolvedValueOnce(mockResponse(200, { accessToken: 'new-token' }))
                .mockResolvedValueOnce(mockResponse(401));

            const error = await get('users/me', {}).catch((e) => e);

            expect((error as ApiError).status).toBe(401);
            expect(mockFetch).toHaveBeenCalledTimes(3);
        });

        it.each(['auth/login', 'auth/register', 'auth/refresh', 'auth/logout'])(
            'does not try to refresh when %s answers 401',
            async (url) => {
                mockFetch.mockResolvedValueOnce(mockResponse(401, { message: 'nope' }));

                await expect(post(url, {})).rejects.toThrow('nope');

                expect(mockFetch).toHaveBeenCalledTimes(1);
            }
        );

        it('shares one refresh between parallel requests that were all rejected', async () => {
            useAuthStore().accessToken = 'old-token';
            mockFetch.mockImplementation(async (url: string, init: RequestInit) => {
                if (url.includes('/auth/refresh')) return mockResponse(200, { accessToken: 'new-token' });
                const headers = init.headers as Record<string, string>;
                return headers['Authorization'] === 'Bearer new-token'
                    ? mockResponse(200, { ok: true })
                    : mockResponse(401);
            });

            await Promise.all([get('a', {}), get('b', {}), get('c', {})]);

            const refreshCalls = mockFetch.mock.calls.filter((c) => (c[0] as string).includes('/auth/refresh'));
            expect(refreshCalls).toHaveLength(1);
        });
    });
});