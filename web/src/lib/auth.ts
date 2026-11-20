import { reactive } from 'vue'
import { ApiError, apiFetch } from './api'

export interface Session {
  userId: string
  tenantId: string
  tenantSlug: string
  fullName: string
  role: string
}

export interface LoginPayload {
  tenantSlug: string
  phone: string
  password: string
}

export interface RegisterPayload {
  slug: string
  clinicName: string
  ownerName: string
  phone: string
  password: string
}

export const session = reactive({
  user: null as Session | null,
  loaded: false,

  async load(): Promise<boolean> {
    try {
      session.user = await apiFetch<Session>('/api/me')
      session.loaded = true
      return true
    } catch (error) {
      if (error instanceof ApiError && error.status === 401) {
        session.user = null
        session.loaded = true
        return false
      }

      throw error
    }
  },

  async login(payload: LoginPayload): Promise<void> {
    session.user = await apiFetch<Session>('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    session.loaded = true
  },

  async register(payload: RegisterPayload): Promise<void> {
    session.user = await apiFetch<Session>('/api/auth/register', {
      method: 'POST',
      body: JSON.stringify(payload),
    })
    session.loaded = true
  },

  async logout(): Promise<void> {
    await apiFetch<void>('/api/auth/logout', { method: 'POST' })
    session.user = null
    session.loaded = true
  },
})
