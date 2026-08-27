import { createRouter, createWebHistory } from 'vue-router';
import { api, getAccess, setActiveRole } from './api';
import AccessPage from './pages/AccessPage.vue';
import AdminPage from './pages/AdminPage.vue';
import ApplyPage from './pages/ApplyPage.vue';
import CatalogPage from './pages/CatalogPage.vue';
import InitializePage from './pages/InitializePage.vue';
import PublisherPage from './pages/PublisherPage.vue';

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: CatalogPage },
    { path: '/access', redirect: '/' },
    { path: '/publisher/access', component: AccessPage, meta: { accessRole: 'publisher' } },
    { path: '/admin/access', component: AccessPage, meta: { accessRole: 'administrator' } },
    { path: '/apply/:role', component: ApplyPage },
    { path: '/initialize', component: InitializePage },
    { path: '/publisher', component: PublisherPage, meta: { role: 'publisher' } },
    { path: '/admin', component: AdminPage, meta: { role: 'administrator' } },
  ],
  scrollBehavior: () => ({ top: 0 }),
});

let initializationRequired: boolean | undefined;
async function requiresInitialization(): Promise<boolean> {
  if (initializationRequired !== undefined) return initializationRequired;
  try {
    initializationRequired = (
      await api<{ required: boolean }>('/api/v1/administrators/initialization')
    ).required;
  } catch {
    initializationRequired = false;
  }
  return initializationRequired;
}
export function resetInitializationRequirement(): void {
  initializationRequired = undefined;
}

router.beforeEach(async (to) => {
  if (to.path !== '/initialize' && (await requiresInitialization())) return '/initialize';
  const role = to.meta.role;
  if (role !== 'publisher' && role !== 'administrator') return;
  if (!getAccess(role))
    return {
      path: role === 'publisher' ? '/publisher/access' : '/admin/access',
      query: { next: to.fullPath },
    };
  setActiveRole(role);
});

window.addEventListener('scnet-content-key-invalid', (event) => {
  const role = (event as CustomEvent<{ role?: unknown }>).detail?.role;
  if (role !== 'publisher' && role !== 'administrator') return;
  const accessPath = role === 'publisher' ? '/publisher/access' : '/admin/access';
  if (router.currentRoute.value.path !== accessPath) void router.replace(accessPath);
});

export default router;
