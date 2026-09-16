<script setup lang="ts">
import { Send, X } from 'lucide-vue-next';
import { reactive, ref } from 'vue';
import { api } from '../api';

const props = defineProps<{ open: boolean }>();
const emit = defineEmits<{ close: []; submitted: [] }>();
const busy = ref(false);
const error = ref('');
const form = reactive({ name: '', address: '', description: '', tags: '' });

async function submit() {
  busy.value = true;
  error.value = '';
  try {
    await api('/api/v1/publisher/servers', {
      method: 'POST',
      body: JSON.stringify({
        name: form.name,
        address: form.address,
        description: form.description,
        tags: form.tags
          .split(',')
          .map((tag) => tag.trim())
          .filter(Boolean),
      }),
    });
    Object.assign(form, { name: '', address: '', description: '', tags: '' });
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
    <div v-if="props.open" class="modal-overlay" @click.self="emit('close')">
      <section class="modal-panel" role="dialog" aria-modal="true" aria-label="提交服务器">
        <div class="modal-head">
          <div>
            <h2 class="modal-title">提交服务器</h2>
            <p>审核通过后会出现在本站内置服务器源中。</p>
          </div>
          <button class="button ghost" @click="emit('close')"><X :size="17" />关闭</button>
        </div>
        <form class="upload-form" @submit.prevent="submit">
          <label>名称<input v-model.trim="form.name" maxlength="100" required /></label>
          <label
            >服务器地址<input
              v-model.trim="form.address"
              required
              placeholder="play.example.com:28887"
          /></label>
          <label
            >标签<input v-model.trim="form.tags" placeholder="生存, 合作（使用逗号分隔）"
          /></label>
          <label>说明<textarea v-model.trim="form.description" maxlength="1024" rows="4" /></label>
          <p v-if="error" class="form-error">{{ error }}</p>
          <div class="modal-actions">
            <button type="button" class="button ghost" @click="emit('close')">取消</button>
            <button class="button primary" :disabled="busy">
              <Send :size="17" />{{ busy ? '正在提交…' : '提交审核' }}
            </button>
          </div>
        </form>
      </section>
    </div>
  </Teleport>
</template>
