<script setup>
import { ref, computed } from 'vue'
import BaseModal from './BaseModal.vue'

const props = defineProps({
  format: { type: String, required: true },
  uploading: { type: Boolean, default: false },
  apiError: { type: String, default: '' }
})

const emit = defineEmits(['submit', 'cancel'])

const selectedFile = ref(null)
const validationError = ref('')

const accept = computed(() => (props.format === 'PDF' ? '.pdf' : '.epub'))

function onFileChange(e) {
  selectedFile.value = e.target.files[0] || null
  validationError.value = ''
}

function submit() {
  validationError.value = ''
  if (!selectedFile.value) {
    validationError.value = `Please select a ${props.format} file.`
    return
  }
  const ext = selectedFile.value.name.split('.').pop()?.toLowerCase()
  if (ext !== props.format.toLowerCase()) {
    validationError.value = `Please upload a .${props.format.toLowerCase()} file.`
    return
  }
  emit('submit', selectedFile.value)
}

const displayError = computed(() => validationError.value || props.apiError)
</script>

<template>
  <BaseModal :title="`Add ${format} format`" :aria-label="`Add ${format} format`" @close="emit('cancel')">
    <div class="modal-content">
      <p class="modal-hint">Upload a .{{ format.toLowerCase() }} file to make this book available in {{ format }} format.</p>
      <label class="file-label">
        <input
          type="file"
          :accept="accept"
          :disabled="uploading"
          @change="onFileChange"
        />
      </label>
      <p v-if="displayError" class="modal-error">{{ displayError }}</p>
      <div class="modal-actions">
        <button class="modal-cancel" :disabled="uploading" @click="emit('cancel')">Cancel</button>
        <button class="modal-confirm" :disabled="uploading" @click="submit">
          {{ uploading ? 'Uploading...' : `Upload ${format}` }}
        </button>
      </div>
    </div>
  </BaseModal>
</template>

<style scoped>
.modal-content {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.modal-hint {
  font-size: 14px;
  color: var(--text);
  line-height: 1.5;
  margin: 0;
}

.file-label input[type='file'] {
  font-size: 14px;
  font-family: var(--sans);
  color: var(--text);
  width: 100%;
}

.modal-error {
  font-size: 13px;
  color: #c0392b;
  margin: 0;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 6px;
}

.modal-cancel {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text-h);
  border-radius: 6px;
  padding: 8px 16px;
  font-size: 14px;
  font-family: var(--sans);
  cursor: pointer;
}

.modal-cancel:hover:not(:disabled) {
  border-color: var(--accent-border);
}

.modal-confirm {
  border: 1px solid var(--accent-border);
  background: var(--accent-bg);
  color: var(--accent);
  border-radius: 6px;
  padding: 8px 16px;
  font-size: 14px;
  font-family: var(--sans);
  font-weight: 600;
  cursor: pointer;
}

.modal-confirm:hover:not(:disabled) {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
}

.modal-cancel:disabled,
.modal-confirm:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
