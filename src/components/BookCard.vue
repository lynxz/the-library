<script setup>
import { ref } from 'vue'
import { getAuthHeaders, apiBase } from '../services/auth.js'

const MAX_TAGS = 10
const MAX_TAG_LENGTH = 24

const props = defineProps({
  id: { type: String, required: true },
  title: { type: String, required: true },
  author: { type: String, required: true },
  format: { type: String, required: true },
  blobPath: { type: String, required: true },
  description: { type: String, default: '' },
  tags: { type: Array, default: () => [] }
})

const emit = defineEmits(['tags-updated'])

const downloading = ref(false)
const editingTags = ref(false)
const tagsDraft = ref([])
const tagInput = ref('')
const savingTags = ref(false)
const tagError = ref('')

function normalizeTag(value) {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9_-]/g, '')
}

function startEditTags() {
  tagsDraft.value = Array.isArray(props.tags) ? [...props.tags] : []
  tagInput.value = ''
  tagError.value = ''
  editingTags.value = true
}

function cancelEditTags() {
  editingTags.value = false
  tagsDraft.value = []
  tagInput.value = ''
  tagError.value = ''
}

function addTagFromInput() {
  const normalized = normalizeTag(tagInput.value)
  if (!normalized) {
    tagInput.value = ''
    return
  }

  if (normalized.length > MAX_TAG_LENGTH) {
    tagError.value = `Each tag must be ${MAX_TAG_LENGTH} characters or fewer.`
    return
  }

  if (tagsDraft.value.includes(normalized)) {
    tagInput.value = ''
    return
  }

  if (tagsDraft.value.length >= MAX_TAGS) {
    tagError.value = `A maximum of ${MAX_TAGS} tags is allowed.`
    return
  }

  tagError.value = ''
  tagsDraft.value.push(normalized)
  tagInput.value = ''
}

function removeTagDraft(tag) {
  tagsDraft.value = tagsDraft.value.filter((t) => t !== tag)
}

function onTagKeyDown(e) {
  if (e.key === 'Enter' || e.key === ',') {
    e.preventDefault()
    addTagFromInput()
  }
}

async function saveTags() {
  tagError.value = ''
  savingTags.value = true
  try {
    const res = await fetch(`${apiBase}/api/bookTags/${encodeURIComponent(props.id)}`, {
      method: 'PUT',
      headers: {
        ...getAuthHeaders(),
        'Content-Type': 'application/json'
      },
      body: JSON.stringify({ tags: tagsDraft.value })
    })

    const data = await res.json().catch(() => ({}))
    if (!res.ok) {
      throw new Error(data.error || 'Failed to update tags.')
    }

    emit('tags-updated', data.tags || [...tagsDraft.value])
    editingTags.value = false
  } catch (e) {
    tagError.value = e.message
  } finally {
    savingTags.value = false
  }
}

async function download() {
  downloading.value = true
  try {
    const res = await fetch(
      `${apiBase}/api/getDownloadUrl?blobPath=${encodeURIComponent(props.blobPath)}`,
      { headers: getAuthHeaders() }
    )
    if (!res.ok) throw new Error('Failed to get download link')
    const data = await res.json()
    window.open(data.url, '_blank')
  } catch (e) {
    alert('Download failed: ' + e.message)
  } finally {
    downloading.value = false
  }
}
</script>

<template>
  <div class="book-card">
    <div class="book-info">
      <span class="format-badge">{{ format }}</span>
      <h2 class="book-title">{{ title }}</h2>
      <p class="book-author">{{ author }}</p>
      <div v-if="editingTags" class="tags-editor">
        <div class="tags-row">
          <span v-for="tag in tagsDraft" :key="tag" class="tag-pill tag-edit-pill">
            {{ tag }}
            <button type="button" class="tag-remove" :disabled="savingTags" @click="removeTagDraft(tag)">x</button>
          </span>
        </div>
        <input
          v-model="tagInput"
          type="text"
          placeholder="Type tag and press Enter"
          :disabled="savingTags"
          @keydown="onTagKeyDown"
          @blur="addTagFromInput"
        />
        <p class="hint">Up to 10 tags. Lowercase letters, numbers, _ and - are allowed.</p>
        <p v-if="tagError" class="tag-error">{{ tagError }}</p>
        <div class="tag-actions">
          <button class="small-button" :disabled="savingTags" @click="saveTags">{{ savingTags ? 'Saving...' : 'Save tags' }}</button>
          <button class="small-button secondary" :disabled="savingTags" @click="cancelEditTags">Cancel</button>
        </div>
      </div>
      <div v-else>
        <div v-if="tags.length" class="tags-row">
          <span v-for="tag in tags" :key="tag" class="tag-pill">{{ tag }}</span>
        </div>
        <button class="edit-tags-button" @click="startEditTags">Edit tags</button>
      </div>
      <p v-if="description" class="book-description">{{ description }}</p>
    </div>
    <button class="download-button" :disabled="downloading" @click="download">
      {{ downloading ? 'Preparing...' : 'Download' }}
    </button>
  </div>
</template>

<style scoped>
.book-card {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  padding: 24px;
  border-radius: 10px;
  border: 1px solid var(--border);
  background: var(--bg);
  transition: box-shadow 0.2s, border-color 0.2s;
}

.book-card:hover {
  border-color: var(--accent-border);
  box-shadow: var(--shadow);
}

.book-info {
  flex: 1;
}

.format-badge {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  background: var(--accent-bg);
  color: var(--accent);
  margin-bottom: 12px;
}

.book-title {
  font-size: 20px;
  margin: 0 0 4px;
}

.book-author {
  font-size: 14px;
  color: var(--text);
  margin: 0 0 8px;
}

.book-description {
  font-size: 14px;
  color: var(--text);
  margin: 0;
  line-height: 1.5;
}

.tags-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin: 0 0 10px;
}

.tag-pill {
  display: inline-flex;
  align-items: center;
  padding: 2px 8px;
  border-radius: 999px;
  border: 1px solid var(--border);
  background: var(--bg-soft);
  color: var(--text);
  font-size: 12px;
}

.tag-edit-pill {
  gap: 6px;
}

.tag-remove {
  border: none;
  background: transparent;
  color: inherit;
  cursor: pointer;
  padding: 0;
  line-height: 1;
}

.tags-editor input {
  margin-top: 6px;
  width: 100%;
  border: 1px solid var(--border);
  border-radius: 6px;
  padding: 8px 10px;
  background: var(--bg);
  color: var(--text-h);
  font-size: 13px;
  font-family: var(--sans);
}

.hint {
  margin: 6px 0 0;
  font-size: 12px;
  color: var(--text);
}

.tag-error {
  margin: 6px 0 0;
  color: #b71c1c;
  font-size: 12px;
}

.tag-actions {
  margin-top: 8px;
  display: flex;
  gap: 8px;
}

.small-button {
  border: 1px solid var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 12px;
  cursor: pointer;
}

.small-button.secondary {
  border-color: var(--border);
  background: var(--bg);
  color: var(--text);
}

.edit-tags-button {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text);
  border-radius: 6px;
  padding: 6px 10px;
  font-size: 12px;
  cursor: pointer;
  margin-bottom: 10px;
}

.download-button {
  margin-top: 16px;
  padding: 10px 20px;
  border-radius: 6px;
  border: 1px solid var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
  transition: background 0.2s;
}

.download-button:hover:not(:disabled) {
  background: var(--accent);
  color: #fff;
}

.download-button:disabled {
  opacity: 0.6;
  cursor: wait;
}
</style>
