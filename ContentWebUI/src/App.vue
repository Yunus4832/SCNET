<script setup lang="ts">
import { Boxes } from 'lucide-vue-next';
import { computed, onMounted, ref } from 'vue';
import { useRoute } from 'vue-router';
import { api, getSavedAccesses, getSavedRoles, updateAccessLabel } from './api';

const route = useRoute();
const accessRevision = ref(0);
const insecureTransport = window.location.protocol === 'http:';
const publisherAccesses = computed(() => {
  route.fullPath;
  accessRevision.value;
  return getSavedAccesses('publisher');
});
const administratorAccesses = computed(() => {
  route.fullPath;
  accessRevision.value;
  return getSavedAccesses('administrator');
});
function workspaceTarget(role: 'publisher' | 'administrator', count: number) {
  if (count > 0) return role === 'publisher' ? '/publisher' : '/admin';
  return role === 'publisher' ? '/publisher/access' : '/admin/access';
}

onMounted(async () => {
  await Promise.all(
    getSavedRoles().map(async (role) => {
      await Promise.all(
        getSavedAccesses(role)
          .filter((access) => !access.label)
          .map(async (access) => {
            const path = role === 'publisher' ? '/api/v1/publisher' : '/api/v1/administrator';
            try {
              const identity = await api<{ displayName?: string; name?: string }>(
                path,
                {},
                access.apiKey,
              );
              updateAccessLabel(role, access.apiKey, identity.displayName ?? identity.name);
            } catch {
              // The stored Key may be inactive, revoked, or belong to another configured server.
            }
          }),
      );
    }),
  );
  accessRevision.value++;
});
</script>

<template>
  <header class="topbar">
    <RouterLink class="brand" to="/">
      <span class="brand-mark"><Boxes :size="20" /></span>
      <span>SCNET <b>CONTENT</b></span>
    </RouterLink>
    <nav>
      <RouterLink class="nav-tab" to="/">内容广场</RouterLink>
      <RouterLink class="nav-tab" :to="workspaceTarget('publisher', publisherAccesses.length)"
        >内容发布</RouterLink
      >
      <RouterLink
        class="nav-tab"
        :to="workspaceTarget('administrator', administratorAccesses.length)"
        >内容管理</RouterLink
      >
    </nav>
  </header>
  <main>
    <div v-if="insecureTransport" class="transport-warning" role="alert">
      当前使用
      HTTP，通信内容未加密。匿名浏览和下载可以继续使用；请勿在不可信网络中输入管理员或发布者 Key。
    </div>
    <RouterView />
  </main>
  <footer>SCNET Content</footer>
</template>
