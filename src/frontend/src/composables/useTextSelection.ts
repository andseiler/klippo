import { ref, computed } from 'vue'

export function useTextSelection() {
  const selectedText = ref('')
  const selectedSegmentId = ref<string | null>(null)
  const selectedStartOffset = ref(0)
  const selectedEndOffset = ref(0)
  const selectionPosition = ref<{ x: number; y: number } | null>(null)

  const hasSelection = computed(
    () => selectedText.value.length > 0 && selectedSegmentId.value !== null,
  )

  function handleMouseUp(_event?: MouseEvent, mode: 'original' | 'pseudonymized' = 'original') {
    const selection = window.getSelection()
    if (!selection || selection.isCollapsed || !selection.rangeCount) {
      clearSelection()
      return
    }

    const text = selection.toString().trim()
    if (!text) {
      clearSelection()
      return
    }

    // Find the segment container
    const anchorNode = selection.anchorNode
    if (!anchorNode) {
      clearSelection()
      return
    }

    // In pseudonymized mode, reject selection inside entity highlights
    if (mode === 'pseudonymized') {
      const parentEl = anchorNode instanceof HTMLElement ? anchorNode : anchorNode.parentElement
      if (parentEl?.closest('[data-entity-id]')) {
        clearSelection()
        return
      }
      const focusNode = selection.focusNode
      const focusEl = focusNode instanceof HTMLElement ? focusNode : focusNode?.parentElement
      if (focusEl?.closest('[data-entity-id]')) {
        clearSelection()
        return
      }
    }

    const segmentEl = (anchorNode instanceof HTMLElement ? anchorNode : anchorNode.parentElement)
      ?.closest('[data-segment-id]')
    if (!segmentEl) {
      clearSelection()
      return
    }

    const segmentId = (segmentEl as HTMLElement).dataset.segmentId!
    const segmentText = (segmentEl as HTMLElement).dataset.segmentText!

    const range = selection.getRangeAt(0)

    if (mode === 'pseudonymized') {
      // In pseudonymized mode, compute offsets using data-offset-start/data-offset-end
      const startOriginalOffset = getOriginalOffset(segmentEl as HTMLElement, range.startContainer, range.startOffset)
      if (startOriginalOffset === -1) {
        clearSelection()
        return
      }

      // Look up the original text from the segment
      const originalText = segmentText.slice(startOriginalOffset, startOriginalOffset + text.length)

      selectedText.value = originalText || text
      selectedSegmentId.value = segmentId
      selectedStartOffset.value = startOriginalOffset
      selectedEndOffset.value = startOriginalOffset + (originalText || text).length

      const rect = range.getBoundingClientRect()
      selectionPosition.value = {
        x: rect.left + rect.width / 2,
        y: rect.bottom + 8,
      }
    } else {
      // Original mode: compute offsets via DOM TreeWalker
      const startOffset = getTextOffset(segmentEl as HTMLElement, range.startContainer, range.startOffset)
      if (startOffset === -1) {
        clearSelection()
        return
      }

      selectedText.value = text
      selectedSegmentId.value = segmentId
      selectedStartOffset.value = startOffset
      selectedEndOffset.value = startOffset + text.length

      const rect = range.getBoundingClientRect()
      selectionPosition.value = {
        x: rect.left + rect.width / 2,
        y: rect.bottom + 8,
      }
    }
  }

  function getTextOffset(root: Element, node: Node, offset: number): number {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT)
    let charCount = 0
    while (walker.nextNode()) {
      if (walker.currentNode === node) {
        return charCount + offset
      }
      charCount += walker.currentNode.textContent?.length ?? 0
    }
    return -1
  }

  function getOriginalOffset(root: Element, node: Node, offset: number): number {
    // Walk text nodes, find the one containing our selection start,
    // then use data-offset-start from the parent span to compute original offset
    const textNode = node.nodeType === Node.TEXT_NODE ? node : node.childNodes[offset] || node
    const parentSpan = (textNode instanceof HTMLElement ? textNode : textNode.parentElement)
      ?.closest('[data-offset-start]')
    if (!parentSpan) return -1

    const spanOriginalStart = parseInt((parentSpan as HTMLElement).dataset.offsetStart!, 10)

    // Calculate how far into this span the selection starts
    const walker = document.createTreeWalker(parentSpan, NodeFilter.SHOW_TEXT)
    let charCount = 0
    while (walker.nextNode()) {
      if (walker.currentNode === node) {
        return spanOriginalStart + charCount + (node.nodeType === Node.TEXT_NODE ? offset : 0)
      }
      charCount += walker.currentNode.textContent?.length ?? 0
    }

    return spanOriginalStart
  }

  function clearSelection() {
    selectedText.value = ''
    selectedSegmentId.value = null
    selectedStartOffset.value = 0
    selectedEndOffset.value = 0
    selectionPosition.value = null
  }

  return {
    selectedText,
    selectedSegmentId,
    selectedStartOffset,
    selectedEndOffset,
    selectionPosition,
    hasSelection,
    clearSelection,
    handleMouseUp,
  }
}
