const TOKEN_KEY = 'library_token'
const ADMIN_KEY = 'library_is_admin'

export const apiBase = import.meta.env.VITE_API_BASE || ''

export function getToken() {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token) {
  localStorage.setItem(TOKEN_KEY, token)
}

export function setAdmin(isAdmin) {
  localStorage.setItem(ADMIN_KEY, isAdmin ? 'true' : 'false')
}

export function isAdmin() {
  return localStorage.getItem(ADMIN_KEY) === 'true'
}

export function clearToken() {
  localStorage.removeItem(TOKEN_KEY)
  localStorage.removeItem(ADMIN_KEY)
}

export function getAuthHeaders() {
  const token = getToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}
