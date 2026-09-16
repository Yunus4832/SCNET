<script setup lang="ts">
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import {
  ArrowLeftRight,
  Check,
  Eye,
  EyeOff,
  Link2,
  LogOut,
  UploadCloud,
  RadioTower,
} from 'lucide-vue-next';
import { ref } from 'vue';
import { useRouter } from 'vue-router';
import { api, clearAccess, getAccess, type ContentVersion, type PagedData } from '../api';
import { copyText } from '../clipboard';
import { getRuntimeConfig } from '../config';
import { contentTypeLabel } from '../contentTypes';
import ContentSubmissionDialog from '../components/ContentSubmissionDialog.vue';
import ServerSourceSubmissionDialog from '../components/ServerSourceSubmissionDialog.vue';
import ServerSubmissionDialog from '../components/ServerSubmissionDialog.vue';

interface Publisher {
  publisherId: string;
  displayName: string;
  contact: string;
  description?: string;
  status: string;
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
interface ServerSourceRegistration {
  id: string;
  name: string;
  apiUrl: string;
  description?: string;
  status: string;
  reviewMessage?: string;
  createdAt: string;
}
interface DirectoryServer {
  id: string;
  name: string;
  address: string;
  description?: string;
  tags: string[];
  reviewStatus: string;
  reviewMessage?: string;
  isEnabledByPublisher: boolean;
  isSuspended: boolean;
  suspensionReason?: string;
  createdAt: string;
}
const router = useRouter();
const queryClient = useQueryClient();
const accessCacheKey = getAccess('publisher')?.apiKey.slice(0, 18) ?? 'anonymous';
const self = useQuery({
  queryKey: ['publisher-self', accessCacheKey],
  queryFn: () => api<Publisher>('/api/v1/publisher'),
});
const contentPage = ref(1);
const versionsPage = ref(1);
const submissions = useQuery({
  queryKey: ['publisher-submissions', accessCacheKey, versionsPage],
  queryFn: () =>
    api<PagedData<ContentVersion>>(
      `/api/v1/publisher/submissions?pageIndex=${versionsPage.value}&pageSize=6`,
    ),
});
const contents = useQuery({
  queryKey: ['publisher-content', accessCacheKey, contentPage],
  queryFn: () =>
    api<PagedData<ContentItem>>(
      `/api/v1/publisher/content?pageIndex=${contentPage.value}&pageSize=6`,
    ),
});
const serverSources = useQuery({
  queryKey: ['publisher-server-sources', accessCacheKey],
  queryFn: () => api<ServerSourceRegistration[]>('/api/v1/publisher/server-sources'),
});
const servers = useQuery({
  queryKey: ['publisher-servers', accessCacheKey],
  queryFn: () => api<DirectoryServer[]>('/api/v1/publisher/servers'),
});
const error = ref('');
const showForm = ref(false);
const showServerSourceForm = ref(false);
const showServerForm = ref(false);
const serverTarget = ref<DirectoryServer>();
const submissionTarget = ref<ContentItem>();
const view = ref<'content' | 'versions' | 'servers' | 'serverSources'>('content');
const copiedVersionId = ref('');
function removeKey() {
  clearAccess('publisher', getAccess('publisher')?.apiKey);
  router.push('/publisher/access');
}
async function submissionCompleted() {
  showForm.value = false;
  submissionTarget.value = undefined;
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ['publisher-submissions'] }),
    queryClient.invalidateQueries({ queryKey: ['publisher-content'] }),
  ]);
}
async function setContentStatus(item: ContentItem) {
  error.value = '';
  try {
    await api(
      `/api/v1/publisher/content/${item.contentId}/${item.status === 'active' ? 'disable' : 'enable'}`,
      { method: 'POST' },
    );
    await queryClient.invalidateQueries({ queryKey: ['publisher-content'] });
  } catch (value) {
    error.value = value instanceof Error ? value.message : '操作失败';
  }
}
async function copyDownloadLink(item: ContentVersion) {
  error.value = '';
  const url = new URL(
    `${getRuntimeConfig().apiBaseUrl}${item.downloadUrl}`,
    window.location.origin,
  ).toString();
  try {
    await copyText(url);
    copiedVersionId.value = item.versionId;
    window.setTimeout(() => {
      if (copiedVersionId.value === item.versionId) copiedVersionId.value = '';
    }, 1800);
  } catch {
    error.value = '无法写入剪贴板，请检查浏览器权限';
  }
}
function openForm(target?: ContentItem) {
  submissionTarget.value = target;
  showForm.value = true;
}
function closeForm() {
  showForm.value = false;
  submissionTarget.value = undefined;
}
async function serverSourceSubmitted() {
  showServerSourceForm.value = false;
  await queryClient.invalidateQueries({ queryKey: ['publisher-server-sources'] });
}
async function serverSubmitted() {
  showServerForm.value = false;
  serverTarget.value = undefined;
  await queryClient.invalidateQueries({ queryKey: ['publisher-servers'] });
}
function editServer(item: DirectoryServer) {
  serverTarget.value = item;
  showServerForm.value = true;
}
function closeServerForm() {
  showServerForm.value = false;
  serverTarget.value = undefined;
}
async function setServerEnabled(item: DirectoryServer) {
  await api(
    `/api/v1/publisher/servers/${item.id}/${item.isEnabledByPublisher ? 'disable' : 'enable'}`,
    {
      method: 'POST',
    },
  );
  await queryClient.invalidateQueries({ queryKey: ['publisher-servers'] });
}
</script>

<template>
  <section class="shell page-pad workspace-page">
    <div class="workspace-head">
      <div>
        <h1 class="page-title">{{ self.data.value?.displayName || '发布者工作台' }}</h1>
        <p>
          状态
          <span class="status" :class="self.data.value?.status">{{
            self.data.value?.status || '加载中'
          }}</span>
        </p>
      </div>
      <div class="workspace-actions">
        <RouterLink class="button ghost" to="/publisher/access"
          ><ArrowLeftRight :size="17" />切换身份</RouterLink
        ><button class="button ghost" @click="removeKey"><LogOut :size="17" />退出</button>
      </div>
    </div>
    <div v-if="self.isError.value" class="state error">{{ self.error.value?.message }}</div>
    <template v-else>
      <div class="workspace-toolbar">
        <div class="workspace-tabs publisher-tabs publisher-primary-tabs">
          <button
            :class="{ active: view === 'content' || view === 'versions' }"
            @click="view = 'content'"
          >
            内容发布
          </button>
          <button :class="{ active: view === 'servers' }" @click="view = 'servers'">服务器</button>
          <button :class="{ active: view === 'serverSources' }" @click="view = 'serverSources'">
            服务器源
          </button>
        </div>
        <button
          v-if="view === 'content' || view === 'versions'"
          class="button primary"
          :disabled="self.data.value?.status !== 'active'"
          @click="openForm()"
        >
          <UploadCloud :size="17" />提交新内容
        </button>
        <button
          v-else-if="view === 'servers'"
          class="button primary"
          :disabled="self.data.value?.status !== 'active'"
          @click="showServerForm = true"
        >
          <RadioTower :size="17" />提交服务器
        </button>
        <button
          v-else
          class="button primary"
          :disabled="self.data.value?.status !== 'active'"
          @click="showServerSourceForm = true"
        >
          <RadioTower :size="17" />提交服务器源
        </button>
      </div>
      <ContentSubmissionDialog
        :open="showForm"
        :target="submissionTarget"
        @close="closeForm"
        @submitted="submissionCompleted"
      />
      <ServerSourceSubmissionDialog
        :open="showServerSourceForm"
        @close="showServerSourceForm = false"
        @submitted="serverSourceSubmitted"
      />
      <ServerSubmissionDialog
        :open="showServerForm"
        :target="serverTarget"
        @close="closeServerForm"
        @submitted="serverSubmitted"
      />
      <div v-if="view === 'content' || view === 'versions'" class="workspace-tabs publisher-tabs">
        <button :class="{ active: view === 'content' }" @click="view = 'content'">内容管理</button
        ><button :class="{ active: view === 'versions' }" @click="view = 'versions'">
          版本记录
        </button>
      </div>
      <p v-if="error && !showForm" class="form-error">{{ error }}</p>
      <template v-if="view === 'content'"
        ><div class="workspace-toolbar compact-toolbar">
          <div>
            <h2>我的内容</h2>
            <p>上下架按内容整体生效，不改变各版本的审核状态。</p>
          </div>
        </div>
        <div v-if="contents.data.value?.items.length" class="content-grid">
          <article
            v-for="item in contents.data.value?.items"
            :key="item.contentId"
            class="content-card"
          >
            <div class="card-top">
              <span class="type-pill">{{ contentTypeLabel(item.type) }}</span
              ><span class="status" :class="item.status">{{
                item.status === 'active' ? '已上架' : '已下架'
              }}</span>
            </div>
            <div>
              <h3>{{ item.name }}</h3>
              <code>{{ item.identifier }}</code>
              <p>{{ item.summary || '暂无简介。' }}</p>
            </div>
            <div class="card-bottom">
              <span>{{ new Date(item.updatedAt).toLocaleDateString() }}</span>
              <div class="card-links">
                <button
                  class="button ghost content-status-button"
                  :disabled="self.data.value?.status !== 'active'"
                  @click="openForm(item)"
                >
                  <UploadCloud :size="15" />提交新版本
                </button>
                <button
                  class="button ghost content-status-button"
                  :disabled="self.data.value?.status !== 'active'"
                  @click="setContentStatus(item)"
                >
                  <EyeOff v-if="item.status === 'active'" :size="15" /><Eye v-else :size="15" />{{
                    item.status === 'active' ? '下架' : '恢复上架'
                  }}
                </button>
              </div>
            </div>
          </article>
        </div>
        <div v-else class="state">暂无内容</div>
        <div v-if="(contents.data.value?.total ?? 0) > 6" class="pager">
          <button :disabled="contentPage <= 1" @click="contentPage--">上一页</button
          ><span>{{ contentPage }} / {{ Math.ceil((contents.data.value?.total ?? 0) / 6) }}</span
          ><button
            :disabled="contentPage * 6 >= (contents.data.value?.total ?? 0)"
            @click="contentPage++"
          >
            下一页
          </button>
        </div></template
      >
      <template v-else-if="view === 'versions'"
        ><div class="workspace-toolbar compact-toolbar"><h2>版本提交记录</h2></div>
        <div v-if="submissions.data.value?.items.length" class="content-grid">
          <article
            v-for="item in submissions.data.value?.items"
            :key="item.versionId"
            class="content-card"
          >
            <div class="card-top">
              <span class="type-pill">{{ contentTypeLabel(item.type) }}</span
              ><span class="version">v{{ item.version }}</span>
            </div>
            <div>
              <h3>{{ item.name }}</h3>
              <code>{{ item.identifier }}</code>
              <p>{{ item.reviewMessage || '提交后等待管理员审核。' }}</p>
            </div>
            <div class="card-bottom">
              <span>{{ new Date(item.createdAt).toLocaleDateString() }}</span>
              <div class="card-links">
                <span class="status" :class="item.status">{{ item.status }}</span
                ><button
                  v-if="item.status === 'published'"
                  class="button ghost content-status-button"
                  @click="copyDownloadLink(item)"
                >
                  <Check v-if="copiedVersionId === item.versionId" :size="15" /><Link2
                    v-else
                    :size="15"
                  />{{ copiedVersionId === item.versionId ? '已复制' : '复制链接' }}
                </button>
              </div>
            </div>
          </article>
        </div>
        <div v-else class="state">暂无提交记录</div>
        <div v-if="(submissions.data.value?.total ?? 0) > 6" class="pager">
          <button :disabled="versionsPage <= 1" @click="versionsPage--">上一页</button
          ><span
            >{{ versionsPage }} / {{ Math.ceil((submissions.data.value?.total ?? 0) / 6) }}</span
          ><button
            :disabled="versionsPage * 6 >= (submissions.data.value?.total ?? 0)"
            @click="versionsPage++"
          >
            下一页
          </button>
        </div></template
      >
      <template v-else-if="view === 'servers'">
        <div v-if="servers.data.value?.length" class="content-grid">
          <article v-for="item in servers.data.value" :key="item.id" class="content-card">
            <div class="card-top">
              <span class="type-pill">服务器</span>
              <span class="status" :class="item.reviewStatus">{{ item.reviewStatus }}</span>
            </div>
            <div>
              <h3>{{ item.name }}</h3>
              <code>{{ item.address }}</code>
              <p>
                {{
                  item.reviewMessage ||
                  item.suspensionReason ||
                  item.description ||
                  '提交后等待管理员审核。'
                }}
              </p>
            </div>
            <div class="card-bottom">
              <span>{{
                item.isSuspended ? '管理员已停用' : item.isEnabledByPublisher ? '已启用' : '已下架'
              }}</span>
              <div class="card-actions">
                <button class="button ghost content-status-button" @click="editServer(item)">
                  编辑
                </button>
                <button
                  class="button ghost content-status-button"
                  :disabled="item.reviewStatus !== 'approved' || item.isSuspended"
                  @click="setServerEnabled(item)"
                >
                  {{ item.isEnabledByPublisher ? '下架' : '启用' }}
                </button>
              </div>
            </div>
          </article>
        </div>
        <div v-else class="state">暂无服务器提交记录</div>
      </template>
      <template v-else>
        <div v-if="serverSources.data.value?.length" class="content-grid">
          <article v-for="item in serverSources.data.value" :key="item.id" class="content-card">
            <div class="card-top">
              <span class="type-pill">服务器源</span
              ><span class="status" :class="item.status">{{ item.status }}</span>
            </div>
            <div>
              <h3>{{ item.name }}</h3>
              <code>{{ item.apiUrl }}</code>
              <p>{{ item.reviewMessage || item.description || '提交后等待管理员审核。' }}</p>
            </div>
            <div class="card-bottom">
              <span>{{ new Date(item.createdAt).toLocaleDateString() }}</span>
            </div>
          </article>
        </div>
        <div v-else class="state">暂无服务器源提交记录</div>
      </template>
    </template>
  </section>
</template>
