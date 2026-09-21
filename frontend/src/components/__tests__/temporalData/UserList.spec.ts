import { describe, it, expect, vi, beforeEach } from 'vitest';
import { mount, flushPromises } from '@vue/test-utils';
import UserList from '../../temporalData/UserList.vue';
import UserService from '@/services/users/UserService';

vi.mock('@/services/users/UserService', () => ({
    default: {
        getAllUsers: vi.fn(),
    },
}));

describe('UserList.vue', () => {
    beforeEach(() => {
        vi.clearAllMocks();
    });

    it('Renders user list on mount', async () => {
        (UserService.getAllUsers as ReturnType<typeof vi.fn>).mockResolvedValueOnce([
            { uid: 144124, username: 'test1', password: '123' },
            { uid: 241241, username: 'test2', password: '321' },
        ]);

        const wrapper = mount(UserList);
        await flushPromises();

        const items = wrapper.findAll('li');
        expect(items).toHaveLength(2);
        expect(items[0].text()).toContain('test1');
        expect(items[1].text()).toContain('test2');
        expect(UserService.getAllUsers).toHaveBeenCalledTimes(1);
    });

    it('Renders empty list', async () => {
        (UserService.getAllUsers as ReturnType<typeof vi.fn>).mockResolvedValueOnce([]);

        const wrapper = mount(UserList);
        await flushPromises();

        expect(UserService.getAllUsers).toHaveBeenCalledTimes(1);
        expect(wrapper.findAll('li')).toHaveLength(0);
    });

    it('Show error message when there is an error getting the user list', async () => {
        (UserService.getAllUsers as ReturnType<typeof vi.fn>).mockRejectedValueOnce('500');

        const wrapper = mount(UserList);
        await flushPromises();

        expect(wrapper.findAll('li')).toHaveLength(0);
        expect(wrapper.find('section').text()).toBe('Error');
    });
});