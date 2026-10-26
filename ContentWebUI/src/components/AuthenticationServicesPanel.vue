<script setup lang="ts">
import { useQuery, useQueryClient } from '@tanstack/vue-query';
import { Copy, Pencil, Plus, RefreshCw, Trash2 } from 'lucide-vue-next';
import { computed, reactive, ref, watch } from 'vue';
import { api, getAccess, type AuthenticationService } from '../api';
import { authenticationError, loadAuthenticationPages } from '../authenticationDirectory';
import { copyText } from '../clipboard';
import AuthenticationSubmissionDialog from './AuthenticationSubmissionDialog.vue';
import AuthenticationAuditDialog from './AuthenticationAuditDialog.vue';

const props = defineProps<{
  mode: 'public' | 'publisher' | 'review' | 'manage';
  search?: string;
  canSubmit?: boolean;
}>();
const client = useQueryClient();
const busy = ref(false);
const error = ref('');
const formOpen = ref(false);
const target = ref<AuthenticationService>();
const auditId = ref<string>();
const auditLookup = ref('');
const notes = reactive<Record<string, string>>({});
const pages = reactive<Record<string, number>>({ all: 1, pending: 1, history: 1 });
const accessKey = computed(() =>
  props.mode === 'public'
    ? ''
    : (getAccess(props.mode === 'publisher' ? 'publisher' : 'administrator')?.apiKey ?? ''),
);
const base = computed(() =>
  props.mode === 'public'
    ? '/api/v1/authentication-services'
    : `/api/v1/${props.mode === 'publisher' ? 'publisher' : 'admin'}/authentication-services`,
);
const services = useQuery({
  queryKey: computed(() => ['authentication-services', props.mode, accessKey.value.slice(0, 18)]),
  retry: false,
  queryFn: ({ signal }) =>
    loadAuthenticationPages<AuthenticationService>(base.value, signal, accessKey.value),
});
const filtered = computed(() => {
  const search = props.search?.trim().toLocaleLowerCase() ?? '';
  return (services.data.value ?? []).filter(
    (item) =>
      !search ||
      [item.name, item.serviceUrl, item.description].some((value) =>
        value?.toLocaleLowerCase().includes(search),
      ),
  );
});
const sections = computed(() =>
  props.mode === 'review'
    ? [
        {
          key: 'pending',
          title: '待审核',
          items: filtered.value.filter((item) => item.status === 'pending'),
        },
        {
          key: 'history',
          title: '审核历史',
          items: filtered.value.filter((item) => item.status !== 'pending'),
        },
      ]
    : [{ key: 'all', title: '', items: filtered.value }],
);
watch([filtered, () => props.mode], () => {
  for (const section of sections.value)
    pages[section.key] = Math.min(
      pages[section.key] ?? 1,
      Math.max(1, Math.ceil(section.items.length / 6)),
    );
});
const labels: Record<string, string> = {
  pending: '待审核',
  active: '已批准',
  rejected: '已拒绝',
  disabled: '已停用',
  withdrawn: '已撤回',
};
function edit(item?: AuthenticationService) {
  target.value = item;
  formOpen.value = true;
}
async function submitted() {
  formOpen.value = false;
  await client.invalidateQueries({ queryKey: ['authentication-services'] });
}
async function action(
  item: AuthenticationService,
  operation: 'approve' | 'reject' | 'withdraw' | 'disable' | 'delete',
) {
  if (busy.value) return;
  const revision = item.revision;
  if (!Number.isSafeInteger(revision) || revision! < 1) {
    error.value = '修订号无效，请刷新记录后重试。';
    return;
  }
  const verb = {
    approve: '批准',
    reject: '拒绝',
    withdraw: '撤回',
    disable: '停用',
    delete: '删除',
  }[operation];
  if (!window.confirm(`${verb}“${item.name}”？\n${item.serviceUrl}\n修订：${revision}`)) return;
  busy.value = true;
  error.value = '';
  try {
    await api(
      `${base.value}/${item.id}${operation === 'delete' ? `?revision=${revision}` : `/${operation}`}`,
      {
        method: operation === 'delete' ? 'DELETE' : 'POST',
        ...(operation !== 'delete'
          ? {
              body: JSON.stringify({
                revision,
                ...(operation === 'disable'
                  ? { reason: notes[item.id] || null }
                  : operation === 'approve' || operation === 'reject'
                    ? { message: notes[item.id] || null }
                    : {}),
              }),
            }
          : {}),
      },
      accessKey.value,
    );
    delete notes[item.id];
    await client.invalidateQueries({ queryKey: ['authentication-services'] });
    await client.invalidateQueries({ queryKey: ['authentication-service-audit', item.id] });
    if (operation === 'delete' && props.mode === 'manage') auditId.value = item.id;
  } catch (value) {
    error.value = authenticationError(value);
  } finally {
    busy.value = false;
  }
}
async function copy(value: string) {
  try {
    await copyText(value);
  } catch {
    error.value = '无法复制，请手动选择文本。';
  }
}
function lookupAudit() {
  if (!/^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(auditLookup.value.trim())) {
    error.value = '请输入有效的目录记录 ID。';
    return;
  }
  error.value = '';
  auditId.value = auditLookup.value.trim();
}
</script>
<template>
  <div class="section-heading auth-service-heading">
    <h2>
      认证服务 <small>{{ filtered.length }} 项</small>
    </h2>
    <div class="review-actions">
      <button
        class="button ghost"
        :disabled="services.isFetching.value || busy"
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
        <Plus :size="17" />提交认证服务
      </button>
    </div>
  </div>
  <p class="muted">
    目录只收录服务来源地址，不记录认证身份。请在游戏客户端直接发现并确认公钥；已审核不代表永久安全或实时可用。
  </p>
  <form
    v-if="mode === 'manage'"
    class="management-filters auth-audit-lookup"
    @submit.prevent="lookupAudit"
  >
    <input
      v-model="auditLookup"
      placeholder="目录记录 ID（也可查看已删除记录）"
      aria-label="历史目录记录 ID"
    /><button class="button ghost">查看历史</button>
  </form>
  <p v-if="error" class="form-error" role="alert">{{ error }}</p>
  <div v-if="services.isPending.value" class="state"><span class="spinner" />正在取得认证服务…</div>
  <div v-else-if="services.isError.value" class="state error">
    {{ services.error.value?.message }}
  </div>
  <template v-else>
    <section v-for="section in sections" :key="section.key" class="review-section">
      <h3 v-if="section.title">
        {{ section.title }} <small>{{ section.items.length }} 项</small>
      </h3>
      <div v-if="!section.items.length" class="state">
        {{
          section.key === 'pending'
            ? '没有待审核申请'
            : section.key === 'history'
              ? '暂无审核历史'
              : '没有匹配的认证服务'
        }}
      </div>
      <div v-else class="content-grid auth-service-grid">
        <article
          v-for="item in section.items.slice(
            ((pages[section.key] ?? 1) - 1) * 6,
            (pages[section.key] ?? 1) * 6,
          )"
          :key="item.id"
          class="content-card auth-service-card"
        >
          <div class="card-top">
            <span class="type-pill">认证服务</span
            ><span v-if="item.status" class="status" :class="item.status">{{
              labels[item.status] ?? item.status
            }}</span>
          </div>
          <div>
            <h3>{{ item.name }}</h3>
            <code>{{ item.serviceUrl }}</code>
            <p>{{ item.description || '未提供说明' }}</p>
            <p v-if="item.reviewMessage">审核意见：{{ item.reviewMessage }}</p>
            <p v-if="item.revision" class="muted">
              目录记录 {{ item.id }} · 修订 {{ item.revision }}
            </p>
            <label
              v-if="
                (mode === 'review' && item.status === 'pending') ||
                (mode === 'manage' && item.status === 'active')
              "
              class="upload-form auth-note"
              >{{ mode === 'review' ? '审核意见（可选）' : '停用原因（可选）'
              }}<textarea v-model="notes[item.id]" maxlength="1000" rows="2" :disabled="busy" />
            </label>
          </div>
          <div class="card-bottom">
            <span>{{ item.publisherName || (mode === 'public' ? '已审核' : '') }}</span>
            <div class="review-actions auth-service-actions">
              <template v-if="mode === 'public'"
                ><button class="button ghost" @click="copy(item.serviceUrl)">
                  <Copy :size="16" />复制地址
                </button></template
              >
              <template v-if="mode === 'publisher'"
                ><button class="button ghost" :disabled="!canSubmit || busy" @click="edit(item)">
                  <Pencil :size="16" />修改</button
                ><button
                  v-if="item.status !== 'withdrawn'"
                  class="button ghost"
                  :disabled="busy"
                  @click="action(item, 'withdraw')"
                >
                  撤回
                </button></template
              >
              <template v-if="mode === 'review' && item.status === 'pending'"
                ><button class="button ghost" :disabled="busy" @click="action(item, 'reject')">
                  拒绝</button
                ><button class="button primary" :disabled="busy" @click="action(item, 'approve')">
                  批准
                </button></template
              >
              <button
                v-if="mode === 'manage' && item.status === 'active'"
                class="button ghost"
                :disabled="busy"
                @click="action(item, 'disable')"
              >
                停用
              </button>
              <button
                v-if="mode === 'manage' || mode === 'review'"
                class="button ghost"
                @click="auditId = item.id"
              >
                历史
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
      <div v-if="section.items.length > 6" class="pager">
        <button
          :disabled="(pages[section.key] ?? 1) <= 1"
          @click="pages[section.key] = (pages[section.key] ?? 1) - 1"
        >
          上一页</button
        ><span>{{ pages[section.key] }} / {{ Math.ceil(section.items.length / 6) }}</span
        ><button
          :disabled="(pages[section.key] ?? 1) * 6 >= section.items.length"
          @click="pages[section.key] = (pages[section.key] ?? 1) + 1"
        >
          下一页
        </button>
      </div>
    </section>
  </template>
  <AuthenticationSubmissionDialog
    :open="formOpen"
    :target="target"
    @close="formOpen = false"
    @submitted="submitted"
  />
  <AuthenticationAuditDialog
    v-if="mode === 'manage' || mode === 'review'"
    :id="auditId"
    @close="auditId = undefined"
  />
</template>
<style scoped>
.auth-service-grid {
  grid-template-columns: repeat(auto-fit, minmax(min(100%, 350px), 1fr));
}
.auth-service-card {
  min-width: 0;
  overflow-wrap: anywhere;
}
.auth-service-card code {
  display: block;
  overflow-wrap: anywhere;
}
.auth-service-actions {
  flex-wrap: wrap;
}
.auth-note {
  display: grid;
  gap: 0.4rem;
  margin-top: 0.75rem;
}
.auth-note textarea {
  width: 100%;
}
@media (max-width: 600px) {
  .auth-service-heading,
  .auth-service-card .card-bottom,
  .auth-audit-lookup {
    flex-direction: column;
    align-items: stretch;
    gap: 10px;
  }
  .auth-service-heading .review-actions {
    width: 100%;
    flex-wrap: wrap;
  }
}
</style>
