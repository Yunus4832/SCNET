<script setup lang="ts">
import { Send, X } from 'lucide-vue-next';
import { reactive, ref, watch } from 'vue';
import { api } from '../api';

const props = defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: []; submitted: [] }>();
const busy = ref(false);
const error = ref('');
const form = reactive({ name: '', apiUrl: '', description: '' });

watch(
  () => props.open,
  (open) => {
    if (open) error.value = '';
  },
);

async function submit() {
  busy.value = true;
  error.value = '';
  try {
    await api('/api/v1/publisher/server-sources', {
      method: 'POST',
      body: JSON.stringify(form),
    });
    Object.assign(form, { name: '', apiUrl: '', description: '' });
    emit('submitted');
  } catch (value) {
    error.value = value instanceof Error ? value.message : '提交失败';
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <Teleport to="body">
    <div v-if="open" class="modal-overlay" @click.self="emit('close')">
      <section class="modal-panel" role="dialog" aria-modal="true" aria-label="提交服务器源">
        <div class="modal-head">
          <div>
            <h2 class="modal-title">提交服务器源</h2>
            <p>服务器源需实现 SCNET 服务器源协议，提交后由管理员审核。</p>
          </div>
          <button class="button ghost" @click="emit('close')"><X :size="17" />关闭</button>
        </div>
        <form class="upload-form" @submit.prevent="submit">
          <label>名称<input v-model.trim="form.name" maxlength="100" required /></label>
          <label
            >服务器源 API 地址<input
              v-model.trim="form.apiUrl"
              type="url"
              required
              placeholder="https://example.com/api/servers"
          /></label>
          <label>说明<textarea v-model.trim="form.description" maxlength="1000" rows="4" /></label>
          <p v-if="error" class="form-error">{{ error }}</p>
          <div class="modal-actions">
            <button type="button" class="button ghost" @click="emit('close')">取消</button>
            <button class="button primary" :disabled="busy">
              <Send :size="17" />{{ busy ? '正在校验…' : '提交审核' }}
            </button>
          </div>
        </form>
      </section>
    </div>
  </Teleport>
</template>
