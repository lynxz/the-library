<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { getAuthHeaders, clearToken, apiBase } from '../services/auth.js'
import { MAX_TAGS, MAX_TAG_LENGTH, normalizeTag } from '../services/tags.js'

const router = useRouter()
const title = ref('')
const author = ref('')
const description = ref('')
const tags = ref([])
const tagInput = ref('')
const file = ref(null)
const submitting = ref(false)
const error = ref('')
const success = ref('')

function onFileChange(e) {
  file.value = e.target.files[0] || null
}

function addTagFromInput() {
  const normalized = normalizeTag(tagInput.value)
  if (!normalized) {
    tagInput.value = ''
    return
  }

  if (normalized.length > MAX_TAG_LENGTH) {
    error.value = `Each tag must be ${MAX_TAG_LENGTH} characters or fewer.`
    return
  }

  if (tags.value.includes(normalized)) {
    tagInput.value = ''
    return
  }

  if (tags.value.length >= MAX_TAGS) {
    error.value = `A maximum of ${MAX_TAGS} tags is allowed.`
    return
  }

  tags.value.push(normalized)
  tagInput.value = ''
}

function removeTag(tag) {
  tags.value = tags.value.filter((t) => t !== tag)
}

function onTagKeyDown(e) {
  if (e.key === 'Enter' || e.key === ',') {
    e.preventDefault()
    addTagFromInput()
  }
}

async function handleSubmit() {
  error.value = ''
  success.value = ''

  if (!title.value.trim() || !author.value.trim()) {
    error.value = 'Title and author are required.'
    return
  }

  if (!file.value) {
    error.value = 'Please select a file to upload.'
    return
  }

  const ext = file.value.name.split('.').pop()?.toLowerCase()
  if (ext !== 'pdf' && ext !== 'epub') {
    error.value = 'Only .pdf and .epub files are allowed.'
    return
  }

  submitting.value = true

  try {
    const formData = new FormData()
    formData.append('title', title.value.trim())
    formData.append('author', author.value.trim())
    formData.append('description', description.value.trim())
    formData.append('tags', tags.value.join(','))
    formData.append('file', file.value)

    const res = await fetch(`${apiBase}/api/uploadBook`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: formData
    })

    if (!res.ok) {
      const data = await res.json().catch(() => ({}))
      throw new Error(data.error || 'Upload failed.')
    }

    success.value = 'Book uploaded successfully!'
    title.value = ''
    author.value = ''
    description.value = ''
    tags.value = []
    tagInput.value = ''
    file.value = null
    // Reset the file input
    const fileInput = document.querySelector('.file-input')
    if (fileInput) fileInput.value = ''
  } catch (e) {
    error.value = e.message
  } finally {
    submitting.value = false
  }
}

function signOut() {
  clearToken()
  router.push('/login')
}
</script>

<template>
  <div class="upload-page">
    <header class="upload-header">
      <div class="header-left">
        <router-link to="/" class="back-link">← Back to Library</router-link>
        <h1>Upload a Book</h1>
      </div>
      <div class="header-right">
        <button class="logout-button" @click="signOut">Sign out</button>
      </div>
    </header>

    <main class="upload-content">
      <form class="upload-form" @submit.prevent="handleSubmit">
        <div class="form-group">
          <label for="title">Title <span class="required">*</span></label>
          <input
            id="title"
            v-model="title"
            type="text"
            placeholder="Book title"
            :disabled="submitting"
          />
        </div>

        <div class="form-group">
          <label for="author">Author <span class="required">*</span></label>
          <input
            id="author"
            v-model="author"
            type="text"
            placeholder="Author name"
            :disabled="submitting"
          />
        </div>

        <div class="form-group">
          <label for="description">Description</label>
          <textarea
            id="description"
            v-model="description"
            placeholder="A brief description of the book (optional)"
            rows="4"
            :disabled="submitting"
          />
        </div>

        <div class="form-group">
          <label for="tags">Tags</label>
          <div class="tags-input-wrapper">
            <span v-for="tag in tags" :key="tag" class="tag-chip">
              {{ tag }}
              <button
                type="button"
                class="tag-remove"
                :disabled="submitting"
                @click="removeTag(tag)"
              >
                x
              </button>
            </span>
            <input
              id="tags"
              v-model="tagInput"
              type="text"
              placeholder="Type tag and press Enter"
              :disabled="submitting"
              @keydown="onTagKeyDown"
              @blur="addTagFromInput"
            />
          </div>
          <p class="hint">Up to 10 tags. Lowercase letters, numbers, _, -, + and # are allowed.</p>
        </div>

        <div class="form-group">
          <label for="file">File <span class="required">*</span></label>
          <input
            id="file"
            class="file-input"
            type="file"
            accept=".pdf,.epub"
            :disabled="submitting"
            @change="onFileChange"
          />
          <p class="hint">Accepted formats: PDF, EPUB (max 50 MB)</p>
        </div>

        <div v-if="error" class="message error">{{ error }}</div>
        <div v-if="success" class="message success">{{ success }}</div>

        <button type="submit" class="submit-button" :disabled="submitting">
          {{ submitting ? 'Uploading...' : 'Upload Book' }}
        </button>
      </form>
    </main>
  </div>
</template>

<style scoped>
.upload-page {
  max-width: 640px;
  margin: 0 auto;
  padding: 0 20px;
}

.upload-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 16px 0;
  border-bottom: 1px solid var(--border);
}

.header-left {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.header-right {
  display: flex;
  align-items: center;
  gap: 16px;
  padding-top: 4px;
}

.back-link {
  font-size: 14px;
  color: var(--accent);
  text-decoration: none;
}

.back-link:hover {
  text-decoration: underline;
}

.upload-header h1 {
  font-size: 28px;
  margin: 0;
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

.upload-content {
  padding: 32px 0;
}

.upload-form {
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.form-group label {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-h);
}

.required {
  color: #e74c3c;
}

.form-group input[type="text"],
.form-group textarea {
  padding: 10px 12px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  font-size: 14px;
  font-family: var(--sans);
  transition: border-color 0.2s;
}

.tags-input-wrapper {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  padding: 8px;
  border-radius: 6px;
  border: 1px solid var(--border);
  background: var(--bg);
}

.tags-input-wrapper:focus-within {
  border-color: var(--accent-border);
}

.tags-input-wrapper input {
  border: none;
  outline: none;
  background: transparent;
  color: var(--text-h);
  min-width: 180px;
  flex: 1;
  font-size: 14px;
  font-family: var(--sans);
}

.tag-chip {
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

.tag-remove {
  border: none;
  background: transparent;
  color: inherit;
  cursor: pointer;
  font-size: 12px;
  line-height: 1;
  padding: 0;
}

.form-group input[type="text"]:focus,
.form-group textarea:focus {
  outline: none;
  border-color: var(--accent-border);
}

.form-group textarea {
  resize: vertical;
}

.file-input {
  font-size: 14px;
  color: var(--text);
}

.hint {
  font-size: 12px;
  color: var(--text);
  margin: 0;
}

.message {
  padding: 10px 14px;
  border-radius: 6px;
  font-size: 14px;
}

.message.error {
  background: #fdecea;
  color: #b71c1c;
  border: 1px solid #f5c6cb;
}

.message.success {
  background: #e8f5e9;
  color: #2e7d32;
  border: 1px solid #c8e6c9;
}

.submit-button {
  padding: 12px 24px;
  border-radius: 6px;
  border: 1px solid var(--accent-border);
  background: var(--accent);
  color: #fff;
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
  transition: opacity 0.2s;
  align-self: flex-start;
}

.submit-button:hover:not(:disabled) {
  opacity: 0.9;
}

.submit-button:disabled {
  opacity: 0.6;
  cursor: wait;
}
</style>
