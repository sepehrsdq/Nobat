<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import SlotField from '../components/SlotField.vue'
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

const activeCount = computed(() => items.value.filter((item) => item.isActive).length)

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
      <div class="page__heading">
        <p class="eyebrow">دفتر خدمات</p>
        <div class="page__title">
          <h1>خدمات</h1>
          <span class="badge">{{ toPersianDigits(items.length) }} خدمت</span>
        </div>
      </div>
      <button v-if="!showForm" class="btn btn--primary" type="button" @click="openForm">
        افزودن خدمت
      </button>
    </header>

    <div v-if="items.length" class="stats">
      <span class="stats__count">
        <strong>{{ toPersianDigits(activeCount) }}</strong>
        قابل رزرو
      </span>
      <span class="stats__rule" aria-hidden="true" />
      <span class="stats__hint">مدت و قیمت هر خدمت در صفحهٔ رزرو به بیمار نشان داده می‌شود.</span>
    </div>

    <form v-if="showForm" v-spotlight class="card editor" novalidate @submit.prevent="create">
      <header class="editor__head">
        <p class="eyebrow">خدمت جدید</p>
        <h2>چه کاری انجام می‌دهید؟</h2>
      </header>

      <div class="field">
        <label class="field__label" for="service-name">نام خدمت</label>
        <input
          id="service-name"
          v-model="name"
          class="field__control"
          required
          maxlength="120"
          autocomplete="off"
          placeholder="مثلاً جرم‌گیری و بروساژ"
        />
      </div>

      <div class="editor__fields">
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
            placeholder="30"
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
            placeholder="450000"
          />
          <p class="field__hint">می‌توانید خالی بگذارید؛ بعداً هم قابل تغییر است.</p>
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

    <ul v-else-if="items.length" class="services">
      <li
        v-for="(item, index) in items"
        :key="item.id"
        v-tilt="3"
        v-reveal="index * 70"
        v-spotlight
        class="service"
        :class="{ 'service--off': !item.isActive }"
      >
        <div class="service__head">
          <span class="service__glyph" aria-hidden="true" />
          <span class="badge" :class="item.isActive ? 'badge--ok' : 'badge--off'">
            {{ item.isActive ? 'فعال' : 'غیرفعال' }}
          </span>
        </div>

        <h2 class="service__name">{{ item.name }}</h2>

        <dl class="service__facts">
          <div>
            <dt>مدت</dt>
            <dd>{{ formatMinutes(item.durationMinutes) }}</dd>
          </div>
          <div>
            <dt>هزینه</dt>
            <dd class="service__price">{{ formatToman(item.priceRial) }}</dd>
          </div>
        </dl>

        <button
          class="btn btn--ghost btn--sm service__toggle"
          type="button"
          :disabled="togglingId === item.id"
          @click="toggle(item)"
        >
          {{ item.isActive ? 'از دسترس بیمار خارج کن' : 'برای بیمار فعال کن' }}
        </button>
      </li>
    </ul>

    <div v-else class="empty">
      <div class="empty__art" aria-hidden="true">
        <SlotField :rows="3" :cols="9" tone="light" sweep />
      </div>
      <p class="empty__title">هنوز خدمتی تعریف نشده است.</p>
      <p>مدت و قیمت هر خدمت را ثبت کنید تا در صفحهٔ رزرو به بیمار نشان داده شود.</p>
      <button class="btn btn--primary" type="button" @click="openForm">افزودن اولین خدمت</button>
    </div>
  </section>
</template>

<style scoped>
.page__heading {
  display: grid;
  gap: 2px;
}

.stats {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: var(--space-2) var(--space-3);
  padding: var(--space-2) var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: rgba(255, 253, 248, 0.7);
  font-size: 0.82rem;
  color: var(--ink-soft);
}

.stats__count strong {
  font-size: 1rem;
  color: var(--brand);
  margin-inline-end: 4px;
}

.stats__rule {
  width: 1px;
  height: 14px;
  background: var(--line-strong);
}

/* ---------- create form ---------- */

.editor {
  display: grid;
  gap: var(--space-4);
  border-block-start: 3px solid var(--accent);
  animation: rise-in var(--motion-enter) var(--ease-out) both;
}

.editor__head {
  display: grid;
  gap: 2px;
}

.editor__head h2 {
  font-size: 1.05rem;
}

.editor__fields {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr);
  gap: var(--space-3);
}

/* ---------- service cards ---------- */

.services {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: var(--space-3);
}

.service {
  display: grid;
  align-content: start;
  gap: var(--space-3);
  padding: var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-card);
  background:
    linear-gradient(180deg, var(--surface), var(--surface-2));
  box-shadow: var(--shadow-card);
  transition:
    border-color var(--motion-fast) var(--ease),
    transform var(--motion-fast) var(--ease-out-soft);
}

@media (hover: hover) and (pointer: fine) {
  .service:hover {
    border-color: var(--line-strong);
    transform: translate3d(0, -3px, 0);
    box-shadow: var(--shadow-lift);
  }
}

@media (hover: hover) and (pointer: fine) {
  .service:hover .service__glyph {
    animation: glyph-bounce 640ms var(--ease-spring);
  }
}

@keyframes glyph-bounce {
  0% {
    transform: translateY(0);
  }

  35% {
    transform: translateY(-5px);
  }

  70% {
    transform: translateY(2px);
  }

  100% {
    transform: translateY(0);
  }
}

.service--off {
  background: var(--surface);
}

.service--off .service__name {
  color: var(--ink-soft);
}

.service__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

/* Three bars stand in for a duration scale: long, short, long. */
.service__glyph {
  position: relative;
  width: 30px;
  height: 6px;
  border-radius: var(--radius-pill);
  background: var(--brand);
  box-shadow:
    0 8px 0 -1px var(--accent),
    0 -8px 0 -1px var(--line-strong);
  opacity: 0.9;
  transition: transform 300ms var(--ease-spring);
}

@media (hover: hover) and (pointer: fine) {
  .service:hover .service__glyph {
    transform: translateY(-2px);
  }
}

.service--off .service__glyph {
  background: var(--line-strong);
  box-shadow:
    0 8px 0 -1px var(--line),
    0 -8px 0 -1px var(--line);
}

.service__name {
  font-size: 1.02rem;
  line-height: 1.5;
  overflow-wrap: anywhere;
}

.service__facts {
  display: grid;
  gap: var(--space-1);
  margin: 0;
  font-size: 0.82rem;
}

.service__facts div {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-2);
  padding-block-end: 4px;
  border-block-end: 1px dashed var(--line);
}

.service__facts dt {
  color: var(--ink-soft);
}

.service__facts dd {
  margin: 0;
  font-weight: 500;
  font-variant-numeric: tabular-nums;
}

.service__price {
  color: var(--brand-deep);
}

.service__toggle {
  justify-self: start;
}

.empty__art {
  width: min(280px, 70%);
  opacity: 0.5;
  margin-block-end: var(--space-1);
}

@media (max-width: 620px) {
  .editor__fields {
    grid-template-columns: 1fr;
  }
}
</style>
