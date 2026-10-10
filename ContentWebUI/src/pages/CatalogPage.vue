<script setup lang="ts">
import { keepPreviousData, useQuery } from '@tanstack/vue-query';
import {
  ArrowDownToLine,
  Check,
  ExternalLink,
  Link2,
  Megaphone,
  PackageSearch,
  RadioTower,
  Rocket,
  Search,
} from 'lucide-vue-next';
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { api, queryString, type ContentVersion, type PagedData } from '../api';
import { copyText } from '../clipboard';
import { getRuntimeConfig } from '../config';
import { contentTypeLabel } from '../contentTypes';

const search = ref('');
const appliedSearch = ref('');
const catalogView = ref<'content' | 'serverSources' | 'gameInformation'>('content');
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
interface Announcement {
  id: string;
  title: string;
  body: string;
  publishedAt?: string;
}
interface GameRelease {
  id: string;
  version: string;
  description: string;
  artifacts: GameReleaseArtifact[];
  publishedAt?: string;
}
interface GameReleaseArtifact {
  platform: string;
  downloadUrl: string;
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
const announcements = useQuery({
  queryKey: ['game-announcements'],
  enabled: computed(() => catalogView.value === 'gameInformation'),
  queryFn: () => api<Announcement[]>('/api/v1/announcements'),
});
const releases = useQuery({
  queryKey: ['game-releases'],
  enabled: computed(() => catalogView.value === 'gameInformation'),
  queryFn: () => api<GameRelease[]>('/api/v1/game-releases'),
});
const filteredAnnouncements = computed(() => {
  const query = appliedSearch.value.toLocaleLowerCase();
  if (!query) return announcements.data.value ?? [];
  return (announcements.data.value ?? []).filter((item) =>
    [item.title, item.body].some((value) => value.toLocaleLowerCase().includes(query)),
  );
});
const filteredReleases = computed(() => {
  const query = appliedSearch.value.toLocaleLowerCase();
  if (!query) return releases.data.value ?? [];
  return (releases.data.value ?? []).filter((item) =>
    [item.version, item.description, ...item.artifacts.map((artifact) => artifact.platform)]
      .some((value) => value.toLocaleLowerCase().includes(query)),
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
      <button
        :class="{ active: catalogView === 'gameInformation' }"
        @click="catalogView = 'gameInformation'"
      >
        公告和游戏版本
      </button>
    </div>
    <form class="search-box" @submit.prevent="submitSearch">
      <Search :size="20" /><input
        v-model="search"
        :placeholder="
          catalogView === 'content'
            ? '搜索名称、标识符或简介'
            : catalogView === 'serverSources'
              ? '搜索服务器源名称、地址或说明'
              : '搜索公告、版本号、平台或说明'
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
            <p>{{ item.summary }}</p>
            <details class="card-details">
              <summary>详细描述</summary>
              <p class="package-description">{{ item.description }}</p>
            </details>
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
                <p class="version-summary" :title="item.summary">{{ item.summary }}</p>
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
    <template v-else-if="catalogView === 'serverSources'">
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
    <template v-else>
      <div v-if="announcements.isPending.value || releases.isPending.value" class="state">
        <span class="spinner" />正在取得公告与游戏版本…
      </div>
      <div v-else-if="announcements.isError.value || releases.isError.value" class="state error">
        {{ announcements.error.value?.message || releases.error.value?.message }}
      </div>
      <div v-else class="game-information-sections">
        <section class="public-information-section">
          <div class="section-title-row">
            <div><Megaphone :size="19" /><h2>游戏公告</h2></div>
            <span class="count">{{ filteredAnnouncements.length }} 条公告</span>
          </div>
          <div v-if="filteredAnnouncements.length" class="information-list">
            <article v-for="item in filteredAnnouncements" :key="item.id" class="information-card">
              <div class="information-card-head">
                <h3>{{ item.title }}</h3>
                <time v-if="item.publishedAt">{{ new Date(item.publishedAt).toLocaleDateString() }}</time>
              </div>
              <p>{{ item.body }}</p>
            </article>
          </div>
          <div v-else class="state compact-state">没有找到匹配的公告</div>
        </section>
        <section class="public-information-section">
          <div class="section-title-row">
            <div><Rocket :size="19" /><h2>游戏版本</h2></div>
            <span class="count">{{ filteredReleases.length }} 个版本</span>
          </div>
          <div v-if="filteredReleases.length" class="information-list release-information-list">
            <article v-for="item in filteredReleases" :key="item.id" class="information-card release-card">
              <div class="information-card-head">
                <div><h3>v{{ item.version }}</h3><span class="platform-label">{{ item.artifacts.map((artifact) => artifact.platform).join(' · ') }}</span></div>
                <time v-if="item.publishedAt">{{ new Date(item.publishedAt).toLocaleDateString() }}</time>
              </div>
              <p>{{ item.description || '未提供版本说明。' }}</p>
              <div class="artifact-downloads">
                <a v-for="artifact in item.artifacts" :key="artifact.platform" :href="artifact.downloadUrl" target="_blank" rel="noreferrer">
                  <span>{{ artifact.platform }}</span><ArrowDownToLine :size="16" />下载
                </a>
              </div>
            </article>
          </div>
          <div v-else class="state compact-state">没有找到匹配的游戏版本</div>
        </section>
      </div>
    </template>
  </section>
</template>

<style scoped>
.game-information-sections { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 18px; align-items: start; }
.public-information-section { min-width: 0; padding: 18px; border: 1px solid var(--line); border-radius: 12px; background: color-mix(in srgb, var(--surface) 86%, transparent); }
.section-title-row, .section-title-row > div, .information-card-head {
  display: flex;
  align-items: center;
}
.section-title-row { justify-content: space-between; gap: 16px; margin-bottom: 10px; }
.section-title-row > div { gap: 8px; }
.section-title-row h2 { margin: 0; font-size: 18px; }
.information-list { display: grid; gap: 10px; }
.information-card { min-width: 0; padding: 17px 18px; border: 1px solid var(--line); border-radius: 10px; background: var(--surface); }
.information-card-head { align-items: flex-start; justify-content: space-between; gap: 14px; }
.information-card h3 { margin: 0; font-size: 17px; }
.information-card time, .platform-label { color: var(--muted); font-size: 12px; }
.information-card p { margin: 12px 0 0; color: #bdc6c0; line-height: 1.6; white-space: pre-wrap; }
.platform-label { display: block; margin-top: 4px; }
.artifact-downloads { display: grid; gap: 7px; margin-top: 16px; padding-top: 12px; border-top: 1px solid var(--line); }
.artifact-downloads a { display: flex; align-items: center; justify-content: space-between; gap: 8px; color: var(--text); font-size: 12px; font-weight: 600; }
.artifact-downloads a span { min-width: 0; margin-right: auto; color: var(--muted); }
.artifact-downloads a:hover { color: var(--accent); }
@media (max-width: 720px) {
  .game-information-sections { grid-template-columns: 1fr; }
}
</style>
