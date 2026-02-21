import { ref, watch, onBeforeUnmount, type Ref } from 'vue'

/**
 * Proportional scroll sync between two scrollable elements.
 * Scroll position is mapped by ratio (scrollTop / maxScroll) so panels
 * with different content heights stay visually aligned.
 */
export function useScrollSync() {
  const scrollElA = ref<HTMLElement | null>(null) as Ref<HTMLElement | null>
  const scrollElB = ref<HTMLElement | null>(null) as Ref<HTMLElement | null>

  let isSyncing = false
  let rafId: number | null = null

  function getScrollRatio(el: HTMLElement): number {
    const max = el.scrollHeight - el.clientHeight
    return max > 0 ? el.scrollTop / max : 0
  }

  function applyScrollRatio(el: HTMLElement, ratio: number) {
    const max = el.scrollHeight - el.clientHeight
    el.scrollTop = ratio * max
  }

  function createHandler(source: Ref<HTMLElement | null>, target: Ref<HTMLElement | null>) {
    return () => {
      if (isSyncing) return
      if (!source.value || !target.value) return

      isSyncing = true
      const ratio = getScrollRatio(source.value)

      if (rafId !== null) cancelAnimationFrame(rafId)
      rafId = requestAnimationFrame(() => {
        if (target.value) {
          applyScrollRatio(target.value, ratio)
        }
        isSyncing = false
        rafId = null
      })
    }
  }

  const handleScrollA = createHandler(scrollElA, scrollElB)
  const handleScrollB = createHandler(scrollElB, scrollElA)

  function bind(el: HTMLElement | null, handler: () => void) {
    if (el) el.addEventListener('scroll', handler, { passive: true })
  }

  function unbind(el: HTMLElement | null, handler: () => void) {
    if (el) el.removeEventListener('scroll', handler)
  }

  watch(scrollElA, (newEl, oldEl) => {
    unbind(oldEl, handleScrollA)
    bind(newEl, handleScrollA)
  }, { immediate: true })

  watch(scrollElB, (newEl, oldEl) => {
    unbind(oldEl, handleScrollB)
    bind(newEl, handleScrollB)
  }, { immediate: true })

  onBeforeUnmount(() => {
    unbind(scrollElA.value, handleScrollA)
    unbind(scrollElB.value, handleScrollB)
    if (rafId !== null) cancelAnimationFrame(rafId)
  })

  return { scrollElA, scrollElB }
}
