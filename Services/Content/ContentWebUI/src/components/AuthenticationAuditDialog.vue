<script setup lang="ts">
import { useQuery } from '@tanstack/vue-query';
import { X } from 'lucide-vue-next';
import { computed, nextTick, ref, watch } from 'vue';
import { getAccess, type AuthenticationServiceAudit } from '../api';
import { loadAuthenticationPages } from '../authenticationDirectory';

const props = defineProps<{ id?: string }>();
const emit = defineEmits<{ close: [] }>();
const page = ref(1);
const closeButton = ref<HTMLButtonElement>();
watch(
  () => props.id,
  async (id) => {
    page.value = 1;
    if (!id) return;
    await nextTick();
    if (props.id === id) closeButton.value?.focus();
  },
  { immediate: true },
);
const history = useQuery({
  queryKey: computed(() => [
    'authentication-service-audit',
    props.id,
    getAccess('administrator')?.apiKey.slice(0, 18),
  ]),
  enabled: computed(() => !!props.id),
  retry: false,
  queryFn: ({ signal }) =>
    loadAuthenticationPages<AuthenticationServiceAudit>(
      `/api/v1/admin/authentication-services/${props.id}/audit`,
      signal,
      getAccess('administrator')?.apiKey ?? '',
    ),
});
</script>
<template>
  <Teleport to="body">
    <div
      v-if="id"
      class="modal-overlay"
      @click.self="emit('close')"
      @keydown.esc.prevent.stop="emit('close')"
    >
      <section
        class="modal-panel auth-audit-panel"
        role="dialog"
        aria-modal="true"
        aria-label="认证服务操作历史"
      >
        <div class="modal-head">
          <div>
            <h2 class="modal-title">认证服务操作历史</h2>
            <code>{{ id }}</code>
          </div>
          <button ref="closeButton" class="button ghost" @click="emit('close')">
            <X :size="17" />关闭
          </button>
        </div>
        <div v-if="history.isPending.value" class="state">正在取得历史…</div>
        <div v-else-if="history.isError.value" class="state error">
          {{ history.error.value?.message }}
        </div>
        <div v-else-if="!history.data.value?.length" class="state">没有此记录的历史</div>
        <div v-else class="auth-audit-entries">
          <article
            v-for="entry in history.data.value.slice((page - 1) * 6, page * 6)"
            :key="entry.revision"
            class="content-card"
          >
            <h3>#{{ entry.revision }} · {{ entry.action }} · {{ entry.name }}</h3>
            <p>
              {{ new Date(entry.occurredAt).toLocaleString() }} ·
              {{ entry.administratorId ? '管理员' : '发布者' }}
            </p>
            <code>{{ entry.administratorId || entry.publisherId }}</code>
            <code>{{ entry.serviceUrl }}</code>

            <p>{{ entry.description || '未提供说明' }}</p>
            <p v-if="entry.message">意见：{{ entry.message }}</p>
          </article>
          <div v-if="history.data.value.length > 6" class="pager">
            <button :disabled="page === 1" @click="page--">上一页</button
            ><span>{{ page }} / {{ Math.ceil(history.data.value.length / 6) }}</span
            ><button :disabled="page * 6 >= history.data.value.length" @click="page++">
              下一页
            </button>
          </div>
        </div>
      </section>
    </div>
  </Teleport>
</template>
<style scoped>
.auth-audit-panel {
  max-height: 90vh;
  overflow: auto;
}
.auth-audit-entries {
  display: grid;
  gap: 1rem;
}
.auth-audit-entries .content-card {
  min-width: 0;
  overflow-wrap: anywhere;
}
code {
  display: block;
  overflow-wrap: anywhere;
}
</style>
