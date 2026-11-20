<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import LoadingRows from '../components/LoadingRows.vue'
import SlotField from '../components/SlotField.vue'
import { ApiError, apiFetch } from '../lib/api'
import { formatMinutes, formatToman, toPersianDigits } from '../lib/format'

interface Appointment {
  id: string
  staffId: string
  staffName: string
  serviceId: string
  serviceName: string
  patientName: string
  patientPhone: string
  startUtc: string
  endUtc: string
  durationMinutes: number
  priceRial: number
  status: string
  note: string | null
  cancelledAtUtc: string | null
  cancellationReason: string | null
}

interface DayResponse {
  date: string
  items: Appointment[]
}

interface Slot {
  startUtc: string
  endUtc: string
}

interface Staff {
  id: string
  displayName: string
  isActive: boolean
}

const dateFormatter = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Tehran' })

const today = ref(tehranDateOf(new Date()))
const selectedDate = ref(today.value)
const items = ref<Appointment[]>([])
const loading = ref(true)
const error = ref('')

const staff = ref<Staff[]>([])
const staffFilter = ref('')

const cancellingId = ref<string | null>(null)
const cancelReason = ref('')
const cancelBusy = ref(false)
const cancelError = ref('')

const reschedulingId = ref<string | null>(null)
const rescheduleDate = ref('')
const rescheduleSlots = ref<Slot[]>([])
const rescheduleStart = ref('')
const rescheduleLoading = ref(false)
const rescheduleBusy = ref(false)
const rescheduleError = ref('')

const now = ref(new Date())
let clockHandle: number | undefined

const bookedCount = computed(() => items.value.filter((item) => item.status === 'Booked').length)
const isToday = computed(() => selectedDate.value === today.value)

/** An appointment is "happening now" if Tehran-now sits inside its window. */
function isCurrent(item: Appointment): boolean {
  if (item.status !== 'Booked') {
    return false
  }

  const nowMs = now.value.getTime()

  return new Date(item.startUtc).getTime() <= nowMs && nowMs < new Date(item.endUtc).getTime()
}
const nowLabel = computed(() =>
  toPersianDigits(
    new Intl.DateTimeFormat('fa-IR', {
      hour: '2-digit',
      minute: '2-digit',
      timeZone: 'Asia/Tehran',
    }).format(now.value),
  ),
)

const dayChips = computed(() =>
  Array.from({ length: 14 }, (_, index) => {
    const date = addDays(selectedDate.value, index)

    return {
      date,
      weekday: formatDate(date, { weekday: 'long' }),
      caption: formatDate(date, { day: 'numeric', month: 'long' }),
      isToday: date === today.value,
    }
  }),
)

const rescheduleChips = computed(() =>
  Array.from({ length: 14 }, (_, index) => {
    const date = addDays(rescheduleDate.value, index)

    return {
      date,
      weekday: formatDate(date, { weekday: 'long' }),
      caption: formatDate(date, { day: 'numeric', month: 'long' }),
      isToday: date === today.value,
    }
  }),
)

function tehranDateOf(value: Date): string {
  return dateFormatter.format(value)
}

function localDateOf(instant: string): string {
  return tehranDateOf(new Date(instant))
}

function addDays(isoDate: string, days: number): string {
  const [year, month, day] = isoDate.split('-').map(Number)
  const base = new Date(Date.UTC(year ?? 2026, (month ?? 1) - 1, day ?? 1))
  base.setUTCDate(base.getUTCDate() + days)

  return base.toISOString().slice(0, 10)
}

function formatDate(isoDate: string, options: Intl.DateTimeFormatOptions): string {
  return new Intl.DateTimeFormat('fa-IR', { ...options, timeZone: 'UTC' }).format(
    new Date(`${isoDate}T00:00:00Z`),
  )
}

function dayLabel(isoDate: string): string {
  const weekday = formatDate(isoDate, { weekday: 'long' })
  const day = formatDate(isoDate, { day: 'numeric' })
  const month = formatDate(isoDate, { month: 'long' })
  const year = formatDate(isoDate, { year: 'numeric' })

  return `${weekday} ${day} ${month} ${year}`
}

function timeLabel(instant: string): string {
  const formatted = new Intl.DateTimeFormat('fa-IR', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Tehran',
  }).format(new Date(instant))

  return toPersianDigits(formatted)
}

function statusLabel(status: string): string {
  switch (status) {
    case 'Booked':
      return 'رزرو شده'
    case 'Cancelled':
      return 'لغو شده'
    case 'Completed':
      return 'انجام شده'
    case 'NoShow':
      return 'غیبت'
    default:
      return status
  }
}

function statusClass(status: string): string {
  if (status === 'Booked') return 'badge--ok'
  if (status === 'Cancelled') return 'badge--off'
  if (status === 'NoShow') return 'badge--accent'

  return 'badge--flat'
}

async function load() {
  loading.value = true
  error.value = ''

  try {
    const query = staffFilter.value ? `&staffId=${staffFilter.value}` : ''
    const day = await apiFetch<DayResponse>(
      `/api/appointments?date=${selectedDate.value}${query}`,
    )

    items.value = day.items
  } catch (problem) {
    error.value = problem instanceof ApiError ? problem.message : 'دریافت نوبت‌های روز ناموفق بود.'
    items.value = []
  } finally {
    loading.value = false
  }
}

async function loadStaff() {
  try {
    staff.value = (await apiFetch<Staff[]>('/api/staff')).filter((member) => member.isActive)
  } catch {
    // The day still works without the filter; the list header shows the real error.
  }
}

function goTo(date: string) {
  selectedDate.value = date
  closePanels()
  void load()
}

function goToday() {
  goTo(today.value)
}

function shiftDay(days: number) {
  goTo(addDays(selectedDate.value, days))
}

function chooseStaff(id: string) {
  staffFilter.value = staffFilter.value === id ? '' : id
  closePanels()
  void load()
}

function closePanels() {
  cancellingId.value = null
  cancelReason.value = ''
  cancelError.value = ''
  reschedulingId.value = null
  rescheduleSlots.value = []
  rescheduleStart.value = ''
  rescheduleError.value = ''
}

function startCancel(item: Appointment) {
  closePanels()
  cancellingId.value = item.id
  cancelReason.value = ''
  cancelError.value = ''
}

async function confirmCancel(item: Appointment) {
  cancelBusy.value = true
  cancelError.value = ''

  try {
    await apiFetch<Appointment>(`/api/appointments/${item.id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason: cancelReason.value.trim() || null }),
    })

    closePanels()
    await load()
  } catch (problem) {
    cancelError.value = problem instanceof ApiError ? problem.message : 'لغو نوبت ناموفق بود.'
    await load()
  } finally {
    cancelBusy.value = false
  }
}

function openReschedule(item: Appointment) {
  closePanels()
  reschedulingId.value = item.id
  rescheduleDate.value = localDateOf(item.startUtc)
  rescheduleStart.value = ''
  rescheduleError.value = ''
  void loadRescheduleSlots()
}

function chooseRescheduleDate(date: string) {
  rescheduleDate.value = date
  rescheduleStart.value = ''
  rescheduleError.value = ''
  void loadRescheduleSlots()
}

async function loadRescheduleSlots() {
  if (!reschedulingId.value) {
    return
  }

  rescheduleLoading.value = true
  rescheduleSlots.value = []
  rescheduleError.value = ''

  try {
    const result = await apiFetch<{ date: string; slots: Slot[] }>(
      `/api/appointments/${reschedulingId.value}/slots?date=${rescheduleDate.value}`,
    )

    rescheduleSlots.value = result.slots
  } catch (problem) {
    rescheduleError.value =
      problem instanceof ApiError ? problem.message : 'دریافت ساعت‌های آزاد ناموفق بود.'
  } finally {
    rescheduleLoading.value = false
  }
}

async function confirmReschedule() {
  if (!reschedulingId.value || !rescheduleStart.value) {
    return
  }

  rescheduleBusy.value = true
  rescheduleError.value = ''

  try {
    await apiFetch<Appointment>(`/api/appointments/${reschedulingId.value}/reschedule`, {
      method: 'POST',
      body: JSON.stringify({ startUtc: rescheduleStart.value }),
    })

    closePanels()
    await load()
  } catch (problem) {
    rescheduleError.value =
      problem instanceof ApiError ? problem.message : 'جابه‌جایی نوبت ناموفق بود.'

    if (
      problem instanceof ApiError &&
      (problem.type === 'appointment.slot_taken' || problem.type === 'appointment.slot_unavailable')
    ) {
      rescheduleStart.value = ''
      await loadRescheduleSlots()
    }
  } finally {
    rescheduleBusy.value = false
  }
}

onMounted(async () => {
  clockHandle = window.setInterval(() => {
    now.value = new Date()
  }, 30_000)

  await Promise.all([load(), loadStaff()])
})

onUnmounted(() => {
  if (clockHandle !== undefined) {
    window.clearInterval(clockHandle)
  }
})
</script>

<template>
  <section class="page day">
    <header class="page__header day__head">
      <div class="day__heading">
        <p class="eyebrow">دفتر نوبت</p>
        <div class="page__title">
          <h1>نوبت‌های روز</h1>
          <span :key="bookedCount" class="badge badge--count">{{ toPersianDigits(bookedCount) }} نوبت فعال</span>
        </div>
      </div>

      <div class="day__meta">
        <span v-if="isToday" class="day__clock">
          <span class="day__pulse" aria-hidden="true" />
          الان {{ nowLabel }}
        </span>
        <button class="btn btn--ghost" type="button" @click="goToday">برو به امروز</button>
      </div>
    </header>

    <div v-spotlight class="card daybar">
      <div class="daybar__nav">
        <button class="btn btn--quiet" type="button" aria-label="روز قبل" @click="shiftDay(-1)">
          روز قبل
        </button>

        <Transition name="date-flip" mode="out-in">
          <p :key="selectedDate" class="daybar__date">{{ dayLabel(selectedDate) }}</p>
        </Transition>

        <button class="btn btn--quiet" type="button" aria-label="روز بعد" @click="shiftDay(1)">
          روز بعد
        </button>
      </div>

      <ul class="rail" aria-label="انتخاب روز">
        <li v-for="day in dayChips" :key="day.date">
          <button
            class="rail__chip"
            :class="{ 'rail__chip--picked': day.date === selectedDate }"
            type="button"
            :aria-pressed="day.date === selectedDate"
            :aria-current="day.isToday ? 'date' : undefined"
            @click="goTo(day.date)"
          >
            <span class="rail__weekday">{{ day.weekday }}</span>
            <span class="rail__caption">{{ day.caption }}</span>
            <span v-if="day.isToday" class="rail__today">امروز</span>
          </button>
        </li>
      </ul>

      <ul v-if="staff.length > 1" class="filters" aria-label="فیلتر پرسنل">
        <li>
          <button
            class="filter"
            :class="{ 'filter--picked': staffFilter === '' }"
            type="button"
            :aria-pressed="staffFilter === ''"
            @click="chooseStaff('')"
          >
            همهٔ پرسنل
          </button>
        </li>
        <li v-for="member in staff" :key="member.id">
          <button
            class="filter"
            :class="{ 'filter--picked': staffFilter === member.id }"
            type="button"
            :aria-pressed="staffFilter === member.id"
            @click="chooseStaff(member.id)"
          >
            {{ member.displayName }}
          </button>
        </li>
      </ul>
    </div>

    <p v-if="error" class="form-error" role="alert">{{ error }}</p>

    <LoadingRows v-if="loading" variant="timeline" :rows="3" />

    <template v-else-if="items.length">
      <TransitionGroup tag="ol" name="slip" class="timeline">
        <li
          v-for="(item, index) in items"
          :key="item.id"
          class="timeline__row"
          :class="{
            'timeline__row--cancelled': item.status === 'Cancelled',
            'timeline__row--now': isCurrent(item),
          }"
          :style="{ '--i': Math.min(index, 8) }"
        >
          <div class="timeline__time">
            <strong>{{ timeLabel(item.startUtc) }}</strong>
            <span>تا {{ timeLabel(item.endUtc) }}</span>
          </div>

          <div class="timeline__axis" aria-hidden="true">
            <span class="timeline__dot" />
          </div>

          <article v-spotlight class="slip">
            <span v-if="isCurrent(item)" class="slip__now" aria-hidden="true">
              <span class="live-dot" />
              همین حالا
            </span>

            <header class="slip__head">
              <div class="slip__who">
                <h2 class="slip__name">{{ item.patientName }}</h2>
                <p class="slip__meta">
                  {{ item.serviceName }} · {{ item.staffName }} · {{ formatMinutes(item.durationMinutes) }}
                </p>
              </div>
              <span class="badge" :class="statusClass(item.status)">{{ statusLabel(item.status) }}</span>
            </header>

            <div class="slip__facts">
              <span class="slip__price">{{ formatToman(item.priceRial) }}</span>
              <span class="slip__phone" dir="ltr">{{ toPersianDigits(item.patientPhone) }}</span>
            </div>

            <p v-if="item.cancellationReason" class="slip__note">
              دلیل لغو: {{ item.cancellationReason }}
            </p>

            <div v-if="item.status === 'Booked'" class="slip__actions">
              <button
                class="btn btn--ghost btn--sm"
                type="button"
                :aria-expanded="reschedulingId === item.id"
                @click="openReschedule(item)"
              >
                جابه‌جایی
              </button>
              <button
                class="btn btn--ghost btn--sm slip__cancel"
                type="button"
                :aria-expanded="cancellingId === item.id"
                @click="startCancel(item)"
              >
                لغو
              </button>
            </div>

            <Transition name="panel">
              <div v-if="cancellingId === item.id" class="panel">
                <p class="panel__title">لغو نوبت {{ item.patientName }}</p>

                <div class="field">
                  <label class="field__label" :for="`cancel-reason-${item.id}`">دلیل (اختیاری)</label>
                  <input
                    :id="`cancel-reason-${item.id}`"
                    v-model="cancelReason"
                    class="field__control"
                    maxlength="200"
                    placeholder="مثلاً بیمار تماس گرفت"
                  />
                </div>

                <p v-if="cancelError" class="form-error" role="alert">{{ cancelError }}</p>

                <div class="row">
                  <button class="btn btn--primary btn--sm" type="button" :disabled="cancelBusy" @click="confirmCancel(item)">
                    {{ cancelBusy ? 'در حال لغو…' : 'تأیید لغو' }}
                  </button>
                  <button class="btn btn--quiet btn--sm" type="button" @click="closePanels">بی‌خیال</button>
                </div>
              </div>
            </Transition>

            <Transition name="panel">
              <div v-if="reschedulingId === item.id" class="panel">
                <p class="panel__title">زمان تازه برای {{ item.patientName }}</p>

                <ul class="rail rail--compact" aria-label="روز مقصد">
                  <li v-for="day in rescheduleChips" :key="day.date">
                    <button
                      class="rail__chip"
                      :class="{ 'rail__chip--picked': day.date === rescheduleDate }"
                      type="button"
                      :aria-pressed="day.date === rescheduleDate"
                      :aria-current="day.isToday ? 'date' : undefined"
                      @click="chooseRescheduleDate(day.date)"
                    >
                      <span class="rail__weekday">{{ day.weekday }}</span>
                      <span class="rail__caption">{{ day.caption }}</span>
                    </button>
                  </li>
                </ul>

                <p v-if="rescheduleLoading" class="muted" aria-live="polite">در حال گرفتن ساعت‌های آزاد…</p>

                <p v-else-if="rescheduleSlots.length === 0" class="muted">
                  این روز وقت آزاد ندارد. روز دیگری را انتخاب کنید.
                </p>

                <ul v-else class="slots stagger">
                  <li
                    v-for="(slot, index) in rescheduleSlots"
                    :key="slot.startUtc"
                    :style="{ '--i': Math.min(index, 12) }"
                  >
                    <button
                      class="slot"
                      :class="{ 'slot--picked': slot.startUtc === rescheduleStart }"
                      type="button"
                      :aria-pressed="slot.startUtc === rescheduleStart"
                      @click="rescheduleStart = slot.startUtc"
                    >
                      {{ timeLabel(slot.startUtc) }}
                    </button>
                  </li>
                </ul>

            <p v-if="rescheduleError" class="form-error" role="alert">{{ rescheduleError }}</p>

            <div class="row">
              <button
                class="btn btn--primary btn--sm"
                type="button"
                :disabled="!rescheduleStart || rescheduleBusy"
                @click="confirmReschedule"
              >
                {{ rescheduleBusy ? 'در حال ثبت…' : 'ثبت جابه‌جایی' }}
              </button>
              <button class="btn btn--quiet btn--sm" type="button" @click="closePanels">انصراف</button>
            </div>
              </div>
            </Transition>
          </article>
        </li>
      </TransitionGroup>
    </template>

    <div v-else class="empty day__empty">
      <div class="day__empty-art" aria-hidden="true">
        <SlotField :rows="3" :cols="10" tone="light" sweep />
      </div>
      <p class="empty__title">این روز نوبتی ندارد.</p>
      <p v-if="staffFilter">با فیلتر پرسنل دیگری هم امتحان کنید.</p>
      <p v-else>بیمار با لینک مطب وقت می‌گیرد و همین‌جا دیده می‌شود.</p>
      <button class="btn btn--ghost" type="button" @click="goToday">برو به امروز</button>
    </div>
  </section>
</template>

<style scoped>
.day {
  gap: var(--space-4);
}

.day__heading {
  display: grid;
  gap: 2px;
}

.day__meta {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.day__clock {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: 0.8rem;
  color: var(--ink-soft);
  font-variant-numeric: tabular-nums;
  padding: 4px 12px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: var(--surface);
}

.day__pulse {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--accent);
  box-shadow: 0 0 0 3px var(--accent-soft);
}

/* Date flips: the label slides like a page of a desk calendar. */
.date-flip-enter-active {
  transition:
    opacity var(--motion-delight) var(--ease-out-soft),
    transform var(--motion-delight) var(--ease-spring);
}

.date-flip-leave-active {
  position: absolute;
  transition: opacity 110ms var(--ease);
}

.date-flip-enter-from {
  opacity: 0;
  transform: translate3d(0, -10px, 0);
}

.date-flip-leave-to {
  opacity: 0;
}

.date-flip-move {
  transition: transform 300ms var(--ease-out-soft);
}

/* ---------- controls ---------- */

.daybar {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
  background:
    linear-gradient(180deg, var(--surface), var(--surface-2));
}

.daybar__nav {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}

.daybar__date {
  font-weight: 700;
  font-size: 1.02rem;
  letter-spacing: -0.01em;
  text-align: center;
}

.rail {
  list-style: none;
  margin: 0;
  padding: 0 0 var(--space-1);
  display: flex;
  gap: var(--space-2);
  overflow-x: auto;
  scrollbar-width: thin;
}

.rail__chip {
  display: grid;
  gap: 1px;
  min-width: 86px;
  padding: 7px 12px;
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
  background: var(--surface);
  font: inherit;
  text-align: center;
  cursor: pointer;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}@media (hover: hover) and (pointer: fine) {
  .rail__chip:hover {
    border-color: var(--line-strong);
    background: var(--surface-2);
  }
}

.rail__chip--picked {
  border-color: var(--brand-deep);
  background: var(--brand-deep);
  color: var(--on-dark);
}

.rail__weekday {
  font-size: 0.8rem;
  font-weight: 500;
}

.rail__caption {
  font-size: 0.7rem;
  color: var(--ink-soft);
}

.rail__chip--picked .rail__caption {
  color: var(--on-dark-soft);
}

.rail__today {
  font-size: 0.62rem;
  color: var(--accent);
}

.rail__chip--picked .rail__today {
  color: #e9a583;
}

.filters {
  list-style: none;
  margin: 0;
  padding: var(--space-3) 0 0;
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
  border-block-start: 1px dashed var(--line-strong);
}

.filter {
  padding: 4px 13px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: var(--surface);
  font: inherit;
  font-size: 0.82rem;
  color: var(--ink-soft);
  cursor: pointer;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}

.filter:hover {
  border-color: var(--line-strong);
}

.filter--picked {
  border-color: var(--brand);
  background: var(--brand-soft);
  color: var(--brand);
}

/* ---------- timeline ---------- */

.timeline {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: var(--space-3);
  position: relative;
}

/* Card enter/exit/cancel keeps the whole day in one calm choreography. */
.slip-enter-active {
  transition:
    opacity var(--motion-delight) var(--ease-out-soft),
    transform var(--motion-delight) var(--ease-spring);
}

.slip-leave-active {
  transition:
    opacity 200ms var(--ease),
    transform 200ms var(--ease);
}

.slip-enter-from {
  opacity: 0;
  transform: translate3d(0, 16px, 0) scale(0.985);
}

.slip-leave-to {
  opacity: 0;
  transform: translate3d(0, -8px, 0) scale(0.985);
}

.slip-move {
  transition: transform 420ms var(--ease-out-soft);
}

.panel-enter-active {
  transition:
    opacity 240ms var(--ease-out-soft),
    transform 240ms var(--ease-spring);
}

.panel-leave-active {
  transition:
    opacity 140ms var(--ease),
    transform 140ms var(--ease);
}

.panel-enter-from {
  opacity: 0;
  transform: translate3d(0, -6px, 0);
}

.panel-leave-to {
  opacity: 0;
  transform: translate3d(0, -4px, 0);
}

.timeline__row {
  display: grid;
  grid-template-columns: 66px 24px minmax(0, 1fr);
  gap: var(--space-3);
  align-items: stretch;
}

.timeline__time {
  display: grid;
  align-content: start;
  justify-items: end;
  gap: 1px;
  padding-block-start: 10px;
  font-variant-numeric: tabular-nums;
  line-height: 1.2;
}

.timeline__time strong {
  font-size: 1.02rem;
}

.timeline__time span {
  font-size: 0.68rem;
  color: var(--ink-faint);
}

.timeline__axis {
  position: relative;
  display: flex;
  justify-content: center;
}

.timeline__axis::before {
  content: '';
  position: absolute;
  inset-block: 0;
  inset-inline-start: calc(50% - 0.5px);
  width: 1px;
  background: var(--line-strong);
}

.timeline__axis::after {
  content: '';
  position: absolute;
  inset-block-end: 0;
  inset-inline-start: calc(50% - 0.5px);
  width: 1px;
  height: 8px;
  background: linear-gradient(var(--line-strong), transparent);
}

.timeline__dot {
  position: relative;
  z-index: 1;
  margin-block-start: 14px;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--surface);
  border: 2px solid var(--brand);
  box-shadow: 0 0 0 3px var(--paper);
  transform: translateZ(0);
}

.timeline__row--cancelled .timeline__dot {
  border-color: var(--line-strong);
  background: var(--paper);
}

.slip {
  position: relative;
  display: grid;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--line);
  border-inline-start: 3px solid var(--brand);
  border-radius: var(--radius-card);
  background: var(--surface);
  box-shadow: var(--shadow-card);
}

/* Hover lift via the shared shadow-layer pattern (opacity only). */
.slip::after {
  content: '';
  position: absolute;
  inset: 0;
  border-radius: inherit;
  box-shadow: var(--shadow-lift);
  opacity: 0;
  transition: opacity var(--motion-fast) var(--ease);
  pointer-events: none;
  z-index: 1;
}

.slip {
  transition: transform var(--motion-fast) var(--ease-out-soft);
}

@media (hover: hover) and (pointer: fine) {
  .slip:hover::after {
    opacity: 1;
  }

  .slip:hover {
    transform: translate3d(0, -2px, 0);
  }
}

.slip > * {
  position: relative;
  z-index: 2;
}

.slip__now {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  justify-self: start;
  font-size: 0.7rem;
  font-weight: 500;
  color: var(--accent-deep);
  padding: 2px 10px;
  border-radius: var(--radius-pill);
  background: var(--accent-soft);
}.timeline__row--cancelled .slip {
  border-inline-start-color: var(--line-strong);
  background: var(--surface-2);
}

.timeline__row--cancelled .slip__name {
  color: var(--ink-soft);
}

.timeline__row--now .slip {
  border-inline-start-color: var(--accent);
  box-shadow: 0 1px 2px rgba(11, 31, 28, 0.05), 0 22px 44px -34px rgba(180, 71, 26, 0.55);
}

/* A ping ring that scales/opacity only — a box-shadow breathe would repaint every frame. */
.timeline__row--now .timeline__dot {
  border-color: var(--accent);
}

.timeline__row--now .timeline__dot::after {
  content: '';
  position: absolute;
  inset: -6px;
  border-radius: 50%;
  border: 2px solid rgba(180, 71, 26, 0.55);
  animation: dot-ping 2.4s var(--ease-out) infinite;
}

@keyframes dot-ping {
  0% {
    transform: scale(0.5);
    opacity: 0.9;
  }

  70%,
  100% {
    transform: scale(1.6);
    opacity: 0;
  }
}

.slip__head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-3);
}

.slip__who {
  display: grid;
  gap: 2px;
  min-width: 0;
}

.slip__name {
  font-size: 1rem;
  font-weight: 700;
  overflow-wrap: anywhere;
  line-height: 1.5;
}

.slip__meta {
  font-size: 0.8rem;
  color: var(--ink-soft);
}

.slip__facts {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  font-size: 0.8rem;
  color: var(--ink-soft);
}

.slip__price {
  font-variant-numeric: tabular-nums;
}

.slip__phone {
  font-variant-numeric: tabular-nums;
  color: var(--ink);
  padding-inline-start: var(--space-3);
  border-inline-start: 1px dashed var(--line-strong);
}

.slip__note {
  font-size: 0.78rem;
  color: var(--accent);
}

.slip__actions {
  display: flex;
  gap: var(--space-2);
  flex-wrap: wrap;
  padding-block-start: var(--space-1);
}

.slip__cancel {
  color: var(--danger);
  border-color: rgba(163, 39, 29, 0.24);
}

@media (hover: hover) and (pointer: fine) {
  .slip__cancel:hover:not(:disabled) {
    background: var(--danger-soft);
    border-color: rgba(163, 39, 29, 0.4);
  }
}

/* ---------- inline panels ---------- */

/* Entry lives in the <Transition>; the border draws itself in like a pen. */
.panel {
  display: grid;
  gap: var(--space-3);
  margin-block-start: var(--space-2);
  padding-block-start: var(--space-3);
  border-block-start: 1px dashed var(--line-strong);
  animation: panel-line 360ms var(--ease-out-soft) both;
}

@keyframes panel-line {
  from {
    background-image: linear-gradient(var(--line-strong), var(--line-strong));
    background-size: 100% 0%;
    background-repeat: no-repeat;
    background-position-y: 0;
  }

  to {
    background-image: linear-gradient(var(--line-strong), var(--line-strong));
    background-size: 100% 1px;
    background-repeat: no-repeat;
    background-position-y: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .panel {
    animation: none;
  }
}

.panel__title {
  font-size: 0.9rem;
  font-weight: 500;
}

.rail--compact .rail__chip {
  min-width: 78px;
  padding: 5px 10px;
}

.slots {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.slots.stagger > * {
  animation: rise-in var(--motion-delight) var(--ease-out-soft) both;
  animation-delay: calc(var(--i, 0) * 30ms);
}

.slot {
  min-width: 74px;
  min-height: 36px;
  padding: 5px 10px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: var(--surface);
  font: inherit;
  font-size: 0.86rem;
  font-variant-numeric: tabular-nums;
  cursor: pointer;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}

.slot:hover {
  border-color: var(--line-strong);
}

.slot--picked {
  border-color: var(--brand);
  background: var(--brand);
  color: #fff;
}

/* ---------- empty ---------- */

.day__empty {
  padding-block: var(--space-6);
}

.day__empty-art {
  width: min(320px, 80%);
  opacity: 0.55;
  margin-block-end: var(--space-2);
}

@media (max-width: 720px) {
  .day__head {
    align-items: flex-start;
  }

  .timeline__row {
    grid-template-columns: 52px 20px minmax(0, 1fr);
    gap: var(--space-2);
  }

  .timeline__time strong {
    font-size: 0.92rem;
  }

  .slip {
    padding: var(--space-3);
  }

  .daybar__nav {
    flex-wrap: wrap;
    justify-content: center;
  }

  .daybar__date {
    order: -1;
    width: 100%;
  }
}

@media (max-width: 420px) {
  .timeline__row {
    grid-template-columns: 46px 16px minmax(0, 1fr);
  }

  .slip__head {
    flex-wrap: wrap;
  }
}
</style>
