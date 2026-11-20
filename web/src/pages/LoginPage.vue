<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ApiError } from '../lib/api'
import { session } from '../lib/auth'

type Mode = 'login' | 'register'

const route = useRoute()
const router = useRouter()

const mode = ref<Mode>('login')
const busy = ref(false)
const errorMessage = ref('')

const tenantSlug = ref('')
const phone = ref('')
const password = ref('')

const clinicName = ref('')
const ownerName = ref('')
const slug = ref('')

const isRegister = computed(() => mode.value === 'register')

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

    const next = typeof route.query.next === 'string' ? route.query.next : '/panel/staff'
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
  <div class="auth">
    <main class="auth__card card">
      <header class="auth__brand">
        <img src="/favicon.svg" alt="" width="44" height="44" />
        <div>
          <h1>نوبت</h1>
          <p class="muted">نوبت‌دهی مطب، بدون تلفن</p>
        </div>
      </header>

      <div class="auth__tabs" role="tablist" aria-label="ورود یا ثبت مطب">
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

      <form class="stack" novalidate @submit.prevent="submit">
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

        <button class="btn btn--primary btn--block" type="submit" :disabled="busy">
          {{ busy ? 'در حال ارسال…' : isRegister ? 'ساخت مطب' : 'ورود به پنل' }}
        </button>
      </form>

      <p class="auth__foot muted">نسخهٔ ۱.۰ — فقط برای مطب‌های دعوت‌شده</p>
    </main>
  </div>
</template>

<style scoped>
.auth {
  min-height: 100vh;
  min-height: 100dvh;
  display: grid;
  place-items: center;
  padding: var(--space-4);
}

.auth__card {
  width: min(440px, 100%);
  display: grid;
  gap: var(--space-5);
}

.auth__brand {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.auth__brand h1 {
  font-size: 1.5rem;
}

.auth__tabs {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 4px;
  padding: 4px;
  background: var(--paper);
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
}

.auth__tab {
  border: 0;
  background: transparent;
  font: inherit;
  font-weight: 500;
  color: var(--ink-soft);
  padding: 8px;
  border-radius: 6px;
  cursor: pointer;
  transition:
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}

.auth__tab[aria-selected='true'] {
  background: var(--surface);
  color: var(--ink);
  box-shadow: var(--shadow-card);
}

.auth__foot {
  text-align: center;
  font-size: 0.78rem;
}
</style>
