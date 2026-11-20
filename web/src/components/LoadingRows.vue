<script setup lang="ts">
withDefaults(
  defineProps<{
    rows?: number
    variant?: 'timeline' | 'list' | 'grid'
  }>(),
  { rows: 3, variant: 'list' },
)
</script>

<template>
  <div class="loading">
    <p class="sr-only" role="status">در حال بارگذاری…</p>

    <div class="loading__body" aria-hidden="true">
      <div v-if="variant === 'timeline'" class="sk-timeline">
        <div v-for="row in rows" :key="row" class="sk-timeline__row">
          <div class="skeleton sk-time" />
          <div class="sk-axis"><span class="sk-dot" /></div>
          <div class="sk-slip">
            <div class="skeleton sk-line" style="width: 46%" />
            <div class="skeleton sk-line sk-line--thin" style="width: 68%" />
            <div class="skeleton sk-line sk-line--thin" style="width: 34%" />
          </div>
        </div>
      </div>

      <div v-else-if="variant === 'grid'" class="sk-grid">
        <div v-for="row in rows" :key="row" class="skeleton sk-card" />
      </div>

      <div v-else class="sk-list">
        <div v-for="row in rows" :key="row" class="sk-row">
          <span class="skeleton sk-avatar" />
          <span class="sk-lines">
            <span class="skeleton sk-line" style="width: 38%" />
            <span class="skeleton sk-line sk-line--thin" style="width: 22%" />
          </span>
          <span class="skeleton sk-pill" />
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.loading__body {
  display: grid;
  gap: var(--space-3);
}

.sk-timeline {
  display: grid;
  gap: var(--space-3);
}

.sk-timeline__row {
  display: grid;
  grid-template-columns: 66px 24px minmax(0, 1fr);
  gap: var(--space-3);
  align-items: start;
}

.sk-time {
  height: 18px;
  margin-block-start: 12px;
}

.sk-axis {
  display: flex;
  justify-content: center;
  padding-block-start: 14px;
}

.sk-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--line-strong);
}

.sk-slip {
  display: grid;
  gap: 10px;
  padding: var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-card);
  background: var(--surface);
}

.sk-line {
  height: 12px;
}

.sk-line--thin {
  height: 9px;
  opacity: 0.7;
}

.sk-list {
  display: grid;
  gap: var(--space-2);
}

.sk-row {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--line);
  border-radius: var(--radius-input);
  background: var(--surface);
}

.sk-avatar {
  width: 44px;
  height: 44px;
  border-radius: 13px;
  flex: none;
}

.sk-lines {
  display: grid;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.sk-pill {
  width: 84px;
  height: 26px;
  border-radius: var(--radius-pill);
  flex: none;
}

.sk-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: var(--space-3);
}

.sk-card {
  height: 210px;
  border-radius: var(--radius-card);
}

@media (max-width: 720px) {
  .sk-timeline__row {
    grid-template-columns: 52px 20px minmax(0, 1fr);
    gap: var(--space-2);
  }

  .sk-row {
    flex-wrap: wrap;
  }
}
</style>
