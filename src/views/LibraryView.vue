<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { getAuthHeaders, clearToken, isAdmin, apiBase } from '../services/auth.js'
import BookCard from '../components/BookCard.vue'

const router = useRouter()
const books = ref([])
const loading = ref(true)
const error = ref(null)
const userName = ref('')
const showAdmin = ref(false)

async function fetchUser() {
  try {
    const res = await fetch(`${apiBase}/api/me`, { headers: getAuthHeaders() })
    if (res.ok) {
      const data = await res.json()
      userName.value = data.username
      showAdmin.value = data.isAdmin
    }
  } catch {
    // ignore - user info is optional for display
  }
}

async function fetchBooks() {
  try {
    const res = await fetch(`${apiBase}/api/listBooks`, { headers: getAuthHeaders() })
    if (!res.ok) throw new Error('Failed to load books')
    books.value = await res.json()
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function signOut() {
  clearToken()
  router.push('/login')
}

onMounted(() => {
  fetchUser()
  fetchBooks()
})
</script>

<template>
  <div class="library-page">
    <header class="library-header">
      <div class="header-left">
        <h1>The Library</h1>
      </div>
      <div class="header-right">
        <span v-if="userName" class="user-name">{{ userName }}</span>
        <router-link to="/upload" class="upload-link">Upload Book</router-link>
        <router-link v-if="showAdmin" to="/admin" class="admin-link">Admin</router-link>
        <button class="logout-button" @click="signOut">Sign out</button>
      </div>
    </header>

    <main class="library-content">
      <div v-if="loading" class="status-message">Loading books...</div>
      <div v-else-if="error" class="status-message error">{{ error }}</div>
      <div v-else-if="books.length === 0" class="status-message">
        No books available yet.
      </div>
      <div v-else class="book-grid">
        <BookCard
          v-for="book in books"
          :key="book.id"
          :title="book.title"
          :author="book.author"
          :format="book.format"
          :blob-path="book.blobPath"
          :description="book.description"
        />
      </div>
    </main>
  </div>
</template>

<style scoped>
.library-page {
  max-width: 1126px;
  margin: 0 auto;
  padding: 0 20px;
}

.library-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 16px 0;
  border-bottom: 1px solid var(--border);
}

.library-header h1 {
  font-size: 28px;
  margin: 0;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
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
  text-decoration: none;
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
  transition: border-color 0.2s;
}

.logout-button:hover {
  border-color: var(--accent-border);
}

.upload-link {
  padding: 8px 16px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  text-decoration: none;
  font-size: 14px;
  font-family: var(--sans);
  transition: border-color 0.2s;
}

.upload-link:hover {
  border-color: var(--accent-border);
}

.admin-link {
  padding: 8px 16px;
  border-radius: 6px;
  border: 1px solid var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
  text-decoration: none;
  font-size: 14px;
  font-family: var(--sans);
  transition: opacity 0.2s;
}

.admin-link:hover {
  opacity: 0.8;
}

.library-content {
  padding: 32px 0;
}

.status-message {
  text-align: center;
  padding: 48px 20px;
  color: var(--text);
}

.status-message.error {
  color: #e74c3c;
}

.book-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 24px;
}
</style>
