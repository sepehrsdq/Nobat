<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import SlotField from '../components/SlotField.vue'
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

const activeCount = computed(() => items.value.filter((item) => item.isActive).length)
const inactiveCount = computed(() => items.value.length - activeCount.value)

/** A monogram keeps the roster readable when every row is a long name. */
function initial(item: Staff): string {
  return item.displayName.trim().charAt(0) || '؟'
}

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
      <div class="page__heading">
        <p class="eyebrow">تیم مطب</p>
        <div class="page__title">
          <h1>پرسنل</h1>
          <span class="badge">{{ toPersianDigits(items.length) }} نفر</span>
        </div>
      </div>
      <button v-if="!showForm" class="btn btn--primary" type="button" @click="openForm">
        افزودن پرسنل
      </button>
    </header>

    <div v-if="items.length" class="stats">
      <span class="stats__count">
        <strong>{{ toPersianDigits(activeCount) }}</strong>
        فعال
      </span>
      <span class="stats__rule" aria-hidden="true" />
      <span class="stats__hint">
        {{ inactiveCount > 0 ? `${toPersianDigits(inactiveCount)} نفر غیرفعال` : 'همه در نوبت‌دهی حاضرند' }}
        · ساعت کاری هر نفر را از همین فهرست تنظیم کنید.
      </span>
    </div>

    <form v-if="showForm" v-spotlight class="card editor" novalidate @submit.prevent="create">
      <header class="editor__head">
        <p class="eyebrow">پرسنل جدید</p>
        <h2>کی به تیم اضافه می‌شود؟</h2>
      </header>

      <div class="editor__fields">
        <div class="field">
          <label class="field__label" for="staff-name">نام نمایشی</label>
          <input
            id="staff-name"
            v-model="displayName"
            class="field__control"
            required
            maxlength="120"
            autocomplete="off"
            placeholder="مثلاً دکتر سارا رضایی"
          />
        </div>

        <div class="field">
          <label class="field__label" for="staff-title">تخصص (اختیاری)</label>
          <input
            id="staff-title"
            v-model="title"
            class="field__control"
            maxlength="80"
            placeholder="مثلاً دندان‌پزشک"
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

    <ul v-else-if="items.length" class="list roster">
      <li
        v-for="(item, index) in items"
        :key="item.id"
        v-reveal="index * 60"
        class="list__item roster__row"
        :class="{ 'roster__row--off': !item.isActive }"
      >
        <div class="roster__id">
          <span class="monogram" aria-hidden="true">{{ initial(item) }}</span>
          <div class="list__main">
            <span class="list__title">{{ item.displayName }}</span>
            <span class="list__meta">{{ item.title || 'بدون تخصص' }}</span>
          </div>
        </div>

        <div class="row roster__actions">
          <span class="badge" :class="item.isActive ? 'badge--ok' : 'badge--off'">
            {{ item.isActive ? 'فعال' : 'غیرفعال' }}
          </span>
          <RouterLink
            class="btn btn--ghost btn--sm"
            :to="{ name: 'working-hours', params: { staffId: item.id } }"
          >
            ساعت کاری
          </RouterLink>
          <button
            class="btn btn--quiet btn--sm"
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
      <div class="empty__art" aria-hidden="true">
        <SlotField :rows="3" :cols="9" tone="light" sweep />
      </div>
      <p class="empty__title">هنوز پرسنلی ثبت نشده است.</p>
      <p>اولین پزشک یا منشی مطب را اضافه کنید تا نوبت‌ها به او تخصیص پیدا کند.</p>
      <button class="btn btn--primary" type="button" @click="openForm">افزودن اولین پرسنل</button>
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

.stats__hint {
  min-width: 0;
}

/* ---------- create form ---------- */

.editor {
  display: grid;
  gap: var(--space-4);
  border-block-start: 3px solid var(--brand);
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
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
  gap: var(--space-3);
}

@media (max-width: 620px) {
  .editor__fields {
    grid-template-columns: 1fr;
  }
}

/* ---------- roster ---------- */

.roster__row {
  padding: var(--space-3);
  transition: transform var(--motion-fast) var(--ease-out-soft);
}

.roster__id {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  min-width: 0;
}

.monogram {
  flex: none;
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  border-radius: 13px;
  background:
    radial-gradient(24px 24px at 70% 0%, rgba(217, 138, 95, 0.35), transparent 70%),
    var(--brand-deep);
  color: var(--on-dark);
  font-size: 1.05rem;
  font-weight: 700;
  box-shadow: 0 8px 16px -12px rgba(6, 58, 52, 0.9);
  transition: transform var(--motion-delight) var(--ease-spring);
}

@media (hover: hover) and (pointer: fine) {
  .roster__row:hover .monogram {
    transform: rotate(-8deg) scale(1.1);
  }

  .roster__row:hover {
    transform: translate3d(0, -2px, 0);
  }
}

.roster__row--off .monogram {
  background: var(--paper-2);
  color: var(--ink-faint);
  box-shadow: inset 0 0 0 1px var(--line);
}

.roster__row--off .list__title {
  color: var(--ink-soft);
}

.roster__actions {
  justify-content: flex-end;
}

.empty__art {
  width: min(280px, 70%);
  opacity: 0.5;
  margin-block-end: var(--space-1);
}

@media (max-width: 640px) {
  .roster__row {
    flex-direction: column;
    align-items: stretch;
    gap: var(--space-3);
  }

  .roster__actions {
    justify-content: flex-start;
  }
}
</style>
