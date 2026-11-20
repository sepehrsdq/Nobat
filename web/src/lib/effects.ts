import type { Directive } from 'vue'

/**
 * Small, dependency-free pointer/reveal effects. All of them are opt-in per element,
 * respect reduced motion (live — a mid-session OS change is picked up), and only
 * touch transform/opacity-class properties.
 *
 * Performance notes baked in:
 * - getBoundingClientRect is cached on pointerenter, not read per pointermove (layout thrash).
 * - Pointer coordinates land on non-inheriting custom properties (@property in base.css),
 *   so only the effect element recalculates styles — never its children.
 * - Transforms are written directly to el.style.transform (no CSS-variable-driven motion).
 */

const canHover = (): boolean =>
  typeof window !== 'undefined' &&
  window.matchMedia('(hover: hover) and (pointer: fine)').matches

const prefersReducedMotion = (): boolean =>
  typeof window !== 'undefined' &&
  window.matchMedia('(prefers-reduced-motion: reduce)').matches

/** Subscribe to reduced-motion changes so a mid-session OS toggle works immediately. */
function onReducedMotionChange(listener: () => void): () => void {
  if (typeof window === 'undefined' || !window.matchMedia) {
    return () => undefined
  }

  const query = window.matchMedia('(prefers-reduced-motion: reduce)')
  query.addEventListener('change', listener)

  return () => query.removeEventListener('change', listener)
}

const revealObservers = new WeakMap<HTMLElement, IntersectionObserver>()

/** v-reveal="index" — fades content in once as it enters the viewport. */
export const vReveal: Directive<HTMLElement, number | undefined> = {
  mounted(el, binding) {
    const delay = Math.max(0, binding.value ?? 0)

    if (prefersReducedMotion() || typeof IntersectionObserver === 'undefined') {
      el.classList.add('is-revealed')
      return
    }

    el.style.setProperty('--reveal-delay', `${delay}ms`)
    el.classList.add('reveal')

    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (!entry.isIntersecting) continue

          el.classList.add('is-revealed')
          observer.disconnect()
        }
      },
      { threshold: 0.12, rootMargin: '0px 0px -6% 0px' },
    )

    observer.observe(el)
    revealObservers.set(el, observer)
  },
  unmounted(el) {
    revealObservers.get(el)?.disconnect()
    revealObservers.delete(el)
  },
}

const spotCleanup = new WeakMap<HTMLElement, () => void>()

/** v-spotlight — a warm light follows the pointer inside the element. */
export const vSpotlight: Directive<HTMLElement> = {
  mounted(el) {
    el.classList.add('fx-spot')

    if (!canHover() || prefersReducedMotion()) {
      return
    }

    let frame = 0
    let lastX = 0
    let lastY = 0
    let rect: DOMRect | null = null

    const apply = () => {
      frame = 0

      if (!rect) {
        return
      }

      el.style.setProperty('--mx', `${lastX - rect.left}px`)
      el.style.setProperty('--my', `${lastY - rect.top}px`)
    }

    const onEnter = (event: PointerEvent) => {
      // One layout read per enter; the rect is reused for the whole hover.
      rect = el.getBoundingClientRect()
      lastX = event.clientX
      lastY = event.clientY
    }

    const onMove = (event: PointerEvent) => {
      lastX = event.clientX
      lastY = event.clientY

      if (frame) {
        return
      }

      frame = requestAnimationFrame(apply)
    }

    const onScroll = () => {
      rect = null
    }

    el.addEventListener('pointerenter', onEnter)
    el.addEventListener('pointermove', onMove, { passive: true })
    window.addEventListener('scroll', onScroll, { passive: true, capture: true })

    spotCleanup.set(el, () => {
      el.removeEventListener('pointerenter', onEnter)
      el.removeEventListener('pointermove', onMove)
      window.removeEventListener('scroll', onScroll, { capture: true })

      if (frame) {
        cancelAnimationFrame(frame)
      }
    })
  },
  unmounted(el) {
    spotCleanup.get(el)?.()
    spotCleanup.delete(el)
  },
}

const tiltCleanup = new WeakMap<HTMLElement, () => void>()

/**
 * v-tilt="maxDegrees" — a restrained 3D lean for focal cards.
 * The value settles with a critically-damped spring toward the pointer,
 * so quick passes feel physical instead of glued to the cursor.
 */
export const vTilt: Directive<HTMLElement, number | undefined> = {
  mounted(el, binding) {
    el.classList.add('fx-tilt')

    if (!canHover() || prefersReducedMotion()) {
      return
    }

    const max = Math.min(Math.max(binding.value ?? 4, 1), 8)
    let rect: DOMRect | null = null
    let targetX = 0
    let targetY = 0
    let currentX = 0
    let currentY = 0
    let raf = 0
    let active = false
    let unlisten: (() => void) | null = null

    const apply = () => {
      if (!active) {
        raf = 0
        return
      }

      // Critically-damped settle: fast approach, no bounce.
      currentX += (targetX - currentX) * 0.16
      currentY += (targetY - currentY) * 0.16
      el.style.transform = `perspective(820px) rotateX(${currentX.toFixed(2)}deg) rotateY(${currentY.toFixed(2)}deg) translate3d(0, -3px, 0)`

      if (Math.abs(targetX - currentX) < 0.02 && Math.abs(targetY - currentY) < 0.02) {
        raf = 0
        return
      }

      raf = requestAnimationFrame(apply)
    }

    const kick = () => {
      if (!raf) {
        raf = requestAnimationFrame(apply)
      }
    }

    const onEnter = (event: PointerEvent) => {
      rect = el.getBoundingClientRect()
      active = true
      el.classList.add('is-tilting')
      onMove(event)
    }

    const onMove = (event: PointerEvent) => {
      if (!active || !rect) {
        return
      }

      targetY = ((event.clientX - rect.left) / rect.width - 0.5) * max * 2
      targetX = -((event.clientY - rect.top) / rect.height - 0.5) * max * 2
      kick()
    }

    const onLeave = () => {
      active = false
      rect = null
      targetX = 0
      targetY = 0
      el.classList.remove('is-tilting')
      kick()
    }

    el.addEventListener('pointerenter', onEnter)
    el.addEventListener('pointermove', onMove, { passive: true })
    el.addEventListener('pointerleave', onLeave)
    unlisten = onReducedMotionChange(() => {
      if (prefersReducedMotion()) {
        onLeave()
        el.style.transform = ''
      }
    })

    tiltCleanup.set(el, () => {
      el.removeEventListener('pointerenter', onEnter)
      el.removeEventListener('pointermove', onMove)
      el.removeEventListener('pointerleave', onLeave)
      unlisten?.()
      el.style.transform = ''

      if (raf) {
        cancelAnimationFrame(raf)
        raf = 0
      }
    })
  },
  unmounted(el) {
    tiltCleanup.get(el)?.()
    tiltCleanup.delete(el)
  },
}
