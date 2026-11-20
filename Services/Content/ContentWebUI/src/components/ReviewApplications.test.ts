import { mount } from '@vue/test-utils';
import { describe, expect, it } from 'vitest';
import ReviewApplications, { type ReviewApplication } from './ReviewApplications.vue';

const application = (id: string, status: string): ReviewApplication => ({
  id,
  name: `申请 ${id}`,
  status,
  path: `/applications/${id}`,
  reviewMessage: status === 'rejected' ? '地址无效' : undefined,
});
describe('Review applications', () => {
  it('shows pending first and reviewed outcomes without review actions', async () => {
    const wrapper = mount(ReviewApplications, {
      props: {
        items: [
          application('old', 'rejected'),
          application('new', 'pending'),
          application('accepted', 'active'),
        ],
        loading: false,
      },
    });
    const sections = wrapper.findAll('section');
    expect(sections[0]!.text()).toContain('待审核');
    expect(sections[0]!.text()).toContain('申请 new');
    expect(sections[1]!.text()).toContain('申请 old');
    expect(sections[1]!.text()).toContain('已批准');
    expect(sections[1]!.text()).toContain('审核意见：地址无效');
    expect(sections[1]!.findAll('[aria-label="批准"], [aria-label="拒绝"]')).toHaveLength(0);
    await sections[0]!.get('[aria-label="批准"]').trigger('click');
    expect(wrapper.emitted('review')).toEqual([['/applications/new', true]]);
    await wrapper.setProps({
      items: [
        application('old', 'rejected'),
        application('new', 'active'),
        application('accepted', 'active'),
      ],
    });
    expect(wrapper.findAll('section')[0]!.text()).toContain('没有待审核申请');
    expect(wrapper.findAll('section')[1]!.text()).toContain('申请 new');
  });
  it('paginates each section independently and retains history when pending is empty', async () => {
    const wrapper = mount(ReviewApplications, {
      props: {
        items: Array.from({ length: 7 }, (_, index) => application(String(index), 'published')),
        loading: false,
      },
    });
    const history = wrapper.findAll('section')[1]!;
    expect(history.findAll('article')).toHaveLength(6);
    await history
      .findAll('button')
      .find((button) => button.text() === '下一页')!
      .trigger('click');
    expect(history.findAll('article')).toHaveLength(1);
    expect(history.text()).toContain('申请 6');
    expect(wrapper.findAll('section')[0]!.text()).toContain('没有待审核申请');
    await wrapper.setProps({ items: [application('0', 'published')] });
    expect(history.text()).toContain('申请 0');
    expect(history.text()).not.toContain('下一页');
  });
  it('does not report an unavailable list as empty history', () => {
    const wrapper = mount(ReviewApplications, {
      props: { items: [], loading: false, error: '连接失败' },
    });
    expect(wrapper.text()).toContain('连接失败');
    expect(wrapper.text()).not.toContain('暂无审核历史');
  });
});
