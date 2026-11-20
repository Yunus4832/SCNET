<script setup lang="ts">
import { Check, Download, X } from 'lucide-vue-next';
import { computed, ref, watch } from 'vue';

export interface ReviewApplication {
  id: string;
  name: string;
  identity?: string;
  summary?: string;
  description?: string;
  status: string;
  reviewMessage?: string;
  createdAt?: string;
  reviewedAt?: string;
  badge?: string;
  path: string;
  packageId?: string;
}
const props = defineProps<{
  items: ReviewApplication[];
  loading: boolean;
  error?: string;
}>();
const emit = defineEmits<{
  review: [path: string, approve: boolean];
  download: [versionId: string];
}>();
const pendingPage = ref(1);
const historyPage = ref(1);
const pending = computed(() => props.items.filter((item) => item.status === 'pending'));
const history = computed(() => props.items.filter((item) => item.status !== 'pending'));
const sections = computed(() => [
  { title: '待审核', items: pending.value, page: pendingPage.value, pending: true },
  { title: '审核历史', items: history.value, page: historyPage.value, pending: false },
]);
watch([pending, history], () => {
  pendingPage.value = Math.min(pendingPage.value, Math.max(1, Math.ceil(pending.value.length / 6)));
  historyPage.value = Math.min(historyPage.value, Math.max(1, Math.ceil(history.value.length / 6)));
});
const labels: Record<string, string> = {
  pending: '待审核',
  published: '已批准',
  approved: '已批准',
  active: '已批准',
  rejected: '已拒绝',
  disabled: '已停用',
  suspended: '已暂停',
};
function changePage(isPending: boolean, delta: number) {
  if (isPending) pendingPage.value += delta;
  else historyPage.value += delta;
}
</script>
<template>
  <div v-if="loading" class="state"><span class="spinner" />正在取得申请记录…</div>
  <div v-else-if="error" class="state error">{{ error }}</div>
  <template v-else>
    <section v-for="section in sections" :key="section.title" class="review-section">
      <div class="section-heading">
        <h2>
          {{ section.title }} <small>{{ section.items.length }} 项</small>
        </h2>
      </div>
      <div v-if="section.items.length" class="content-grid">
        <article
          v-for="item in section.items.slice((section.page - 1) * 6, section.page * 6)"
          :key="item.id"
          class="content-card"
        >
          <div class="card-top">
            <span v-if="item.badge" class="type-pill">{{ item.badge }}</span>
            <span class="status" :class="item.status">{{
              labels[item.status] ?? item.status
            }}</span>
          </div>
          <div>
            <h3>{{ item.name }}</h3>
            <code v-if="item.identity">{{ item.identity }}</code>
            <p v-if="item.summary">{{ item.summary }}</p>
            <details v-if="item.description" class="card-details">
              <summary>详细描述</summary>
              <p class="package-description">{{ item.description }}</p>
            </details>
            <p v-if="item.reviewMessage">审核意见：{{ item.reviewMessage }}</p>
            <p v-if="item.reviewedAt || item.createdAt" class="muted">
              {{ item.reviewedAt ? '审核时间' : '申请时间' }}：{{
                new Date(item.reviewedAt || item.createdAt!).toLocaleString()
              }}
            </p>
          </div>
          <div v-if="section.pending || item.packageId" class="card-bottom">
            <div class="card-actions">
              <button
                v-if="item.packageId"
                class="button ghost"
                @click="emit('download', item.packageId)"
              >
                <Download :size="16" />测试包
              </button>
              <div v-if="section.pending" class="review-actions">
                <button
                  class="icon-button reject"
                  aria-label="拒绝"
                  @click="emit('review', item.path, false)"
                >
                  <X />
                </button>
                <button
                  class="icon-button approve"
                  aria-label="批准"
                  @click="emit('review', item.path, true)"
                >
                  <Check />
                </button>
              </div>
            </div>
          </div>
        </article>
      </div>
      <div v-else class="state">{{ section.pending ? '没有待审核申请' : '暂无审核历史' }}</div>
      <div v-if="section.items.length > 6" class="pager">
        <button :disabled="section.page <= 1" @click="changePage(section.pending, -1)">
          上一页
        </button>
        <span>{{ section.page }} / {{ Math.ceil(section.items.length / 6) }}</span>
        <button
          :disabled="section.page * 6 >= section.items.length"
          @click="changePage(section.pending, 1)"
        >
          下一页
        </button>
      </div>
    </section>
  </template>
</template>
