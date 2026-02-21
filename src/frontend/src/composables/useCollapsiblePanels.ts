import { ref, computed } from 'vue'
import { useMediaQuery } from '@vueuse/core'

export function useCollapsiblePanels() {
  const isLargeScreen = useMediaQuery('(min-width: 1024px)')
  const expandedPanels = ref<Set<string>>(new Set(['pseudonymized']))

  const isCollapsible = computed(() => !isLargeScreen.value)

  function isPanelExpanded(key: string): boolean {
    if (isLargeScreen.value) return true
    return expandedPanels.value.has(key)
  }

  function togglePanel(key: string) {
    const next = new Set(expandedPanels.value)
    if (next.has(key)) next.delete(key)
    else next.add(key)
    expandedPanels.value = next
  }

  return { isLargeScreen, isCollapsible, isPanelExpanded, togglePanel }
}
