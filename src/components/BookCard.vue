<script setup>
import { ref } from 'vue'
import { getAuthHeaders, apiBase } from '../services/auth.js'

const props = defineProps({
  title: { type: String, required: true },
  author: { type: String, required: true },
  format: { type: String, required: true },
  blobPath: { type: String, required: true },
  description: { type: String, default: '' },
  tags: { type: Array, default: () => [] }
})

const downloading = ref(false)

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
      <div v-if="tags.length" class="tags-row">
        <span v-for="tag in tags" :key="tag" class="tag-pill">{{ tag }}</span>
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
