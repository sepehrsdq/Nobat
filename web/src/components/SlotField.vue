<script setup lang="ts">
import { computed } from 'vue'

/**
 * The product's signature: a quiet grid of slots where a few light up, like free
 * times surfacing on a day. Decorative only — it never carries information, so it is
 * hidden from assistive tech and its animation is switched off under reduced motion.
 */
const props = withDefaults(
  defineProps<{
    rows?: number
    cols?: number
    tone?: 'dark' | 'light'
    /** A slow light beam crossing the grid; use on hero surfaces only. */
    sweep?: boolean
  }>(),
  { rows: 5, cols: 12, tone: 'dark', sweep: false },
)

interface Cell {
  lit: boolean
  accent: boolean
  delay: number
}

const cells = computed<Cell[]>(() => {
  const all: Cell[] = []

  for (let row = 0; row < props.rows; row += 1) {
    for (let col = 0; col < props.cols; col += 1) {
      const index = row * props.cols + col
      // A diagonal band of taken slots plus a few isolated bookings.
      const inBand = col - row * 1.6 >= 2 && col - row * 1.6 <= 4
      const scattered = (row * 7 + col * 5) % 13 === 0
      const lit = inBand || scattered

      all.push({
        lit,
        accent: scattered,
        delay: (row + col) * 26 + index * 3,
      })
    }
  }

  return all
})
</script>

<template>
  <div
    class="slot-field"
    :class="[`slot-field--${tone}`, { 'slot-field--sweep': sweep }]"
    :style="{ '--cols': cols }"
    aria-hidden="true"
  >
    <span
      v-for="(cell, index) in cells"
      :key="index"
      class="slot-field__cell"
      :class="{ 'slot-field__cell--lit': cell.lit, 'slot-field__cell--accent': cell.accent }"
      :style="{ '--d': `${cell.delay}ms` }"
    />
    <span v-if="sweep" class="slot-field__beam" />
  </div>
</template>

<style scoped>
.slot-field {
  position: relative;
  overflow: hidden;
  display: grid;
  grid-template-columns: repeat(var(--cols), minmax(0, 1fr));
  gap: clamp(3px, 0.6vw, 6px);
  width: 100%;
}

.slot-field__cell {
  aspect-ratio: 1;
  border-radius: 3px;
  background: rgba(245, 241, 230, 0.09);
  box-shadow: inset 0 0 0 1px rgba(245, 241, 230, 0.06);
  animation: slot-in 620ms var(--ease-out) both;
  animation-delay: var(--d);
}

.slot-field--light .slot-field__cell {
  background: rgba(11, 31, 28, 0.06);
  box-shadow: inset 0 0 0 1px rgba(11, 31, 28, 0.05);
}

.slot-field__cell--lit {
  background: var(--on-dark);
  box-shadow: none;
}

.slot-field--light .slot-field__cell--lit {
  background: var(--brand);
}

.slot-field__cell--accent {
  background: #d98a5f;
}

.slot-field--light .slot-field__cell--accent {
  background: var(--accent);
}

.slot-field__beam {
  position: absolute;
  inset-block: -30%;
  inline-size: 26%;
  background: linear-gradient(90deg, transparent, rgba(245, 241, 230, 0.22), transparent);
  transform: translate3d(-220%, 0, 0) rotate(10deg);
  animation: slot-beam 11s var(--ease-in-out) infinite;
  pointer-events: none;
}

.slot-field--light .slot-field__beam {
  background: linear-gradient(90deg, transparent, rgba(10, 91, 81, 0.14), transparent);
}

@keyframes slot-in {
  from {
    opacity: 0;
    transform: scale(0.9);
  }

  to {
    opacity: 1;
    transform: scale(1);
  }
}

@keyframes slot-beam {
  0% {
    transform: translate3d(-220%, 0, 0) rotate(10deg);
    opacity: 0;
  }

  18% {
    opacity: 1;
  }

  82% {
    opacity: 1;
  }

  100% {
    transform: translate3d(420%, 0, 0) rotate(10deg);
    opacity: 0;
  }
}

@media (prefers-reduced-motion: reduce) {
  .slot-field__cell {
    animation: none;
  }

  .slot-field__beam {
    display: none;
  }
}
</style>
