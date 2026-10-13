import { flushPromises, mount } from '@vue/test-utils';
import { QueryClient, VueQueryPlugin } from '@tanstack/vue-query';
import { expect, it, vi } from 'vitest';
import { api } from '../api';
import AdminPage from './AdminPage.vue';

vi.mock('vue-router', () => ({ useRouter: () => ({ push: vi.fn() }) }));
vi.mock('../api', () => ({
  api: vi.fn(),
  getAccess: () => ({ apiKey: 'test-admin' }),
  clearAccess: vi.fn(),
  download: vi.fn(),
  queryString: () => '',
}));
it('counts all submissions and loads later pages before separating pending and history', async () => {
  const records = Array.from({ length: 101 }, (_, index) => ({
    versionId: String(index),
    name: `版本 ${index}`,
    identifier: 'test.mod',
    type: 'Mod',
    version: '1.0.0',
    status: index === 100 ? 'pending' : 'published',
    createdAt: '2026-10-13T00:00:00Z',
  }));
  vi.mocked(api).mockImplementation(async (path) => {
    if (path === '/api/v1/administrator') return { status: 'active' };
    if (path.startsWith('/api/v1/admin/submissions?')) {
      const pageIndex = Number(new URL(path, 'https://test.local').searchParams.get('pageIndex'));
      return {
        items: records.slice((pageIndex - 1) * 100, pageIndex * 100),
        total: 101,
        pageIndex,
        pageSize: 100,
      };
    }
    if (
      path.includes('publishers') ||
      path.includes('administrator-applications') ||
      path.includes('/content?')
    )
      return { items: [], total: 0, pageIndex: 1, pageSize: 100 };
    return [];
  });
  const wrapper = mount(AdminPage, {
    global: {
      plugins: [
        [
          VueQueryPlugin,
          { queryClient: new QueryClient({ defaultOptions: { queries: { retry: false } } }) },
        ],
      ],
      stubs: { GameInformationManagement: true, ServerSubmissionDialog: true },
    },
  });
  await flushPromises();
  await flushPromises();
  const statistic = wrapper
    .findAll('.stats button')
    .find((button) => button.text().includes('内容申请'))!;
  expect(statistic.text()).toContain('101');
  const sections = wrapper.findAll('.review-section');
  expect(sections[0]!.text()).toContain('版本 100');
  expect(sections[1]!.text()).toContain('100 项');
  expect(api).toHaveBeenCalledWith(
    '/api/v1/admin/submissions?pageIndex=2&pageSize=100',
    expect.anything(),
  );
  expect(vi.mocked(api).mock.calls.every(([path]) => !path.includes('status=Pending'))).toBe(true);
  wrapper.unmount();
});
