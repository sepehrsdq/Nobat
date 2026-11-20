<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import BrandMark from '../components/BrandMark.vue'
import SlotField from '../components/SlotField.vue'
import { ApiError, apiFetch } from '../lib/api'
import { session } from '../lib/auth'

type Mode = 'login' | 'register'

const route = useRoute()
const router = useRouter()

const mode = ref<Mode>('login')
const busy = ref(false)
const errorMessage = ref('')
const signupOpen = ref(true)

const tenantSlug = ref('')
const phone = ref('')
const password = ref('')

const clinicName = ref('')
const ownerName = ref('')
const slug = ref('')

const isRegister = computed(() => mode.value === 'register')

const words = [
  'شنبه ۹ صبح',
  'یک نوبت خالی',
  '۰۹:۳۰',
  'پنجرهٔ پذیرش',
  'کد پیامکی',
  'جابه‌جایی',
  'بدون تلفن',
  'لغو با دلیل',
]

onMounted(async () => {
  try {
    const options = await apiFetch<{ allowPublicSignup: boolean }>('/api/auth/options')
    signupOpen.value = options.allowPublicSignup
  } catch {
    // Keep the default; a failed options call must not lock a real clinic out of signup.
  }

  if (!signupOpen.value && isRegister.value) {
    mode.value = 'login'
  }
})

function switchMode(next: Mode) {
  mode.value = next
  errorMessage.value = ''
}

async function submit() {
  busy.value = true
  errorMessage.value = ''

  try {
    if (isRegister.value) {
      await session.register({
        slug: slug.value.trim(),
        clinicName: clinicName.value.trim(),
        ownerName: ownerName.value.trim(),
        phone: phone.value.trim(),
        password: password.value,
      })
    } else {
      await session.login({
        tenantSlug: tenantSlug.value.trim(),
        phone: phone.value.trim(),
        password: password.value,
      })
    }

    const next = typeof route.query.next === 'string' ? route.query.next : '/panel/appointments'
    await router.replace(next)
  } catch (error) {
    errorMessage.value =
      error instanceof ApiError ? error.message : 'ارتباط با سرور برقرار نشد. دوباره تلاش کنید.'
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <main class="auth">
    <div class="auth__stage">
      <aside class="auth__hero">
        <div class="auth__hero-head">
          <span class="auth__mark"><BrandMark :size="30" live /></span>
          <span class="eyebrow eyebrow--inverse">دفتر نوبت مطب</span>
        </div>

        <h1 class="display auth__headline">
          <span class="auth__line">وقت‌ها را</span>
          <span class="auth__line"><span class="auth__accent">بی‌تلفن</span> پر کنید.</span>
        </h1>

        <p class="auth__copy">
          بیمار از لینک مطب وقت خالی را می‌بیند و با یک کد پیامکی نوبت می‌گیرد. شما همان روز را
          از پنل می‌بندید — لغو، جابه‌جایی، بدون دفتر کاغذی.
        </p>

        <ol class="auth__points">
          <li v-reveal="100"><span>۰۱</span> لینک عمومی هر مطب، بدون اپ</li>
          <li v-reveal="180"><span>۰۲</span> کد پیامکی، بدون حساب بیمار</li>
          <li v-reveal="260"><span>۰۳</span> پنل روز، لغو و جابه‌جایی</li>
        </ol>

        <div class="auth__slots">
          <SlotField :rows="4" :cols="12" tone="dark" sweep />
        </div>

        <div class="auth__ticker ticker" aria-hidden="true">
          <div class="ticker__track">
            <div v-for="copy in 2" :key="copy" class="ticker__group">
              <span v-for="word in words" :key="`${copy}-${word}`" class="ticker__item">
                <span class="ticker__dot" />
                {{ word }}
              </span>
            </div>
          </div>
        </div>
      </aside>

      <section v-tilt="3" class="auth__panel">
        <header class="auth__panel-head">
          <p class="eyebrow">پنل مطب</p>
          <h2 class="display--sm">خوش برگشتید.</h2>
          <p class="muted auth__panel-copy">با همان نشانی و شمارهٔ مطب وارد شوید.</p>
        </header>

        <div v-if="signupOpen" class="auth__tabs" role="tablist" aria-label="ورود یا ثبت مطب">
          <button
            class="auth__tab"
            type="button"
            role="tab"
            :aria-selected="!isRegister"
            @click="switchMode('login')"
          >
            ورود
          </button>
          <button
            class="auth__tab"
            type="button"
            role="tab"
            :aria-selected="isRegister"
            @click="switchMode('register')"
          >
            ثبت مطب
          </button>
        </div>

        <form class="stack auth__form" novalidate @submit.prevent="submit">
          <template v-if="isRegister">
            <div class="field">
              <label class="field__label" for="clinic-name">نام مطب</label>
              <input
                id="clinic-name"
                v-model="clinicName"
                class="field__control"
                required
                maxlength="120"
                autocomplete="organization"
              />
            </div>

            <div class="field">
              <label class="field__label" for="owner-name">نام شما</label>
              <input
                id="owner-name"
                v-model="ownerName"
                class="field__control"
                required
                maxlength="120"
                autocomplete="name"
              />
            </div>

            <div class="field">
              <label class="field__label" for="slug">نشانی مطب</label>
              <input
                id="slug"
                v-model="slug"
                class="field__control"
                dir="ltr"
                required
                minlength="3"
                maxlength="40"
                pattern="[a-z0-9-]{3,40}"
                autocomplete="off"
              />
              <p class="field__hint">حروف لاتین کوچک، رقم و خط تیره؛ بیمار از همین نشانی نوبت می‌گیرد.</p>
            </div>
          </template>

          <div v-else class="field">
            <label class="field__label" for="tenant-slug">نشانی مطب</label>
            <input
              id="tenant-slug"
              v-model="tenantSlug"
              class="field__control"
              dir="ltr"
              required
              autocomplete="username"
              placeholder="demo-clinic"
            />
          </div>

          <div class="field">
            <label class="field__label" for="phone">شمارهٔ موبایل</label>
            <input
              id="phone"
              v-model="phone"
              class="field__control"
              dir="ltr"
              inputmode="numeric"
              required
              autocomplete="tel"
              placeholder="09121234567"
            />
          </div>

          <div class="field">
            <label class="field__label" for="password">رمز عبور</label>
            <input
              id="password"
              v-model="password"
              class="field__control"
              type="password"
              required
              minlength="8"
              :autocomplete="isRegister ? 'new-password' : 'current-password'"
            />
          </div>

          <p v-if="errorMessage" class="form-error" role="alert">{{ errorMessage }}</p>

          <button class="btn btn--primary btn--block auth__submit" type="submit" :disabled="busy">
            {{ busy ? 'در حال ارسال…' : isRegister ? 'ساخت مطب' : 'ورود به پنل' }}
          </button>
        </form>

        <p class="auth__foot muted">
          {{
            signupOpen
              ? 'نسخهٔ ۱.۰ — ثبت مطب باز است'
              : 'ثبت‌نام مطب بسته است؛ برای دمو از حساب آزمایشی استفاده کنید.'
          }}
        </p>
      </section>
    </div>
  </main>
</template>

<style scoped>
.auth {
  min-height: 100vh;
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: clamp(var(--space-3), 4vw, var(--space-7));
}

.auth__stage {
  width: min(1060px, 100%);
  display: grid;
  grid-template-columns: 1.02fr 0.98fr;
  gap: clamp(var(--space-4), 3vw, var(--space-6));
  align-items: stretch;
}

/* ---------- hero ---------- */

.auth__hero {
  position: relative;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: clamp(var(--space-5), 4vw, var(--space-7));
  border-radius: var(--radius-card-lg);
  background:
    radial-gradient(600px 320px at 80% 0%, rgba(217, 138, 95, 0.16), transparent 65%),
    radial-gradient(500px 400px at 10% 100%, rgba(10, 91, 81, 0.55), transparent 70%),
    var(--brand-deep);
  color: var(--on-dark);
  box-shadow: var(--shadow-pop);
  animation: rise-in 420ms var(--ease-out) both;
}

/* A hairline compass ring behind the hero — decoration, so reduced motion kills it. */
.auth__hero::before {
  content: '';
  position: absolute;
  inset-block-start: -220px;
  inset-inline-end: -220px;
  width: 460px;
  height: 460px;
  border-radius: 50%;
  border: 1px dashed rgba(245, 241, 230, 0.16);
  animation: hero-orbit 60s linear infinite;
  pointer-events: none;
}

@keyframes hero-orbit {
  to {
    transform: rotate(360deg);
  }
}

.auth__ticker {
  margin-block-start: var(--space-2);
  padding-block-start: var(--space-3);
  border-block-start: 1px solid rgba(245, 241, 230, 0.1);
  max-width: 100%;
  min-width: 0;
}

.auth__hero { position: relative; }

/* Keep the marquee's animated track from leaking past the hero's clip box. */
.auth__ticker .ticker__track {
  contain: layout paint style;
}

.auth__hero-head {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.auth__mark {
  display: grid;
  place-items: center;
  width: 44px;
  height: 44px;
  border-radius: 13px;
  background: rgba(245, 241, 230, 0.08);
  border: 1px solid rgba(245, 241, 230, 0.16);
  color: var(--on-dark);
}

.auth__headline {
  margin-block-start: var(--space-2);
  font-size: clamp(2.1rem, 4.2vw, 3.4rem);
  line-height: 1.16;
  letter-spacing: -0.015em;
}

.auth__line {
  display: block;
  animation: line-in 620ms var(--ease-out-soft) both;
}

.auth__line:nth-child(2) {
  animation-delay: 110ms;
}

@keyframes line-in {
  from {
    opacity: 0;
    transform: translate3d(0, 22px, 0);
  }

  to {
    opacity: 1;
    transform: none;
  }
}

.auth__accent {
  color: #e9a583;
}

.auth__copy {
  max-width: 42ch;
  font-size: 0.92rem;
  line-height: 1.95;
  color: var(--on-dark-soft);
}

.auth__points {
  list-style: none;
  margin: 0;
  padding: var(--space-3) 0 0;
  display: grid;
  gap: var(--space-2);
  border-block-start: 1px solid rgba(245, 241, 230, 0.14);
  font-size: 0.85rem;
  color: var(--on-dark-soft);
}

.auth__points li {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
}

.auth__points span {
  font-size: 0.68rem;
  letter-spacing: 0.12em;
  color: #e9a583;
  font-variant-numeric: tabular-nums;
}

.auth__slots {
  margin-block-start: auto;
  padding-block-start: var(--space-5);
  opacity: 0.9;
}

/* ---------- form ---------- */

.auth__panel {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
  padding: clamp(var(--space-5), 3.4vw, var(--space-6));
  border: 1px solid var(--line);
  border-radius: var(--radius-card-lg);
  background: var(--surface);
  box-shadow: var(--shadow-lift);
  animation: rise-in 420ms var(--ease-out) 80ms both;
}

.auth__panel-head {
  display: grid;
  gap: var(--space-2);
}

.auth__panel-copy {
  font-size: 0.85rem;
}

.auth__tabs {
  position: relative;
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 3px;
  padding: 3px;
  background: var(--paper);
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
}

.auth__tab {
  border: 0;
  background: transparent;
  font: inherit;
  font-size: 0.88rem;
  font-weight: 500;
  color: var(--ink-soft);
  padding: 7px;
  border-radius: var(--radius-pill);
  cursor: pointer;
  transition:
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}

.auth__tab[aria-selected='true'] {
  background: var(--brand-deep);
  color: var(--on-dark);
  box-shadow: 0 6px 14px -12px rgba(6, 58, 52, 0.9);
}

.auth__form {
  gap: var(--space-3);
}

.auth__form :deep(.field__control) {
  transition:
    border-color var(--motion-fast) var(--ease),
    box-shadow var(--motion-fast) var(--ease),
    background-color var(--motion-fast) var(--ease);
}

.auth__submit {
  margin-block-start: var(--space-2);
  min-height: 46px;
}

.auth__foot {
  margin-block-start: auto;
  text-align: center;
  font-size: 0.76rem;
}

@media (max-width: 880px) {
  .auth__stage {
    grid-template-columns: 1fr;
    max-width: 520px;
  }

  .auth__hero {
    padding: var(--space-5);
  }

  .auth__headline {
    font-size: clamp(1.9rem, 7vw, 2.6rem);
  }

  .auth__points,
  .auth__slots {
    display: none;
  }

  .auth__hero::before {
    display: none;
  }
}

@media (prefers-reduced-motion: reduce) {
  .auth__hero::before,
  .auth__line {
    animation: none;
  }

  .auth__line {
    opacity: 1;
    transform: none;
  }
}
</style>
