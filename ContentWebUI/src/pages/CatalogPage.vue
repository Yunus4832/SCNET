<script setup lang="ts">
import { keepPreviousData, useQuery } from '@tanstack/vue-query';
import {
  ArrowDownToLine,
  Check,
  ExternalLink,
  Link2,
  PackageSearch,
  RadioTower,
  Search,
} from 'lucide-vue-next';
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { api, queryString, type ContentVersion, type PagedData } from '../api';
import { copyText } from '../clipboard';
import { getRuntimeConfig } from '../config';
import { contentTypeLabel } from '../contentTypes';

const search = ref('');
const appliedSearch = ref('');
const catalogView = ref<'content' | 'serverSources'>('content');
const type = ref('');
const page = ref(1);
const selectedContent = ref<ContentVersion>();
const historyPage = ref(1);
const copiedVersionId = ref('');
const copyError = ref('');
interface ServerSource {
  id: string;
  name: string;
  apiUrl: string;
  description?: string;
}
const serverSources = useQuery({
  queryKey: ['server-sources'],
  enabled: computed(() => catalogView.value === 'serverSources'),
  queryFn: () => api<ServerSource[]>('/api/v1/server-sources'),
});
const filteredServerSources = computed(() => {
  const query = appliedSearch.value.toLocaleLowerCase();
  if (!query) return serverSources.data.value ?? [];
  return (serverSources.data.value ?? []).filter((item) =>
    [item.name, item.apiUrl, item.description]
      .filter(Boolean)
      .some((value) => value!.toLocaleLowerCase().includes(query)),
  );
});
const types = [
  ['', '全部'],
  ['Mod', '模组'],
  ['World', '世界'],
  ['BlocksTexture', '材质'],
  ['CharacterSkin', '皮肤'],
  ['FurniturePack', '家具包'],
];
const queryKey = computed(() => ['content', appliedSearch.value, type.value, page.value]);
const content = useQuery({
  queryKey,
  enabled: computed(() => catalogView.value === 'content'),
  queryFn: () =>
    api<PagedData<ContentVersion>>(
      `/api/v1/content?${queryString({ query: appliedSearch.value, type: type.value, pageIndex: page.value, pageSize: 6 })}`,
    ),
  placeholderData: keepPreviousData,
});
const history = useQuery({
  queryKey: computed(() => [
    'content-versions',
    selectedContent.value?.contentId,
    historyPage.value,
  ]),
  enabled: computed(() => Boolean(selectedContent.value)),
  queryFn: () =>
    api<PagedData<ContentVersion>>(
      `/api/v1/content/${selectedContent.value!.contentId}/versions?${queryString({ pageIndex: historyPage.value, pageSize: 10 })}`,
    ),
});
const pages = computed(() => Math.max(1, Math.ceil((content.data.value?.total ?? 0) / 6)));
const historyPages = computed(() => Math.max(1, Math.ceil((history.data.value?.total ?? 0) / 10)));
function submitSearch() {
  appliedSearch.value = search.value.trim();
  page.value = 1;
}
function selectType(value: string) {
  type.value = value;
  page.value = 1;
}
function downloadUrl(item: ContentVersion) {
  return `${getRuntimeConfig().apiBaseUrl}${item.downloadUrl}`;
}
function size(value: number) {
  return value < 1048576 ? `${Math.ceil(value / 1024)} KB` : `${(value / 1048576).toFixed(1)} MB`;
}
function showVersions(item: ContentVersion) {
  selectedContent.value = selectedContent.value?.contentId === item.contentId ? undefined : item;
  historyPage.value = 1;
}
function closeVersions() {
  selectedContent.value = undefined;
}
function closeOnEscape(event: KeyboardEvent) {
  if (event.key === 'Escape') closeVersions();
}
async function copyDownloadLink(item: ContentVersion) {
  copyError.value = '';
  try {
    await copyText(new URL(downloadUrl(item), window.location.origin).toString());
    copiedVersionId.value = item.versionId;
    window.setTimeout(() => {
      if (copiedVersionId.value === item.versionId) copiedVersionId.value = '';
    }, 1800);
  } catch {
    copyError.value = '无法复制下载链接，请检查浏览器权限';
  }
}
onMounted(() => window.addEventListener('keydown', closeOnEscape));
onUnmounted(() => window.removeEventListener('keydown', closeOnEscape));
</script>

<template>
  <section class="hero shell">
    <h1>社区内容</h1>
    <p>集中浏览经审核的游戏内容与服务器源；所有公开内容均可匿名访问。</p>
    <div class="workspace-tabs catalog-tabs">
      <button :class="{ active: catalogView === 'content' }" @click="catalogView = 'content'">
        内容广场
      </button>
      <button
        :class="{ active: catalogView === 'serverSources' }"
        @click="catalogView = 'serverSources'"
      >
        服务器源
      </button>
    </div>
    <form class="search-box" @submit.prevent="submitSearch">
      <Search :size="20" /><input
        v-model="search"
        :placeholder="
          catalogView === 'content' ? '搜索名称、标识符或简介' : '搜索服务器源名称、地址或说明'
        "
      />
      <button class="button primary">搜索</button>
    </form>
  </section>

  <section class="shell catalog-section">
    <template v-if="catalogView === 'content'">
      <div class="catalog-filter-row">
        <div class="filters">
          <button
            v-for="item in types"
            :key="item[0]"
            :class="{ active: type === item[0] }"
            @click="selectType(item[0])"
          >
            {{ item[1] }}
          </button>
        </div>
        <span class="count">{{ content.data.value?.total ?? 0 }} 项内容</span>
      </div>
      <div v-if="content.isPending.value" class="state"><span class="spinner" />正在取得内容…</div>
      <div v-else-if="content.isError.value" class="state error">
        {{ content.error.value?.message }}
      </div>
      <div v-else-if="!content.data.value?.items.length" class="state">
        <PackageSearch :size="32" />没有找到匹配的内容
      </div>
      <div v-else class="content-grid">
        <article
          v-for="item in content.data.value.items"
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
            <p>{{ item.summary || '创作者暂未提供简介。' }}</p>
          </div>
          <div class="card-bottom">
            <span>{{ size(item.packageSize) }}</span>
            <div class="card-links">
              <button type="button" @click="showVersions(item)">历史版本</button
              ><button type="button" @click="copyDownloadLink(item)">
                <Check v-if="copiedVersionId === item.versionId" :size="15" /><Link2
                  v-else
                  :size="15"
                />{{ copiedVersionId === item.versionId ? '已复制' : '复制链接' }}</button
              ><a :href="downloadUrl(item)"><ArrowDownToLine :size="17" />下载</a>
            </div>
          </div>
        </article>
      </div>
      <Teleport to="body"
        ><div v-if="selectedContent" class="history-overlay" @click.self="closeVersions">
          <section class="history-panel" role="dialog" aria-modal="true" aria-label="历史版本">
            <div class="history-head">
              <div>
                <h3>{{ selectedContent.name }}</h3>
                <code>{{ selectedContent.identifier }}</code>
              </div>
              <button class="button ghost" @click="closeVersions">关闭</button>
            </div>
            <div v-if="history.isPending.value" class="state">
              <span class="spinner" />正在取得版本…
            </div>
            <div v-else-if="history.isError.value" class="state error">
              {{ history.error.value?.message }}
            </div>
            <div v-else class="version-list">
              <article v-for="item in history.data.value?.items" :key="item.versionId">
                <div>
                  <strong>v{{ item.version }}</strong
                  ><small
                    >{{ new Date(item.publishedAt || item.createdAt).toLocaleDateString() }} ·
                    {{ size(item.packageSize) }}</small
                  >
                </div>
                <div class="version-actions">
                  <button class="button ghost" @click="copyDownloadLink(item)">
                    <Check v-if="copiedVersionId === item.versionId" :size="15" /><Link2
                      v-else
                      :size="15"
                    />{{ copiedVersionId === item.versionId ? '已复制' : '复制链接' }}</button
                  ><a class="button ghost" :href="downloadUrl(item)"
                    ><ArrowDownToLine :size="16" />下载</a
                  >
                </div>
              </article>
              <div v-if="!history.data.value?.items.length" class="state">没有可下载版本</div>
            </div>
            <p v-if="copyError" class="form-error">{{ copyError }}</p>
            <div v-if="historyPages > 1" class="pager">
              <button :disabled="historyPage === 1" @click="historyPage--">上一页</button
              ><span>{{ historyPage }} / {{ historyPages }}</span
              ><button :disabled="historyPage === historyPages" @click="historyPage++">
                下一页
              </button>
            </div>
          </section>
        </div></Teleport
      >
      <p v-if="copyError && !selectedContent" class="form-error catalog-copy-error">
        {{ copyError }}
      </p>
      <div v-if="pages > 1" class="pager">
        <button :disabled="page === 1" @click="page--">上一页</button
        ><span>{{ page }} / {{ pages }}</span
        ><button :disabled="page === pages" @click="page++">下一页</button>
      </div>
    </template>
    <template v-else>
      <div class="catalog-filter-row server-source-count">
        <span class="count">{{ filteredServerSources.length }} 个来源</span>
      </div>
      <div v-if="serverSources.isPending.value" class="state">
        <span class="spinner" />正在取得服务器源…
      </div>
      <div v-else-if="serverSources.isError.value" class="state error">
        {{ serverSources.error.value?.message }}
      </div>
      <div v-else-if="!filteredServerSources.length" class="state">
        <RadioTower :size="32" />没有找到匹配的服务器源
      </div>
      <div v-else class="content-grid">
        <article v-for="item in filteredServerSources" :key="item.id" class="content-card">
          <div class="card-top"><span class="type-pill">服务器源</span></div>
          <div>
            <h3>{{ item.name }}</h3>
            <code>{{ item.apiUrl }}</code>
            <p>{{ item.description || '发布者暂未提供说明。' }}</p>
          </div>
          <div class="card-bottom">
            <span>已审核</span
            ><a :href="item.apiUrl" target="_blank" rel="noreferrer"
              ><ExternalLink :size="16" />查看接口</a
            >
          </div>
        </article>
      </div>
    </template>
  </section>
</template>
