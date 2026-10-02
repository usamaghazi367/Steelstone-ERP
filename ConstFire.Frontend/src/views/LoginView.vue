<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'

const router = useRouter()
const auth = useAuthStore()

const email = ref('admin@steelstoneit.com')
const password = ref('Admin@123')
const loading = ref(false)
const error = ref('')

async function handleSubmit() {
  error.value = ''
  loading.value = true
  try {
    await auth.login(email.value, password.value)
    await router.push({ name: 'module', params: { code: '01' }, query: { action: 'view' } })
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } }; code?: string; message?: string }
    if (!err.response) {
      error.value = 'Cannot reach API. Make sure the backend is running on http://localhost:5086'
    } else {
      error.value = err.response?.data?.message ?? 'Login failed. Check your credentials.'
    }
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-card">
      <div class="brand">
        <div class="logo">ST</div>
        <h1>Steelstone</h1>
        <p>Cloud ERP</p>
      </div>

      <form @submit.prevent="handleSubmit">
        <div class="field">
          <label for="email">Email</label>
          <input
            id="email"
            v-model="email"
            type="email"
            autocomplete="email"
            required
            placeholder="you@company.com"
          />
        </div>

        <div class="field">
          <label for="password">Password</label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
            placeholder="••••••••"
          />
        </div>

        <p v-if="error" class="error">{{ error }}</p>

        <button type="submit" class="btn-primary" :disabled="loading">
          {{ loading ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>

      <p class="hint">Demo: admin@constfire.com / Admin@123</p>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 1rem;
  padding: max(1rem, env(safe-area-inset-top)) max(1rem, env(safe-area-inset-right))
    max(1rem, env(safe-area-inset-bottom)) max(1rem, env(safe-area-inset-left));
  background: linear-gradient(135deg, #0f172a 0%, #1e3a5f 50%, #b45309 100%);
}

.login-card {
  width: 100%;
  max-width: 420px;
  background: #fff;
  border-radius: 16px;
  padding: 2rem 1.5rem;
  box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.35);
}

@media (min-width: 480px) {
  .login-card {
    padding: 2.5rem;
  }
}

.brand {
  text-align: center;
  margin-bottom: 2rem;
}

.logo {
  width: 56px;
  height: 56px;
  margin: 0 auto 1rem;
  background: linear-gradient(135deg, #ea580c, #dc2626);
  color: #fff;
  font-weight: 700;
  font-size: 1.25rem;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.brand h1 {
  margin: 0 0 0.25rem;
  font-size: 1.75rem;
  color: #0f172a;
}

.brand p {
  margin: 0;
  color: #64748b;
  font-size: 0.9rem;
}

.field {
  margin-bottom: 1.25rem;
}

.field label {
  display: block;
  margin-bottom: 0.4rem;
  font-size: 0.875rem;
  font-weight: 500;
  color: #334155;
}

.field input {
  width: 100%;
  padding: 0.75rem 1rem;
  min-height: 44px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  font-size: 1rem;
  transition: border-color 0.15s, box-shadow 0.15s;
  box-sizing: border-box;
}

.field input:focus {
  outline: none;
  border-color: #ea580c;
  box-shadow: 0 0 0 3px rgba(234, 88, 12, 0.15);
}

.error {
  color: #dc2626;
  font-size: 0.875rem;
  margin: 0 0 1rem;
}

.btn-primary {
  width: 100%;
  padding: 0.875rem;
  min-height: 48px;
  background: linear-gradient(135deg, #ea580c, #dc2626);
  color: #fff;
  border: none;
  border-radius: 8px;
  font-size: 1rem;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.15s, transform 0.1s;
}

.btn-primary:hover:not(:disabled) {
  opacity: 0.92;
}

.btn-primary:active:not(:disabled) {
  transform: scale(0.98);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.hint {
  margin-top: 1.5rem;
  text-align: center;
  font-size: 0.8rem;
  color: #94a3b8;
}
</style>
