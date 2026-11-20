<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ApiError, apiFetch } from '../lib/api'
import { toPersianDigits } from '../lib/format'

interface Staff {
  id: string
  displayName: string
  title: string | null
  isActive: boolean
}

const items = ref<Staff[]>([])
const loading = ref(true)
const error = ref('')
const showForm = ref(false)
const busy = ref(false)
const formError = ref('')
const displayName = ref('')
const title = ref('')
const togglingId = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = ''

  try {
    items.value = await apiFetch<Staff[]>('/api/staff')
  } catch (problem) {
    error.value = problem instanceof ApiError ? problem.message : 'دریافت فهرست پرسنل ناموفق بود.'
  } finally {
    loading.value = false
  }
}

function openForm() {
  showForm.value = true
  formError.value = ''
}

function closeForm() {
  showForm.value = false
  displayName.value = ''
  title.value = ''
  formError.value = ''
}

async function create() {
  busy.value = true
  formError.value = ''

  try {
    await apiFetch<Staff>('/api/staff', {
      method: 'POST',
      body: JSON.stringify({
        displayName: displayName.value.trim(),
        title: title.value.trim() || null,
      }),
    })

    closeForm()
    await load()
  } catch (problem) {
    formError.value = problem instanceof ApiError ? problem.message : 'ذخیرهٔ پرسنل ناموفق بود.'
  } finally {
    busy.value = false
  }
}

async function toggle(item: Staff) {
  togglingId.value = item.id
  error.value = ''

  try {
    await apiFetch<Staff>(`/api/staff/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        displayName: item.displayName,
        title: item.title,
        isActive: !item.isActive,
      }),
    })

    await load()
  } catch (problem) {
    error.value = problem instanceof ApiError ? problem.message : 'تغییر وضعیت ناموفق بود.'
  } finally {
    togglingId.value = null
  }
}

onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page__header">
      <div class="page__title">
        <h1>پرسنل</h1>
        <span class="badge">{{ toPersianDigits(items.length) }} نفر</span>
      </div>
      <button v-if="!showForm" class="btn btn--primary" type="button" @click="openForm">
        افزودن پرسنل
      </button>
    </header>

    <form v-if="showForm" class="card stack" novalidate @submit.prevent="create">
      <h2>پرسنل جدید</h2>

      <div class="field">
        <label class="field__label" for="staff-name">نام نمایشی</label>
        <input
          id="staff-name"
          v-model="displayName"
          class="field__control"
          required
          maxlength="120"
          autocomplete="off"
        />
      </div>

      <div class="field">
        <label class="field__label" for="staff-title">تخصص (اختیاری)</label>
        <input id="staff-title" v-model="title" class="field__control" maxlength="80" />
      </div>

      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

      <div class="row">
        <button class="btn btn--primary" type="submit" :disabled="busy">
          {{ busy ? 'در حال ذخیره…' : 'ذخیره' }}
        </button>
        <button class="btn btn--ghost" type="button" @click="closeForm">انصراف</button>
      </div>
    </form>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>

    <p v-if="loading" class="muted" aria-live="polite">در حال بارگذاری…</p>

    <ul v-else-if="items.length" class="list">
      <li v-for="item in items" :key="item.id" class="list__item">
        <div class="list__main">
          <span class="list__title">{{ item.displayName }}</span>
          <span class="list__meta">{{ item.title || 'بدون تخصص' }}</span>
        </div>

        <div class="row">
          <span class="badge" :class="item.isActive ? 'badge--ok' : 'badge--off'">
            {{ item.isActive ? 'فعال' : 'غیرفعال' }}
          </span>
          <RouterLink
            class="btn btn--ghost"
            :to="{ name: 'working-hours', params: { staffId: item.id } }"
          >
            ساعت کاری
          </RouterLink>
          <button
            class="btn btn--ghost"
            type="button"
            :disabled="togglingId === item.id"
            @click="toggle(item)"
          >
            {{ item.isActive ? 'غیرفعال کن' : 'فعال کن' }}
          </button>
        </div>
      </li>
    </ul>

    <div v-else class="empty">
      <p class="empty__title">هنوز پرسنلی ثبت نشده است.</p>
      <p>اولین پزشک یا منشی مطب را اضافه کنید تا نوبت‌ها به او تخصیص پیدا کند.</p>
      <button class="btn btn--primary" type="button" @click="openForm">افزودن اولین پرسنل</button>
    </div>
  </section>
</template>
