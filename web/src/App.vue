<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'

/**
 * Route transitions: prefer the View Transitions API (runs off the main thread), fall
 * back to the <Transition> crossfade. Skip the view-transition when the browser reports
 * reduced motion — the fallback CSS already flattens itself there.
 */
const router = useRouter()

onMounted(() => {
  const supportsViewTransitions =
    typeof document !== 'undefined' &&
    'startViewTransition' in document &&
    !window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (!supportsViewTransitions) {
    return
  }

  router.beforeResolve(async (_to, from) => {
    if (!from.matched.length) {
      return
    }

    await document.startViewTransition(async () => {
      // The DOM swap happens when the promise resolves; Vue flushes the new route here.
      await new Promise((resolve) => setTimeout(resolve, 0))
    }).finished
  })
})
</script>

<template>
  <RouterView v-slot="{ Component }">
    <Transition name="page" mode="out-in">
      <component :is="Component" />
    </Transition>
  </RouterView>
</template>
