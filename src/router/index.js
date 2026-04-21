import { createRouter, createWebHistory } from 'vue-router'
import { getAuthHeaders, clearToken, setAdmin, apiBase } from '../services/auth.js'
import LoginView from '../views/LoginView.vue'
import LibraryView from '../views/LibraryView.vue'
import AdminView from '../views/AdminView.vue'
import UploadView from '../views/UploadView.vue'

const routes = [
  {
    path: '/login',
    name: 'Login',
    component: LoginView,
    meta: { public: true }
  },
  {
    path: '/',
    name: 'Library',
    component: LibraryView
  },
  {
    path: '/upload',
    name: 'Upload',
    component: UploadView
  },
  {
    path: '/admin',
    name: 'Admin',
    component: AdminView,
    meta: { requiresAdmin: true }
  }
]

const router = createRouter({
  history: createWebHistory(),
  routes
})

router.beforeEach(async (to) => {
  if (to.meta.public) return true

  try {
    const res = await fetch(`${apiBase}/api/me`, { headers: getAuthHeaders() })
    if (res.ok) {
      const data = await res.json()
      setAdmin(data.isAdmin)

      if (to.meta.requiresAdmin && !data.isAdmin) {
        return { name: 'Library' }
      }

      return true
    }
  } catch {
    // server unreachable
  }

  clearToken()
  return { name: 'Login' }
})

export default router
