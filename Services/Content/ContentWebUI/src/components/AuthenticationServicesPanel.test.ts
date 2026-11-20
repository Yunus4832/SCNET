import { flushPromises, mount } from '@vue/test-utils';
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { api } from '../api';
import { loadAuthenticationPages } from '../authenticationDirectory';
import AuthenticationServicesPanel from './AuthenticationServicesPanel.vue';
import AuthenticationSubmissionDialog from './AuthenticationSubmissionDialog.vue';

vi.mock('../api', () => ({
  api: vi.fn(),
  getAccess: (role: string) => ({ apiKey: role === 'publisher' ? 'publisher-test' : 'admin-test' }),
}));
const service = {
  id: '0bf090a8-e809-4f6d-a31d-36f1bf7000ba',
  name: 'Community Authentication',
  serviceUrl: 'https://auth.example/',
  status: 'active',
  revision: 7,
};
const page = (items: unknown[]) => ({ items, total: items.length, pageIndex: 1, pageSize: 100 });
function panel(mode: 'public' | 'publisher' | 'review' | 'manage') {
  return mount(AuthenticationServicesPanel, {
    props: { mode, canSubmit: true },
    global: {
      plugins: [
        [
          VueQueryPlugin,
          { queryClient: new QueryClient({ defaultOptions: { queries: { retry: false } } }) },
        ],
      ],
      stubs: { teleport: true },
    },
  });
}
const previousConfirm = window.confirm;
beforeEach(() => {
  vi.resetAllMocks();
  window.confirm = vi.fn(() => true);
});
afterEach(() => {
  vi.restoreAllMocks();
  window.confirm = previousConfirm;
});

describe('Authentication service directory', () => {
  it('uses anonymous bounded pagination for the public directory', async () => {
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
      '/api/v1/authentication-services?pageIndex=2&pageSize=100',
      expect.objectContaining({ signal: expect.anything() }),
      '',
    );
    expect(wrapper.text()).toContain('Second');
    expect(wrapper.text()).toContain(service.serviceUrl);
    expect(wrapper.findAll('button').some((button) => button.text() === '批准')).toBe(false);
    wrapper.unmount();
  });

  it('keeps an unavailable directory distinct from an empty one', async () => {
    vi.mocked(api).mockRejectedValue(new Error('服务不可访问'));
    const wrapper = panel('public');
    await flushPromises();
    expect(wrapper.text()).toContain('服务不可访问');
    expect(wrapper.text()).not.toContain('没有匹配的认证服务');
    expect(api).toHaveBeenCalledTimes(1);
    wrapper.unmount();
  });

  it('rejects malformed or incomplete pagination instead of showing partial trusted results', async () => {
    const signal = new AbortController().signal;
    vi.mocked(api).mockResolvedValueOnce({ items: [], total: 10001, pageIndex: 1, pageSize: 100 });
    await expect(loadAuthenticationPages('/directory', signal, '')).rejects.toThrow('数据无效');
    vi.mocked(api)
      .mockResolvedValueOnce({ items: [service], total: 201, pageIndex: 1, pageSize: 100 })
      .mockResolvedValueOnce({ items: [], total: 201, pageIndex: 2, pageSize: 100 });
    await expect(loadAuthenticationPages('/directory', signal, '')).rejects.toThrow('分页不完整');
  });

  it('submits the reviewed revision and preserves notes after a conflict without retrying approval', async () => {
    vi.mocked(api).mockImplementation(async (_path, init) => {
      if (init?.method === 'POST') throw new Error('authentication_service_conflict');
      return page([
        { ...service, status: 'pending' },
        { ...service, id: 'old', status: 'active' },
      ]);
    });
    const wrapper = panel('review');
    await flushPromises();
    await wrapper.find('textarea').setValue('Reviewed the displayed source');
    const approve = wrapper.findAll('button').filter((button) => button.text() === '批准');
    expect(approve).toHaveLength(1);
    await approve[0]!.trigger('click');
    await flushPromises();
    expect(window.confirm).toHaveBeenCalledWith(expect.stringContaining(service.serviceUrl));
    expect(api).toHaveBeenCalledWith(
      `/api/v1/admin/authentication-services/${service.id}/approve`,
      {
        method: 'POST',
        body: JSON.stringify({ revision: 7, message: 'Reviewed the displayed source' }),
      },
      'admin-test',
    );
    expect(vi.mocked(api).mock.calls.filter(([, init]) => init?.method === 'POST')).toHaveLength(1);
    expect(wrapper.text()).toContain('记录已变化');
    expect((wrapper.find('textarea').element as HTMLTextAreaElement).value).toBe(
      'Reviewed the displayed source',
    );
    wrapper.unmount();
  });

  it('withdraws a publisher listing without deleting it and includes its revision', async () => {
    vi.mocked(api).mockResolvedValue(page([service]));
    const wrapper = panel('publisher');
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '撤回')!
      .trigger('click');
    await flushPromises();
    expect(api).toHaveBeenCalledWith(
      `/api/v1/publisher/authentication-services/${service.id}/withdraw`,
      { method: 'POST', body: JSON.stringify({ revision: 7 }) },
      'publisher-test',
    );
    expect(vi.mocked(api).mock.calls.some(([, init]) => init?.method === 'DELETE')).toBe(false);
    wrapper.unmount();
  });

  it('retains access to audited source after administrator deletion', async () => {
    vi.mocked(api).mockImplementation(async (path) =>
      path.includes('/audit')
        ? page([
            {
              revision: 8,
              action: 'Deleted',
              name: service.name,
              serviceUrl: service.serviceUrl,
              publisherId: 'publisher-id',
              administratorId: 'administrator-id',
              occurredAt: '2026-10-25T00:00:00Z',
              status: 'active',
            },
          ])
        : page([service]),
    );
    const wrapper = panel('manage');
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '删除')!
      .trigger('click');
    await flushPromises();
    expect(api).toHaveBeenCalledWith(
      `/api/v1/admin/authentication-services/${service.id}?revision=7`,
      { method: 'DELETE' },
      'admin-test',
    );
    expect(api).toHaveBeenCalledWith(
      `/api/v1/admin/authentication-services/${service.id}/audit?pageIndex=1&pageSize=100`,
      expect.objectContaining({ signal: expect.anything() }),
      'admin-test',
    );
    expect(wrapper.find('[role="dialog"]').text()).toContain('Deleted');
    expect(wrapper.find('[role="dialog"]').text()).toContain(service.serviceUrl);
    wrapper.unmount();
  });

  it('preserves editor fields and revision after server validation failure', async () => {
    vi.mocked(api).mockRejectedValue(new Error('authentication_service_unavailable_or_invalid'));
    const wrapper = mount(AuthenticationSubmissionDialog, {
      props: { open: true, target: service },
      global: { stubs: { teleport: true } },
    });
    await wrapper.find('input[maxlength="100"]').setValue('Edited Authority');
    await wrapper.find('form').trigger('submit');
    await flushPromises();
    expect(api).toHaveBeenCalledWith(
      `/api/v1/publisher/authentication-services/${service.id}/edit`,
      {
        method: 'POST',
        body: JSON.stringify({
          name: 'Edited Authority',
          serviceUrl: service.serviceUrl,
          description: '',
          revision: 7,
        }),
      },
      'publisher-test',
    );
    expect((wrapper.find('input[maxlength="100"]').element as HTMLInputElement).value).toBe(
      'Edited Authority',
    );
    expect(wrapper.text()).toContain('无法访问或验证认证服务入口');
    expect(wrapper.emitted('submitted')).toBeUndefined();
    wrapper.unmount();
  });

  it('focuses the editor and lets Escape close it without submitting', async () => {
    const wrapper = mount(AuthenticationSubmissionDialog, {
      attachTo: document.body,
      props: { open: true },
      global: { stubs: { teleport: true } },
    });
    await flushPromises();
    const input = wrapper.find('input[maxlength="100"]');
    expect(document.activeElement).toBe(input.element);
    await input.trigger('keydown', { key: 'Escape' });
    expect(wrapper.emitted('close')).toHaveLength(1);
    expect(api).not.toHaveBeenCalled();
    wrapper.unmount();
  });

  it('never submits a malformed management record lacking revision', async () => {
    vi.mocked(api).mockResolvedValue(
      page([{ ...service, status: 'pending', revision: undefined }]),
    );
    const wrapper = panel('review');
    await flushPromises();
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '批准')!
      .trigger('click');
    await flushPromises();
    expect(wrapper.text()).toContain('修订号无效');
    expect(window.confirm).not.toHaveBeenCalled();
    wrapper.unmount();
    const editor = mount(AuthenticationSubmissionDialog, {
      props: { open: true, target: { ...service, revision: undefined } },
      global: { stubs: { teleport: true } },
    });
    await editor.find('form').trigger('submit');
    await flushPromises();
    expect(vi.mocked(api).mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false);
    expect(editor.text()).toContain('修订号无效');
    editor.unmount();
  });
});
