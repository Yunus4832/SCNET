import { VueQueryPlugin } from '@tanstack/vue-query';
import { createApp } from 'vue';
import App from './App.vue';
import { loadRuntimeConfig } from './config';
import router from './router';
import './style.css';

await loadRuntimeConfig();

createApp(App).use(router).use(VueQueryPlugin).mount('#app');
