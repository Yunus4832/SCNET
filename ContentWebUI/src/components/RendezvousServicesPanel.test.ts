import { flushPromises, mount } from '@vue/test-utils';
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api';
import RendezvousServicesPanel from './RendezvousServicesPanel.vue';
import RendezvousSubmissionDialog from './RendezvousSubmissionDialog.vue';

vi.mock('../api', () => ({ api: vi.fn(), getAccess: () => ({ apiKey: 'test-key' }) }));
const service = {
  id: 'test-id',
  name: 'Signal',
  controlUrl: 'https://signal.example/',
  status: 'active',
};
function panel(mode: 'public' | 'publisher' | 'manage') {
  return mount(RendezvousServicesPanel, {
    props: { mode, canSubmit: true },
    global: {
      plugins: [
        [
          VueQueryPlugin,
          { queryClient: new QueryClient({ defaultOptions: { queries: { retry: false } } }) },
        ],
      ],
    },
  });
}
beforeEach(() => {
  vi.clearAllMocks();
});
describe('Rendezvous services', () => {
  it('keeps an unavailable directory distinct from an empty directory', async () => {
    vi.mocked(api).mockRejectedValue(new Error('服务不可访问'));
    const wrapper = panel('public');
    await flushPromises();
    expect(wrapper.text()).toContain('服务不可访问');
    expect(wrapper.text()).not.toContain('没有匹配');
    wrapper.unmount();
  });
  it('loads all public pages using the public paged contract', async () => {
    vi.mocked(api)
      .mockResolvedValueOnce({ items: [service], total: 101, pageIndex: 1, pageSize: 100 })
      .mockResolvedValueOnce({
        items: [{ ...service, id: 'second', name: 'Second' }],
        total: 101,
        pageIndex: 2,
        pageSize: 100,
      });
    const wrapper = panel('public');
    await flushPromises();
    expect(api).toHaveBeenCalledWith(
      '/api/v1/rendezvous-services?pageIndex=2&pageSize=100',
      expect.objectContaining({ signal: expect.anything() }),
    );
    expect(wrapper.text()).toContain('Second');
    wrapper.unmount();
  });
  it('stops publication without deleting the registration', async () => {
    vi.mocked(api).mockResolvedValue([service]);
    const previousConfirm = window.confirm;
    window.confirm = vi.fn(() => true);
    const wrapper = panel('manage');
    await flushPromises();
    const disable = wrapper.findAll('button').find((button) => button.text() === '停用')!;
    await disable.trigger('click');
    await flushPromises();
    expect(api).toHaveBeenCalledWith('/api/v1/admin/rendezvous-services/test-id/disable', {
      method: 'POST',
    });
    wrapper.unmount();
    window.confirm = previousConfirm;
    vi.restoreAllMocks();
  });
  it('edits via the publisher scenario endpoint and retains input after failure', async () => {
    vi.mocked(api).mockRejectedValue(new Error('地址不可访问'));
    const wrapper = mount(RendezvousSubmissionDialog, {
      props: { open: false, target: service },
      global: { stubs: { teleport: true } },
    });
    await wrapper.setProps({ open: true });
    await wrapper.find('form').trigger('submit');
    await flushPromises();
    expect(api).toHaveBeenCalledWith('/api/v1/publisher/rendezvous-services/test-id/edit', {
      method: 'POST',
      body: JSON.stringify({
        name: 'Signal',
        controlUrl: 'https://signal.example/',
        description: '',
      }),
    });
    expect(wrapper.text()).toContain('地址不可访问');
    expect((wrapper.find('input').element as HTMLInputElement).value).toBe('Signal');
    expect(wrapper.emitted('submitted')).toBeUndefined();
    wrapper.unmount();
  });
});
