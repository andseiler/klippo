import { ref, computed, type Ref } from 'vue'
import type { SegmentDto, EntityDto } from '../api/types'

export interface SearchMatch {
  segmentId: string
  startOffset: number
  endOffset: number
  text: string
  overlapsEntity: boolean
}

export function useDocumentSearch(
  segments: Ref<SegmentDto[]>,
  entities: Ref<EntityDto[]>,
) {
  const searchQuery = ref('')
  const isSearchOpen = ref(false)
  const currentMatchIndex = ref(0)

  const allMatches = computed<SearchMatch[]>(() => {
    const query = searchQuery.value.trim().toLowerCase()
    if (!query) return []

    const matches: SearchMatch[] = []
    for (const segment of segments.value) {
      const text = segment.textContent
      const lower = text.toLowerCase()
      let searchFrom = 0
      while (true) {
        const idx = lower.indexOf(query, searchFrom)
        if (idx === -1) break
        const endIdx = idx + query.length

        const overlapsEntity = entities.value.some(
          (e) =>
            e.segmentId === segment.id &&
            e.startOffset < endIdx &&
            e.endOffset > idx,
        )

        matches.push({
          segmentId: segment.id,
          startOffset: idx,
          endOffset: endIdx,
          text: text.slice(idx, endIdx),
          overlapsEntity,
        })
        searchFrom = idx + 1
      }
    }
    return matches
  })

  const actionableMatches = computed(() =>
    allMatches.value.filter((m) => !m.overlapsEntity),
  )

  const matchesBySegment = computed(() => {
    const map = new Map<string, SearchMatch[]>()
    for (const match of allMatches.value) {
      const list = map.get(match.segmentId) || []
      list.push(match)
      map.set(match.segmentId, list)
    }
    return map
  })

  const currentMatch = computed(() =>
    allMatches.value[currentMatchIndex.value] ?? null,
  )

  function nextMatch() {
    if (allMatches.value.length === 0) return
    currentMatchIndex.value = (currentMatchIndex.value + 1) % allMatches.value.length
  }

  function prevMatch() {
    if (allMatches.value.length === 0) return
    currentMatchIndex.value =
      currentMatchIndex.value <= 0
        ? allMatches.value.length - 1
        : currentMatchIndex.value - 1
  }

  function toggleSearch() {
    isSearchOpen.value = !isSearchOpen.value
    if (!isSearchOpen.value) {
      searchQuery.value = ''
      currentMatchIndex.value = 0
    }
  }

  function closeSearch() {
    isSearchOpen.value = false
    searchQuery.value = ''
    currentMatchIndex.value = 0
  }

  return {
    searchQuery,
    isSearchOpen,
    allMatches,
    actionableMatches,
    matchesBySegment,
    currentMatchIndex,
    currentMatch,
    nextMatch,
    prevMatch,
    toggleSearch,
    closeSearch,
  }
}
