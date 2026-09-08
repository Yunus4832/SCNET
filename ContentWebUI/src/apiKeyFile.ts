import type { AccessRole } from './api';

const keyPrefixes: Record<AccessRole, string> = {
  administrator: 'scadm_',
  publisher: 'scpub_',
};

export function findApiKeyInText(content: string, role: AccessRole): string {
  const prefix = keyPrefixes[role];
  const matches = content
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line.startsWith(prefix));

  if (matches.length === 0) throw new Error(`文件中没有找到 ${prefix} 开头的 API Key`);
  if (matches.length > 1) throw new Error(`文件中包含多个 ${prefix} 开头的 API Key`);
  return matches[0];
}
