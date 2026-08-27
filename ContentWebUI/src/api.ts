import { getRuntimeConfig } from './config';

export interface ResponseData<T> {
  success: boolean;
  message: string;
  code: number;
  data: T;
}
export interface PagedData<T> {
  items: T[];
  total: number;
  pageIndex: number;
  pageSize: number;
}
export type AccessRole = 'publisher' | 'administrator';
export interface StoredAccess {
  role: AccessRole;
  apiKey: string;
  label?: string;
  keyPrefix?: string;
  lastUsedAt?: number;
  invalid?: boolean;
}
export interface ContentVersion {
  contentId: string;
  publisherId: string;
  type: string;
  identifier: string;
  name: string;
  summary?: string;
  contentStatus: string;
  versionId: string;
  version: string;
  packageHash: string;
  packageSize: number;
  fileName: string;
  metadataJson?: string;
  status: string;
  reviewMessage?: string;
  createdAt: string;
  publishedAt?: string;
  downloadUrl: string;
}

interface CredentialStore {
  publisher?: StoredCredential[] | StoredCredential | string;
  administrator?: StoredCredential[] | StoredCredential | string;
  activeAccess?: { role: AccessRole; apiKey: string };
  activeRole?: AccessRole;
}
interface StoredCredential {
  apiKey: string;
  label?: string;
  keyPrefix?: string;
  lastUsedAt?: number;
  invalid?: boolean;
}

const credentialsKey = 'scnet.content.credentials';
function getCredentialStore(): CredentialStore {
  const value = localStorage.getItem(credentialsKey);
  if (!value) return {};
  try {
    return JSON.parse(value) as CredentialStore;
  } catch {
    return {};
  }
}
function setCredentialStore(store: CredentialStore): void {
  localStorage.setItem(credentialsKey, JSON.stringify(store));
}
function getStoredCredentials(store: CredentialStore, role: AccessRole): StoredCredential[] {
  const value = store[role];
  if (!value) return [];
  if (typeof value === 'string') return [{ apiKey: value }];
  return Array.isArray(value) ? value : [value];
}
export function getSavedAccesses(role: AccessRole): StoredAccess[] {
  return getStoredCredentials(getCredentialStore(), role)
    .map((credential) => ({ role, ...credential }))
    .sort((left, right) => (right.lastUsedAt ?? 0) - (left.lastUsedAt ?? 0));
}
export function getAccess(role?: AccessRole): StoredAccess | null {
  const store = getCredentialStore();
  const selectedRole = role ?? store.activeAccess?.role ?? store.activeRole;
  if (!selectedRole) return null;
  const credentials = getSavedAccesses(selectedRole).filter((credential) => !credential.invalid);
  const selectedKey =
    store.activeAccess?.role === selectedRole ? store.activeAccess.apiKey : undefined;
  return selectedKey
    ? (credentials.find((item) => item.apiKey === selectedKey) ?? credentials[0] ?? null)
    : (credentials[0] ?? null);
}
export function getSavedRoles(): AccessRole[] {
  const store = getCredentialStore();
  return (['publisher', 'administrator'] as const).filter(
    (role) => getStoredCredentials(store, role).length > 0,
  );
}
export function setAccess(access: StoredAccess): void {
  const store = getCredentialStore();
  const credentials = getStoredCredentials(store, access.role);
  const existingIndex = credentials.findIndex((item) => item.apiKey === access.apiKey);
  const credential = {
    apiKey: access.apiKey,
    label: access.label,
    keyPrefix: access.keyPrefix ?? access.apiKey.slice(0, 18),
    lastUsedAt: Date.now(),
    invalid: false,
  };
  if (existingIndex >= 0) credentials[existingIndex] = credential;
  else credentials.push(credential);
  store[access.role] = credentials;
  store.activeAccess = { role: access.role, apiKey: access.apiKey };
  delete store.activeRole;
  setCredentialStore(store);
}
export function setActiveRole(role: AccessRole, apiKey?: string): void {
  const store = getCredentialStore();
  const credentials = getStoredCredentials(store, role);
  const selected = apiKey
    ? credentials.find((item) => item.apiKey === apiKey && !item.invalid)
    : getAccess(role);
  if (!selected) return;
  const index = credentials.findIndex((item) => item.apiKey === selected.apiKey);
  credentials[index] = { ...credentials[index], lastUsedAt: Date.now() };
  store[role] = credentials;
  store.activeAccess = { role, apiKey: selected.apiKey };
  delete store.activeRole;
  setCredentialStore(store);
}
export function updateAccessLabel(
  role: AccessRole,
  apiKey: string,
  label: string | undefined,
): void {
  const store = getCredentialStore();
  const credentials = getStoredCredentials(store, role);
  const index = credentials.findIndex((item) => item.apiKey === apiKey);
  if (index < 0) return;
  credentials[index] = {
    ...credentials[index],
    label,
    keyPrefix: credentials[index].keyPrefix ?? apiKey.slice(0, 18),
  };
  store[role] = credentials;
  setCredentialStore(store);
}
export function clearAccess(role?: AccessRole, apiKey?: string): void {
  if (!role) {
    localStorage.removeItem(credentialsKey);
    return;
  }
  const store = getCredentialStore();
  const credentials = getStoredCredentials(store, role);
  const remaining = apiKey ? credentials.filter((item) => item.apiKey !== apiKey) : [];
  if (remaining.length) store[role] = remaining;
  else delete store[role];
  if (store.activeAccess?.role === role && (!apiKey || store.activeAccess.apiKey === apiKey))
    delete store.activeAccess;
  if (store.activeRole === role) delete store.activeRole;
  setCredentialStore(store);
}

export function markAccessInvalid(role: AccessRole, apiKey: string): boolean {
  const store = getCredentialStore();
  const credentials = getStoredCredentials(store, role);
  const index = credentials.findIndex((item) => item.apiKey === apiKey);
  if (index < 0) return false;
  credentials[index] = { ...credentials[index], invalid: true };
  store[role] = credentials;
  const wasActive = store.activeAccess?.role === role && store.activeAccess.apiKey === apiKey;
  if (wasActive) delete store.activeAccess;
  setCredentialStore(store);
  return wasActive;
}

function roleForApiPath(path: string): AccessRole | undefined {
  if (path.startsWith('/api/v1/publisher')) return 'publisher';
  if (path.startsWith('/api/v1/admin') || path.startsWith('/api/v1/administrator'))
    return 'administrator';
  return undefined;
}

function handleUnauthorized(path: string, apiKey: string | undefined): void {
  const role = roleForApiPath(path);
  if (!role || !apiKey) return;
  const wasActive = markAccessInvalid(role, apiKey);
  if (wasActive)
    window.dispatchEvent(new CustomEvent('scnet-content-key-invalid', { detail: { role } }));
}

export async function api<T>(path: string, init: RequestInit = {}, apiKey?: string): Promise<T> {
  const headers = new Headers(init.headers);
  const key = apiKey ?? getAccess()?.apiKey;
  if (key) headers.set('Authorization', `Bearer ${key}`);
  if (init.body && !(init.body instanceof FormData))
    headers.set('Content-Type', 'application/json');
  const apiBaseUrl = getRuntimeConfig().apiBaseUrl;
  let response: Response;
  try {
    response = await fetch(`${apiBaseUrl}${path}`, { ...init, headers });
  } catch {
    throw new Error(`无法连接 ContentServer（${apiBaseUrl || window.location.origin}）`);
  }
  const result = (await response.json().catch(() => null)) as ResponseData<T> | null;
  if (response.status === 401) handleUnauthorized(path, key);
  if (!response.ok || !result?.success)
    throw new Error(result?.message || `请求失败 (${response.status})`);
  return result.data;
}

export async function download(path: string): Promise<void> {
  const headers = new Headers();
  const key = getAccess()?.apiKey;
  if (key) headers.set('Authorization', `Bearer ${key}`);
  const response = await fetch(`${getRuntimeConfig().apiBaseUrl}${path}`, { headers });
  if (response.status === 401) handleUnauthorized(path, key);
  if (!response.ok) {
    const result = (await response.json().catch(() => null)) as ResponseData<unknown> | null;
    throw new Error(result?.message || `下载失败 (${response.status})`);
  }
  const name = response.headers
    .get('Content-Disposition')
    ?.match(/filename\*?=(?:UTF-8''|\")?([^\";]+)/i)?.[1];
  const url = URL.createObjectURL(await response.blob());
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = name ? decodeURIComponent(name.replace(/"/g, '')) : 'package';
  anchor.click();
  URL.revokeObjectURL(url);
}

export function queryString(values: Record<string, string | number | undefined>): string {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(values))
    if (value !== undefined && value !== '') search.set(key, String(value));
  return search.toString();
}
