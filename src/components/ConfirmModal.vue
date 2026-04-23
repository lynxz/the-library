<script setup>
defineProps({
  title: { type: String, default: 'Confirm' },
  message: { type: String, required: true },
  confirmLabel: { type: String, default: 'Confirm' },
  cancelLabel: { type: String, default: 'Cancel' },
  destructive: { type: Boolean, default: false }
})

const emit = defineEmits(['confirm', 'cancel'])
</script>

<template>
  <Teleport to="body">
    <div class="modal-backdrop" @click.self="emit('cancel')">
      <div class="modal" role="dialog" aria-modal="true" :aria-label="title">
        <h3 class="modal-title">{{ title }}</h3>
        <p class="modal-message">{{ message }}</p>
        <div class="modal-actions">
          <button class="modal-cancel" @click="emit('cancel')">{{ cancelLabel }}</button>
          <button
            class="modal-confirm"
            :class="{ destructive }"
            @click="emit('confirm')"
          >
            {{ confirmLabel }}
          </button>
        </div>
      </div>
    </div>
  </Teleport>
</template>

<style scoped>
.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.45);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.modal {
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 28px 28px 24px;
  max-width: 420px;
  width: calc(100% - 40px);
  box-shadow: var(--shadow);
}

.modal-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-h);
  margin: 0 0 10px;
}

.modal-message {
  font-size: 14px;
  color: var(--text);
  line-height: 1.6;
  margin: 0 0 24px;
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
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

.modal-cancel:hover {
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

.modal-confirm:hover {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
}

.modal-confirm.destructive {
  border-color: #c0392b;
  background: rgba(192, 57, 43, 0.1);
  color: #c0392b;
}

.modal-confirm.destructive:hover {
  background: #c0392b;
  color: #fff;
}
</style>
