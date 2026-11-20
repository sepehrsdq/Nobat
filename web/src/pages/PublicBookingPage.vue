<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { ApiError, apiFetch } from '../lib/api'
import { formatMinutes, formatToman, toPersianDigits } from '../lib/format'

interface PublicClinic {
  slug: string
  name: string
  services: Array<{ id: string; name: string; durationMinutes: number; priceRial: number }>
  staff: Array<{ id: string; displayName: string; title: string | null }>
}

interface Slot {
  startUtc: string
  endUtc: string
}

interface DayOption {
  date: string
  label: string
  caption: string
}

interface BookedAppointment {
  id: string
  startUtc: string
  endUtc: string
  staffName: string
  serviceName: string
  durationMinutes: number
  priceRial: number
}

const route = useRoute()
const slug = String(route.params.slug ?? '')

const clinic = ref<PublicClinic | null>(null)
const loading = ref(true)
const loadError = ref('')

const serviceId = ref('')
const staffId = ref('')
const days = ref<DayOption[]>([])
const selectedDate = ref('')
const slots = ref<Slot[]>([])
const slotsLoading = ref(false)
const selectedStart = ref('')

const patientName = ref('')
const phone = ref('')
const code = ref('')
const codeSent = ref(false)
const devCode = ref<string | null>(null)
const otpBusy = ref(false)

const booking = ref(false)
const formError = ref('')
const booked = ref<BookedAppointment | null>(null)

const selectedService = computed(
  () => clinic.value?.services.find((service) => service.id === serviceId.value) ?? null,
)
const selectedStaff = computed(
  () => clinic.value?.staff.find((member) => member.id === staffId.value) ?? null,
)

/** Tehran "today", as an ISO date, so the day chips match the server's idea of a day. */
function buildDays(): DayOption[] {
  const todayIso = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Tehran' }).format(new Date())
  const [year, month, day] = todayIso.split('-').map(Number)
  const base = Date.UTC(year ?? 2026, (month ?? 1) - 1, day ?? 1)

  return Array.from({ length: 14 }, (_, index) => {
    const iso = new Date(base + index * 86_400_000).toISOString().slice(0, 10)
    const at = new Date(`${iso}T00:00:00Z`)

    return {
      date: iso,
      label: new Intl.DateTimeFormat('fa-IR', { weekday: 'long', timeZone: 'UTC' }).format(at),
      caption: new Intl.DateTimeFormat('fa-IR', { day: 'numeric', month: 'long', timeZone: 'UTC' }).format(at),
    }
  })
}

function timeLabel(iso: string): string {
  const formatted = new Intl.DateTimeFormat('fa-IR', {
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'Asia/Tehran',
  }).format(new Date(iso))

  return toPersianDigits(formatted)
}

function dayLabel(iso: string): string {
  const at = new Date(iso)

  return new Intl.DateTimeFormat('fa-IR', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    timeZone: 'Asia/Tehran',
  }).format(at)
}

async function load() {
  loading.value = true
  loadError.value = ''

  try {
    clinic.value = await apiFetch<PublicClinic>(`/api/public/${slug}`)
    days.value = buildDays()
    selectedDate.value = days.value[0]?.date ?? ''

    if (clinic.value.staff.length === 1 && clinic.value.staff[0]) {
      staffId.value = clinic.value.staff[0].id
    }
  } catch (error) {
    loadError.value =
      error instanceof ApiError ? error.message : 'ارتباط با سرور برقرار نشد.'
  } finally {
    loading.value = false
  }
}

function chooseService(id: string) {
  serviceId.value = id
  selectedStart.value = ''
  slots.value = []
  formError.value = ''

  if (selectedDate.value) {
    void loadSlots()
  }
}

function chooseStaff(id: string) {
  staffId.value = id
  selectedStart.value = ''
  slots.value = []
  formError.value = ''

  if (selectedDate.value) {
    void loadSlots()
  }
}

async function chooseDate(date: string) {
  selectedDate.value = date
  selectedStart.value = ''
  await loadSlots()
}

async function loadSlots() {
  if (!serviceId.value || !staffId.value || !selectedDate.value) {
    return
  }

  slotsLoading.value = true
  slots.value = []
  formError.value = ''

  try {
    slots.value = await apiFetch<Slot[]>(
      `/api/public/${slug}/slots?serviceId=${serviceId.value}&staffId=${staffId.value}&date=${selectedDate.value}`,
    )
  } catch (error) {
    formError.value = error instanceof ApiError ? error.message : 'دریافت ساعت‌های آزاد ناموفق بود.'
  } finally {
    slotsLoading.value = false
  }
}

function chooseSlot(start: string) {
  selectedStart.value = start
  formError.value = ''
  codeSent.value = false
  devCode.value = null
  code.value = ''
}

async function requestCode() {
  formError.value = ''

  if (!/^09\d{9}$/.test(phone.value.trim())) {
    formError.value = 'شمارهٔ موبایل را درست وارد کنید. نمونه: ۰۹۱۲۱۲۳۴۵۶۷'
    return
  }

  otpBusy.value = true

  try {
    const result = await apiFetch<{ expiresInSeconds: number; devCode: string | null }>(
      `/api/public/${slug}/otp`,
      {
        method: 'POST',
        body: JSON.stringify({ phone: phone.value.trim() }),
      },
    )

    codeSent.value = true
    devCode.value = result.devCode
  } catch (error) {
    formError.value = error instanceof ApiError ? error.message : 'ارسال کد ناموفق بود.'
  } finally {
    otpBusy.value = false
  }
}

async function submit() {
  formError.value = ''

  if (patientName.value.trim().length === 0) {
    formError.value = 'نام و نام خانوادگی را بنویسید.'
    return
  }

  if (!selectedStart.value) {
    formError.value = 'یک ساعت انتخاب کنید.'
    return
  }

  booking.value = true

  try {
    booked.value = await apiFetch<BookedAppointment>(`/api/public/${slug}/appointments`, {
      method: 'POST',
      body: JSON.stringify({
        serviceId: serviceId.value,
        staffId: staffId.value,
        startUtc: selectedStart.value,
        patientName: patientName.value.trim(),
        phone: phone.value.trim(),
        code: code.value.trim(),
      }),
    })
  } catch (error) {
    formError.value = error instanceof ApiError ? error.message : 'ثبت نوبت ناموفق بود.'

    if (
      error instanceof ApiError &&
      (error.type === 'appointment.slot_taken' || error.type === 'appointment.slot_unavailable')
    ) {
      selectedStart.value = ''
      codeSent.value = false
      devCode.value = null
      code.value = ''
      await loadSlots()
    }
  } finally {
    booking.value = false
  }
}

function startOver() {
  booked.value = null
  selectedStart.value = ''
  codeSent.value = false
  devCode.value = null
  code.value = ''
  patientName.value = ''
  void loadSlots()
}

onMounted(load)
</script>

<template>
  <div class="booking">
    <header class="booking__bar">
      <div class="booking__inner">
        <span class="booking__brand">نوبت</span>
        <span v-if="clinic" class="booking__clinic">{{ clinic.name }}</span>
      </div>
    </header>

    <main class="booking__main">
      <p v-if="loadError" class="form-error" role="alert">{{ loadError }}</p>

      <p v-else-if="loading" class="muted" aria-live="polite">در حال بارگذاری…</p>

      <article v-else-if="booked" class="card done">
        <span class="done__mark" aria-hidden="true">✓</span>
        <h1>نوبت شما ثبت شد</h1>
        <p class="muted">
          {{ dayLabel(booked.startUtc) }} ساعت {{ timeLabel(booked.startUtc) }}
        </p>

        <ul class="done__facts">
          <li><span>خدمت</span><strong>{{ booked.serviceName }}</strong></li>
          <li><span>پزشک</span><strong>{{ booked.staffName }}</strong></li>
          <li><span>مدت</span><strong>{{ formatMinutes(booked.durationMinutes) }}</strong></li>
          <li><span>هزینه</span><strong>{{ formatToman(booked.priceRial) }}</strong></li>
          <li><span>کد پیگیری</span><strong dir="ltr">{{ booked.id.slice(0, 8) }}</strong></li>
        </ul>

        <p class="done__hint muted">
          این کد را نگه دارید. برای تغییر یا لغو، با مطب تماس بگیرید.
        </p>

        <button class="btn btn--ghost btn--block" type="button" @click="startOver">
          گرفتن نوبت دیگر
        </button>
      </article>

      <template v-else-if="clinic">
        <section class="card step">
          <header class="step__head">
            <h1>{{ clinic.name }}</h1>
            <p class="muted">خدمت، پزشک و ساعت را انتخاب کنید.</p>
          </header>

          <h2 class="step__title">۱. خدمت</h2>

          <div v-if="clinic.services.length === 0" class="empty">
            <p class="empty__title">این مطب هنوز خدمتی ثبت نکرده است.</p>
            <p>کمی بعد دوباره سر بزنید.</p>
          </div>

          <ul v-else class="options">
            <li v-for="service in clinic.services" :key="service.id">
              <button
                class="option"
                :class="{ 'option--picked': service.id === serviceId }"
                type="button"
                :aria-pressed="service.id === serviceId"
                @click="chooseService(service.id)"
              >
                <span class="option__title">{{ service.name }}</span>
                <span class="option__meta">
                  {{ formatMinutes(service.durationMinutes) }} · {{ formatToman(service.priceRial) }}
                </span>
              </button>
            </li>
          </ul>
        </section>

        <section v-if="serviceId" class="card step">
          <h2 class="step__title">۲. پزشک</h2>

          <ul v-if="clinic.staff.length > 0" class="options">
            <li v-for="member in clinic.staff" :key="member.id">
              <button
                class="option"
                :class="{ 'option--picked': member.id === staffId }"
                type="button"
                :aria-pressed="member.id === staffId"
                @click="chooseStaff(member.id)"
              >
                <span class="option__title">{{ member.displayName }}</span>
                <span class="option__meta">{{ member.title || 'پزشک' }}</span>
              </button>
            </li>
          </ul>

          <p v-else class="muted">برای این مطب پزشکی ثبت نشده است.</p>
        </section>

        <section v-if="serviceId && staffId" class="card step">
          <h2 class="step__title">۳. روز</h2>

          <ul class="days">
            <li v-for="day in days" :key="day.date">
              <button
                class="day"
                :class="{ 'day--picked': day.date === selectedDate }"
                type="button"
                :aria-pressed="day.date === selectedDate"
                @click="chooseDate(day.date)"
              >
                <span class="day__name">{{ day.label }}</span>
                <span class="day__caption">{{ day.caption }}</span>
              </button>
            </li>
          </ul>

          <h2 class="step__title step__title--gap">۴. ساعت</h2>

          <p v-if="slotsLoading" class="muted" aria-live="polite">در حال گرفتن ساعت‌های آزاد…</p>

          <p v-else-if="slots.length === 0" class="muted">
            این روز وقت آزاد ندارد. روز دیگری را امتحان کنید.
          </p>

          <ul v-else class="slots">
            <li v-for="slot in slots" :key="slot.startUtc">
              <button
                class="slot"
                :class="{ 'slot--picked': slot.startUtc === selectedStart }"
                type="button"
                :aria-pressed="slot.startUtc === selectedStart"
                @click="chooseSlot(slot.startUtc)"
              >
                {{ timeLabel(slot.startUtc) }}
              </button>
            </li>
          </ul>
        </section>

        <section v-if="selectedStart" class="card step">
          <h2 class="step__title">۵. اطلاعات شما</h2>

          <p class="step__summary">
            {{ selectedService?.name }} · {{ selectedStaff?.displayName }} ·
            {{ dayLabel(selectedStart) }} ساعت {{ timeLabel(selectedStart) }}
          </p>

          <form class="stack" novalidate @submit.prevent="submit">
            <div class="field">
              <label class="field__label" for="patient-name">نام و نام خانوادگی</label>
              <input
                id="patient-name"
                v-model="patientName"
                class="field__control"
                required
                maxlength="120"
                autocomplete="name"
              />
            </div>

            <div class="field">
              <label class="field__label" for="patient-phone">شمارهٔ موبایل</label>
              <div class="phone">
                <input
                  id="patient-phone"
                  v-model="phone"
                  class="field__control"
                  dir="ltr"
                  inputmode="numeric"
                  required
                  maxlength="11"
                  autocomplete="tel"
                  placeholder="09121234567"
                />
                <button
                  class="btn btn--ghost"
                  type="button"
                  :disabled="otpBusy || codeSent"
                  @click="requestCode"
                >
                  {{ codeSent ? 'کد فرستاده شد' : otpBusy ? 'در حال ارسال…' : 'دریافت کد' }}
                </button>
              </div>
            </div>

            <div v-if="codeSent" class="field">
              <label class="field__label" for="patient-code">کد تأیید</label>
              <input
                id="patient-code"
                v-model="code"
                class="field__control field__control--code"
                dir="ltr"
                inputmode="numeric"
                autocomplete="one-time-code"
                maxlength="6"
                required
              />
              <p v-if="devCode" class="field__hint">
                حالت آزمایشی — کد شما: <strong>{{ toPersianDigits(devCode) }}</strong>
              </p>
              <p v-else class="field__hint">کد شش رقمی به پیامک شما رسیده است.</p>
            </div>

            <p v-if="formError" class="form-error" role="alert">{{ formError }}</p>

            <button class="btn btn--primary btn--block" type="submit" :disabled="booking || !codeSent">
              {{ booking ? 'در حال ثبت…' : 'ثبت نوبت' }}
            </button>
          </form>
        </section>
      </template>
    </main>

    <footer class="booking__footer muted">نوبت — نوبت‌دهی آنلاین مطب</footer>
  </div>
</template>

<style scoped>
.booking {
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}

.booking__bar {
  background: var(--surface);
  border-block-end: 1px solid var(--line);
}

.booking__inner {
  width: 100%;
  max-width: 720px;
  margin-inline: auto;
  min-height: 60px;
  padding-inline: var(--space-4);
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.booking__brand {
  font-weight: 700;
  color: var(--brand);
}

.booking__clinic {
  font-size: 0.9rem;
  color: var(--ink-soft);
  border-inline-start: 1px solid var(--line);
  padding-inline-start: var(--space-3);
}

.booking__main {
  width: 100%;
  max-width: 720px;
  margin-inline: auto;
  padding: var(--space-4);
  display: grid;
  gap: var(--space-4);
  flex: 1;
  align-content: start;
}

.booking__footer {
  text-align: center;
  font-size: 0.78rem;
  padding: var(--space-5) var(--space-4);
}

.step {
  display: grid;
  gap: var(--space-3);
}

.step__head h1 {
  font-size: 1.3rem;
}

.step__head p {
  font-size: 0.9rem;
}

.step__title {
  font-size: 0.95rem;
  color: var(--ink-soft);
  font-weight: 500;
}

.step__title--gap {
  margin-block-start: var(--space-3);
}

.step__summary {
  font-size: 0.9rem;
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-input);
  background: var(--brand-soft);
  color: var(--brand);
}

.options {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: var(--space-2);
}

.option {
  width: 100%;
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
  background: var(--surface);
  font: inherit;
  text-align: start;
  cursor: pointer;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease);
}

.option:hover {
  border-color: #c6cfd2;
}

.option--picked {
  border-color: var(--brand);
  background: var(--brand-soft);
}

.option__title {
  font-weight: 500;
}

.option__meta {
  font-size: 0.82rem;
  color: var(--ink-soft);
}

.days {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  gap: var(--space-2);
  flex-wrap: wrap;
}

.day {
  display: grid;
  gap: 2px;
  min-width: 82px;
  padding: var(--space-2) var(--space-3);
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
  background: var(--surface);
  font: inherit;
  cursor: pointer;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease);
}

.day:hover {
  border-color: #c6cfd2;
}

.day--picked {
  border-color: var(--brand);
  background: var(--brand-soft);
}

.day__name {
  font-weight: 500;
  font-size: 0.86rem;
}

.day__caption {
  font-size: 0.72rem;
  color: var(--ink-soft);
}

.slots {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.slot {
  min-width: 76px;
  padding: 8px 10px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: var(--surface);
  font: inherit;
  font-variant-numeric: tabular-nums;
  cursor: pointer;
  transition:
    border-color var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease);
}

.slot:hover {
  border-color: #c6cfd2;
}

.slot--picked {
  border-color: var(--brand);
  background: var(--brand);
  color: #fff;
}

.phone {
  display: flex;
  gap: var(--space-2);
  align-items: center;
}

.phone .field__control {
  flex: 1;
}

.field__control--code {
  max-width: 180px;
  letter-spacing: 0.35em;
  text-align: center;
  font-variant-numeric: tabular-nums;
}

.done {
  display: grid;
  justify-items: center;
  gap: var(--space-3);
  text-align: center;
}

.done__mark {
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  border-radius: 50%;
  background: var(--brand-soft);
  color: var(--brand);
  font-size: 1.2rem;
}

.done__facts {
  list-style: none;
  margin: 0;
  padding: 0;
  width: 100%;
  display: grid;
  gap: var(--space-2);
  text-align: start;
}

.done__facts li {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
  padding-block: 6px;
  border-block-end: 1px solid var(--line);
  font-size: 0.9rem;
}

.done__facts span {
  color: var(--ink-soft);
}

.done__hint {
  font-size: 0.82rem;
}

@media (max-width: 480px) {
  .phone {
    flex-direction: column;
    align-items: stretch;
  }

  .option {
    flex-direction: column;
    align-items: flex-start;
    gap: 2px;
  }
}
</style>
