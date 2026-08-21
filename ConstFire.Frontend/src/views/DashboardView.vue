<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()

function logout() {
  auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <div class="dashboard">
    <header>
      <div class="header-left">
        <div class="logo-sm">CF</div>
        <span class="title">ConstFire</span>
      </div>
      <button class="btn-logout" @click="logout">Sign out</button>
    </header>

    <main>
      <h1>Welcome, {{ auth.user?.name ?? 'User' }}</h1>
      <p class="subtitle">You are signed in. Phase 1 dashboard placeholder.</p>

      <div class="card">
        <h2>Account</h2>
        <dl>
          <dt>Email</dt>
          <dd>{{ auth.user?.email }}</dd>
          <dt>Role</dt>
          <dd>{{ auth.user?.role }}</dd>
        </dl>
      </div>
    </main>
  </div>
</template>

<style scoped>
.dashboard {
  min-height: 100vh;
  background: #f8fafc;
}

header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 2rem;
  background: #fff;
  border-bottom: 1px solid #e2e8f0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.logo-sm {
  width: 36px;
  height: 36px;
  background: linear-gradient(135deg, #ea580c, #dc2626);
  color: #fff;
  font-weight: 700;
  font-size: 0.85rem;
  border-radius: 8px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.title {
  font-weight: 600;
  color: #0f172a;
}

.btn-logout {
  padding: 0.5rem 1rem;
  background: transparent;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  color: #64748b;
  cursor: pointer;
  font-size: 0.875rem;
}

.btn-logout:hover {
  background: #f1f5f9;
  color: #0f172a;
}

main {
  max-width: 640px;
  margin: 0 auto;
  padding: 3rem 1.5rem;
}

h1 {
  margin: 0 0 0.5rem;
  color: #0f172a;
  font-size: 1.75rem;
}

.subtitle {
  color: #64748b;
  margin: 0 0 2rem;
}

.card {
  background: #fff;
  border-radius: 12px;
  padding: 1.5rem;
  border: 1px solid #e2e8f0;
}

.card h2 {
  margin: 0 0 1rem;
  font-size: 1rem;
  color: #334155;
}

dl {
  display: grid;
  grid-template-columns: 100px 1fr;
  gap: 0.75rem 1rem;
  margin: 0;
}

dt {
  color: #94a3b8;
  font-size: 0.875rem;
}

dd {
  margin: 0;
  color: #0f172a;
  font-weight: 500;
}
</style>
