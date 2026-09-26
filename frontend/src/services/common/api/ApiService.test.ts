import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { get, post } from './ApiService';

const mockFetch = vi.fn();

describe('ApiService', () => {
    beforeEach(() => {
        vi.stubGlobal('fetch', mockFetch);
    });

    afterEach(() => {
        vi.unstubAllGlobals();
        mockFetch.mockReset();
    });

    describe('get', () => {
        it('calls fetch with method GET and mode cors', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ({ foo: 'bar' }),
            });

            await get('resource', {});

            expect(mockFetch).toHaveBeenCalledWith(
                expect.any(String),
                expect.objectContaining({ method: 'GET', mode: 'cors' })
            );
        });

        it('builds the url including apiVersion and the given path', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ([]),
            });

            await get('resource', {});

            const calledUrl = mockFetch.mock.lastCall?.[0] as string;
            expect(calledUrl).toContain('/v1/resource');
        });

        it('does not append "?" to the url when no parameters are given', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ([]),
            });

            await get('resource', {});

            const calledUrl = mockFetch.mock.lastCall?.[0] as string;
            expect(calledUrl).not.toContain('?');
        });

        it('serializes parameters as a query string', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ([]),
            });

            await get('resource', { id: 1, active: true });

            const calledUrl = mockFetch.mock.lastCall?.[0] as string;
            expect(calledUrl).toContain('?');
            expect(calledUrl).toContain('id=1');
            expect(calledUrl).toContain('active=true');
        });

        it('returns the parsed json body when the response is ok', async () => {
            const payload = { data: [1, 2, 3] };
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => payload,
            });

            const result = await get('resource', {});

            expect(result).toEqual(payload);
        });

        it('throws an error with the body message when the response is not ok', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: false,
                status: 404,
                json: async () => ({ message: 'Not found' }),
            });

            await expect(get('resource', {})).rejects.toThrow('Not found');
        });

        it('throws an error with the status when the error body is not valid json', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: false,
                status: 500,
                json: async () => { throw new Error('invalid json'); },
            });

            await expect(get('resource', {})).rejects.toThrow('HTTP 500');
        });
    });

    describe('post', () => {
        it('calls fetch with method POST, mode cors and json content-type', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ({}),
            });

            await post('resource', { foo: 'bar' });

            expect(mockFetch).toHaveBeenCalledWith(
                expect.any(String),
                expect.objectContaining({
                    method: 'POST',
                    mode: 'cors',
                    headers: { 'Content-Type': 'application/json' },
                })
            );
        });

        it('serializes the body as JSON', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ({}),
            });

            const body = { username: 'test1' };
            await post('resource', body);

            const calledOptions = mockFetch.mock.lastCall?.[1] as RequestInit;
            expect(calledOptions.body).toBe(JSON.stringify(body));
        });

        it('builds the url including apiVersion and the given path', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => ({}),
            });

            await post('resource', {});

            const calledUrl = mockFetch.mock.lastCall?.[0] as string;
            expect(calledUrl).toContain('/v1/resource');
        });

        it('returns the parsed json body when the response is ok', async () => {
            const payload = { uid: 1 };
            mockFetch.mockResolvedValueOnce({
                ok: true,
                json: async () => payload,
            });

            const result = await post('resource', {});

            expect(result).toEqual(payload);
        });

        it('throws an error with the body message when the response is not ok', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: false,
                status: 400,
                json: async () => ({ message: 'Invalid data' }),
            });

            await expect(post('resource', {})).rejects.toThrow('Invalid data');
        });

        it('throws an error with the status when the error body is not valid json', async () => {
            mockFetch.mockResolvedValueOnce({
                ok: false,
                status: 500,
                json: async () => { throw new Error('invalid json'); },
            });

            await expect(post('resource', {})).rejects.toThrow('HTTP 500');
        });
    });
});