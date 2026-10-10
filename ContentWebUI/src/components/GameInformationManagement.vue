<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { api } from '../api';

interface Announcement {
  id: string;
  title: string;
  body: string;
  status: string;
  publishedAt?: string;
}
interface GameRelease {
  id: string;
  version: string;
  description: string;
  artifacts: GameReleaseArtifact[];
  status: string;
}
interface GameReleaseArtifact {
  platform: string;
  downloadUrl: string;
  sha256: string;
}
const announcements = ref<Announcement[]>([]);
const releases = ref<GameRelease[]>([]);
const announcement = ref({ id: '', title: '', body: '' });
const release = ref({
  id: '', version: '', description: '', artifacts: [newArtifact()],
});
const error = ref('');
const busy = ref(false);
const announcementEditorVisible = ref(false);
const releaseEditorVisible = ref(false);
const announcementPublishRequested = ref(false);
const releasePublishRequested = ref(false);
const announcementView = ref<'published' | 'draft' | 'withdrawn'>('published');
const releaseView = ref<'published' | 'draft' | 'withdrawn'>('published');
const statusLabels: Record<string, string> = {
  draft: '草稿', published: '已发布', withdrawn: '已撤回',
};
const platforms = [
  ['android-arm64', 'Android Arm64'],
  ['android-arm32', 'Android Arm32'],
  ['windows-x64', 'Windows x64'],
  ['linux-x64', 'Linux x64'],
] as const;
const errorMessages: Record<string, string> = {
  invalid_game_release: '版本信息无效，请检查版本号、平台和 SHA-256。',
  game_release_exists: '该版本已经存在。',
};

function describeError(value: unknown, fallback: string) {
  if (!(value instanceof Error)) return fallback;
  return errorMessages[value.message] ?? value.message;
}
const visibleAnnouncements = computed(() =>
  announcements.value.filter((item) => item.status === announcementView.value));
const visibleReleases = computed(() =>
  releases.value.filter((item) => item.status === releaseView.value));

function resetAnnouncement() {
  announcement.value = { id: '', title: '', body: '' };
  announcementEditorVisible.value = false;
  announcementPublishRequested.value = false;
  error.value = '';
}
function resetRelease() {
  release.value = {
    id: '', version: '', description: '', artifacts: [newArtifact()],
  };
  releaseEditorVisible.value = false;
  releasePublishRequested.value = false;
  error.value = '';
}
function openAnnouncementEditor() {
  resetAnnouncement();
  error.value = '';
  announcementEditorVisible.value = true;
}
function openReleaseEditor() {
  resetRelease();
  error.value = '';
  releaseEditorVisible.value = true;
}
function editAnnouncement(item: Announcement) {
  error.value = '';
  announcement.value = { id: item.id, title: item.title, body: item.body };
  announcementEditorVisible.value = true;
}
function editRelease(item: GameRelease) {
  error.value = '';
  release.value = {
    id: item.id,
    version: item.version,
    description: item.description,
    artifacts: item.artifacts.map((artifact) => ({ ...artifact })),
  };
  releaseEditorVisible.value = true;
}
function newArtifact(): GameReleaseArtifact {
  return { platform: 'android-arm64', downloadUrl: '', sha256: '' };
}
function addArtifact() {
  const platform = platforms.find(([value]) =>
    !release.value.artifacts.some((artifact) => artifact.platform === value))?.[0];
  if (platform) release.value.artifacts.push({ ...newArtifact(), platform });
}
function removeArtifact(index: number) {
  if (release.value.artifacts.length > 1) release.value.artifacts.splice(index, 1);
}
function platformUsed(platform: string, currentIndex: number) {
  return release.value.artifacts.some((artifact, index) =>
    index !== currentIndex && artifact.platform === platform);
}

async function refresh() {
  const [a, r] = await Promise.all([
    api<Announcement[]>('/api/v1/admin/announcements'),
    api<GameRelease[]>('/api/v1/admin/game-releases'),
  ]);
  announcements.value = a;
  releases.value = r;
}
async function execute(action: () => Promise<unknown>) {
  busy.value = true;
  error.value = '';
  try {
    await action();
    await refresh();
  } catch (value) {
    error.value = describeError(value, '操作失败');
  } finally {
    busy.value = false;
  }
}
async function load() {
  busy.value = true;
  error.value = '';
  try {
    await refresh();
  } catch (value) {
    error.value = describeError(value, '加载失败');
  } finally {
    busy.value = false;
  }
}
function saveAnnouncement(publish = false) {
  void execute(async () => {
    let id = announcement.value.id;
    const body = JSON.stringify({ title: announcement.value.title, body: announcement.value.body });
    if (id) await api(`/api/v1/admin/announcements/${id}`, { method: 'PUT', body });
    else id = (await api<{ id: string }>('/api/v1/admin/announcements', { method: 'POST', body })).id;
    if (publish) {
      await api(`/api/v1/admin/announcements/${id}/publish`, { method: 'POST' });
      announcementView.value = 'published';
    } else announcementView.value = 'draft';
    resetAnnouncement();
  });
}
function saveRelease(publish = false) {
  void execute(async () => {
    let id = release.value.id;
    const body = JSON.stringify(release.value);
    if (id) await api(`/api/v1/admin/game-releases/${id}`, { method: 'PUT', body });
    else id = (await api<{ id: string }>('/api/v1/admin/game-releases', { method: 'POST', body })).id;
    if (publish) {
      await api(`/api/v1/admin/game-releases/${id}/publish`, { method: 'POST' });
      releaseView.value = 'published';
    } else releaseView.value = 'draft';
    resetRelease();
  });
}
function setStatus(kind: 'announcements' | 'game-releases', id: string, action: 'publish' | 'withdraw') {
  void execute(async () => {
    await api(`/api/v1/admin/${kind}/${id}/${action}`, { method: 'POST' });
    if (kind === 'announcements') announcementView.value = action === 'publish' ? 'published' : 'withdrawn';
    else releaseView.value = action === 'publish' ? 'published' : 'withdrawn';
  });
}
onMounted(() => { void load(); });
</script>

<template>
  <section class="game-info-workspace">
    <div class="game-info-intro">
      <div>
        <span class="eyebrow">客户端信息</span>
        <h2>公告与游戏版本</h2>
        <p>发布客户端公告，并维护各平台可用的游戏版本与下载信息。</p>
      </div>
      <div class="game-info-summary">
        <span><b>{{ announcements.length }}</b> 条公告</span>
        <span><b>{{ releases.length }}</b> 个版本</span>
      </div>
    </div>
    <p v-if="error" class="form-error">{{ error }}</p>
    <div class="game-info-columns">
      <section class="game-info-panel">
        <div class="panel-heading">
          <div><h3>游戏公告</h3><p>展示在客户端主菜单中的滚动公告。</p></div>
          <button class="button primary" @click="openAnnouncementEditor">
            发布新公告
          </button>
        </div>
        <div class="record-tabs">
          <button :class="{ active: announcementView === 'published' }" @click="announcementView = 'published'">已发布</button>
          <button :class="{ active: announcementView === 'draft' }" @click="announcementView = 'draft'">草稿</button>
          <button :class="{ active: announcementView === 'withdrawn' }" @click="announcementView = 'withdrawn'">已撤回</button>
        </div>
        <div v-if="visibleAnnouncements.length" class="game-info-list">
          <article v-for="item in visibleAnnouncements" :key="item.id" class="game-info-card">
            <div class="card-top"><h4>{{ item.title }}</h4><span class="status" :class="item.status">{{ statusLabels[item.status] ?? item.status }}</span></div>
            <p>{{ item.body }}</p>
            <div class="card-actions">
              <button v-if="item.status === 'draft'" class="button ghost" @click="editAnnouncement(item)">编辑</button>
              <button v-if="item.status === 'draft'" class="button primary" :disabled="busy"
                @click="setStatus('announcements', item.id, 'publish')">发布</button>
              <button v-if="item.status === 'published'" class="button ghost" :disabled="busy"
                @click="setStatus('announcements', item.id, 'withdraw')">撤回</button>
            </div>
          </article>
        </div>
        <div v-else class="state compact-state">此视图暂无公告</div>
      </section>
      <section class="game-info-panel">
        <div class="panel-heading">
          <div><h3>游戏版本</h3><p>按平台发布版本说明和外部安装包信息。</p></div>
          <button class="button primary" @click="openReleaseEditor">发布新版本</button>
        </div>
        <div class="record-tabs">
          <button :class="{ active: releaseView === 'published' }" @click="releaseView = 'published'">已发布</button>
          <button :class="{ active: releaseView === 'draft' }" @click="releaseView = 'draft'">草稿</button>
          <button :class="{ active: releaseView === 'withdrawn' }" @click="releaseView = 'withdrawn'">已撤回</button>
        </div>
        <div v-if="visibleReleases.length" class="game-info-list">
          <article v-for="item in visibleReleases" :key="item.id" class="game-info-card">
            <div class="card-top"><div><h4>v{{ item.version }}</h4><span class="platform-label">{{ item.artifacts.map((artifact) => artifact.platform).join(' · ') }}</span></div><span class="status" :class="item.status">{{ statusLabels[item.status] ?? item.status }}</span></div>
            <p>{{ item.description || '未填写更新说明' }}</p>
            <div class="card-actions">
              <button v-if="item.status === 'draft'" class="button ghost" @click="editRelease(item)">编辑</button>
              <button v-if="item.status === 'draft'" class="button primary" :disabled="busy"
                @click="setStatus('game-releases', item.id, 'publish')">发布</button>
              <button v-if="item.status === 'published'" class="button ghost" :disabled="busy"
                @click="setStatus('game-releases', item.id, 'withdraw')">撤回</button>
            </div>
          </article>
        </div>
        <div v-else class="state compact-state">此视图暂无游戏版本</div>
      </section>
    </div>
    <div v-if="announcementEditorVisible" class="modal-overlay" @click.self="resetAnnouncement">
      <section class="modal-panel game-info-dialog" role="dialog" aria-modal="true"
        :aria-label="announcement.id ? '编辑公告草稿' : '发布新公告'">
        <div class="modal-head">
          <div>
            <h2 class="modal-title">{{ announcement.id ? '编辑公告草稿' : '发布新公告' }}</h2>
            <p>公告会先保存为草稿，确认内容后再从草稿列表发布。</p>
          </div>
          <button type="button" class="button ghost" @click="resetAnnouncement">关闭</button>
        </div>
        <p v-if="error" class="form-error">{{ error }}</p>
        <form class="game-info-form" @submit.prevent="saveAnnouncement(announcementPublishRequested)">
          <label>公告标题<input v-model="announcement.title" maxlength="120" placeholder="简短明确的标题" required /></label>
          <label>公告内容<textarea v-model="announcement.body" maxlength="10000" placeholder="输入面向玩家的公告正文" required /></label>
          <div class="modal-actions">
            <button type="button" class="button ghost" @click="resetAnnouncement">取消</button>
            <button class="button ghost" :disabled="busy" @click="announcementPublishRequested = false">保存草稿</button>
            <button class="button primary" :disabled="busy" @click="announcementPublishRequested = true">直接发布</button>
          </div>
        </form>
      </section>
    </div>
    <div v-if="releaseEditorVisible" class="modal-overlay" @click.self="resetRelease">
      <section class="modal-panel game-info-dialog" role="dialog" aria-modal="true"
        :aria-label="release.id ? '编辑版本草稿' : '发布新版本'">
        <div class="modal-head">
          <div>
            <h2 class="modal-title">{{ release.id ? '编辑版本草稿' : '发布新版本' }}</h2>
            <p>为同一版本配置各平台的安装包链接与 SHA-256。</p>
          </div>
          <button type="button" class="button ghost" @click="resetRelease">关闭</button>
        </div>
        <p v-if="error" class="form-error">{{ error }}</p>
        <form class="game-info-form" @submit.prevent="saveRelease(releasePublishRequested)">
          <label>版本号<input v-model="release.version" placeholder="例如 1.0.0" required /></label>
          <label>更新说明<textarea v-model="release.description" placeholder="概述本次版本的主要变化" /></label>
          <div class="artifact-heading"><strong>平台安装包</strong><button type="button" class="button ghost" :disabled="release.artifacts.length >= platforms.length" @click="addArtifact">添加平台</button></div>
          <div v-for="(artifact, index) in release.artifacts" :key="index" class="artifact-editor">
            <div class="field-row">
              <label>目标平台<select v-model="artifact.platform">
              <option v-for="platform in platforms" :key="platform[0]" :value="platform[0]" :disabled="platformUsed(platform[0], index)">{{ platform[1] }}</option>
              </select></label>
              <button type="button" class="button ghost remove-artifact" :disabled="release.artifacts.length === 1" @click="removeArtifact(index)">移除</button>
            </div>
            <label>下载地址<input v-model="artifact.downloadUrl" type="url" placeholder="公开可直接下载的安装包 URL" required /></label>
            <label>SHA-256<input v-model="artifact.sha256" minlength="64" maxlength="64" placeholder="64 位十六进制摘要" required /></label>
          </div>
          <div class="modal-actions">
            <button type="button" class="button ghost" @click="resetRelease">取消</button>
            <button class="button ghost" :disabled="busy" @click="releasePublishRequested = false">保存草稿</button>
            <button class="button primary" :disabled="busy" @click="releasePublishRequested = true">直接发布</button>
          </div>
        </form>
      </section>
    </div>
  </section>
</template>

<style scoped>
.game-info-workspace { display: grid; gap: 20px; }
.eyebrow { color: var(--teal); font-size: 11px; font-weight: 700; letter-spacing: 0.08em; text-transform: uppercase; }
.game-info-intro { display: flex; align-items: end; justify-content: space-between; gap: 24px; padding-bottom: 18px; border-bottom: 1px solid var(--line); }
.game-info-intro h2 { margin: 5px 0 6px; font-size: 24px; }
.game-info-intro p, .panel-heading p { margin: 0; color: var(--muted); }
.game-info-summary { display: flex; gap: 10px; }
.game-info-summary span { padding: 9px 12px; border: 1px solid var(--line); border-radius: 10px; color: var(--muted); white-space: nowrap; }
.game-info-summary b { color: var(--accent); margin-right: 4px; }
.game-info-columns { display: grid; grid-template-columns: minmax(0, 0.9fr) minmax(0, 1.1fr); gap: 18px; align-items: start; }
.game-info-panel { min-width: 0; padding: 20px; border: 1px solid var(--line); border-radius: 14px; background: color-mix(in srgb, var(--surface) 86%, transparent); }
.panel-heading { display: flex; align-items: center; justify-content: space-between; gap: 16px; }
.panel-heading h3 { margin: 0 0 5px; font-size: 19px; }
.game-info-form { display: grid; gap: 12px; }
.game-info-form label { display: grid; gap: 6px; color: var(--muted); font-size: 13px; }
.game-info-form input, .game-info-form textarea, .game-info-form select { width: 100%; padding: 10px 11px; }
.game-info-form textarea { min-height: 92px; resize: vertical; }
.field-row { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 12px; }
.artifact-heading { display: flex; align-items: center; justify-content: space-between; gap: 12px; margin-top: 4px; }
.artifact-editor { display: grid; gap: 10px; padding: 13px; border: 1px solid var(--line); border-radius: 9px; background: var(--surface-soft); }
.artifact-editor .field-row { grid-template-columns: minmax(0, 1fr) auto; align-items: end; }
.remove-artifact { min-height: 39px; }
.game-info-card .card-actions { display: flex; justify-content: flex-end; gap: 8px; }
.game-info-dialog { width: min(640px, 100%); }
.record-tabs { display: flex; gap: 6px; margin: 18px 0 10px; padding-bottom: 10px; border-bottom: 1px solid var(--line); }
.record-tabs button { padding: 6px 10px; border: 0; border-radius: 7px; color: var(--muted); background: transparent; }
.record-tabs button:hover { color: var(--text); background: var(--surface-hover); }
.record-tabs button.active { color: var(--text); background: var(--surface-hover); }
.game-info-list { display: grid; gap: 10px; }
.game-info-card { min-width: 0; padding: 15px; border: 1px solid var(--line); border-radius: 12px; background: var(--surface); }
.game-info-card .card-top { align-items: flex-start; }
.game-info-card h4 { margin: 0; font-size: 16px; }
.game-info-card p { margin: 10px 0; color: var(--muted); white-space: pre-wrap; line-height: 1.55; }
.game-info-card code { display: block; overflow-wrap: anywhere; color: var(--muted); }
.game-info-card .card-actions { margin-top: 14px; }
.platform-label { display: block; margin-top: 3px; color: var(--muted); font-size: 12px; }
.status.draft { color: var(--warning); background: #382f1d; }
.status.withdrawn { color: var(--muted); background: #252b27; }
@media (max-width: 900px) {
  .game-info-columns { grid-template-columns: 1fr; }
  .game-info-intro { align-items: flex-start; flex-direction: column; }
}
@media (max-width: 560px) {
  .field-row { grid-template-columns: 1fr; }
  .panel-heading { align-items: flex-start; }
  .game-info-summary { width: 100%; }
  .game-info-summary span { flex: 1; }
}
</style>
