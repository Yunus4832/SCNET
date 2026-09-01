<script setup lang="ts">
import { Download, Save, Send, Trash2, X } from 'lucide-vue-next';
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { api, getAccess } from '../api';
import { getRuntimeConfig } from '../config';
import {
  clearDrafts,
  deleteDraft,
  listDrafts,
  loadDraft,
  saveDraft,
  type ContentDraft,
  type ImageContentType,
} from '../contentDraftStore';

interface PackagePreview {
  type: string;
  identifier: string;
  name: string;
  version: string;
  packageHash: string;
  packageSize: number;
  entries: { path: string; length: number }[];
}
interface SourceInspection {
  width: number;
  height: number;
  size: number;
  sha256: string;
  mediaType: string;
}

const props = defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: []; submitted: [] }>();
const mode = ref<'package' | 'image'>('package');
const busy = ref(false);
const error = ref('');
const packageFile = ref<File>();
const packagePreview = ref<PackagePreview>();
const sourceFile = ref<File>();
const sourceInspection = ref<SourceInspection>();
const previewUrl = ref('');
const drafts = ref<ContentDraft[]>([]);
const draftWarning = ref('');
const form = reactive({
  draftId: crypto.randomUUID(),
  sourceBlobId: crypto.randomUUID(),
  type: 'CharacterSkin' as ImageContentType,
  identifier: crypto.randomUUID(),
  name: '',
  version: '1.0.0',
  description: '',
  createdAt: new Date().toISOString(),
});
const canCreate = computed(() =>
  !!sourceFile.value && !!sourceInspection.value && !!form.identifier && !!form.name && !!form.version,
);

function resetImageIdentity() {
  Object.assign(form, {
    draftId: crypto.randomUUID(),
    sourceBlobId: crypto.randomUUID(),
    identifier: crypto.randomUUID(),
    name: '',
    version: '1.0.0',
    description: '',
    createdAt: new Date().toISOString(),
  });
  sourceFile.value = undefined;
  sourceInspection.value = undefined;
  setPreviewUrl();
}
function setPreviewUrl(file?: Blob) {
  if (previewUrl.value) URL.revokeObjectURL(previewUrl.value);
  previewUrl.value = file ? URL.createObjectURL(file) : '';
}
async function selectPackage(event: Event) {
  packageFile.value = (event.target as HTMLInputElement).files?.[0];
  packagePreview.value = undefined;
  if (!packageFile.value) return;
  await run(async () => {
    const body = new FormData();
    body.set('package', packageFile.value!);
    packagePreview.value = await api<PackagePreview>('/api/v1/publisher/packages/inspect', {
      method: 'POST', body,
    });
  });
}
async function submitPackage() {
  if (!packageFile.value || !packagePreview.value) return;
  await run(async () => {
    const body = new FormData();
    body.set('package', packageFile.value!);
    await api('/api/v1/publisher/submissions', { method: 'POST', body });
    emit('submitted');
  });
}
async function selectSource(event: Event) {
  sourceFile.value = (event.target as HTMLInputElement).files?.[0];
  sourceInspection.value = undefined;
  setPreviewUrl(sourceFile.value);
  if (!sourceFile.value) return;
  await validateSource();
}
async function validateSource() {
  if (!sourceFile.value) return;
  await run(async () => {
    const body = new FormData();
    body.set('type', form.type);
    body.set('source', sourceFile.value!);
    sourceInspection.value = await api<SourceInspection>(
      '/api/v1/publisher/packages/image/validate-source', { method: 'POST', body },
    );
  });
}
function imageBody() {
  const body = new FormData();
  body.set('type', form.type);
  body.set('identifier', form.identifier);
  body.set('name', form.name);
  body.set('version', form.version);
  body.set('description', form.description);
  body.set('source', sourceFile.value!);
  return body;
}
async function buildImagePackage() {
  if (!canCreate.value) return;
  await run(async () => {
    const response = await fetch(`${getRuntimeConfig().apiBaseUrl}/api/v1/publisher/packages/image/build`, {
      method: 'POST',
      headers: { Authorization: `Bearer ${getAccess('publisher')?.apiKey ?? ''}` },
      body: imageBody(),
    });
    if (!response.ok) throw new Error('内容包生成失败');
    const url = URL.createObjectURL(await response.blob());
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${form.identifier}-${form.version}.scpkg`;
    anchor.click();
    URL.revokeObjectURL(url);
  });
}
async function submitImagePackage() {
  if (!canCreate.value) return;
  await run(async () => {
    await api('/api/v1/publisher/packages/image/submit', { method: 'POST', body: imageBody() });
    emit('submitted');
  });
}
async function persistDraft() {
  if (!sourceFile.value || !sourceInspection.value) return;
  const now = new Date().toISOString();
  const draft: ContentDraft = {
    schemaVersion: 1,
    draftId: form.draftId,
    type: form.type,
    identifier: form.identifier,
    name: form.name,
    version: form.version,
    description: form.description,
    sourceBlobId: form.sourceBlobId,
    sourceFileName: sourceFile.value.name,
    sourceMediaType: 'image/png',
    createdAt: form.createdAt,
    updatedAt: now,
  };
  await run(async () => {
    try {
      await saveDraft(draft, sourceFile.value!, sourceInspection.value!.sha256);
      drafts.value = await listDrafts();
      draftWarning.value = '';
    } catch {
      draftWarning.value = '浏览器存储空间不足，旧草稿未被修改；仍可直接生成或提交。';
    }
  });
}
async function restoreDraft(id: string) {
  await run(async () => {
    const { draft, source } = await loadDraft(id);
    Object.assign(form, draft);
    sourceFile.value = new File([source], draft.sourceFileName, { type: 'image/png' });
    setPreviewUrl(source);
    await validateSource();
  });
}
async function removeDraft(id: string) {
  await deleteDraft(id);
  drafts.value = await listDrafts();
}
async function removeAllDrafts() {
  await clearDrafts();
  drafts.value = [];
  resetImageIdentity();
}
async function run(action: () => Promise<void>) {
  error.value = '';
  busy.value = true;
  try { await action(); }
  catch (value) { error.value = value instanceof Error ? value.message : '操作失败'; }
  finally { busy.value = false; }
}
function close() { if (!busy.value) emit('close'); }
function onEscape(event: KeyboardEvent) { if (event.key === 'Escape') close(); }
watch(() => form.type, () => { if (sourceFile.value) void validateSource(); });
onMounted(async () => {
  window.addEventListener('keydown', onEscape);
  drafts.value = await listDrafts().catch(() => []);
});
onUnmounted(() => {
  window.removeEventListener('keydown', onEscape);
  setPreviewUrl();
});
</script>

<template>
  <Teleport to="body">
    <div v-if="props.open" class="modal-overlay" @click.self="close">
      <section class="modal-panel upload-form content-submission-dialog">
        <div class="modal-head">
          <div><span class="modal-title">提交内容</span><p>上传完整内容包，或制造皮肤与方块材质。</p></div>
          <button class="button ghost" :disabled="busy" @click="close"><X :size="16" />关闭</button>
        </div>
        <div class="role-picker submission-picker">
          <button :class="{ active: mode === 'package' }" @click="mode = 'package'">
            <strong>完整 .scpkg</strong><small>游戏、CLI 或 MSBuild 生成</small>
          </button>
          <button :class="{ active: mode === 'image' }" @click="mode = 'image'">
            <strong>图片制造</strong><small>仅皮肤与方块材质</small>
          </button>
        </div>

        <template v-if="mode === 'package'">
          <label class="file-field">选择内容包<input type="file" accept=".scpkg" @change="selectPackage" /></label>
          <div v-if="packagePreview" class="state package-preview">
            <strong>{{ packagePreview.name }} · {{ packagePreview.version }}</strong>
            <code>{{ packagePreview.identifier }}</code>
            <span>{{ packagePreview.type }} · {{ packagePreview.packageSize }} bytes</span>
            <code>{{ packagePreview.packageHash }}</code>
          </div>
          <p>包内类型、Identifier、名称和版本由 ContentServer 权威解析，提交时不会重新打包。</p>
          <div class="modal-actions"><button class="button primary" :disabled="busy || !packagePreview" @click="submitPackage"><Send :size="17" />提交审核</button></div>
        </template>

        <template v-else>
          <div class="form-grid">
            <label>类型<select v-model="form.type"><option value="CharacterSkin">角色皮肤</option><option value="BlocksTexture">方块材质</option></select></label>
            <label>Identifier<input v-model="form.identifier" readonly /></label>
            <label>名称<input v-model="form.name" /></label>
            <label>版本<input v-model="form.version" placeholder="1.0.0" /></label>
          </div>
          <label>简介<textarea v-model="form.description" rows="2" /></label>
          <label class="file-field">PNG 源文件<input type="file" accept="image/png,.png" @change="selectSource" /></label>
          <div v-if="sourceInspection" class="state package-preview">
            <img v-if="previewUrl" :src="previewUrl" alt="源图片预览" />
            <span>{{ sourceInspection.width }} × {{ sourceInspection.height }} · {{ sourceInspection.size }} bytes</span>
            <code>{{ sourceInspection.sha256 }}</code>
          </div>
          <div class="modal-actions">
            <button class="button ghost" :disabled="busy || !canCreate" @click="persistDraft"><Save :size="16" />保存草稿</button>
            <button class="button ghost" :disabled="busy || !canCreate" @click="buildImagePackage"><Download :size="16" />下载包</button>
            <button class="button primary" :disabled="busy || !canCreate" @click="submitImagePackage"><Send :size="16" />直接提交</button>
          </div>
          <p class="draft-risk">草稿仅保存在当前浏览器。清理站点数据、隐私模式回收或浏览器策略可能永久删除草稿。</p>
          <p v-if="draftWarning" class="form-error">{{ draftWarning }}</p>
          <div v-if="drafts.length" class="draft-list">
            <div v-for="draft in drafts" :key="draft.draftId">
              <button class="button ghost" @click="restoreDraft(draft.draftId)">{{ draft.name || '未命名草稿' }} · {{ draft.version }}</button>
              <button class="button ghost" title="删除草稿" @click="removeDraft(draft.draftId)"><Trash2 :size="15" /></button>
            </div>
          </div>
          <div class="modal-actions">
            <button class="button ghost" @click="resetImageIdentity">创建新内容</button>
            <button v-if="drafts.length" class="button ghost" @click="removeAllDrafts"><Trash2 :size="15" />清理全部草稿</button>
          </div>
        </template>
        <p v-if="error" class="form-error">{{ error }}</p>
      </section>
    </div>
  </Teleport>
</template>
