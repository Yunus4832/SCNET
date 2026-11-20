import { describe, expect, it } from 'vitest';
import { findApiKeyInText } from './apiKeyFile';

describe('findApiKeyInText', () => {
  it('finds the key line for the requested role', () => {
    const content = 'SCNET 内容服务器 发布者 API Key\n\nscpub_0123456789abcdef\n\n请妥善保存';
    expect(findApiKeyInText(content, 'publisher')).toBe('scpub_0123456789abcdef');
  });

  it('does not import a key for another role', () => {
    expect(() => findApiKeyInText('scadm_0123456789abcdef', 'publisher')).toThrow(
      '文件中没有找到 scpub_ 开头的 API Key',
    );
  });

  it('rejects ambiguous files', () => {
    expect(() => findApiKeyInText('scpub_first\nscpub_second', 'publisher')).toThrow(
      '文件中包含多个 scpub_ 开头的 API Key',
    );
  });
});
