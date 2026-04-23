import { ref } from 'vue'
import { normalizeTag } from '../services/tags.js'

export function useTagInput({
  tagsRef,
  errorRef,
  maxTags,
  maxTagLength,
  clearErrorOnSuccess = false
}) {
  const tagInput = ref('')

  function setError(message) {
    if (errorRef) {
      errorRef.value = message
    }
  }

  function resetTagInput() {
    tagInput.value = ''
  }

  function addTagFromInput() {
    const normalized = normalizeTag(tagInput.value)
    if (!normalized) {
      resetTagInput()
      return false
    }

    if (maxTagLength && normalized.length > maxTagLength) {
      setError(`Each tag must be ${maxTagLength} characters or fewer.`)
      return false
    }

    if (tagsRef.value.includes(normalized)) {
      resetTagInput()
      return false
    }

    if (maxTags && tagsRef.value.length >= maxTags) {
      setError(`A maximum of ${maxTags} tags is allowed.`)
      return false
    }

    if (clearErrorOnSuccess) {
      setError('')
    }

    tagsRef.value.push(normalized)
    resetTagInput()
    return true
  }

  function removeTag(tag) {
    const next = tagsRef.value.filter((t) => t !== tag)
    if (next.length === tagsRef.value.length) {
      return false
    }

    tagsRef.value = next
    return true
  }

  function onTagKeyDown(e) {
    if (e.key === 'Enter' || e.key === ',') {
      e.preventDefault()
      addTagFromInput()
    }
  }

  return {
    tagInput,
    addTagFromInput,
    removeTag,
    onTagKeyDown,
    resetTagInput
  }
}
