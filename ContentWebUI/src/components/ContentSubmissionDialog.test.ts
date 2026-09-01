import { mount } from '@vue/test-utils';
import { describe, expect, it, vi } from 'vitest';
import ContentSubmissionDialog from './ContentSubmissionDialog.vue';

vi.mock('../api', () => ({
  api: vi.fn(),
  getAccess: () => ({ apiKey: 'publisher-key' }),
}));
vi.mock('../config', () => ({ getRuntimeConfig: () => ({ apiBaseUrl: '' }) }));

describe('ContentSubmissionDialog', () => {
  it('separates authoritative package upload from limited image manufacturing', async () => {
    const wrapper = mount(ContentSubmissionDialog, { props: { open: true }, attachTo: document.body });
    expect(document.body.textContent).toContain('完整 .scpkg');
    expect(document.body.textContent).toContain('包内类型、Identifier、名称和版本由 ContentServer 权威解析');

    const imageMode = Array.from(document.body.querySelectorAll('button'))
      .find((button) => button.textContent?.includes('图片制造'))!;
    imageMode.click();
    await wrapper.vm.$nextTick();
    expect(document.body.textContent).toContain('仅皮肤与方块材质');
    expect(document.body.textContent).toContain('草稿仅保存在当前浏览器');
    expect(Array.from(document.body.querySelectorAll('option')).map((option) => option.value)).toEqual([
      'CharacterSkin', 'BlocksTexture',
    ]);
    wrapper.unmount();
  });
});
