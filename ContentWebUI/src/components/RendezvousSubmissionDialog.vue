<script setup lang="ts">
import { Send, X } from 'lucide-vue-next';
import { reactive, ref, watch } from 'vue';
import { api, type RendezvousService } from '../api';

const props = defineProps<{ open: boolean; target?: RendezvousService }>();
const emit = defineEmits<{ close: []; submitted: [] }>();
const busy = ref(false);
const error = ref('');
const form = reactive({ name: '', controlUrl: '', description: '' });
watch(
  () => props.open,
  (open) => {
    if (!open) return;
    error.value = '';
    Object.assign(form, {
      name: props.target?.name ?? '',
      controlUrl: props.target?.controlUrl ?? '',
      description: props.target?.description ?? '',
    });
  },
  { immediate: true },
);
async function submit() {
  busy.value = true;
  error.value = '';
  try {
    await api(
      '/api/v1/publisher/rendezvous-services' + (props.target ? `/${props.target.id}/edit` : ''),
      {
        method: 'POST',
        body: JSON.stringify(form),
      },
    );
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
    <div v-if="open" class="modal-overlay" @click.self="!busy && emit('close')">
      <section class="modal-panel" role="dialog" aria-modal="true" aria-label="提交信令服务器">
        <div class="modal-head">
          <div>
            <h2 class="modal-title">{{ target ? '修改信令服务器' : '提交信令服务器' }}</h2>
            <p>服务需支持 SCNET 信令与房间发现。提交或修改后由管理员审核。</p>
          </div>
          <button class="button ghost" :disabled="busy" @click="emit('close')">
            <X :size="17" />关闭
          </button>
        </div>
        <form class="upload-form" @submit.prevent="submit">
          <label
            >名称<input v-model.trim="form.name" maxlength="100" required :disabled="busy"
          /></label>
          <label
            >信令控制地址<input
              v-model.trim="form.controlUrl"
              type="url"
              required
              placeholder="https://signal.example.org/"
              :disabled="busy"
          /></label>
          <label
            >说明<textarea
              v-model.trim="form.description"
              maxlength="1000"
              rows="4"
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
