export const MAX_TAGS = 10
export const MAX_TAG_LENGTH = 24

export function normalizeTag(value) {
  return value
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9_+#-]/g, '')
}