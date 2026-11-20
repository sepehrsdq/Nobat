<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ApiError, apiFetch } from '../lib/api'
import { formatMinutes, formatToman, toPersianDigits } from '../lib/format'

interface Service {
  id: string
  name: string
  durationMinutes: number
  priceRial: number
  isActive: boolean
}

const items = ref<Service[]>([])
const loading = ref(true)
const error = ref('')
const showForm = ref(false)
const busy = ref(false)
const formError = ref('')
const name = ref('')
const durationMinutes = ref<number | null>(null)
const priceToman = ref<number | null>(null)
const togglingId = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = ''

  try {
    items.value = await apiFetch<Service[]>('/api/services')
  } catch (problem) {
    error.value = problem instanceof ApiError ? problem.message : 'دریافت فهرست خدمات ناموفق بود.'
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
  name.value = ''
  durationMinutes.value = null
  priceToman.value = null
  formError.value = ''
}

async function create() {
  busy.value = true
  formError.value = ''

  try {
    await apiFetch<Service>('/api/services', {
      method: 'POST',
      body: JSON.stringify({
        name: name.value.trim(),
        durationMinutes: durationMinutes.value ?? 0,
        priceRial: Math.round((priceToman.value ?? 0) * 10),
      }),
    })

    closeForm()
    await load()
  } catch (problem) {
    formError.value = problem instanceof ApiError ? problem.message : 'ذخیرهٔ خدمت ناموفق بود.'
  } finally {
    busy.value = false
  }
}

async function toggle(item: Service) {
  togglingId.value = item.id
  error.value = ''

  try {
    await apiFetch<Service>(`/api/services/${item.id}`, {
      method: 'PUT',
      body: JSON.stringify({
        name: item.name,
        durationMinutes: item.durationMinutes,
        priceRial: item.priceRial,
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
        <h1>خدمات</h1>
        <span class="badge">{{ toPersianDigits(items.length) }} خدمت</span>
      </div>
      <button v-if="!showForm" class="btn btn--primary" type="button" @click="openForm">
        افزودن خدمت
      </button>
    </header>

    <form v-if="showForm" class="card stack" novalidate @submit.prevent="create">
      <h2>خدمت جدید</h2>

      <div class="field">
        <label class="field__label" for="service-name">نام خدمت</label>
        <input
          id="service-name"
          v-model="name"
          class="field__control"
          required
          maxlength="120"
          autocomplete="off"
        />
      </div>

      <div class="row">
        <div class="field">
          <label class="field__label" for="service-duration">مدت (دقیقه)</label>
          <input
            id="service-duration"
            v-model.number="durationMinutes"
            class="field__control"
            type="number"
            dir="ltr"
            min="5"
            max="480"
            step="5"
            required
          />
        </div>

        <div class="field">
          <label class="field__label" for="service-price">قیمت (تومان)</label>
          <input
            id="service-price"
            v-model.number="priceToman"
            class="field__control"
            type="number"
            dir="ltr"
            min="0"
            step="1000"
          />
        </div>
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
          <span class="list__title">{{ item.name }}</span>
          <span class="list__meta">
            {{ formatMinutes(item.durationMinutes) }} · {{ formatToman(item.priceRial) }}
          </span>
        </div>

        <div class="row">
          <span class="badge" :class="item.isActive ? 'badge--ok' : 'badge--off'">
            {{ item.isActive ? 'فعال' : 'غیرفعال' }}
          </span>
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
      <p class="empty__title">هنوز خدمتی تعریف نشده است.</p>
      <p>مدت و قیمت هر خدمت را ثبت کنید تا در صفحهٔ رزرو به بیمار نشان داده شود.</p>
      <button class="btn btn--primary" type="button" @click="openForm">افزودن اولین خدمت</button>
    </div>
  </section>
</template>
