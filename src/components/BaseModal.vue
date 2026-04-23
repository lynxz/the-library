<script setup>
defineProps({
  title: { type: String, default: '' },
  ariaLabel: { type: String, default: '' },
  maxWidth: { type: String, default: '420px' }
})

const emit = defineEmits(['close'])
</script>

<template>
  <Teleport to="body">
    <div class="modal-backdrop" @click.self="emit('close')">
      <div class="modal" role="dialog" aria-modal="true" :aria-label="ariaLabel || title" :style="{ maxWidth }">
        <h3 v-if="title" class="modal-title">{{ title }}</h3>
        <slot />
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
  width: calc(100% - 40px);
  box-shadow: var(--shadow);
}

.modal-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--text-h);
  margin: 0 0 10px;
}
</style>