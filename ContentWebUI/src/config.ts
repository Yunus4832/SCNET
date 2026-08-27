export interface RuntimeConfig {
  apiBaseUrl: string;
}

let runtimeConfig: RuntimeConfig = { apiBaseUrl: '' };

export async function loadRuntimeConfig(): Promise<void> {
  const response = await fetch('/runtime-config.json', { cache: 'no-store' });
  if (!response.ok) throw new Error('无法加载运行时配置');
  runtimeConfig = (await response.json()) as RuntimeConfig;
  runtimeConfig.apiBaseUrl = runtimeConfig.apiBaseUrl.replace(/\/$/, '');
}

export function getRuntimeConfig(): RuntimeConfig {
  return runtimeConfig;
}
