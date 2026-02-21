import { useReviewStore } from '../stores/review'

function scrollToEntity(entityId: string) {
  const el = document.getElementById(`entity-${entityId}`)
  if (el) {
    el.scrollIntoView({ behavior: 'smooth', block: 'center' })
  }
}

export function useReviewNavigation() {
  const reviewStore = useReviewStore()

  function handleKeydown(e: KeyboardEvent) {
    // Ignore when typing in inputs
    const target = e.target as HTMLElement
    if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT') {
      return
    }

    switch (e.key) {
      case 'Tab': {
        e.preventDefault()
        const id = e.shiftKey
          ? reviewStore.navigateToPrevEntity()
          : reviewStore.navigateToNextEntity()
        if (id) scrollToEntity(id)
        break
      }
      case 'Escape': {
        e.preventDefault()
        reviewStore.setActiveEntity(null)
        break
      }
    }
  }

  function setupKeyboardNav() {
    document.addEventListener('keydown', handleKeydown)
  }

  function teardownKeyboardNav() {
    document.removeEventListener('keydown', handleKeydown)
  }

  function goToEntity(id: string) {
    reviewStore.setActiveEntity(id)
    scrollToEntity(id)
  }

  return {
    setupKeyboardNav,
    teardownKeyboardNav,
    goToEntity,
  }
}
