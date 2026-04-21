<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getAuthHeaders, clearToken, isAdmin as checkIsAdmin, apiBase } from '../services/auth.js'

const router = useRouter()
const users = ref([])
const loading = ref(true)
const error = ref(null)
const userName = ref('')

// Create user form
const newUsername = ref('')
const newPassword = ref('')
const createError = ref('')
const createSuccess = ref('')
const creating = ref(false)

// Reset password state
const resetTarget = ref(null)
const resetPassword = ref('')
const resetError = ref('')
const resetSuccess = ref('')
const resetting = ref(false)

async function fetchUser() {
  try {
    const res = await fetch(`${apiBase}/api/me`, { headers: getAuthHeaders() })
    if (res.ok) {
      const data = await res.json()
      userName.value = data.username
    }
  } catch {
    // ignore
  }
}

async function fetchUsers() {
  try {
    const res = await fetch(`${apiBase}/api/useradmin/users`, { headers: getAuthHeaders() })
    if (!res.ok) throw new Error('Failed to load users')
    users.value = await res.json()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

async function handleCreateUser() {
  createError.value = ''
  createSuccess.value = ''
  creating.value = true

  try {
    const res = await fetch(`${apiBase}/api/useradmin/users`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
      body: JSON.stringify({ username: newUsername.value, password: newPassword.value })
    })

    const data = await res.json()

    if (!res.ok) {
      createError.value = data.error || 'Failed to create user.'
      return
    }

    createSuccess.value = `User "${data.username}" created successfully.`
    newUsername.value = ''
    newPassword.value = ''
    await fetchUsers()
  } catch {
    createError.value = 'Unable to connect to the server.'
  } finally {
    creating.value = false
  }
}

function startReset(username) {
  resetTarget.value = username
  resetPassword.value = ''
  resetError.value = ''
  resetSuccess.value = ''
}

function cancelReset() {
  resetTarget.value = null
  resetPassword.value = ''
  resetError.value = ''
  resetSuccess.value = ''
}

async function handleResetPassword() {
  resetError.value = ''
  resetSuccess.value = ''
  resetting.value = true

  try {
    const res = await fetch(`${apiBase}/api/useradmin/users/${encodeURIComponent(resetTarget.value)}/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', ...getAuthHeaders() },
      body: JSON.stringify({ newPassword: resetPassword.value })
    })

    const data = await res.json()

    if (!res.ok) {
      resetError.value = data.error || 'Failed to reset password.'
      return
    }

    resetSuccess.value = `Password for "${resetTarget.value}" reset successfully.`
    resetTarget.value = null
    resetPassword.value = ''
  } catch {
    resetError.value = 'Unable to connect to the server.'
  } finally {
    resetting.value = false
  }
}

function signOut() {
  clearToken()
  router.push('/login')
}

onMounted(() => {
  fetchUser()
  fetchUsers()
})
</script>

<template>
  <div class="admin-page">
    <header class="admin-header">
      <div class="header-left">
        <h1>Admin</h1>
        <router-link to="/" class="back-link">← Back to Library</router-link>
      </div>
      <div class="header-right">
        <span v-if="userName" class="user-name">{{ userName }}</span>
        <button class="logout-button" @click="signOut">Sign out</button>
      </div>
    </header>

    <main class="admin-content">
      <!-- Create User Section -->
      <section class="admin-section">
        <h2>Create User</h2>
        <form @submit.prevent="handleCreateUser" class="admin-form">
          <div class="form-row">
            <div class="form-field">
              <label for="new-username">Username</label>
              <input id="new-username" v-model="newUsername" type="text" required />
            </div>
            <div class="form-field">
              <label for="new-password">Password</label>
              <input id="new-password" v-model="newPassword" type="password" required minlength="8" />
            </div>
            <button type="submit" class="action-button" :disabled="creating">
              {{ creating ? 'Creating...' : 'Create' }}
            </button>
          </div>
          <p v-if="createError" class="error-message">{{ createError }}</p>
          <p v-if="createSuccess" class="success-message">{{ createSuccess }}</p>
        </form>
      </section>

      <!-- User List Section -->
      <section class="admin-section">
        <h2>Users</h2>
        <div v-if="loading" class="status-message">Loading users...</div>
        <div v-else-if="error" class="status-message error">{{ error }}</div>
        <table v-else class="user-table">
          <thead>
            <tr>
              <th>Username</th>
              <th>Role</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="user in users" :key="user.username">
              <td>{{ user.username }}</td>
              <td>{{ user.isAdmin ? 'Admin' : 'User' }}</td>
              <td>
                <button class="table-action" @click="startReset(user.username)">Reset password</button>
              </td>
            </tr>
          </tbody>
        </table>
      </section>

      <!-- Reset Password Modal -->
      <div v-if="resetTarget" class="modal-overlay" @click.self="cancelReset">
        <div class="modal">
          <h3>Reset password for "{{ resetTarget }}"</h3>
          <form @submit.prevent="handleResetPassword" class="admin-form">
            <div class="form-field">
              <label for="reset-password">New password</label>
              <input id="reset-password" v-model="resetPassword" type="password" required minlength="8" />
            </div>
            <p v-if="resetError" class="error-message">{{ resetError }}</p>
            <p v-if="resetSuccess" class="success-message">{{ resetSuccess }}</p>
            <div class="modal-actions">
              <button type="button" class="cancel-button" @click="cancelReset">Cancel</button>
              <button type="submit" class="action-button" :disabled="resetting">
                {{ resetting ? 'Resetting...' : 'Reset password' }}
              </button>
            </div>
          </form>
        </div>
      </div>
    </main>
  </div>
</template>

<style scoped>
.admin-page {
  max-width: 900px;
  margin: 0 auto;
  padding: 0 20px;
}

.admin-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 0;
  border-bottom: 1px solid var(--border);
}

.admin-header h1 {
  font-size: 28px;
  margin: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 20px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.back-link {
  color: var(--accent);
  text-decoration: none;
  font-size: 14px;
}

.back-link:hover {
  text-decoration: underline;
}

.user-name {
  color: var(--text);
  font-size: 14px;
}

.logout-button {
  padding: 8px 16px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
  transition: border-color 0.2s;
}

.logout-button:hover {
  border-color: var(--accent-border);
}

.admin-content {
  padding: 32px 0;
}

.admin-section {
  margin-bottom: 40px;
}

.admin-section h2 {
  font-size: 20px;
  margin-bottom: 16px;
  color: var(--text-h);
}

.admin-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.form-row {
  display: flex;
  gap: 12px;
  align-items: flex-end;
}

.form-field {
  display: flex;
  flex-direction: column;
  gap: 6px;
  flex: 1;
}

.form-field label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-h);
}

.form-field input {
  padding: 10px 12px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  font-size: 15px;
  font-family: var(--sans);
  outline: none;
  transition: border-color 0.2s;
}

.form-field input:focus {
  border-color: var(--accent-border);
}

.action-button {
  padding: 10px 20px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--text-h);
  color: var(--bg);
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
  transition: opacity 0.2s;
  white-space: nowrap;
  align-self: flex-end;
}

.action-button:hover:not(:disabled) {
  opacity: 0.9;
}

.action-button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.error-message {
  color: #e74c3c;
  font-size: 14px;
  margin: 0;
}

.success-message {
  color: #27ae60;
  font-size: 14px;
  margin: 0;
}

.status-message {
  text-align: center;
  padding: 24px 20px;
  color: var(--text);
}

.status-message.error {
  color: #e74c3c;
}

/* User table */
.user-table {
  width: 100%;
  border-collapse: collapse;
}

.user-table th,
.user-table td {
  padding: 12px 16px;
  text-align: left;
  border-bottom: 1px solid var(--border);
}

.user-table th {
  font-size: 13px;
  font-weight: 600;
  color: var(--text);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.user-table td {
  font-size: 15px;
  color: var(--text-h);
}

.table-action {
  padding: 6px 12px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--accent);
  font-size: 13px;
  font-family: var(--sans);
  cursor: pointer;
  transition: border-color 0.2s;
}

.table-action:hover {
  border-color: var(--accent-border);
}

/* Modal */
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.5);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 100;
}

.modal {
  background: var(--bg);
  border-radius: 12px;
  padding: 28px;
  max-width: 420px;
  width: 100%;
  box-shadow: var(--shadow);
}

.modal h3 {
  font-size: 18px;
  margin: 0 0 20px;
  color: var(--text-h);
}

.modal-actions {
  display: flex;
  gap: 12px;
  justify-content: flex-end;
  margin-top: 8px;
}

.cancel-button {
  padding: 10px 20px;
  border-radius: 8px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
  transition: border-color 0.2s;
}

.cancel-button:hover {
  border-color: var(--accent-border);
}
</style>
