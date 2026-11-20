<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError, apiFetch } from '../lib/api'

interface StaffSummary {
  id: string
  displayName: string
}

interface WorkingHourResponse {
  dayOfWeek: string
  startTime: string
  endTime: string
}

interface TimeRange {
  start: string
  end: string
}

interface DayRow {
  day: string
  label: string
  ranges: TimeRange[]
}

/** Iranians read the week from Saturday. */
const dayOrder = [
  { day: 'Saturday', label: 'شنبه' },
  { day: 'Sunday', label: 'یکشنبه' },
  { day: 'Monday', label: 'دوشنبه' },
  { day: 'Tuesday', label: 'سه‌شنبه' },
  { day: 'Wednesday', label: 'چهارشنبه' },
  { day: 'Thursday', label: 'پنجشنبه' },
  { day: 'Friday', label: 'جمعه' },
]

const route = useRoute()
const staffId = String(route.params.staffId ?? '')

const days = ref<DayRow[]>(dayOrder.map((entry) => ({ ...entry, ranges: [] })))
const staffName = ref('')
const loading = ref(true)
const error = ref('')
const saving = ref(false)
const savedMessage = ref('')
const formError = ref('')
const savedSnapshot = ref('')

const dirty = computed(() => snapshot() !== savedSnapshot.value)
const totalRanges = computed(() => days.value.reduce((sum, day) => sum + day.ranges.length, 0))

function snapshot(): string {
  return JSON.stringify(
    days.value.map((day) => ({
      day: day.day,
      ranges: day.ranges.map((range) => ({ start: range.start, end: range.end })),
    })),
  )
}

function markSaved() {
  savedSnapshot.value = snapshot()
}

function toApiTime(value: string): string {
  return value.length === 5 ? `${value}:00` : value
}

function addHour(value: string): string {
  const [hours, minutes] = value.split(':').map(Number)
  const total = Math.min((hours ?? 9) * 60 + (minutes ?? 0) + 60, 23 * 60 + 59)
  return `${String(Math.floor(total / 60)).padStart(2, '0')}:${String(total % 60).padStart(2, '0')}`
}

async function load() {
  loading.value = true
  error.value = ''

  try {
    const staff = await apiFetch<StaffSummary[]>('/api/staff')
    const member = staff.find((candidate) => candidate.id === staffId)

    if (!member) {
      error.value = 'این پرسنل پیدا نشد یا به مطب دیگری تعلق دارد.'
      return
    }

    staffName.value = member.displayName

    const hours = await apiFetch<{ ranges: WorkingHourResponse[] }>(
      `/api/staff/${staffId}/working-hours`,
    )

    for (const day of days.value) {
      day.ranges = hours.ranges
        .filter((range) => range.dayOfWeek === day.day)
        .map((range) => ({ start: range.startTime.slice(0, 5), end: range.endTime.slice(0, 5) }))
        .sort((left, right) => left.start.localeCompare(right.start))
    }

    markSaved()
  } catch (problem) {
    error.value =
      problem instanceof ApiError ? problem.message : 'دریافت ساعت کاری ناموفق بود.'
  } finally {
    loading.value = false
  }
}

function addRange(day: DayRow) {
  const last = day.ranges[day.ranges.length - 1]
  const start = last?.end ?? '09:00'

  day.ranges.push({ start, end: addHour(start) })
}

function removeRange(day: DayRow, index: number) {
  day.ranges.splice(index, 1)
}

/** Client-side mirror of the server rules, so the user sees the problem next to the field. */
function firstValidationError(): string | null {
  for (const day of days.value) {
    const ordered = [...day.ranges].sort((left, right) => left.start.localeCompare(right.start))

    for (const range of day.ranges) {
      if (!range.start || !range.end) {
        return `ساعت شروع و پایان ${day.label} را کامل کنید.`
      }

      if (range.end <= range.start) {
        return `در ${day.label}، ساعت پایان باید بعد از شروع باشد.`
      }
    }

    for (let index = 1; index < ordered.length; index++) {
      if (ordered[index].start < ordered[index - 1].end) {
        return `بازه‌های ${day.label} با هم هم‌پوشانی دارند.`
      }
    }
  }

  return null
}

async function save() {
  formError.value = ''
  savedMessage.value = ''

  const invalid = firstValidationError()

  if (invalid) {
    formError.value = invalid
    return
  }

  saving.value = true

  try {
    const ranges = days.value.flatMap((day) =>
      day.ranges.map((range) => ({
        dayOfWeek: day.day,
        startTime: toApiTime(range.start),
        endTime: toApiTime(range.end),
      })),
    )

    await apiFetch(`/api/staff/${staffId}/working-hours`, {
      method: 'PUT',
      body: JSON.stringify({ ranges }),
    })

    markSaved()
    savedMessage.value = 'ساعت کاری ذخیره شد.'
  } catch (problem) {
    formError.value = problem instanceof ApiError ? problem.message : 'ذخیرهٔ ساعت کاری ناموفق بود.'
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="page">
    <header class="page__header">
      <div class="page__title">
        <h1>ساعت کاری</h1>
        <span v-if="staffName" class="badge">{{ staffName }}</span>
      </div>
      <RouterLink class="btn btn--ghost" to="/panel/staff">بازگشت به پرسنل</RouterLink>
    </header>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>

    <p v-else-if="loading" class="muted" aria-live="polite">در حال بارگذاری…</p>

    <template v-else>
      <section class="card week">
        <p class="week__hint muted">
          هر بازهٔ زمانی که بیمار می‌تواند در آن نوبت بگیرد را وارد کنید. روزهای بدون بازه تعطیل
          حساب می‌شوند.
        </p>

        <div v-for="day in days" :key="day.day" class="day">
          <div class="day__name">
            <span>{{ day.label }}</span>
            <span v-if="day.ranges.length === 0" class="badge badge--off">تعطیل</span>
          </div>

          <div class="day__ranges">
            <div v-for="(range, index) in day.ranges" :key="index" class="range">
              <label class="sr-only" :for="`${day.day}-start-${index}`">
                شروع {{ day.label }}
              </label>
              <input
                :id="`${day.day}-start-${index}`"
                v-model="range.start"
                class="field__control range__input"
                type="time"
                dir="ltr"
                required
              />

              <span class="range__dash" aria-hidden="true">تا</span>

              <label class="sr-only" :for="`${day.day}-end-${index}`">
                پایان {{ day.label }}
              </label>
              <input
                :id="`${day.day}-end-${index}`"
                v-model="range.end"
                class="field__control range__input"
                type="time"
                dir="ltr"
                required
              />

              <button
                class="btn btn--quiet range__remove"
                type="button"
                @click="removeRange(day, index)"
              >
                حذف
                <span class="sr-only">بازهٔ {{ index + 1 }} روز {{ day.label }}</span>
              </button>
            </div>

            <button class="btn btn--ghost day__add" type="button" @click="addRange(day)">
              افزودن بازه
            </button>
          </div>
        </div>
      </section>

      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

      <div class="save-bar">
        <p v-if="savedMessage && !dirty" class="save-bar__ok" role="status">
          {{ savedMessage }}
        </p>
        <p v-else-if="dirty" class="muted">تغییرات ذخیره نشده است.</p>
        <p v-else class="muted">
          {{ totalRanges === 0 ? 'هنوز هیچ بازه‌ای ثبت نشده؛ مطب تعطیل است.' : 'همه‌چیز ذخیره شده است.' }}
        </p>

        <button
          class="btn btn--primary"
          type="button"
          :disabled="saving || !dirty"
          @click="save"
        >
          {{ saving ? 'در حال ذخیره…' : 'ذخیرهٔ ساعت کاری' }}
        </button>
      </div>
    </template>
  </section>
</template>

<style scoped>
.week {
  display: grid;
  gap: var(--space-4);
}

.week__hint {
  font-size: 0.86rem;
  max-width: 62ch;
}

.day {
  display: grid;
  grid-template-columns: 96px 1fr;
  align-items: start;
  gap: var(--space-3);
  padding-block-start: var(--space-3);
  border-block-start: 1px solid var(--line);
}

.day:first-of-type {
  border-block-start: 0;
  padding-block-start: 0;
}

.day__name {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
  font-weight: 500;
  padding-block-start: 8px;
}

.day__ranges {
  display: grid;
  gap: var(--space-2);
  justify-items: start;
}

.range {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.range__input {
  width: 126px;
}

.range__dash {
  font-size: 0.8rem;
  color: var(--ink-soft);
}

.range__remove {
  color: var(--ink-soft);
  min-height: 34px;
}

.range__remove:hover:not(:disabled) {
  color: var(--danger);
}

.day__add {
  min-height: 36px;
  font-size: 0.86rem;
  padding-inline: var(--space-3);
}

.save-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  flex-wrap: wrap;
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-card);
  background: var(--surface);
}

.save-bar p {
  font-size: 0.86rem;
}

.save-bar__ok {
  color: var(--ok);
}

@media (max-width: 560px) {
  .day {
    grid-template-columns: 1fr;
    gap: var(--space-2);
  }

  .day__name {
    padding-block-start: 0;
  }

  .save-bar {
    position: sticky;
    inset-block-end: 0;
  }
}
</style>
