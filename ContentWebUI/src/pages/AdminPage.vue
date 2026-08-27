<script setup lang="ts">
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { ArrowLeftRight, Check, Download, LogOut, Search, X } from 'lucide-vue-next';
import { computed, ref } from 'vue';
import { useRouter } from 'vue-router';
import {
  api,
  clearAccess,
  download,
  getAccess,
  queryString,
  type ContentVersion,
  type PagedData,
} from '../api';

interface Applicant {
  administratorId?: string;
  publisherId?: string;
  name?: string;
  displayName?: string;
  contact: string;
  description?: string;
  status: string;
  createdAt: string;
  isSuperAdministrator?: boolean;
  hasActiveKey?: boolean;
}
interface Administrator {
  administratorId: string;
  name: string;
  status: string;
  isSuperAdministrator: boolean;
  hasActiveKey: boolean;
  reviewMessage?: string;
}
interface ContentItem {
  contentId: string;
  publisherId: string;
  type: string;
  identifier: string;
  name: string;
  summary?: string;
  status: string;
  createdAt: string;
  updatedAt: string;
}
const router = useRouter();
const client = useQueryClient();
const mode = ref<'review' | 'manage'>('review');
const tab = ref<'content' | 'publishers' | 'administrators'>('content');
const actionError = ref('');
const accessCacheKey = getAccess('administrator')?.apiKey.slice(0, 18) ?? 'anonymous';
const manageSearchInput = ref('');
const manageSearch = ref('');
const contentType = ref('');
const contentPage = ref(1);
const publisherPage = ref(1);
const administratorPage = ref(1);
const reviewVersionsPage = ref(1);
const reviewPublishersPage = ref(1);
const reviewAdministratorsPage = ref(1);
const contentTypes = [
  ['', '全部'],
  ['Mod', '模组'],
  ['World', '世界'],
  ['BlocksTexture', '材质'],
  ['CharacterSkin', '皮肤'],
  ['FurniturePack', '家具包'],
];
const self = useQuery({
  queryKey: ['admin-self', accessCacheKey],
  queryFn: () => api<Administrator>('/api/v1/administrator'),
});
const isActive = computed(() => self.data.value?.status === 'active');
const statusTitle = computed(() => {
  switch (self.data.value?.status) {
    case 'pending':
      return '管理员申请正在审核';
    case 'rejected':
      return '管理员申请未通过';
    case 'suspended':
      return '管理员权限已暂停';
    default:
      return '管理员工作台暂不可用';
  }
});
const statusDescription = computed(
  () =>
    self.data.value?.reviewMessage ??
    (self.data.value?.status === 'pending'
      ? '审核通过后即可处理申请与内容。'
      : '请联系服务器管理员，或提交新的管理员申请。'),
);
const versions = useQuery({
  queryKey: ['admin-versions', accessCacheKey, reviewVersionsPage],
  enabled: isActive,
  queryFn: () =>
    api<PagedData<ContentVersion>>(
      `/api/v1/admin/submissions?status=Pending&pageIndex=${reviewVersionsPage.value}&pageSize=6`,
    ),
});
const publishers = useQuery({
  queryKey: ['admin-publishers', accessCacheKey, reviewPublishersPage],
  enabled: isActive,
  queryFn: () =>
    api<PagedData<Applicant>>(
      `/api/v1/admin/publishers?status=Pending&pageIndex=${reviewPublishersPage.value}&pageSize=6`,
    ),
});
const administrators = useQuery({
  queryKey: ['admin-applications', accessCacheKey, reviewAdministratorsPage],
  enabled: isActive,
  queryFn: () =>
    api<PagedData<Applicant>>(
      `/api/v1/admin/administrator-applications?status=Pending&pageIndex=${reviewAdministratorsPage.value}&pageSize=6`,
    ),
});
const managedContent = useQuery({
  queryKey: computed(() => [
    'admin-content',
    accessCacheKey,
    manageSearch.value,
    contentType.value,
    contentPage.value,
  ]),
  enabled: isActive,
  queryFn: () =>
    api<PagedData<ContentItem>>(
      `/api/v1/admin/content?${queryString({ query: manageSearch.value, type: contentType.value, pageIndex: contentPage.value, pageSize: 6 })}`,
    ),
});
const publisherKeys = useQuery({
  queryKey: computed(() => [
    'admin-publisher-keys',
    accessCacheKey,
    manageSearch.value,
    publisherPage.value,
  ]),
  enabled: isActive,
  queryFn: () =>
    api<PagedData<Applicant>>(
      `/api/v1/admin/publishers?${queryString({ query: manageSearch.value, pageIndex: publisherPage.value, pageSize: 6 })}`,
    ),
});
const administratorKeys = useQuery({
  queryKey: computed(() => [
    'admin-administrator-keys',
    accessCacheKey,
    manageSearch.value,
    administratorPage.value,
  ]),
  enabled: computed(() => isActive.value && self.data.value?.isSuperAdministrator === true),
  queryFn: () =>
    api<PagedData<Applicant>>(
      `/api/v1/admin/administrator-applications?${queryString({ query: manageSearch.value, pageIndex: administratorPage.value, pageSize: 6 })}`,
    ),
});
const contentCount = useQuery({
  queryKey: ['admin-content-count', accessCacheKey],
  enabled: isActive,
  queryFn: () => api<PagedData<ContentItem>>('/api/v1/admin/content?pageIndex=1&pageSize=1'),
});
const publisherKeyCount = useQuery({
  queryKey: ['admin-publisher-key-count', accessCacheKey],
  enabled: isActive,
  queryFn: () => api<PagedData<Applicant>>('/api/v1/admin/publishers?pageIndex=1&pageSize=1'),
});
const administratorKeyCount = useQuery({
  queryKey: ['admin-administrator-key-count', accessCacheKey],
  enabled: computed(() => isActive.value && self.data.value?.isSuperAdministrator === true),
  queryFn: () =>
    api<PagedData<Applicant>>('/api/v1/admin/administrator-applications?pageIndex=1&pageSize=1'),
});
function removeKey() {
  clearAccess('administrator', getAccess('administrator')?.apiKey);
  router.push('/admin/access');
}
async function review(path: string, approve: boolean) {
  actionError.value = '';
  try {
    await api(`${path}/${approve ? 'approve' : 'reject'}`, {
      method: 'POST',
      body: approve ? undefined : JSON.stringify({ message: '未通过审核' }),
    });
    await client.invalidateQueries();
  } catch (value) {
    actionError.value = value instanceof Error ? value.message : '操作失败';
  }
}
async function downloadPackage(versionId: string) {
  actionError.value = '';
  try {
    await download(`/api/v1/admin/submissions/${versionId}/package`);
  } catch (value) {
    actionError.value = value instanceof Error ? value.message : '下载失败';
  }
}
async function setContentStatus(contentId: string, enabled: boolean) {
  try {
    await api('/api/v1/admin/content/' + contentId + '/' + (enabled ? 'enable' : 'disable'), {
      method: 'POST',
    });
    await client.invalidateQueries();
  } catch (value) {
    actionError.value = value instanceof Error ? value.message : '操作失败';
  }
}
async function revokeKey(publisherId: string) {
  try {
    await api('/api/v1/admin/publishers/' + publisherId + '/revoke-key', { method: 'POST' });
    await client.invalidateQueries();
  } catch (value) {
    actionError.value = value instanceof Error ? value.message : '操作失败';
  }
}
async function revokeAdministratorKey(administratorId: string) {
  try {
    await api('/api/v1/admin/administrators/' + administratorId + '/revoke-key', {
      method: 'POST',
    });
    await client.invalidateQueries();
  } catch (value) {
    actionError.value = value instanceof Error ? value.message : '操作失败';
  }
}
async function restoreKey(role: 'publishers' | 'administrators', id: string) {
  try {
    await api('/api/v1/admin/' + role + '/' + id + '/restore-key', { method: 'POST' });
    await client.invalidateQueries();
  } catch (value) {
    actionError.value = value instanceof Error ? value.message : '操作失败';
  }
}
function switchMode(value: 'review' | 'manage') {
  mode.value = value;
  if (
    value === 'manage' &&
    tab.value === 'administrators' &&
    !self.data.value?.isSuperAdministrator
  )
    tab.value = 'content';
}
function selectManageTab(value: 'content' | 'publishers' | 'administrators') {
  tab.value = value;
  manageSearchInput.value = '';
  manageSearch.value = '';
}
function submitManageSearch() {
  manageSearch.value = manageSearchInput.value.trim();
  if (tab.value === 'content') contentPage.value = 1;
  else if (tab.value === 'publishers') publisherPage.value = 1;
  else administratorPage.value = 1;
}
function selectContentType(value: string) {
  contentType.value = value;
  contentPage.value = 1;
}
</script>

<template>
  <section class="shell page-pad admin-page workspace-page">
    <div class="workspace-head">
      <div><h1 class="page-title">内容管理</h1></div>
      <div class="workspace-actions">
        <RouterLink class="button ghost" to="/admin/access"
          ><ArrowLeftRight :size="17" />切换身份</RouterLink
        ><button class="button ghost" @click="removeKey"><LogOut :size="17" />退出</button>
      </div>
    </div>
    <div v-if="self.isError.value" class="state error">{{ self.error.value?.message }}</div>
    <div v-else-if="self.data.value && !isActive" class="state">
      <span class="status" :class="self.data.value.status">{{ self.data.value.status }}</span
      ><strong>{{ statusTitle }}</strong
      ><span>{{ statusDescription }}</span>
    </div>
    <template v-else>
      <div class="workspace-tabs primary-tabs">
        <button :class="{ active: mode === 'review' }" @click="switchMode('review')">审核</button
        ><button :class="{ active: mode === 'manage' }" @click="switchMode('manage')">管理</button>
      </div>
      <template v-if="mode === 'review'"
        ><div class="stats">
          <button :class="{ active: tab === 'content' }" @click="tab = 'content'">
            <b>{{ versions.data.value?.total ?? 0 }}</b
            ><span>待审内容</span></button
          ><button :class="{ active: tab === 'publishers' }" @click="tab = 'publishers'">
            <b>{{ publishers.data.value?.total ?? 0 }}</b
            ><span>发布者申请</span></button
          ><button :class="{ active: tab === 'administrators' }" @click="tab = 'administrators'">
            <b>{{ administrators.data.value?.total ?? 0 }}</b
            ><span>管理员申请</span>
          </button>
        </div>
        <p v-if="actionError" class="form-error">{{ actionError }}</p>
        <div v-if="tab === 'content'">
          <h2>内容审核</h2>
          <div v-if="versions.data.value?.items.length" class="content-grid">
            <article
              v-for="item in versions.data.value?.items"
              :key="item.versionId"
              class="content-card"
            >
              <div class="card-top">
                <span class="type-pill">{{ item.type }}</span
                ><span class="version">v{{ item.version }}</span>
              </div>
              <div>
                <h3>{{ item.name }}</h3>
                <code>{{ item.identifier }}</code>
                <p>{{ item.summary || '无简介' }}</p>
              </div>
              <div class="card-bottom">
                <button class="button ghost" @click="downloadPackage(item.versionId)">
                  <Download :size="16" />测试包
                </button>
                <div class="review-actions">
                  <button
                    class="icon-button reject"
                    @click="review(`/api/v1/admin/submissions/${item.versionId}`, false)"
                  >
                    <X /></button
                  ><button
                    class="icon-button approve"
                    @click="review(`/api/v1/admin/submissions/${item.versionId}`, true)"
                  >
                    <Check />
                  </button>
                </div>
              </div>
            </article>
          </div>
          <div v-else class="state">没有待审核内容</div>
          <div v-if="(versions.data.value?.total ?? 0) > 6" class="pager">
            <button :disabled="reviewVersionsPage <= 1" @click="reviewVersionsPage--">上一页</button
            ><span
              >{{ reviewVersionsPage }} /
              {{ Math.ceil((versions.data.value?.total ?? 0) / 6) }}</span
            ><button
              :disabled="reviewVersionsPage * 6 >= (versions.data.value?.total ?? 0)"
              @click="reviewVersionsPage++"
            >
              下一页
            </button>
          </div>
        </div>
        <div v-else>
          <h2>{{ tab === 'publishers' ? '发布者申请审核' : '管理员申请审核' }}</h2>
          <div
            v-if="
              (tab === 'publishers'
                ? publishers.data.value?.items
                : administrators.data.value?.items
              )?.length
            "
            class="content-grid"
          >
            <article
              v-for="item in tab === 'publishers'
                ? publishers.data.value?.items
                : administrators.data.value?.items"
              :key="item.publisherId || item.administratorId"
              class="content-card"
            >
              <div class="card-top">
                <span class="eyebrow">{{ item.contact }}</span>
              </div>
              <div>
                <h3>{{ item.displayName || item.name }}</h3>
                <p>{{ item.description || '未提供申请说明' }}</p>
              </div>
              <div class="card-bottom">
                <span>待审核</span>
                <div class="review-actions">
                  <button
                    class="icon-button reject"
                    @click="
                      review(
                        `/api/v1/admin/${tab === 'publishers' ? 'publishers' : 'administrator-applications'}/${item.publisherId || item.administratorId}`,
                        false,
                      )
                    "
                  >
                    <X /></button
                  ><button
                    class="icon-button approve"
                    @click="
                      review(
                        `/api/v1/admin/${tab === 'publishers' ? 'publishers' : 'administrator-applications'}/${item.publisherId || item.administratorId}`,
                        true,
                      )
                    "
                  >
                    <Check />
                  </button>
                </div>
              </div>
            </article>
          </div>
          <div v-else class="state">没有待审核申请</div>
          <div v-if="tab === 'publishers' && (publishers.data.value?.total ?? 0) > 6" class="pager">
            <button :disabled="reviewPublishersPage <= 1" @click="reviewPublishersPage--">
              上一页</button
            ><span
              >{{ reviewPublishersPage }} /
              {{ Math.ceil((publishers.data.value?.total ?? 0) / 6) }}</span
            ><button
              :disabled="reviewPublishersPage * 6 >= (publishers.data.value?.total ?? 0)"
              @click="reviewPublishersPage++"
            >
              下一页
            </button>
          </div>
          <div
            v-if="tab === 'administrators' && (administrators.data.value?.total ?? 0) > 6"
            class="pager"
          >
            <button :disabled="reviewAdministratorsPage <= 1" @click="reviewAdministratorsPage--">
              上一页</button
            ><span
              >{{ reviewAdministratorsPage }} /
              {{ Math.ceil((administrators.data.value?.total ?? 0) / 6) }}</span
            ><button
              :disabled="reviewAdministratorsPage * 6 >= (administrators.data.value?.total ?? 0)"
              @click="reviewAdministratorsPage++"
            >
              下一页
            </button>
          </div>
        </div></template
      >
      <div v-else>
        <div class="stats">
          <button :class="{ active: tab === 'content' }" @click="selectManageTab('content')">
            <b>{{ contentCount.data.value?.total ?? 0 }}</b
            ><span>累计内容</span>
          </button>
          <button :class="{ active: tab === 'publishers' }" @click="selectManageTab('publishers')">
            <b>{{ publisherKeyCount.data.value?.total ?? 0 }}</b
            ><span>发布者 Key</span>
          </button>
          <button
            v-if="self.data.value?.isSuperAdministrator"
            :class="{ active: tab === 'administrators' }"
            @click="selectManageTab('administrators')"
          >
            <b>{{ administratorKeyCount.data.value?.total ?? 0 }}</b
            ><span>管理员 Key</span>
          </button>
        </div>
        <p v-if="actionError" class="form-error">{{ actionError }}</p>
        <div v-if="tab === 'content'" class="review-list">
          <h2>
            管理内容 <small>{{ managedContent.data.value?.total ?? 0 }} 项结果</small>
          </h2>
          <form class="management-filters" @submit.prevent="submitManageSearch">
            <Search :size="17" /><input
              v-model="manageSearchInput"
              placeholder="搜索名称、标识符或简介"
            /><button class="button primary">搜索</button>
          </form>
          <div class="filters management-types">
            <button
              v-for="item in contentTypes"
              :key="item[0]"
              :class="{ active: contentType === item[0] }"
              @click="selectContentType(item[0])"
            >
              {{ item[1] }}
            </button>
          </div>
          <div v-if="managedContent.data.value?.items.length" class="admin-content-grid">
            <article
              v-for="item in managedContent.data.value?.items"
              :key="item.contentId"
              class="content-card admin-content-card"
            >
              <div class="card-top">
                <span class="type-pill">{{ item.type }}</span
                ><span class="status" :class="item.status">{{
                  item.status === 'active' ? '已启用' : '已下架'
                }}</span>
              </div>
              <div>
                <h3>{{ item.name }}</h3>
                <code>{{ item.identifier }}</code>
                <p>{{ item.summary || '暂无简介。' }}</p>
              </div>
              <div class="card-bottom">
                <span>{{
                  item.updatedAt ? new Date(item.updatedAt).toLocaleDateString() : ''
                }}</span
                ><button
                  class="button ghost content-status-button"
                  @click="setContentStatus(item.contentId, item.status !== 'active')"
                >
                  {{ item.status === 'active' ? '下架内容' : '启用内容' }}
                </button>
              </div>
            </article>
          </div>
          <div v-if="!managedContent.data.value?.items.length" class="state compact-state">
            没有匹配的内容
          </div>
          <div v-if="(managedContent.data.value?.total ?? 0) > 6" class="pager">
            <button :disabled="contentPage <= 1" @click="contentPage--">上一页</button
            ><span
              >{{ contentPage }} /
              {{ Math.ceil((managedContent.data.value?.total ?? 0) / 6) }}</span
            ><button
              :disabled="contentPage * 6 >= (managedContent.data.value?.total ?? 0)"
              @click="contentPage++"
            >
              下一页
            </button>
          </div>
        </div>
        <div v-else class="review-list">
          <h2>
            {{ tab === 'publishers' ? '发布者 Key 管理' : '管理员 Key 管理' }}
            <small
              >{{
                (tab === 'publishers'
                  ? publisherKeys.data.value?.total
                  : administratorKeys.data.value?.total) ?? 0
              }}
              项结果</small
            >
          </h2>
          <form class="management-filters" @submit.prevent="submitManageSearch">
            <Search :size="17" /><input
              v-model="manageSearchInput"
              placeholder="搜索名称或联系方式"
            /><button class="button primary">搜索</button>
          </form>
          <div
            v-if="
              (tab === 'publishers'
                ? publisherKeys.data.value?.items
                : administratorKeys.data.value?.items
              )?.length
            "
            class="admin-content-grid"
          >
            <article
              v-for="item in tab === 'publishers'
                ? publisherKeys.data.value?.items
                : administratorKeys.data.value?.items"
              :key="item.publisherId || item.administratorId"
              class="content-card admin-content-card"
            >
              <div class="card-top">
                <span
                  class="status"
                  :class="item.isSuperAdministrator || item.hasActiveKey ? 'active' : 'disabled'"
                  >{{
                    item.isSuperAdministrator
                      ? '超级管理员'
                      : item.hasActiveKey
                        ? 'Key 有效'
                        : 'Key 已撤销'
                  }}</span
                >
              </div>
              <div>
                <h3>{{ item.displayName || item.name }}</h3>
                <code>{{ item.contact }}</code>
                <p>
                  {{
                    item.isSuperAdministrator
                      ? '超级管理员 Key 受保护，不能撤销。'
                      : item.hasActiveKey
                        ? '撤销后该身份的 Key 将立即失效。'
                        : '该 Key 已撤销，可以恢复使用。'
                  }}
                </p>
              </div>
              <div class="card-bottom">
                <span>{{ item.hasActiveKey ? '当前可用' : '已失效' }}</span
                ><button
                  v-if="tab === 'publishers' && item.hasActiveKey"
                  class="button ghost content-status-button"
                  @click="revokeKey(item.publisherId!)"
                >
                  撤销 Key</button
                ><button
                  v-else-if="tab === 'publishers'"
                  class="button ghost content-status-button"
                  @click="restoreKey('publishers', item.publisherId!)"
                >
                  恢复 Key</button
                ><button
                  v-else-if="!item.isSuperAdministrator && item.hasActiveKey"
                  class="button ghost content-status-button"
                  @click="revokeAdministratorKey(item.administratorId!)"
                >
                  撤销 Key</button
                ><button
                  v-else-if="!item.isSuperAdministrator"
                  class="button ghost content-status-button"
                  @click="restoreKey('administrators', item.administratorId!)"
                >
                  恢复 Key</button
                ><span v-else></span>
              </div>
            </article>
          </div>
          <div
            v-if="
              !(
                tab === 'publishers'
                  ? publisherKeys.data.value?.items
                  : administratorKeys.data.value?.items
              )?.length
            "
            class="state compact-state"
          >
            没有匹配的 Key
          </div>
          <div
            v-if="tab === 'publishers' && (publisherKeys.data.value?.total ?? 0) > 6"
            class="pager"
          >
            <button :disabled="publisherPage <= 1" @click="publisherPage--">上一页</button
            ><span
              >{{ publisherPage }} /
              {{ Math.ceil((publisherKeys.data.value?.total ?? 0) / 6) }}</span
            ><button
              :disabled="publisherPage * 6 >= (publisherKeys.data.value?.total ?? 0)"
              @click="publisherPage++"
            >
              下一页
            </button>
          </div>
          <div
            v-if="tab === 'administrators' && (administratorKeys.data.value?.total ?? 0) > 6"
            class="pager"
          >
            <button :disabled="administratorPage <= 1" @click="administratorPage--">上一页</button
            ><span
              >{{ administratorPage }} /
              {{ Math.ceil((administratorKeys.data.value?.total ?? 0) / 6) }}</span
            ><button
              :disabled="administratorPage * 6 >= (administratorKeys.data.value?.total ?? 0)"
              @click="administratorPage++"
            >
              下一页
            </button>
          </div>
        </div>
      </div>
    </template>
  </section>
</template>
