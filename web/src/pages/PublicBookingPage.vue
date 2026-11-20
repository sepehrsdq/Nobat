<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import BrandMark from '../components/BrandMark.vue'
import ConfettiBurst from '../components/ConfettiBurst.vue'
import LoadingRows from '../components/LoadingRows.vue'
import SlotField from '../components/SlotField.vue'
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
        <span class="booking__brand">
          <BrandMark :size="24" />
          <span>نوبت</span>
        </span>
        <span v-if="clinic" class="booking__clinic">{{ clinic.name }}</span>
        <span class="booking__tag">نوبت آنلاین</span>
      </div>
    </header>

    <main class="booking__main">
      <p v-if="loadError" class="form-error" role="alert">{{ loadError }}</p>

      <LoadingRows v-else-if="loading" variant="grid" :rows="3" />

      <template v-else-if="booked">
        <Transition name="ticket">
          <article :key="booked.id" class="ticket">
            <ConfettiBurst :count="22" />
            <div class="ticket__body">
              <span class="ticket__stamp" aria-hidden="true">✓</span>
              <p class="eyebrow">تأیید شد</p>
              <h1 class="display--sm">نوبت شما ثبت شد.</h1>
              <p class="ticket__when">{{ dayLabel(booked.startUtc) }} · {{ timeLabel(booked.startUtc) }}</p>

              <dl class="ticket__rows">
                <div><dt>خدمت</dt><dd>{{ booked.serviceName }}</dd></div>
                <div><dt>پزشک</dt><dd>{{ booked.staffName }}</dd></div>
                <div><dt>مدت</dt><dd>{{ formatMinutes(booked.durationMinutes) }}</dd></div>
                <div><dt>هزینه</dt><dd>{{ formatToman(booked.priceRial) }}</dd></div>
              </dl>
            </div>

            <div class="ticket__stub">
              <p class="ticket__code-label">کد پیگیری</p>
              <p class="ticket__code" dir="ltr">{{ booked.id.slice(0, 8) }}</p>
              <p class="ticket__hint">این کد را نگه دارید. برای تغییر یا لغو، با مطب تماس بگیرید.</p>
              <button class="btn btn--ghost btn--block" type="button" @click="startOver">
                گرفتن نوبت دیگر
              </button>
            </div>
          </article>
        </Transition>
      </template>

      <template v-else-if="clinic">
        <section v-spotlight class="hero">
          <div class="hero__text">
            <p class="eyebrow eyebrow--inverse">وقت خالی، بدون تلفن</p>
            <h1 class="display hero__title">
              <span class="hero__line" v-reveal="60">{{ clinic.name }}</span>
            </h1>
            <p class="hero__copy" v-reveal="140">
              خدمت و پزشک را انتخاب کنید؛ ساعت‌های خالی همین‌جا هستند و نوبت با یک کد پیامکی
              تأیید می‌شود.
            </p>
          </div>
          <div class="hero__art" aria-hidden="true">
            <SlotField :rows="4" :cols="9" tone="dark" sweep />
          </div>
        </section>

        <div class="layout">
          <div class="steps">
            <section v-spotlight v-reveal class="step">
              <header class="step__head">
                <span class="step__index">۰۱</span>
                <h2>خدمت</h2>
              </header>

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
                    <span v-if="service.id === serviceId" class="option__check" aria-hidden="true">✓</span>
                  </button>
                </li>
              </ul>
            </section>

            <section v-if="serviceId" v-reveal v-spotlight class="step">
              <header class="step__head">
                <span class="step__index">۰۲</span>
                <h2>پزشک</h2>
              </header>

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
                    <span v-if="member.id === staffId" class="option__check" aria-hidden="true">✓</span>
                  </button>
                </li>
              </ul>

              <p v-else class="muted">برای این مطب پزشکی ثبت نشده است.</p>
            </section>

            <section v-if="serviceId && staffId" v-reveal v-spotlight class="step">
              <header class="step__head">
                <span class="step__index">۰۳</span>
                <h2>روز و ساعت</h2>
              </header>

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

              <div v-if="slotsLoading" class="slot-skeleton" aria-hidden="true">
                <span v-for="n in 10" :key="n" class="skeleton slot-skeleton__pill" />
              </div>
              <p v-else-if="slots.length === 0" class="muted" aria-live="polite">
                این روز وقت آزاد ندارد. روز دیگری را امتحان کنید.
              </p>

              <ul v-else class="slots stagger">
                <li v-for="(slot, index) in slots" :key="slot.startUtc" :style="{ '--i': Math.min(index, 12) }">
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

            <section v-if="selectedStart" v-reveal v-spotlight class="step">
              <header class="step__head">
                <span class="step__index">۰۴</span>
                <h2>اطلاعات شما</h2>
              </header>

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
          </div>

          <aside v-spotlight class="summary" aria-label="خلاصهٔ انتخاب">
            <p class="eyebrow">خلاصهٔ نوبت</p>
            <dl class="summary__rows">
              <div><dt>خدمت</dt><dd>{{ selectedService?.name ?? 'انتخاب نشده' }}</dd></div>
              <div><dt>پزشک</dt><dd>{{ selectedStaff?.displayName ?? 'انتخاب نشده' }}</dd></div>
              <div>
                <dt>روز</dt>
                <dd>{{ selectedStart ? dayLabel(selectedStart) : 'انتخاب نشده' }}</dd>
              </div>
              <div>
                <dt>ساعت</dt>
                <dd>{{ selectedStart ? timeLabel(selectedStart) : 'انتخاب نشده' }}</dd>
              </div>
            </dl>

            <p class="summary__price">
              {{ selectedService ? formatToman(selectedService.priceRial) : '—' }}
            </p>

            <p class="summary__hint">
              کد تأیید به موبایل شما می‌رود؛ ثبت نهایی بعد از وارد کردن کد انجام می‌شود.
            </p>
          </aside>
        </div>
      </template>
    </main>

    <footer class="booking__footer">
      <BrandMark :size="18" />
      <span>نوبت — نوبت‌دهی آنلاین مطب</span>
    </footer>
  </div>
</template>

<style scoped>
.booking {
  --booking-width: 980px;
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}

.booking__bar {
  border-block-end: 1px solid var(--line);
  background: rgba(243, 239, 230, 0.8);
  backdrop-filter: blur(8px);
}

.booking__inner {
  width: 100%;
  max-width: var(--booking-width);
  margin-inline: auto;
  min-height: 58px;
  padding-inline: var(--space-4);
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.booking__brand {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-weight: 700;
  color: var(--ink);
}

.booking__clinic {
  font-size: 0.85rem;
  color: var(--ink-soft);
  border-inline-start: 1px solid var(--line-strong);
  padding-inline-start: var(--space-3);
}

.booking__tag {
  margin-inline-start: auto;
  font-size: 0.72rem;
  letter-spacing: 0.12em;
  color: var(--brand);
}

.booking__main {
  width: 100%;
  max-width: var(--booking-width);
  margin-inline: auto;
  padding: var(--space-4);
  display: grid;
  gap: var(--space-4);
  flex: 1;
  align-content: start;
}

/* ---------- hero ---------- */

.hero {
  position: relative;
  overflow: hidden;
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(0, 0.75fr);
  gap: var(--space-5);
  align-items: end;
  padding: clamp(var(--space-5), 4vw, var(--space-6));
  border-radius: var(--radius-card-lg);
  background:
    radial-gradient(620px 300px at 85% -20%, rgba(217, 138, 95, 0.18), transparent 60%),
    radial-gradient(520px 380px at 0% 120%, rgba(10, 91, 81, 0.55), transparent 70%),
    var(--brand-deep);
  color: var(--on-dark);
}

.hero__text {
  display: grid;
  gap: var(--space-3);
}

.hero__title {
  font-size: clamp(2rem, 4.4vw, 3.1rem);
  line-height: 1.12;
}

.hero__copy {
  max-width: 46ch;
  font-size: 0.9rem;
  line-height: 1.95;
  color: var(--on-dark-soft);
}

.hero__art {
  align-self: end;
  opacity: 0.9;
}

/* ---------- layout ---------- */

.layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 280px;
  gap: var(--space-4);
  align-items: start;
}

.steps {
  display: grid;
  gap: var(--space-3);
}

.step {
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-card);
  background: var(--surface);
  box-shadow: var(--shadow-card);
  animation: rise-in 260ms var(--ease-out-soft) both;
}

.step__head {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.step__head h2 {
  font-size: 1rem;
}

.step__index {
  display: grid;
  place-items: center;
  width: 30px;
  height: 30px;
  border-radius: 50%;
  border: 1px solid var(--line-strong);
  font-size: 0.66rem;
  letter-spacing: 0.06em;
  color: var(--ink-soft);
  font-variant-numeric: tabular-nums;
}

.step__summary {
  font-size: 0.86rem;
  padding: var(--space-2) var(--space-3);
  border-radius: var(--radius-input);
  background: var(--brand-soft);
  color: var(--brand-deep);
  animation: rise-in 260ms var(--ease-out-soft) 60ms both;
}

/* ---------- slot skeleton ---------- */

.slot-skeleton {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

.slot-skeleton__pill {
  width: 84px;
  height: 38px;
  border-radius: var(--radius-pill);
}

/* ---------- options ---------- */

.options {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: var(--space-2);
}

.option {
  position: relative;
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

@media (hover: hover) and (pointer: fine) {
  .option:hover {
    border-color: var(--line-strong);
    box-shadow: var(--shadow-card);
  }
}

.option--picked {
  border-color: var(--brand);
  background: var(--brand-soft);
  padding-inline-end: 42px;
}

.option__title {
  font-weight: 500;
}

.option__meta {
  font-size: 0.82rem;
  color: var(--ink-soft);
}

.option__check {
  position: absolute;
  inset-inline-end: 14px;
  inset-block-start: 50%;
  transform: translateY(-50%) scale(0.94);
  display: grid;
  place-items: center;
  width: 20px;
  height: 20px;
  border-radius: 50%;
  background: var(--brand);
  color: #fff;
  font-size: 0.7rem;
  animation: check-in 180ms var(--ease-spring) backwards;
}

@keyframes check-in {
  to {
    transform: translateY(-50%) scale(1);
    opacity: 1;
  }
}

@media (prefers-reduced-motion: reduce) {
  .option__check {
    animation: none;
    transform: translateY(-50%) scale(1);
    opacity: 1;
  }
}

/* ---------- days & slots ---------- */

.days {
  list-style: none;
  margin: 0;
  padding: 0 0 4px;
  display: flex;
  gap: var(--space-2);
  overflow-x: auto;
  scrollbar-width: thin;
}

.day {
  display: grid;
  gap: 1px;
  min-width: 82px;
  padding: var(--space-2) var(--space-3);
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
}

.day:hover {
  border-color: var(--line-strong);
}

.day--picked {
  border-color: var(--brand-deep);
  background: var(--brand-deep);
  color: var(--on-dark);
}

.day__name {
  font-size: 0.84rem;
  font-weight: 500;
}

.day__caption {
  font-size: 0.7rem;
  color: var(--ink-soft);
}

.day--picked .day__caption {
  color: var(--on-dark-soft);
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
  min-width: 74px;
  min-height: 38px;
  padding: 6px 10px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: var(--surface);
  font: inherit;
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

/* ---------- form ---------- */

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

/* ---------- summary ---------- */

.summary {
  position: sticky;
  inset-block-start: var(--space-4);
  display: grid;
  gap: var(--space-3);
  padding: var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-card);
  background: var(--surface-2);
}

.summary__rows {
  display: grid;
  gap: var(--space-1);
  margin: 0;
  font-size: 0.84rem;
}

.summary__rows div {
  display: flex;
  justify-content: space-between;
  gap: var(--space-2);
  padding-block: 4px;
  border-block-end: 1px dashed var(--line);
}

.summary__rows dt {
  color: var(--ink-soft);
}

.summary__rows dd {
  margin: 0;
  text-align: end;
  font-weight: 500;
  overflow-wrap: anywhere;
}

.summary__price {
  font-size: 1.1rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--brand-deep);
}

.summary__hint {
  font-size: 0.76rem;
  line-height: 1.8;
  color: var(--ink-soft);
}

/* ---------- ticket ---------- */

.ticket {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 240px;
  overflow: hidden;
  border: 1px solid var(--line);
  border-radius: var(--radius-card-lg);
  background: var(--surface);
  box-shadow: var(--shadow-lift);
}

.ticket__body {
  display: grid;
  gap: var(--space-2);
  padding: clamp(var(--space-5), 4vw, var(--space-6));
}

.ticket__stamp {
  justify-self: start;
  display: grid;
  place-items: center;
  width: 54px;
  height: 54px;
  margin-block-end: var(--space-2);
  border: 2px solid var(--brand);
  border-radius: 50%;
  color: var(--brand);
  font-size: 1.4rem;
  transform: rotate(-8deg);
  animation: stamp-in var(--motion-delight) var(--ease-out) both;
}

.ticket__when {
  font-size: 0.95rem;
  color: var(--ink-soft);
}

.ticket__rows {
  display: grid;
  gap: 0;
  margin: var(--space-3) 0 0;
}

.ticket__rows div {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: var(--space-3);
  padding-block: 7px;
  border-block-end: 1px solid var(--line);
  font-size: 0.88rem;
}

.ticket__rows dt {
  color: var(--ink-soft);
}

.ticket__rows dd {
  margin: 0;
  font-weight: 500;
}

.ticket__stub {
  display: grid;
  align-content: center;
  gap: var(--space-2);
  padding: var(--space-5);
  background: var(--surface-2);
  border-inline-start: 1px dashed var(--line-strong);
  text-align: center;
}

.ticket__code-label {
  font-size: 0.72rem;
  letter-spacing: 0.1em;
  color: var(--ink-soft);
}

.ticket__code {
  font-size: 1.3rem;
  font-weight: 700;
  letter-spacing: 0.14em;
  font-variant-numeric: tabular-nums;
  color: var(--brand-deep);
}

/* The code is the one thing the patient must keep: let it land last and softly. */
.ticket__code {
  animation: code-in 320ms var(--ease-out-soft) 260ms both;
}

@keyframes code-in {
  from {
    opacity: 0;
    transform: translate3d(0, 10px, 0);
  }

  to {
    opacity: 1;
    transform: none;
  }
}

.ticket__hint {
  font-size: 0.74rem;
  line-height: 1.8;
  color: var(--ink-soft);
}

.ticket__stub .btn {
  margin-block-start: var(--space-2);
}

@keyframes stamp-in {
  from {
    opacity: 0;
    transform: scale(0.9) rotate(-14deg);
  }

  to {
    opacity: 1;
    transform: scale(1) rotate(-8deg);
  }
}

@media (prefers-reduced-motion: reduce) {
  .ticket__code {
    animation: none;
  }
}

/* ---------- footer ---------- */

.booking__footer {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  padding: var(--space-5) var(--space-4);
  font-size: 0.76rem;
  color: var(--ink-faint);
}

@media (max-width: 900px) {
  .layout {
    grid-template-columns: 1fr;
  }

  .summary {
    position: static;
    order: -1;
  }

  .hero {
    grid-template-columns: 1fr;
  }

  .hero__art {
    display: none;
  }
}

/* The ticket is the reward moment: give it a real entrance via <Transition>. */
.ticket-enter-active {
  transition:
    opacity var(--motion-delight) var(--ease-out-soft),
    transform var(--motion-delight) var(--ease-spring);
}

.ticket-enter-from {
  opacity: 0;
  transform: translate3d(0, 14px, 0) scale(0.97);
}

@media (max-width: 700px) {
  .ticket {
    grid-template-columns: 1fr;
  }

  .ticket__stub {
    border-inline-start: 0;
    border-block-start: 1px dashed var(--line-strong);
  }
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

  .option--picked {
    padding-inline-end: var(--space-4);
  }

  .option__check {
    inset-block-start: 14px;
    transform: none;
  }
}
</style>
