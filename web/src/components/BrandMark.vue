<script setup lang="ts">
withDefaults(defineProps<{ size?: number; live?: boolean }>(), { size: 30, live: false })
</script>

<template>
  <!-- One hand, one slot: a clock that reads like an appointment card. -->
  <svg
    class="brand-mark"
    :class="{ 'brand-mark--live': live }"
    :width="size"
    :height="size"
    viewBox="0 0 32 32"
    fill="none"
    aria-hidden="true"
    focusable="false"
  >
    <circle cx="16" cy="16" r="13" stroke="currentColor" stroke-width="1.6" opacity="0.45" />
    <path d="M16 16V7" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
    <path d="M16 16l5.2 4.4" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" opacity="0.55" />
    <circle class="brand-mark__tip" cx="16" cy="6.4" r="2.5" fill="currentColor" />
    <circle cx="16" cy="16" r="1.6" fill="currentColor" />
  </svg>
</template>

<style scoped>
.brand-mark {
  display: block;
  flex: none;
}

.brand-mark--live .brand-mark__tip {
  transform-box: view-box;
  transform-origin: 16px 6.4px;
  animation: mark-pulse 3.4s var(--ease-in-out) infinite;
}

@keyframes mark-pulse {
  0%,
  100% {
    transform: scale(1);
    opacity: 1;
  }

  50% {
    transform: scale(0.72);
    opacity: 0.6;
  }
}

@media (prefers-reduced-motion: reduce) {
  .brand-mark--live .brand-mark__tip {
    animation: none;
  }
}
</style>
