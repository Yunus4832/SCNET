<script setup lang="ts">
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Copy, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-vue-next';
import { computed, ref } from 'vue';
import { api, getAccess, type RendezvousService } from '../api';
import { copyText } from '../clipboard';
import RendezvousSubmissionDialog from './RendezvousSubmissionDialog.vue';

const props = defineProps<{
  mode: 'public' | 'publisher' | 'manage';
  search?: string;
  canSubmit?: boolean;
}>();
const client = useQueryClient();
const busy = ref(false);
const error = ref('');
const formOpen = ref(false);
const target = ref<RendezvousService>();
const accessKey = computed(() =>
  props.mode === 'public'
    ? 'anonymous'
    : (getAccess(props.mode === 'publisher' ? 'publisher' : 'administrator')?.apiKey.slice(0, 18) ??
      'anonymous'),
);
const base = computed(() =>
  props.mode === 'public'
    ? '/api/v1/rendezvous-services'
    : `/api/v1/${props.mode === 'publisher' ? 'publisher' : 'admin'}/rendezvous-services`,
);
const services = useQuery({
  queryKey: computed(() => ['rendezvous-services', props.mode, accessKey.value]),
  queryFn: async ({ signal }) => {
    const url = base.value;
    if (props.mode !== 'public') {
      return api<RendezvousService[]>(url, { signal });
    }
    const result: RendezvousService[] = [];
    for (let pageIndex = 1; pageIndex <= 100; pageIndex++) {
      const page = await api<{
        items: RendezvousService[];
        total: number;
        pageIndex: number;
        pageSize: number;
      }>(`${url}?pageIndex=${pageIndex}&pageSize=100`, { signal });
      if (
        page.pageIndex !== pageIndex ||
        page.pageSize !== 100 ||
        !Array.isArray(page.items) ||
        page.items.length > 100 ||
        !Number.isInteger(page.total) ||
        page.total < 0 ||
        page.total > 10000
      ) {
        throw new Error('信令服务目录返回的数据无效');
      }
      result.push(...page.items);
      if (pageIndex * page.pageSize >= page.total) return result;
      if (page.items.length === 0) throw new Error('信令服务目录分页不完整');
    }
    throw new Error('信令服务目录超出分页限制');
  },
});
const filtered = computed(() => {
  const search = props.search?.trim().toLocaleLowerCase() ?? '';
  return (services.data.value ?? []).filter(
    (item) =>
      !search ||
      [item.name, item.controlUrl, item.description].some((value) =>
        value?.toLocaleLowerCase().includes(search),
      ),
  );
});
const labels: Record<string, string> = {
  pending: '待审核',
  active: '已批准',
  rejected: '已拒绝',
  disabled: '已停用',
};
function edit(item?: RendezvousService) {
  target.value = item;
  formOpen.value = true;
}
async function submitted() {
  formOpen.value = false;
  await client.invalidateQueries({ queryKey: ['rendezvous-services'] });
}
async function action(item: RendezvousService, operation: 'disable' | 'delete') {
  if (busy.value) return;
  if (!window.confirm(`${operation === 'delete' ? '删除' : '停用'}“${item.name}”？`)) return;
  busy.value = true;
  error.value = '';
  try {
    await api(`${base.value}/${item.id}${operation === 'delete' ? '' : `/${operation}`}`, {
      method: operation === 'delete' ? 'DELETE' : 'POST',
    });
    await client.invalidateQueries({ queryKey: ['rendezvous-services'] });
  } catch (value) {
    error.value = value instanceof Error ? value.message : '操作失败';
  } finally {
    busy.value = false;
  }
}
async function copy(item: RendezvousService) {
  try {
    await copyText(item.controlUrl);
  } catch {
    error.value = '无法复制地址';
  }
}
</script>
<template>
  <div class="section-heading rendezvous-heading">
    <h2>
      {{ mode === 'manage' ? '管理信令服务器' : '信令服务器' }}
      <small>{{ filtered.length }} 项</small>
    </h2>
    <div class="review-actions">
      <button
        class="button ghost"
        :disabled="services.isFetching.value"
        @click="services.refetch()"
      >
        <RefreshCw :size="16" />刷新
      </button>
      <button
        v-if="mode === 'publisher'"
        class="button primary"
        :disabled="!canSubmit || busy"
        @click="edit()"
      >
        <Plus :size="17" />提交信令服务器
      </button>
    </div>
  </div>
  <p v-if="error" class="form-error">{{ error }}</p>
  <div v-if="services.isPending.value" class="state">
    <span class="spinner" />正在取得信令服务器…
  </div>
  <div v-else-if="services.isError.value" class="state error">
    {{ services.error.value?.message }}
  </div>
  <div v-else-if="!filtered.length" class="state">没有匹配的信令服务器</div>
  <div v-else class="content-grid" :class="{ 'rendezvous-grid': mode === 'public' }">
    <article v-for="item in filtered" :key="item.id" class="content-card rendezvous-card">
      <div class="card-top">
        <span class="type-pill">信令服务器</span
        ><span v-if="item.status" class="status" :class="item.status">{{
          labels[item.status] ?? item.status
        }}</span>
      </div>
      <div>
        <h3>{{ item.name }}</h3>
        <code>{{ item.controlUrl }}</code>
        <p>{{ item.description || '未提供说明' }}</p>
        <p v-if="item.reviewMessage">审核意见：{{ item.reviewMessage }}</p>
      </div>
      <div class="card-bottom">
        <span>{{ item.publisherName || (mode === 'public' ? '已审核' : '') }}</span>
        <div class="review-actions">
          <button v-if="mode === 'public'" class="button ghost" @click="copy(item)">
            <Copy :size="16" />复制地址
          </button>
          <button
            v-if="mode === 'publisher'"
            class="button ghost"
            :disabled="!canSubmit || busy"
            @click="edit(item)"
          >
            <Pencil :size="16" />修改
          </button>
          <button
            v-if="mode === 'manage' && item.status === 'active'"
            class="button ghost"
            :disabled="busy"
            @click="action(item, 'disable')"
          >
            停用
          </button>
          <button
            v-if="mode === 'publisher' || mode === 'manage'"
            class="button ghost content-status-button"
            :disabled="busy"
            @click="action(item, 'delete')"
          >
            <Trash2 :size="16" />删除
          </button>
        </div>
      </div>
    </article>
  </div>
  <RendezvousSubmissionDialog
    :open="formOpen"
    :target="target"
    @close="formOpen = false"
    @submitted="submitted"
  />
</template>
