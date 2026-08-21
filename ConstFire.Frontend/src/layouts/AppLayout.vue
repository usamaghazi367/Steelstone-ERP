<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import type { ModuleSummary } from '../types/modules'

defineProps<{ modules: ModuleSummary[] }>()

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const sidebarOpen = ref(false)

const currentCode = computed(() => route.params.code as string | undefined)

function isActive(code: string) {
  return currentCode.value === code
}

function goModule(code: string) {
  sidebarOpen.value = false
  router.push({ name: 'module', params: { code }, query: { action: 'view' } })
}

function logout() {
  auth.logout()
  router.push({ name: 'login' })
}

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
}

watch(
  () => route.fullPath,
  () => {
    sidebarOpen.value = false
  },
)
</script>

<template>
  <div class="app-layout">
    <div
      v-if="sidebarOpen"
      class="sidebar-backdrop"
      aria-hidden="true"
      @click="sidebarOpen = false"
    />

    <aside class="sidebar" :class="{ open: sidebarOpen }">
      <div class="sidebar-brand">
        <div class="logo">CF</div>
        <div class="brand-text">
          <strong>Steelstone IT</strong>
          <span>Cloud ERP</span>
        </div>
        <button
          type="button"
          class="sidebar-close"
          aria-label="Close menu"
          @click="sidebarOpen = false"
        >
          ×
        </button>
      </div>

      <nav class="menu">
        <p class="menu-label">Modules</p>
        <button
          v-for="mod in modules"
          :key="mod.code"
          class="menu-item"
          :class="{ active: isActive(mod.code) }"
          @click="goModule(mod.code)"
        >
          <span class="code">{{ mod.code }}</span>
          <span class="label">{{ mod.name }}</span>
        </button>
      </nav>
    </aside>

    <div class="main-area">
      <header class="topbar">
        <div class="topbar-left">
          <button
            type="button"
            class="menu-toggle"
            aria-label="Open menu"
            :aria-expanded="sidebarOpen"
            @click="toggleSidebar"
          >
            <span /><span /><span />
          </button>
          <slot name="title" />
        </div>
        <div class="topbar-right">
          <span class="user">{{ auth.user?.name }}</span>
          <button type="button" class="btn-logout" @click="logout">Sign out</button>
        </div>
      </header>
      <main class="content">
        <slot />
      </main>
    </div>
  </div>
</template>

<style scoped>
.app-layout {
  display: flex;
  min-height: 100vh;
  min-height: 100dvh;
  background: #f1f5f9;
}

.sidebar {
  width: 300px;
  max-width: min(300px, 88vw);
  background: #0f172a;
  color: #e2e8f0;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  z-index: 200;
}

.sidebar-brand {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  padding: 1.25rem 1rem;
  border-bottom: 1px solid #1e293b;
}

.brand-text {
  flex: 1;
  min-width: 0;
}

.logo {
  width: 40px;
  height: 40px;
  background: linear-gradient(135deg, #ea580c, #dc2626);
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  color: #fff;
  flex-shrink: 0;
}

.sidebar-brand strong {
  display: block;
  font-size: 0.95rem;
  color: #fff;
}

.sidebar-brand span {
  font-size: 0.75rem;
  color: #94a3b8;
}

.sidebar-close {
  display: none;
  width: 44px;
  height: 44px;
  border: none;
  border-radius: 8px;
  background: #1e293b;
  color: #e2e8f0;
  font-size: 1.5rem;
  line-height: 1;
  cursor: pointer;
  flex-shrink: 0;
}

.menu {
  flex: 1;
  overflow-y: auto;
  padding: 0.75rem 0.5rem 1.5rem;
  -webkit-overflow-scrolling: touch;
}

.menu-label {
  margin: 0 0 0.5rem 0.75rem;
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  color: #64748b;
}

.menu-item {
  width: 100%;
  display: flex;
  align-items: flex-start;
  gap: 0.6rem;
  padding: 0.75rem;
  min-height: 44px;
  margin-bottom: 2px;
  border: none;
  border-radius: 8px;
  background: transparent;
  color: #cbd5e1;
  text-align: left;
  cursor: pointer;
  transition: background 0.15s;
}

.menu-item:hover {
  background: #1e293b;
}

.menu-item.active {
  background: #ea580c;
  color: #fff;
}

.menu-item .code {
  font-size: 0.75rem;
  font-weight: 700;
  min-width: 1.5rem;
  opacity: 0.85;
}

.menu-item .label {
  font-size: 0.78rem;
  line-height: 1.35;
}

.sidebar-backdrop {
  display: none;
}

.main-area {
  flex: 1;
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 0.75rem 1rem;
  background: #fff;
  border-bottom: 1px solid #e2e8f0;
  position: sticky;
  top: 0;
  z-index: 50;
}

.topbar-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  min-width: 0;
  flex: 1;
}

.menu-toggle {
  display: none;
  flex-direction: column;
  justify-content: center;
  gap: 5px;
  width: 44px;
  height: 44px;
  padding: 0;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #fff;
  cursor: pointer;
  flex-shrink: 0;
}

.menu-toggle span {
  display: block;
  width: 18px;
  height: 2px;
  margin: 0 auto;
  background: #334155;
  border-radius: 1px;
}

.topbar-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-shrink: 0;
}

.user {
  font-size: 0.875rem;
  color: #64748b;
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.btn-logout {
  padding: 0.5rem 0.875rem;
  min-height: 44px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: #fff;
  color: #64748b;
  cursor: pointer;
  font-size: 0.8rem;
}

.btn-logout:hover {
  background: #f8fafc;
  color: #0f172a;
}

.content {
  flex: 1;
  padding: 1rem;
  overflow: auto;
  -webkit-overflow-scrolling: touch;
}

@media (max-width: 900px) {
  .sidebar {
    position: fixed;
    top: 0;
    left: 0;
    height: 100vh;
    height: 100dvh;
    transform: translateX(-100%);
    transition: transform 0.25s ease;
    box-shadow: 4px 0 24px rgba(0, 0, 0, 0.2);
  }

  .sidebar.open {
    transform: translateX(0);
  }

  .sidebar-backdrop {
    display: block;
    position: fixed;
    inset: 0;
    background: rgba(15, 23, 42, 0.45);
    z-index: 150;
  }

  .sidebar-close {
    display: flex;
    align-items: center;
    justify-content: center;
  }

  .menu-toggle {
    display: flex;
  }

  .content {
    padding: 0.75rem;
  }
}

@media (max-width: 480px) {
  .user {
    display: none;
  }

  .topbar {
    padding: 0.5rem 0.75rem;
  }
}
</style>
