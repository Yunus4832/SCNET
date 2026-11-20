<script setup lang="ts">
import { CheckCircle2, Download } from 'lucide-vue-next';
import { computed, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { api, setAccess } from '../api';

const route = useRoute();
const router = useRouter();
const isAdmin = computed(() => route.params.role === 'administrator');
const form = reactive({ name: '', contact: '', description: '' });
const result = ref<{ apiKey: string; status: string } | null>(null);
const error = ref('');
const busy = ref(false);
const downloaded = ref(false);
async function submit() {
  error.value = '';
  busy.value = true;
  try {
    const path = isAdmin.value ? '/api/v1/administrators/applications' : '/api/v1/publishers';
    const body = isAdmin.value
      ? { name: form.name, contact: form.contact, description: form.description }
      : { displayName: form.name, contact: form.contact, description: form.description };
    result.value = await api<{ apiKey: string; status: string }>(path, {
      method: 'POST',
      body: JSON.stringify(body),
    });
  } catch (value) {
    error.value = value instanceof Error ? value.message : '提交失败';
  } finally {
    busy.value = false;
  }
}
function downloadKey() {
  if (!result.value) return;
  const applicantName =
    form.name
      .trim()
      .replace(/[\\/:*?"<>|\u0000-\u001F]/g, '_')
      .slice(0, 80) || 'applicant';
  const timestamp = new Date()
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}/, '');
  const roleLabel = isAdmin.value ? '管理员' : '发布者';
  const content = `SCNET 内容服务器 ${roleLabel} API Key\n\n${result.value.apiKey}\n\n请仅在受信任的个人设备上保存此文件。任何持有此 Key 的人都可代表该 ${roleLabel} 操作。\n`;
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain;charset=utf-8' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `${isAdmin.value ? 'admin' : 'publisher'}-${applicantName}-${timestamp}-key.txt`;
  anchor.click();
  URL.revokeObjectURL(url);
  downloaded.value = true;
}
async function enterWorkspace() {
  if (!result.value) return;
  const role = isAdmin.value ? 'administrator' : 'publisher';
  setAccess({ role, apiKey: result.value.apiKey, label: form.name.trim() });
  await router.push(role === 'administrator' ? '/admin' : '/publisher');
}
</script>

<template>
  <section class="narrow-shell page-pad application-page">
    <h1 class="page-title">申请成为{{ isAdmin ? '管理员' : '发布者' }}</h1>
    <p class="lead">
      提交必要的联系信息。申请创建后会立即生成 API
      Key；待审核期间可进入工作台查看状态，审核通过后可使用相应功能。
    </p>
    <div v-if="result" class="panel success-panel">
      <CheckCircle2 :size="38" />
      <h2>申请已提交</h2>
      <p>状态：{{ result.status }}。这是服务器唯一一次显示完整密钥，请先下载并妥善保存。</p>
      <div class="revealed-key">
        <code>{{ result.apiKey }}</code
        ><button @click="downloadKey">
          <Download :size="16" />{{ downloaded ? '已下载' : '下载 Key' }}
        </button>
      </div>
      <button class="button primary" :disabled="!downloaded" @click="enterWorkspace">
        {{ downloaded ? '进入工作台' : '请先下载 Key' }}
      </button>
    </div>
    <form v-else class="panel form-panel" @submit.prevent="submit">
      <label
        >{{ isAdmin ? '姓名或称呼' : '发布者名称'
        }}<input v-model="form.name" required maxlength="80" /></label
      ><label
        >联系方式<input
          v-model="form.contact"
          required
          maxlength="200"
          placeholder="电子邮件、即时通讯账号或主页" /></label
      ><label>说明（可选）<textarea v-model="form.description" rows="5" maxlength="1000" /></label>
      <p v-if="error" class="form-error">{{ error }}</p>
      <button class="button primary wide" :disabled="busy">
        {{ busy ? '正在提交…' : '提交申请并生成 API Key' }}
      </button>
      <div class="application-access">
        <div><strong>已有可用的 API Key？</strong><small>可直接输入 Key 进入工作台。</small></div>
        <RouterLink class="button ghost" :to="isAdmin ? '/admin/access' : '/publisher/access'"
          >输入 Key 进入工作台</RouterLink
        >
      </div>
    </form>
  </section>
</template>
