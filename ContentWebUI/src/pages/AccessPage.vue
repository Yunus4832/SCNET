<script setup lang="ts">
import { ArrowRight, KeyRound } from 'lucide-vue-next';
import { computed, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import {
  api,
  clearAccess,
  getSavedAccesses,
  setAccess,
  setActiveRole,
  type AccessRole,
  type StoredAccess,
} from '../api';

const router = useRouter();
const route = useRoute();
const role = computed<AccessRole>(() =>
  route.meta.accessRole === 'administrator' ? 'administrator' : 'publisher',
);
const apiKey = ref('');
const error = ref('');
const busy = ref(false);
const savedRevision = ref(0);
const selectedKeys = ref<string[]>([]);
const roleLabel = computed(() => (role.value === 'publisher' ? '发布者' : '管理员'));
const workspaceLabel = computed(() =>
  role.value === 'publisher' ? '发布者工作台' : '管理员工作台',
);
const savedAccesses = computed(() => {
  savedRevision.value;
  return getSavedAccesses(role.value);
});
const selectedAccesses = computed(() =>
  savedAccesses.value.filter((access) => selectedKeys.value.includes(access.apiKey)),
);

function destination(accessRole: AccessRole) {
  const next = route.query.next;
  return typeof next === 'string' ? next : accessRole === 'publisher' ? '/publisher' : '/admin';
}
async function enterSaved(access: StoredAccess) {
  if (access.invalid) return;
  setActiveRole(access.role, access.apiKey);
  await router.push(destination(access.role));
}
function removeSelectedKey() {
  if (!selectedAccesses.value.length) return;
  for (const access of selectedAccesses.value) clearAccess(role.value, access.apiKey);
  selectedKeys.value = [];
  savedRevision.value++;
}
async function enter() {
  error.value = '';
  busy.value = true;
  try {
    const path = role.value === 'publisher' ? '/api/v1/publisher' : '/api/v1/administrator';
    const identity = await api<{ displayName?: string; name?: string }>(
      path,
      {},
      apiKey.value.trim(),
    );
    setAccess({
      role: role.value,
      apiKey: apiKey.value.trim(),
      label: identity.displayName ?? identity.name,
    });
    savedRevision.value++;
    await router.push(destination(role.value));
  } catch (value) {
    error.value = value instanceof Error ? value.message : '验证失败';
  } finally {
    busy.value = false;
  }
}
</script>

<template>
  <section class="narrow-shell page-pad access-page">
    <h1 class="page-title">{{ workspaceLabel }}</h1>
    <p class="lead">
      选择已保存的{{ roleLabel }}身份，或输入、申请新的 API Key。Key
      仅保存在此浏览器本地，请只在受信任的个人设备上使用。
    </p>

    <div class="panel access-panel">
      <div v-if="savedAccesses.length" class="saved-access saved-access-primary">
        <span>切换已有{{ roleLabel }}身份</span>
        <div
          v-for="access in savedAccesses"
          :key="access.apiKey"
          class="saved-identity"
          :class="{ 'is-invalid': access.invalid }"
        >
          <button
            class="saved-enter"
            type="button"
            :disabled="access.invalid"
            @click="enterSaved(access)"
          >
            <strong>{{ access.label || '未命名身份' }}</strong
            ><code>{{ access.keyPrefix || '已保存的 API Key' }}…</code
            ><span v-if="access.invalid">Key 已失效</span
            ><span v-else>进入工作区<ArrowRight :size="16" /></span></button
          ><label class="saved-select" :title="`选择 ${access.label || '已保存身份'} 以删除`"
            ><input v-model="selectedKeys" type="checkbox" :value="access.apiKey" /><span
          /></label>
        </div>
        <button
          v-if="selectedAccesses.length"
          class="clear-keys"
          type="button"
          @click="removeSelectedKey"
        >
          删除选中的 {{ selectedAccesses.length }} 个{{ roleLabel }} Key
        </button>
      </div>

      <form class="access-form" @submit.prevent="enter">
        <h2>输入 API Key</h2>
        <label>API Key</label>
        <div class="key-input">
          <KeyRound :size="18" /><input
            :key="role"
            v-model="apiKey"
            :name="`${role}-api-key`"
            type="password"
            autocomplete="new-password"
            placeholder="粘贴申请时获得的密钥"
          />
        </div>
        <p v-if="error" class="form-error">{{ error }}</p>
        <button class="button primary wide" :disabled="!apiKey.trim() || busy">
          {{ busy ? '正在验证…' : '验证并进入工作台' }}
        </button>
      </form>

      <div class="access-actions">
        <div class="access-apply">
          <div>
            <strong>还没有 API Key？</strong><small>提交申请后即可进入工作台查看审核状态。</small>
          </div>
          <RouterLink class="button ghost" :to="`/apply/${role}`"
            >申请{{ roleLabel }} Key</RouterLink
          >
        </div>
      </div>
    </div>
  </section>
</template>
