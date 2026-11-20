<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query';
import { Check, Download, Eye, EyeOff, KeyRound, ShieldCheck, Sparkles, X } from 'lucide-vue-next';
import { computed, reactive, ref } from 'vue';
import { useRouter } from 'vue-router';
import { api, setAccess } from '../api';
import { resetInitializationRequirement } from '../router';

const router = useRouter();
const status = useQuery({
  queryKey: ['administrator-initialization'],
  queryFn: () => api<InitializationStatus>('/api/v1/administrators/initialization'),
});
interface InitializationStatus {
  required: boolean;
  apiKeyMinimumLength: number;
  apiKeyMaximumLength: number;
  apiKeyAllowedCharacters: string;
}
const form = reactive({ name: '', apiKey: '', confirmation: '' });
const error = ref('');
const busy = ref(false);
const showKey = ref(false);
const initialized = ref(false);
const downloaded = ref(false);
const minimumLength = computed(() => status.data.value?.apiKeyMinimumLength ?? 16);
const maximumLength = computed(() => status.data.value?.apiKeyMaximumLength ?? 128);
const lengthValid = computed(
  () => form.apiKey.length >= minimumLength.value && form.apiKey.length <= maximumLength.value,
);
const charactersValid = computed(() => /^[A-Za-z0-9._~-]+$/.test(form.apiKey));
const keysMatch = computed(() => form.confirmation.length > 0 && form.apiKey === form.confirmation);

function generateKey() {
  const bytes = crypto.getRandomValues(new Uint8Array(16));
  const key = `scadm_${Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('')}`;
  form.apiKey = key;
  form.confirmation = key;
  showKey.value = true;
  error.value = '';
}

async function initialize() {
  error.value = '';
  if (!form.name.trim()) {
    error.value = '请输入管理员名称';
    return;
  }
  if (!lengthValid.value) {
    error.value = `API Key 必须为 ${minimumLength.value}–${maximumLength.value} 个字符`;
    return;
  }
  if (!charactersValid.value) {
    error.value = 'API Key 包含不允许的字符';
    return;
  }
  if (!keysMatch.value) {
    error.value = '两次输入的 API Key 不一致';
    return;
  }
  busy.value = true;
  try {
    await api('/api/v1/administrators/initialize', {
      method: 'POST',
      body: JSON.stringify({ name: form.name, apiKey: form.apiKey }),
    });
    resetInitializationRequirement();
    initialized.value = true;
  } catch (value) {
    error.value = value instanceof Error ? value.message : '初始化失败';
  } finally {
    busy.value = false;
  }
}
function downloadKey() {
  const administratorName =
    form.name
      .trim()
      .replace(/[\\/:*?"<>|\u0000-\u001F]/g, '_')
      .slice(0, 80) || 'administrator';
  const timestamp = new Date()
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}/, '');
  const content = `SCNET 内容服务器 管理员 API Key\n\n${form.apiKey}\n\n请仅在受信任的个人设备上保存此文件。任何持有此 Key 的人都可代表该管理员操作。\n`;
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `admin-${administratorName}-${timestamp}-key.txt`;
  anchor.click();
  URL.revokeObjectURL(url);
  downloaded.value = true;
}
async function enterWorkspace() {
  setAccess({ role: 'administrator', apiKey: form.apiKey, label: form.name.trim() });
  await router.push('/admin');
}
</script>

<template>
  <section class="narrow-shell page-pad initialize-page">
    <h1 class="page-title">初始化管理员</h1>
    <p class="lead">
      仅空数据库可以执行一次。你设置的 API Key 只以 SHA-256 hash 保存，服务器无法恢复明文。
    </p>
    <div v-if="status.isPending.value" class="state">
      <span class="spinner" />正在检查服务器状态…
    </div>
    <div v-else-if="status.isError.value" class="state error">
      {{ status.error.value?.message }}
    </div>
    <div v-else-if="initialized" class="panel success-panel">
      <ShieldCheck :size="38" />
      <h2>管理员已创建</h2>
      <p>这是服务器唯一一次显示完整密钥，请先下载并妥善保存。</p>
      <div class="revealed-key">
        <code>{{ form.apiKey }}</code
        ><button @click="downloadKey">
          <Download :size="16" />{{ downloaded ? '已下载' : '下载 Key' }}
        </button>
      </div>
      <button class="button primary" :disabled="!downloaded" @click="enterWorkspace">
        {{ downloaded ? '进入管理员工作台' : '请先下载 Key' }}
      </button>
    </div>
    <div v-else-if="!status.data.value?.required" class="panel success-panel">
      <ShieldCheck :size="38" />
      <h2>服务器已经初始化</h2>
      <p>不能再次创建初始管理员。请使用已有 Key，或提交管理员申请。</p>
      <RouterLink class="button primary" to="/admin/access">前往内容审核</RouterLink>
    </div>
    <form v-else class="panel form-panel" @submit.prevent="initialize">
      <label>管理员名称<input v-model="form.name" maxlength="80" autocomplete="name" /></label>
      <div class="key-label">
        <span>设置 API Key</span
        ><button type="button" @click="generateKey"><Sparkles :size="15" />生成安全密钥</button>
      </div>
      <div class="key-input">
        <KeyRound :size="18" /><input
          v-model="form.apiKey"
          :type="showKey ? 'text' : 'password'"
          :maxlength="maximumLength"
          autocomplete="new-password"
        /><button
          type="button"
          :aria-label="showKey ? '隐藏 API Key' : '显示 API Key'"
          @click="showKey = !showKey"
        >
          <EyeOff v-if="showKey" :size="18" /><Eye v-else :size="18" />
        </button>
      </div>
      <div class="key-label"><span>确认 API Key</span></div>
      <div class="key-input">
        <KeyRound :size="18" /><input
          v-model="form.confirmation"
          :type="showKey ? 'text' : 'password'"
          :maxlength="maximumLength"
          autocomplete="new-password"
        /><button
          type="button"
          :aria-label="showKey ? '隐藏确认 API Key' : '显示确认 API Key'"
          @click="showKey = !showKey"
        >
          <EyeOff v-if="showKey" :size="18" /><Eye v-else :size="18" />
        </button>
      </div>
      <ul class="key-rules">
        <li :class="{ valid: lengthValid, invalid: form.apiKey.length > 0 && !lengthValid }">
          <Check v-if="lengthValid" :size="15" /><X v-else :size="15" />{{ minimumLength }}–{{
            maximumLength
          }}
          个字符（当前 {{ form.apiKey.length }} 个）
        </li>
        <li
          :class="{ valid: charactersValid, invalid: form.apiKey.length > 0 && !charactersValid }"
        >
          <Check v-if="charactersValid" :size="15" /><X v-else :size="15" />允许字符：{{
            status.data.value?.apiKeyAllowedCharacters
          }}
        </li>
        <li :class="{ valid: keysMatch, invalid: form.confirmation.length > 0 && !keysMatch }">
          <Check v-if="keysMatch" :size="15" /><X v-else :size="15" />两次输入一致
        </li>
      </ul>
      <p class="field-help">
        密钥只保存 hash，遗失后无法恢复。建议使用“生成安全密钥”并立即保存明文。
      </p>
      <p v-if="error" class="form-error">{{ error }}</p>
      <button class="button primary wide" :disabled="busy">
        {{ busy ? '正在初始化…' : '创建初始管理员' }}
      </button>
    </form>
  </section>
</template>
