<script setup lang="ts">
import { useRouter } from 'vue-router'
import { session } from '../lib/auth'

const router = useRouter()

async function logout() {
  await session.logout()
  await router.replace({ name: 'login' })
}
</script>

<template>
  <div class="shell">
    <header class="shell__bar">
      <div class="container shell__inner">
        <RouterLink class="shell__brand" to="/panel/staff">
          <img src="/favicon.svg" alt="" width="28" height="28" />
          <span>نوبت</span>
        </RouterLink>

        <nav class="shell__nav" aria-label="بخش‌های پنل">
          <RouterLink to="/panel/staff">پرسنل</RouterLink>
          <RouterLink to="/panel/services">خدمات</RouterLink>
        </nav>

        <div class="shell__user">
          <span class="chip">{{ session.user?.tenantSlug }}</span>
          <span class="muted shell__name">{{ session.user?.fullName }}</span>
          <button class="btn btn--quiet" type="button" @click="logout">خروج</button>
        </div>
      </div>
    </header>

    <main class="container">
      <RouterView />
    </main>
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
  background: var(--surface);
  border-block-end: 1px solid var(--line);
}

.shell__inner {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  min-height: 64px;
  flex-wrap: wrap;
  padding-block: var(--space-2);
}

.shell__brand {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-weight: 700;
  color: var(--ink);
  text-decoration: none;
}

.shell__nav {
  display: flex;
  gap: var(--space-1);
  margin-inline-end: auto;
}

.shell__nav a {
  padding: 6px 12px;
  border-radius: var(--radius-input);
  color: var(--ink-soft);
  text-decoration: none;
  font-weight: 500;
  transition:
    background-color var(--motion-fast) var(--ease),
    color var(--motion-fast) var(--ease);
}

.shell__nav a:hover {
  background: var(--paper);
  color: var(--ink);
}

.shell__nav a.router-link-active {
  background: var(--brand-soft);
  color: var(--brand);
}

.shell__user {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-inline-start: auto;
}

.shell__name {
  font-size: 0.86rem;
}

@media (max-width: 640px) {
  .shell__inner {
    gap: var(--space-2);
  }

  .shell__name {
    display: none;
  }

  .shell__nav {
    order: 3;
    width: 100%;
    margin-inline-end: 0;
  }
}
</style>
