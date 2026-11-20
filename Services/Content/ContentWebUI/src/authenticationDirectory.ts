import { api, type PagedData } from './api';

export async function loadAuthenticationPages<T>(
  path: string,
  signal: AbortSignal,
  key: string,
): Promise<T[]> {
  const result: T[] = [];
  for (let pageIndex = 1; pageIndex <= 100; pageIndex++) {
    const page = await api<PagedData<T>>(
      `${path}${path.includes('?') ? '&' : '?'}pageIndex=${pageIndex}&pageSize=100`,
      { signal },
      key,
    );
    if (
      page.pageIndex !== pageIndex ||
      page.pageSize !== 100 ||
      !Array.isArray(page.items) ||
      page.items.length > 100 ||
      !Number.isInteger(page.total) ||
      page.total < 0 ||
      page.total > 10000
    ) {
      throw new Error('认证服务目录返回的数据无效');
    }
    result.push(...page.items);
    if (pageIndex * page.pageSize >= page.total) return result;
    if (!page.items.length) throw new Error('认证服务目录分页不完整');
  }
  throw new Error('认证服务目录超出分页限制');
}

export function authenticationError(value: unknown): string {
  const message = value instanceof Error ? value.message : '操作失败';
  const labels: Record<string, string> = {
    authentication_service_conflict:
      '记录已变化或地址重复。请刷新后核对最新信息；不会自动重试本次操作。',
    resource_conflict: '记录已被其他操作修改。请刷新并重新核对，本次操作未自动重试。',
    authentication_service_unavailable_or_invalid:
      '无法访问或验证认证服务入口。请确认 HTTPS 服务可公开访问且符合发现协议。',
    invalid_authentication_service_submission: '请检查名称、HTTPS 地址和说明。',
    authentication_service_not_found: '记录不存在，或当前发布者无权操作。',
    publisher_not_active: '发布者通过审核后才能投稿或修改。',
  };
  return labels[message] ?? message;
}
