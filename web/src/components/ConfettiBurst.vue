<script setup lang="ts">
import { computed } from 'vue'

/** One-shot paper confetti for the booking confirmation. Purely decorative. */
const props = withDefaults(defineProps<{ count?: number }>(), { count: 20 })

const tokens = ['--brand', '--accent', '--on-dark', '--brand-soft', '--paper-2']

interface Piece {
  dx: number
  dy: number
  rot: number
  delay: number
  color: string
  width: number
  height: number
  round: boolean
}

const pieces = computed<Piece[]>(() =>
  Array.from({ length: props.count }, (_, index) => {
    const angle = (index / props.count) * Math.PI * 2 + 0.35
    const spread = 96 + ((index * 37) % 84)

    return {
      dx: Math.round(Math.cos(angle) * spread),
      dy: Math.round(Math.sin(angle) * spread * 0.78 - 34),
      rot: (index % 2 === 0 ? 1 : -1) * (120 + ((index * 53) % 240)),
      delay: (index % 5) * 28,
      color: `var(${tokens[index % tokens.length]})`,
      width: index % 3 === 0 ? 7 : 5,
      height: index % 3 === 0 ? 7 : 11,
      round: index % 4 === 0,
    }
  }),
)
</script>

<template>
  <div class="confetti" aria-hidden="true">
    <span
      v-for="(piece, index) in pieces"
      :key="index"
      class="confetti__piece"
      :class="{ 'confetti__piece--round': piece.round }"
      :style="{
        '--dx': `${piece.dx}px`,
        '--dy': `${piece.dy}px`,
        '--rot': `${piece.rot}deg`,
        '--delay': `${piece.delay}ms`,
        width: `${piece.width}px`,
        height: `${piece.height}px`,
        background: piece.color,
      }"
    />
  </div>
</template>

<style scoped>
.confetti {
  position: absolute;
  inset: 0;
  overflow: visible;
  pointer-events: none;
  z-index: 3;
}

.confetti__piece {
  position: absolute;
  inset-block-start: 44%;
  inset-inline-start: 42%;
  border-radius: 1px;
  opacity: 0;
  animation: confetti-burst 980ms var(--ease-out) var(--delay) both;
}

.confetti__piece--round {
  border-radius: 50%;
}

@keyframes confetti-burst {
  0% {
    opacity: 0;
    transform: translate3d(0, 0, 0) rotate(0deg) scale(0.4);
  }

  12% {
    opacity: 1;
  }

  70% {
    opacity: 1;
  }

  100% {
    opacity: 0;
    transform: translate3d(var(--dx), var(--dy), 0) rotate(var(--rot)) scale(1);
  }
}

@media (prefers-reduced-motion: reduce) {
  .confetti {
    display: none;
  }
}
</style>
