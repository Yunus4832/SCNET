<script setup lang="ts">
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import {
  ArrowLeftRight,
  Check,
  Eye,
  EyeOff,
  Link2,
  LogOut,
  Send,
  UploadCloud,
  X,
} from 'lucide-vue-next';
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { api, clearAccess, getAccess, type ContentVersion, type PagedData } from '../api';
import { copyText } from '../clipboard';
import { getRuntimeConfig } from '../config';

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
const contentChoices = useQuery({
  queryKey: ['publisher-content-choices', accessCacheKey],
  queryFn: () => api<PagedData<ContentItem>>('/api/v1/publisher/content?pageIndex=1&pageSize=100'),
});
const form = reactive({
  type: 'Mod',
  identifier: '',
  name: '',
  version: '',
  summary: '',
  metadata: '',
});
const file = ref<File>();
const error = ref('');
const busy = ref(false);
const showForm = ref(false);
const submissionMode = ref<'create' | 'update'>('create');
const selectedContentId = ref('');
const view = ref<'content' | 'versions'>('content');
const copiedVersionId = ref('');
const ownContents = computed(() => contentChoices.data.value?.items ?? []);
function removeKey() {
  clearAccess('publisher', getAccess('publisher')?.apiKey);
  router.push('/publisher/access');
}
function resetForm() {
  Object.assign(form, {
    type: 'Mod',
    identifier: '',
    name: '',
    version: '',
    summary: '',
    metadata: '',
  });
  selectedContentId.value = '';
  file.value = undefined;
  error.value = '';
}
function setSubmissionMode(mode: 'create' | 'update') {
  submissionMode.value = mode;
  resetForm();
  if (mode === 'update' && ownContents.value.length === 1)
    selectContent(ownContents.value[0].contentId);
}
function selectContent(contentId: string) {
  selectedContentId.value = contentId;
  const item = ownContents.value.find((content) => content.contentId === contentId);
  if (!item) return;
  Object.assign(form, {
    type: item.type,
    identifier: item.identifier,
    name: item.name,
    summary: item.summary ?? '',
    version: '',
    metadata: '',
  });
}
async function submit() {
  if (!file.value || (submissionMode.value === 'update' && !selectedContentId.value)) return;
  error.value = '';
  busy.value = true;
  const body = new FormData();
  Object.entries(form).forEach(([key, value]) => body.set(key, value));
  body.set('package', file.value);
  try {
    await api('/api/v1/publisher/submissions', { method: 'POST', body });
    showForm.value = false;
    resetForm();
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ['publisher-submissions'] }),
      queryClient.invalidateQueries({ queryKey: ['publisher-content'] }),
    ]);
  } catch (value) {
    error.value = value instanceof Error ? value.message : '上传失败';
  } finally {
    busy.value = false;
  }
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
function closeForm() {
  if (busy.value) return;
  showForm.value = false;
  resetForm();
}
function openForm() {
  showForm.value = true;
}
function closeOnEscape(event: KeyboardEvent) {
  if (event.key === 'Escape') closeForm();
}
onMounted(() => window.addEventListener('keydown', closeOnEscape));
onUnmounted(() => window.removeEventListener('keydown', closeOnEscape));
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
        <h2>内容发布</h2>
        <button
          class="button primary"
          :disabled="self.data.value?.status !== 'active'"
          @click="openForm"
        >
          <UploadCloud :size="17" />提交内容
        </button>
      </div>
      <Teleport to="body">
        <div v-if="showForm" class="modal-overlay" @click.self="closeForm">
          <form class="modal-panel upload-form" @submit.prevent="submit">
            <div class="modal-head">
              <div>
                <span class="modal-title">提交内容</span>
                <p>提交新内容或为已有内容发布新版本。</p>
              </div>
              <button class="button ghost" type="button" :disabled="busy" @click="closeForm">
                <X :size="16" />关闭
              </button>
            </div>
            <div class="role-picker submission-picker">
              <button
                type="button"
                :class="{ active: submissionMode === 'create' }"
                @click="setSubmissionMode('create')"
              >
                <strong>创建新内容</strong><small>填写完整资料并提交首个版本</small></button
              ><button
                type="button"
                :class="{ active: submissionMode === 'update' }"
                :disabled="!ownContents.length"
                @click="setSubmissionMode('update')"
              >
                <strong>更新已有内容</strong><small>选择内容并提交新的版本文件</small>
              </button>
            </div>
            <label v-if="submissionMode === 'update'"
              >选择已有内容<select
                :value="selectedContentId"
                required
                @change="selectContent(($event.target as HTMLSelectElement).value)"
              >
                <option value="" disabled>请选择要更新的内容</option>
                <option v-for="item in ownContents" :key="item.contentId" :value="item.contentId">
                  {{ item.name }} · {{ item.identifier }}
                </option>
              </select></label
            >
            <div class="form-grid">
              <label
                >类型<select v-model="form.type" :disabled="submissionMode === 'update'">
                  <option>Mod</option>
                  <option>World</option>
                  <option>BlocksTexture</option>
                  <option>CharacterSkin</option>
                  <option>FurniturePack</option>
                </select></label
              ><label
                >标识符<input
                  v-model="form.identifier"
                  required
                  minlength="3"
                  :readonly="submissionMode === 'update'" /></label
              ><label>名称<input v-model="form.name" required /></label
              ><label
                >新版本号<input v-model="form.version" required placeholder="例如 1.1.0"
              /></label>
            </div>
            <label>简介<textarea v-model="form.summary" rows="2" /></label>
            <label class="file-field"
              >新版本资源包<input
                type="file"
                required
                @change="file = ($event.target as HTMLInputElement).files?.[0]"
            /></label>
            <p v-if="error" class="form-error">{{ error }}</p>
            <div class="modal-actions">
              <button class="button ghost" type="button" :disabled="busy" @click="closeForm">
                取消</button
              ><button
                class="button primary"
                :disabled="busy || (submissionMode === 'update' && !selectedContentId)"
              >
                <Send :size="17" />{{
                  busy
                    ? '正在上传…'
                    : submissionMode === 'create'
                      ? '创建并送交审核'
                      : '提交新版本审核'
                }}
              </button>
            </div>
          </form>
        </div>
      </Teleport>
      <div class="workspace-tabs publisher-tabs">
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
              <span class="type-pill">{{ item.type }}</span
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
              <span>{{ new Date(item.updatedAt).toLocaleDateString() }}</span
              ><button
                class="button ghost content-status-button"
                :disabled="self.data.value?.status !== 'active'"
                @click="setContentStatus(item)"
              >
                <EyeOff v-if="item.status === 'active'" :size="15" /><Eye v-else :size="15" />{{
                  item.status === 'active' ? '下架' : '恢复上架'
                }}
              </button>
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
      <template v-else
        ><div class="workspace-toolbar compact-toolbar"><h2>版本提交记录</h2></div>
        <div v-if="submissions.data.value?.items.length" class="content-grid">
          <article
            v-for="item in submissions.data.value?.items"
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
    </template>
  </section>
</template>
