<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { clearToken } from '../services/auth.js'
import { get } from '../services/apiClient.js'
import { useTagInput } from '../composables/useTagInput.js'
import BookCard from '../components/BookCard.vue'

const router = useRouter()
const books = ref([])
const loading = ref(true)
const error = ref(null)
const userName = ref('')
const showAdmin = ref(false)
const selectedTags = ref([])
const knownTags = ref([])

const { tagInput, addTagFromInput, removeTag } = useTagInput({ tagsRef: selectedTags })

async function fetchUser() {
  try {
    const data = await get('/api/me')
    userName.value = data.username
    showAdmin.value = data.isAdmin
  } catch {
    // ignore - user info is optional for display
  }
}

function updateKnownTags(bookList) {
  const tags = bookList.flatMap((book) => Array.isArray(book.tags) ? book.tags : [])
  knownTags.value = [...new Set([...knownTags.value, ...tags])].sort((a, b) => a.localeCompare(b))
}

async function fetchBooks() {
  try {
    const query = new URLSearchParams()
    if (selectedTags.value.length) {
      query.set('tags', selectedTags.value.join(','))
    }

    const url = query.toString() ? `/api/listBooks?${query.toString()}` : '/api/listBooks'
    books.value = await get(url)
    updateKnownTags(books.value)
  } catch (e) {
    error.value = e.message
  } finally {
    loading.value = false
  }
}

function addSelectedTag() {
  const added = addTagFromInput()
  if (!added) {
    return
  }

  loading.value = true
  fetchBooks()
}

function removeSelectedTag(tag) {
  const removed = removeTag(tag)
  if (!removed) {
    return
  }

  loading.value = true
  fetchBooks()
}

function onTagKeyDown(e) {
  if (e.key === 'Enter' || e.key === ',') {
    e.preventDefault()
    addSelectedTag()
  }
}

function toggleSuggestedTag(tag) {
  if (selectedTags.value.includes(tag)) {
    removeSelectedTag(tag)
    return
  }

  selectedTags.value.push(tag)
  loading.value = true
  fetchBooks()
}

function onBookTagsUpdated(bookId, tags) {
  const idx = books.value.findIndex((book) => book.id === bookId)
  if (idx >= 0) {
    books.value[idx] = { ...books.value[idx], tags }
  }
  updateKnownTags(books.value)
}

function onBookFormatsUpdated(bookId, payload) {
  const idx = books.value.findIndex((book) => book.id === bookId)
  if (idx >= 0) {
    books.value[idx] = {
      ...books.value[idx],
      formats: payload.formats,
      blobPaths: payload.blobPaths
    }
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
      <section class="filter-panel">
        <div class="filter-input">
          <label for="tag-filter">Filter by tags (match all)</label>
          <div class="tag-entry">
            <span v-for="tag in selectedTags" :key="tag" class="selected-tag">
              {{ tag }}
              <button type="button" @click="removeSelectedTag(tag)">x</button>
            </span>
            <input
              id="tag-filter"
              v-model="tagInput"
              type="text"
              placeholder="Type tag and press Enter"
              @keydown="onTagKeyDown"
              @blur="addSelectedTag"
            />
          </div>
        </div>
        <div v-if="knownTags.length" class="suggested-tags">
          <button
            v-for="tag in knownTags"
            :key="tag"
            type="button"
            class="suggested-tag"
            :class="{ active: selectedTags.includes(tag) }"
            @click="toggleSuggestedTag(tag)"
          >
            {{ tag }}
          </button>
        </div>
      </section>

      <div v-if="loading" class="status-message">Loading books...</div>
      <div v-else-if="error" class="status-message error">{{ error }}</div>
      <div v-else-if="books.length === 0" class="status-message">
        No books available yet.
      </div>
      <div v-else class="book-grid">
        <BookCard
          v-for="book in books"
          :key="book.id"
          :id="book.id"
          :title="book.title"
          :author="book.author"
          :format="book.format"
          :blob-path="book.blobPath"
          :formats="book.formats || []"
          :blob-paths="book.blobPaths || {}"
          :description="book.description"
          :tags="book.tags || []"
          @tags-updated="(tags) => onBookTagsUpdated(book.id, tags)"
          @formats-updated="(payload) => onBookFormatsUpdated(book.id, payload)"
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

.filter-panel {
  margin-bottom: 22px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.filter-input label {
  display: block;
  margin-bottom: 6px;
  font-size: 13px;
  color: var(--text);
}

.tag-entry {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 8px;
}

.tag-entry input {
  border: none;
  outline: none;
  background: transparent;
  color: var(--text-h);
  min-width: 200px;
  flex: 1;
  font-size: 14px;
  font-family: var(--sans);
}

.selected-tag {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 4px 10px;
  border-radius: 999px;
  border: 1px solid var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
  font-size: 12px;
}

.selected-tag button {
  border: none;
  background: transparent;
  color: inherit;
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.suggested-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}

.suggested-tag {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text);
  border-radius: 999px;
  padding: 6px 12px;
  font-size: 12px;
  cursor: pointer;
}

.suggested-tag.active {
  border-color: var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
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
