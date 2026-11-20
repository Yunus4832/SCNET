<script setup lang="ts">
import { Send, X } from 'lucide-vue-next';
import { reactive, ref, watch } from 'vue';
import { api } from '../api';

interface EditableServer {
  id: string;
  name: string;
  address: string;
  description?: string;
  tags: string[];
}

const props = defineProps<{
  open: boolean;
  target?: EditableServer;
  administrator?: boolean;
}>();
const emit = defineEmits<{ close: []; submitted: [] }>();
const busy = ref(false);
const error = ref('');
const form = reactive({ name: '', address: '', description: '', tags: '' });

watch(
  () => [props.open, props.target] as const,
  ([open, target]) => {
    if (!open) return;
    Object.assign(form, {
      name: target?.name ?? '',
      address: target?.address ?? '',
      description: target?.description ?? '',
      tags: target?.tags.join(', ') ?? '',
    });
    error.value = '';
  },
  { immediate: true },
);

async function submit() {
  busy.value = true;
  error.value = '';
  try {
    const basePath = props.administrator ? '/api/v1/admin/servers' : '/api/v1/publisher/servers';
    await api(props.target ? `${basePath}/${props.target.id}` : basePath, {
      method: props.target ? 'PUT' : 'POST',
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
    if (!props.target) Object.assign(form, { name: '', address: '', description: '', tags: '' });
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
      <section class="modal-panel" role="dialog" aria-modal="true" aria-label="服务器信息">
        <div class="modal-head">
          <div>
            <h2 class="modal-title">{{ props.target ? '编辑服务器' : '提交服务器' }}</h2>
            <p v-if="props.target && !props.administrator">修改后需要管理员重新审核。</p>
            <p v-else-if="props.target">管理员保存后立即生效。</p>
            <p v-else>审核通过后会出现在本站内置服务器源中。</p>
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
              <Send :size="17" />{{ busy ? '正在保存…' : props.target ? '保存更改' : '提交审核' }}
            </button>
          </div>
        </form>
      </section>
    </div>
  </Teleport>
</template>
