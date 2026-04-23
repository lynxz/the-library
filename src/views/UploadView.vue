<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { clearToken } from '../services/auth.js'
import { postForm } from '../services/apiClient.js'
import { MAX_TAGS, MAX_TAG_LENGTH } from '../services/tags.js'
import { useTagInput } from '../composables/useTagInput.js'

const router = useRouter()
const title = ref('')
const author = ref('')
const description = ref('')
const tags = ref([])
const primaryFile = ref(null)
const secondaryFile = ref(null)
const primaryFileInput = ref(null)
const secondaryFileInput = ref(null)
const submitting = ref(false)
const error = ref('')
const success = ref('')

const { tagInput, addTagFromInput, removeTag, onTagKeyDown, resetTagInput } = useTagInput({
  tagsRef: tags,
  errorRef: error,
  maxTags: MAX_TAGS,
  maxTagLength: MAX_TAG_LENGTH
})

function onFileChange(e) {
  primaryFile.value = e.target.files[0] || null
}

function onSecondaryFileChange(e) {
  secondaryFile.value = e.target.files[0] || null
}

function getFormatFromFileName(name) {
  const ext = name.split('.').pop()?.toLowerCase()
  if (ext === 'pdf') return 'PDF'
  if (ext === 'epub') return 'EPUB'
  return ''
}

async function handleSubmit() {
  error.value = ''
  success.value = ''

  if (!title.value.trim() || !author.value.trim()) {
    error.value = 'Title and author are required.'
    return
  }

  if (!primaryFile.value) {
    error.value = 'Please select a file to upload.'
    return
  }

  const primaryFormat = getFormatFromFileName(primaryFile.value.name)
  if (!primaryFormat) {
    error.value = 'Only .pdf and .epub files are allowed.'
    return
  }

  const secondaryFormat = secondaryFile.value ? getFormatFromFileName(secondaryFile.value.name) : ''
  if (secondaryFile.value && !secondaryFormat) {
    error.value = 'Optional second file must be .pdf or .epub.'
    return
  }

  if (secondaryFile.value && primaryFormat === secondaryFormat) {
    error.value = 'Primary and secondary files must be different formats.'
    return
  }

  submitting.value = true

  try {
    const createForm = new FormData()
    createForm.append('title', title.value.trim())
    createForm.append('author', author.value.trim())
    createForm.append('description', description.value.trim())
    createForm.append('tags', tags.value.join(','))
    createForm.append('file', primaryFile.value)

    const created = await postForm('/api/uploadBook', createForm)

    if (secondaryFile.value) {
      const addForm = new FormData()
      addForm.append('bookId', created.id)
      addForm.append('file', secondaryFile.value)

      try {
        await postForm('/api/uploadBook', addForm)
      } catch (e) {
        throw new Error(e.message || 'Primary format uploaded, but failed to upload second format.')
      }
    }

    success.value = secondaryFile.value
      ? 'Book uploaded with both formats successfully!'
      : 'Book uploaded successfully!'
    title.value = ''
    author.value = ''
    description.value = ''
    tags.value = []
    resetTagInput()
    primaryFile.value = null
    secondaryFile.value = null
    if (primaryFileInput.value) primaryFileInput.value.value = ''
    if (secondaryFileInput.value) secondaryFileInput.value.value = ''
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
          <label for="file">Primary file <span class="required">*</span></label>
          <input
            id="file"
            ref="primaryFileInput"
            type="file"
            accept=".pdf,.epub"
            :disabled="submitting"
            @change="onFileChange"
          />
          <p class="hint">Upload either PDF or EPUB first.</p>
        </div>

        <div class="form-group">
          <label for="file-secondary">Optional second file</label>
          <input
            id="file-secondary"
            ref="secondaryFileInput"
            type="file"
            accept=".pdf,.epub"
            :disabled="submitting"
            @change="onSecondaryFileChange"
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
