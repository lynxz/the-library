<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { setToken, setAdmin, apiBase } from '../services/auth.js'

const router = useRouter()
const username = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

async function handleLogin() {
  error.value = ''
  loading.value = true

  try {
    const res = await fetch(`${apiBase}/api/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username: username.value, password: password.value })
    })

    const data = await res.json()

    if (!res.ok) {
      error.value = data.error || 'Login failed.'
      return
    }

    setToken(data.token)
    setAdmin(data.isAdmin)
    router.push('/')
  } catch {
    error.value = 'Unable to connect to the server.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-page">
    <div class="login-card">
      <h1>The Library</h1>
      <p>Sign in to access the book collection.</p>
      <form @submit.prevent="handleLogin" class="login-form">
        <div class="form-field">
          <label for="username">Username</label>
          <input
            id="username"
            v-model="username"
            type="text"
            autocomplete="username"
            required
          />
        </div>
        <div class="form-field">
          <label for="password">Password</label>
          <input
            id="password"
            v-model="password"
            type="password"
            autocomplete="current-password"
            required
          />
        </div>
        <p v-if="error" class="error-message">{{ error }}</p>
        <button type="submit" class="login-button" :disabled="loading">
          {{ loading ? 'Signing in...' : 'Sign in' }}
        </button>
      </form>
    </div>
  </div>
</template>

<style scoped>
.login-page {
  display: flex;
  justify-content: center;
  align-items: center;
  min-height: 100vh;
  padding: 20px;
}

.login-card {
  max-width: 400px;
  width: 100%;
  text-align: center;
}

.login-card h1 {
  font-size: 36px;
  margin-bottom: 8px;
}

.login-card p {
  margin-bottom: 32px;
  color: var(--text);
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
  text-align: left;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-field label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-h);
}

.form-field input {
  padding: 12px 14px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  font-size: 16px;
  font-family: var(--sans);
  outline: none;
  transition: border-color 0.2s;
}

.form-field input:focus {
  border-color: var(--accent-border);
}

.error-message {
  color: #e74c3c;
  font-size: 14px;
  margin: 0;
  text-align: center;
}

.login-button {
  padding: 14px 24px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--text-h);
  color: var(--bg);
  font-size: 16px;
  font-family: var(--sans);
  cursor: pointer;
  transition: opacity 0.2s;
}

.login-button:hover:not(:disabled) {
  opacity: 0.9;
}

.login-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
