import { describe, it, expect, vi, beforeEach } from 'vitest';
import UserService from './UserService';
import { get } from '@/services/common/api/ApiService';

vi.mock('@/services/common/api/ApiService', () => ({
    get: vi.fn(),
    post: vi.fn(),
}));

describe('UserService', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('getAllUsers returns an empty user lists', async () => {
        (get as ReturnType<typeof vi.fn>).mockResolvedValueOnce([]);

        const users = await UserService.getAllUsers();

        expect(users).toEqual([]);
    });

    it('getAllUsers returns a user list', async () => {
        const mockUsers = [
            { uid: 1123123, username: 'test1', password: '1' },
            { uid: 4141241, username: 'test2', password: '2' },
        ];
        (get as ReturnType<typeof vi.fn>).mockResolvedValueOnce(mockUsers);

        const users = await UserService.getAllUsers();

        expect(users).toEqual(mockUsers);
        expect(get).toHaveBeenCalledTimes(1);
        expect(get).toHaveBeenCalledWith('users/all');
    });

    it('getAllUsers check error propagation', async () => {
        (get as ReturnType<typeof vi.fn>).mockRejectedValueOnce(new Error('500'));

        await expect(UserService.getAllUsers()).rejects.toThrow('500');
    });
});