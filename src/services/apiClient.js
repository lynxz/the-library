import { apiBase, getAuthHeaders } from './auth.js'

export class ApiError extends Error {
  constructor(message, status, data) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.data = data
  }
}

async function parseJsonSafe(response) {
  try {
    return await response.json()
  } catch {
    return null
  }
}

export async function request(path, options = {}) {
  const {
    method = 'GET',
    auth = true,
    headers = {},
    body
  } = options

  const mergedHeaders = {
    ...(auth ? getAuthHeaders() : {}),
    ...headers
  }

  const response = await fetch(`${apiBase}${path}`, {
    method,
    headers: mergedHeaders,
    body
  })

  const data = await parseJsonSafe(response)
  if (!response.ok) {
    const message = data?.error || data?.message || `Request failed with status ${response.status}.`
    throw new ApiError(message, response.status, data)
  }

  return data
}

export function get(path, options = {}) {
  return request(path, { ...options, method: 'GET' })
}

export function postJson(path, payload, options = {}) {
  return request(path, {
    ...options,
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    },
    body: JSON.stringify(payload)
  })
}

export function putJson(path, payload, options = {}) {
  return request(path, {
    ...options,
    method: 'PUT',
    headers: {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    },
    body: JSON.stringify(payload)
  })
}

export function deleteRequest(path, options = {}) {
  return request(path, {
    ...options,
    method: 'DELETE'
  })
}

export function postForm(path, formData, options = {}) {
  return request(path, {
    ...options,
    method: 'POST',
    body: formData
  })
}