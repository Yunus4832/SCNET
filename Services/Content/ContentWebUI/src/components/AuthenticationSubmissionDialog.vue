<script setup lang="ts">
import { Send, X } from 'lucide-vue-next';
import { nextTick, reactive, ref, watch } from 'vue';
import { api, getAccess, type AuthenticationService } from '../api';
import { authenticationError } from '../authenticationDirectory';

const props = defineProps<{ open: boolean; target?: AuthenticationService }>();
const emit = defineEmits<{ close: []; submitted: [] }>();
const busy = ref(false);
const error = ref('');
const nameInput = ref<HTMLInputElement>();
const form = reactive({ name: '', serviceUrl: '', description: '' });
watch(
  () => props.open,
  async (open) => {
    if (!open) return;
    error.value = '';
    Object.assign(form, {
      name: props.target?.name ?? '',
      serviceUrl: props.target?.serviceUrl ?? '',
      description: props.target?.description ?? '',
    });
    await nextTick();
    if (props.open) nameInput.value?.focus();
  },
  { immediate: true },
);
async function submit() {
  if (busy.value) return;
  busy.value = true;
  error.value = '';
  try {
    const revision = props.target?.revision;
    if (props.target && (!Number.isSafeInteger(revision) || revision! < 1))
      throw new Error('修订号无效，请刷新记录后重试。');
    await api(
      '/api/v1/publisher/authentication-services' +
        (props.target ? `/${props.target.id}/edit` : ''),
      {
        method: 'POST',
        body: JSON.stringify({ ...form, ...(props.target ? { revision } : {}) }),
      },
      getAccess('publisher')?.apiKey ?? '',
    );
    emit('submitted');
  } catch (value) {
    error.value = authenticationError(value);
  } finally {
    busy.value = false;
  }
}
</script>
<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="modal-overlay"
      @click.self="!busy && emit('close')"
      @keydown.esc.prevent.stop="!busy && emit('close')"
    >
      <section
        class="modal-panel auth-submission-panel"
        role="dialog"
        aria-modal="true"
        aria-label="提交认证服务"
      >
        <div class="modal-head">
          <div>
            <h2 class="modal-title">{{ target ? '修改认证服务' : '提交认证服务' }}</h2>
            <p>
              提交或修改后须重新审核。填写认证服务的 HTTPS
              来源地址；身份由游戏客户端直接发现并由玩家确认。
            </p>
          </div>
          <button class="button ghost" :disabled="busy" @click="emit('close')">
            <X :size="17" />关闭
          </button>
        </div>
        <form class="upload-form" @submit.prevent="submit">
          <label
            >名称<input
              ref="nameInput"
              v-model.trim="form.name"
              maxlength="100"
              required
              :disabled="busy"
          /></label>
          <label
            >认证服务 HTTPS 基址<input
              v-model.trim="form.serviceUrl"
              type="url"
              maxlength="2048"
              required
              placeholder="https://auth.example.org/"
              :disabled="busy"
          /></label>
          <label
            >说明<textarea
              v-model.trim="form.description"
              maxlength="1000"
              rows="3"
              :disabled="busy"
            />
          </label>
          <p v-if="error" class="form-error">{{ error }}</p>
          <div class="modal-actions">
            <button type="button" class="button ghost" :disabled="busy" @click="emit('close')">
              取消
            </button>
            <button class="button primary" :disabled="busy">
              <Send :size="17" />{{ busy ? '正在校验…' : '提交审核' }}
            </button>
          </div>
        </form>
      </section>
    </div>
  </Teleport>
</template>

<style scoped>
.auth-submission-panel {
  max-height: 90vh;
  overflow: auto;
}
.auth-submission-panel code {
  display: block;
  white-space: normal;
  overflow-wrap: anywhere;
}
</style>
