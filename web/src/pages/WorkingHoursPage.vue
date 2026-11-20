<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import SlotField from '../components/SlotField.vue'
import { ApiError, apiFetch } from '../lib/api'
import { toPersianDigits } from '../lib/format'

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
const openDays = computed(() => days.value.filter((day) => day.ranges.length > 0).length)

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
      <div class="page__heading">
        <p class="eyebrow">برنامهٔ هفته</p>
        <div class="page__title">
          <h1>ساعت کاری</h1>
          <span v-if="staffName" class="badge">{{ staffName }}</span>
        </div>
      </div>
      <RouterLink class="btn btn--ghost btn--sm" to="/panel/staff">بازگشت به پرسنل</RouterLink>
    </header>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>

    <p v-else-if="loading" class="muted" aria-live="polite">در حال بارگذاری…</p>

    <template v-else>
      <section class="week-strip" aria-label="خلاصهٔ هفته">
        <div
          v-for="day in days"
          :key="day.day"
          class="week-strip__day"
          :class="{ 'week-strip__day--open': day.ranges.length > 0 }"
        >
          <span class="week-strip__name">{{ day.label }}</span>
          <span class="week-strip__mark" aria-hidden="true" />
          <span class="week-strip__meta">
            {{ day.ranges.length > 0 ? `${toPersianDigits(day.ranges.length)} بازه` : 'تعطیل' }}
          </span>
        </div>
      </section>

      <section v-spotlight class="card week">
        <header class="week__head">
          <div>
            <p class="eyebrow">بازه‌های پذیرش</p>
            <h2>بیمار کِی می‌تواند نوبت بگیرد؟</h2>
          </div>
          <span class="week__total">
            {{ toPersianDigits(openDays) }} روز باز
            <span aria-hidden="true">·</span>
            {{ toPersianDigits(totalRanges) }} بازه
          </span>
        </header>

        <p class="week__hint muted">
          هر بازهٔ زمانی که بیمار می‌تواند در آن نوبت بگیرد را وارد کنید. روزهای بدون بازه تعطیل
          حساب می‌شوند.
        </p>

        <div v-for="day in days" :key="day.day" class="day" :class="{ 'day--closed': day.ranges.length === 0 }">
          <div class="day__name">
            <span>{{ day.label }}</span>
            <span v-if="day.ranges.length === 0" class="badge badge--off">تعطیل</span>
            <span v-else class="badge badge--ok">{{ toPersianDigits(day.ranges.length) }} بازه</span>
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
              {{ day.ranges.length === 0 ? 'این روز را باز کن' : 'افزودن بازه' }}
            </button>
          </div>
        </div>

        <div v-if="totalRanges === 0" class="week__void" aria-hidden="true">
          <SlotField :rows="2" :cols="14" tone="light" />
        </div>
      </section>

      <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

      <div class="save-bar">
        <p v-if="savedMessage && !dirty" class="save-bar__ok" role="status">
          <span class="save-bar__dot" aria-hidden="true" />
          {{ savedMessage }}
        </p>
        <p v-else-if="dirty" class="save-bar__dirty">تغییرات ذخیره نشده است.</p>
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
.page__heading {
  display: grid;
  gap: 2px;
}

/* ---------- week overview ---------- */

.week-strip {
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: var(--space-2);
}

.week-strip__day {
  display: grid;
  justify-items: center;
  gap: 6px;
  padding: var(--space-3) var(--space-1);
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
  background: var(--surface);
  text-align: center;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease),
    transform var(--motion-fast) var(--ease-spring);
}

.week-strip__day--open {
  border-color: rgba(10, 91, 81, 0.28);
  background:
    linear-gradient(180deg, var(--surface), var(--brand-soft));
}

@media (hover: hover) and (pointer: fine) {
  .week-strip__day--open:hover {
    transform: translate3d(0, -2px, 0);
  }
}

.week-strip__name {
  font-size: 0.8rem;
  font-weight: 500;
}

.week-strip__mark {
  width: 100%;
  height: 4px;
  border-radius: var(--radius-pill);
  background: var(--line);
  overflow: hidden;
  position: relative;
}

.week-strip__day--open .week-strip__mark {
  background: var(--brand-soft);
}

.week-strip__day--open .week-strip__mark::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  background: linear-gradient(90deg, var(--brand), var(--accent));
  transform-origin: right;
  animation: strip-fill 700ms var(--ease-out-soft) both;
}

@keyframes strip-fill {
  from {
    transform: scaleX(0);
  }

  to {
    transform: scaleX(1);
  }
}

.week-strip__meta {
  font-size: 0.68rem;
  color: var(--ink-soft);
  font-variant-numeric: tabular-nums;
}

/* ---------- editor ---------- */

.week {
  display: grid;
  gap: var(--space-4);
}

.week__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
  flex-wrap: wrap;
}

.week__head h2 {
  font-size: 1.05rem;
}

.week__total {
  font-size: 0.76rem;
  color: var(--ink-soft);
  padding: 3px 12px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: var(--paper);
  white-space: nowrap;
}

.week__hint {
  font-size: 0.86rem;
  max-width: 62ch;
}

.day {
  display: grid;
  grid-template-columns: 116px 1fr;
  align-items: start;
  gap: var(--space-3);
  padding-block-start: var(--space-3);
  border-block-start: 1px solid var(--line);
}

.day:first-of-type {
  border-block-start: 0;
  padding-block-start: 0;
}

.day--closed {
  opacity: 0.86;
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
  min-width: 0;
  max-width: 100%;
}

.range {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
  padding: 4px;
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
  background: var(--surface-2);
}

.range__input {
  width: 118px;
  border-color: transparent;
  background: var(--surface);
}

.range__input:focus {
  border-color: var(--brand);
}

.range__dash {
  font-size: 0.78rem;
  color: var(--ink-soft);
}

.range__remove {
  color: var(--ink-soft);
  min-height: 34px;
}

.range__remove:hover:not(:disabled) {
  color: var(--danger);
  background: var(--danger-soft);
}

.day__add {
  min-height: 36px;
  font-size: 0.86rem;
  padding-inline: var(--space-3);
  border-style: dashed;
}

.week__void {
  width: min(360px, 100%);
  margin-inline: auto;
  opacity: 0.4;
}

/* ---------- save bar ---------- */

.save-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
  flex-wrap: wrap;
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-card);
  background: rgba(255, 253, 248, 0.86);
  backdrop-filter: blur(8px);
  box-shadow: var(--shadow-card);
}

.save-bar p {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  font-size: 0.86rem;
}

.save-bar__ok {
  color: var(--ok);
}

.save-bar__dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--ok);
  box-shadow: 0 0 0 3px var(--brand-soft);
}

.save-bar__dirty {
  color: var(--accent);
}

@media (prefers-reduced-motion: reduce) {
  .week-strip__day--open .week-strip__mark::after {
    animation: none;
    transform: none;
  }
}

@media (max-width: 720px) {
  .week-strip {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .day {
    grid-template-columns: 1fr;
    gap: var(--space-2);
  }

  .day__name {
    padding-block-start: 0;
  }

  .range {
    width: 100%;
  }

  .range__input {
    flex: 1;
    min-width: 0;
    width: auto;
  }

  .save-bar {
    position: sticky;
    inset-block-end: 0;
    z-index: 5;
  }
}
</style>
