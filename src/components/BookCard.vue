<script setup>
import { computed, ref, watch } from 'vue'
import { getAuthHeaders, apiBase } from '../services/auth.js'
import { MAX_TAGS, MAX_TAG_LENGTH, normalizeTag } from '../services/tags.js'
import ConfirmModal from './ConfirmModal.vue'
import AddFormatModal from './AddFormatModal.vue'

const props = defineProps({
  id: { type: String, required: true },
  title: { type: String, required: true },
  author: { type: String, required: true },
  format: { type: String, default: '' },
  blobPath: { type: String, default: '' },
  formats: { type: Array, default: () => [] },
  blobPaths: { type: Object, default: () => ({}) },
  description: { type: String, default: '' },
  tags: { type: Array, default: () => [] }
})

const emit = defineEmits(['tags-updated', 'formats-updated'])

const downloading = ref(false)
const editingTags = ref(false)
const tagsDraft = ref([])
const tagInput = ref('')
const savingTags = ref(false)
const tagError = ref('')
const selectedFormat = ref('')
const addFormatFile = ref(null)
const addingFormat = ref(false)
const formatError = ref('')
const showAddFormatModal = ref(false)
const showReplaceConfirm = ref(false)
const replaceConfirmMessage = ref('')
const pendingReplaceFormat = ref('')
let pendingReplaceFile = null

const availableFormats = computed(() => {
  const normalized = (Array.isArray(props.formats) ? props.formats : [])
    .map((f) => String(f || '').toUpperCase())
    .filter((f) => f === 'PDF' || f === 'EPUB')

  if (normalized.length > 0) {
    return [...new Set(normalized)]
  }

  const legacy = String(props.format || '').toUpperCase()
  return legacy === 'PDF' || legacy === 'EPUB' ? [legacy] : []
})

const blobPathByFormat = computed(() => {
  const mapped = {}
  if (props.blobPaths && typeof props.blobPaths === 'object') {
    for (const [format, path] of Object.entries(props.blobPaths)) {
      const normalizedFormat = String(format || '').toUpperCase()
      if ((normalizedFormat === 'PDF' || normalizedFormat === 'EPUB') && typeof path === 'string' && path) {
        mapped[normalizedFormat] = path
      }
    }
  }

  if (Object.keys(mapped).length === 0) {
    const legacy = String(props.format || '').toUpperCase()
    if ((legacy === 'PDF' || legacy === 'EPUB') && props.blobPath) {
      mapped[legacy] = props.blobPath
    }
  }

  return mapped
})

const missingFormat = computed(() => {
  const set = new Set(availableFormats.value)
  if (set.has('PDF') && !set.has('EPUB')) return 'EPUB'
  if (set.has('EPUB') && !set.has('PDF')) return 'PDF'
  return ''
})

watch(
  availableFormats,
  (formats) => {
    if (!formats.includes(selectedFormat.value)) {
      selectedFormat.value = formats[0] || ''
    }
  },
  { immediate: true }
)

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

function openAddFormatModal() {
  formatError.value = ''
  addFormatFile.value = null
  showAddFormatModal.value = true
}

function closeAddFormatModal() {
  showAddFormatModal.value = false
  addFormatFile.value = null
  formatError.value = ''
}

async function onAddFormatSubmit(file) {
  addFormatFile.value = file
  await doUploadFormat(file, false)
}

async function doUploadFormat(file, replace) {
  addingFormat.value = true

  try {
    const formData = new FormData()
    formData.append('bookId', props.id)
    if (replace) formData.append('replaceExisting', 'true')
    formData.append('file', file)

    const res = await fetch(`${apiBase}/api/uploadBook`, {
      method: 'POST',
      headers: getAuthHeaders(),
      body: formData
    })

    const data = await res.json().catch(() => ({}))

    if (res.status === 409 && data.requiresConfirmation) {
      pendingReplaceFile = file
      pendingReplaceFormat.value = data.format || ''
      replaceConfirmMessage.value = data.error || `Replace existing ${data.format || ''} file?`
      showReplaceConfirm.value = true
      return
    }

    if (!res.ok) {
      throw new Error(data.error || 'Failed to add format.')
    }

    addFormatFile.value = null
    pendingReplaceFile = null
    showAddFormatModal.value = false
    emit('formats-updated', {
      formats: data.formats || [],
      blobPaths: data.blobPaths || {}
    })
    if (pendingReplaceFormat.value || missingFormat.value) {
      selectedFormat.value = pendingReplaceFormat.value || missingFormat.value
    }
    pendingReplaceFormat.value = ''
  } catch (e) {
    formatError.value = e.message
  } finally {
    addingFormat.value = false
  }
}

async function onReplaceConfirmed() {
  showReplaceConfirm.value = false
  const file = pendingReplaceFile
  pendingReplaceFile = null
  if (file) {
    await doUploadFormat(file, true)
  }
}

function onReplaceCancelled() {
  showReplaceConfirm.value = false
  pendingReplaceFile = null
  pendingReplaceFormat.value = ''
  replaceConfirmMessage.value = ''
}

async function download() {
  const format = selectedFormat.value
  const selectedBlobPath = blobPathByFormat.value[format]
  if (!selectedBlobPath) {
    alert('No file is available for the selected format.')
    return
  }

  downloading.value = true
  try {
    const res = await fetch(
      `${apiBase}/api/getDownloadUrl?blobPath=${encodeURIComponent(selectedBlobPath)}`,
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
      <div class="format-pills" v-if="availableFormats.length">
        <button
          v-for="fmt in availableFormats"
          :key="fmt"
          type="button"
          class="format-pill"
          :class="{ active: selectedFormat === fmt }"
          @click="selectedFormat = fmt"
        >
          {{ fmt }}
        </button>
      </div>
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
        <p class="hint">Up to 10 tags. Lowercase letters, numbers, _, -, + and # are allowed.</p>
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
      <div v-if="missingFormat" class="add-format-row">
        <button class="small-button" @click="openAddFormatModal">
          + Add {{ missingFormat }}
        </button>
      </div>
      <p v-if="description" class="book-description">{{ description }}</p>
    </div>
    <button class="download-button" :disabled="downloading" @click="download">
      {{ downloading ? 'Preparing...' : `Download ${selectedFormat || ''}` }}
    </button>
    <AddFormatModal
      v-if="showAddFormatModal"
      :format="missingFormat"
      :uploading="addingFormat"
      :api-error="formatError"
      @submit="onAddFormatSubmit"
      @cancel="closeAddFormatModal"
    />
    <ConfirmModal
      v-if="showReplaceConfirm"
      title="Replace existing file?"
      :message="replaceConfirmMessage"
      confirm-label="Replace"
      :destructive="true"
      @confirm="onReplaceConfirmed"
      @cancel="onReplaceCancelled"
    />
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

.format-pills {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 12px;
}

.format-pill {
  border: 1px solid var(--border);
  background: var(--bg-soft);
  color: var(--text);
  border-radius: 999px;
  padding: 4px 12px;
  font-size: 12px;
  font-weight: 600;
  cursor: pointer;
}

.format-pill.active {
  border-color: var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
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

.add-format-row {
  margin: 8px 0 12px;
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
