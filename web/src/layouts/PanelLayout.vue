<script setup lang="ts">
import { useRouter } from 'vue-router'
import BrandMark from '../components/BrandMark.vue'
import { session } from '../lib/auth'

const router = useRouter()

async function logout() {
  await session.logout()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <div class="shell">
    <header class="shell__bar scroll-shadow">
      <div class="container shell__inner">
        <RouterLink class="shell__brand" to="/panel/appointments">
          <span class="shell__mark"><BrandMark :size="27" live /></span>
          <span class="shell__wordmark">
            <strong>نوبت</strong>
            <small dir="ltr">{{ session.user?.tenantSlug }}</small>
          </span>
        </RouterLink>

        <nav class="shell__nav" aria-label="بخش‌های پنل">
          <RouterLink to="/panel/appointments">نوبت‌ها</RouterLink>
          <RouterLink to="/panel/staff">پرسنل</RouterLink>
          <RouterLink to="/panel/services">خدمات</RouterLink>
        </nav>

        <div class="shell__user">
          <a
            v-if="session.user"
            class="shell__link"
            :href="`/b/${session.user.tenantSlug}`"
            target="_blank"
            rel="noopener"
          >
            صفحهٔ رزرو
            <span aria-hidden="true">↗</span>
          </a>
          <span class="muted shell__name">{{ session.user?.fullName }}</span>
          <button class="btn btn--quiet" type="button" @click="logout">خروج</button>
        </div>
      </div>
    </header>

    <main class="container shell__main">
      <RouterView />
    </main>

    <footer class="shell__foot container">
      <span>نوبت — دفتر نوبت مطب</span>
      <span class="shell__foot-mark" aria-hidden="true">۱.۰</span>
    </footer>
  </div>
</template>

<style scoped>
.shell {
  min-height: 100vh;
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}

.shell__bar {
  position: sticky;
  inset-block-start: 0;
  z-index: 20;
  background: rgba(243, 239, 230, 0.86);
  backdrop-filter: blur(10px) saturate(1.1);
  border-block-end: 1px solid var(--line);
}

.shell__bar::before {
  content: '';
  display: block;
  height: 2px;
  background: linear-gradient(90deg, var(--brand), var(--accent));
  opacity: 0.9;
}

.shell__inner {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  min-height: 62px;
  flex-wrap: wrap;
}

.shell__brand {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  color: var(--ink);
  text-decoration: none;
}

.shell__mark {
  display: grid;
  place-items: center;
  width: 34px;
  height: 34px;
  border-radius: 10px;
  background: var(--brand-deep);
  color: var(--on-dark);
  box-shadow: 0 8px 18px -12px rgba(6, 58, 52, 0.9);
}

.shell__wordmark {
  display: grid;
  line-height: 1.15;
}

.shell__wordmark strong {
  font-size: 1.02rem;
  letter-spacing: -0.01em;
}

.shell__wordmark small {
  font-size: 0.66rem;
  color: var(--ink-faint);
  letter-spacing: 0.08em;
}

.shell__nav {
  display: flex;
  gap: 2px;
  margin-inline-end: auto;
  padding: 3px;
  border: 1px solid var(--line);
  border-radius: var(--radius-pill);
  background: rgba(255, 253, 248, 0.55);
}

.shell__nav a {
  position: relative;
  padding: 5px 14px;
  border-radius: var(--radius-pill);
  color: var(--ink-soft);
  text-decoration: none;
  font-size: 0.9rem;
  font-weight: 500;
  transition:
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}

/* The active pill arrives with a quick settle — routing happens all day, stay crisp. */
.shell__nav a.router-link-active {
  background: var(--brand);
  color: #fff;
  box-shadow: 0 6px 14px -12px rgba(6, 58, 52, 0.9);
  animation: pill-in 180ms var(--ease-out);
}

@keyframes pill-in {
  from {
    transform: scale(0.96);
    opacity: 0.5;
  }

  to {
    transform: scale(1);
    opacity: 1;
  }
}

@media (hover: hover) and (pointer: fine) {
  .shell__nav a:hover:not(.router-link-active) {
    color: var(--ink);
    background: rgba(11, 31, 28, 0.05);
  }
}

.shell__user {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.shell__link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 0.85rem;
  color: var(--brand);
  text-decoration: none;
  padding: 5px 10px;
  border-radius: var(--radius-pill);
  border: 1px solid transparent;
  transition:
    background-color var(--motion-fast) var(--ease),
    border-color var(--motion-fast) var(--ease);
}

.shell__link:hover {
  background: var(--surface);
  border-color: var(--line);
}

.shell__name {
  font-size: 0.84rem;
  max-width: 16ch;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.shell__main {
  flex: 1;
}

.shell__foot {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding-block: var(--space-5);
  font-size: 0.74rem;
  color: var(--ink-faint);
}

.shell__foot-mark {
  letter-spacing: 0.12em;
}

@media (max-width: 760px) {
  .shell__bar {
    position: static;
  }

  .shell__name {
    display: none;
  }

  .shell__nav {
    order: 3;
    width: 100%;
    margin-inline-end: 0;
    justify-content: space-between;
  }

  .shell__nav a {
    flex: 1;
    text-align: center;
  }

  .shell__user {
    margin-inline-start: auto;
  }
}
</style>
