import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import {
  getReviewData,
  updateEntity,
  deleteEntity,
  deleteAllEntities as deleteAllEntitiesApi,
  addEntity,
  completeReview,
  reopenReview,
  updateSegment,
} from '../api/review'
import type {
  EntityDto,
  SegmentDto,
  ReviewSummary,
  EntityOffsetUpdate,
} from '../api/types'

export type ViewMode = 'pseudonymized' | 'depseudonymized'

export const useReviewStore = defineStore('review', () => {
  // State
  const jobId = ref('')
  const status = ref('')
  const segments = ref<SegmentDto[]>([])
  const entities = ref<EntityDto[]>([])
  const summary = ref<ReviewSummary>({
    totalEntities: 0,
    highConfidence: 0,
    mediumConfidence: 0,
    lowConfidence: 0,
    confirmed: 0,
    manuallyAdded: 0,
    pending: 0,
  })
  const loading = ref(false)
  const error = ref<string | null>(null)
  const saving = ref(false)
  const activeEntityId = ref<string | null>(null)
  const viewMode = ref<ViewMode>('pseudonymized')
  const reviewCompleted = ref(false)
  const fileName = ref('')
  const completeReviewLoading = ref(false)
  const depseudoInputText = ref('')

  // Computed
  const activeEntity = computed(() =>
    activeEntityId.value ? entities.value.find((e) => e.id === activeEntityId.value) ?? null : null,
  )

  const activeEntityToken = computed(() => {
    if (!activeEntity.value?.replacementPreview) return null
    return activeEntity.value.replacementPreview
  })

  // Highlight all occurrences of the same PII when one is active
  const highlightedEntityIds = computed(() => {
    if (!activeEntityId.value) return new Set<string>()
    const active = entities.value.find((e) => e.id === activeEntityId.value)
    if (!active) return new Set<string>()
    return new Set(
      entities.value
        .filter(
          (e) =>
            e.text.toLowerCase() === active.text.toLowerCase() &&
            e.entityType === active.entityType,
        )
        .map((e) => e.id),
    )
  })

  const entitiesBySegment = computed(() => {
    const map = new Map<string, EntityDto[]>()
    for (const entity of entities.value) {
      const list = map.get(entity.segmentId) || []
      list.push(entity)
      map.set(entity.segmentId, list)
    }
    return map
  })

  // Token mapping: replacement token → { originals, entityType, count }
  const pseudoTokenMappings = computed(() => {
    const map = new Map<string, { originals: string[]; entityType: string; count: number }>()
    for (const entity of entities.value) {
      if (!entity.replacementPreview) continue
      const existing = map.get(entity.replacementPreview)
      if (existing) {
        existing.count++
        if (!existing.originals.includes(entity.text)) {
          existing.originals.push(entity.text)
        }
      } else {
        map.set(entity.replacementPreview, {
          originals: [entity.text],
          entityType: entity.entityType,
          count: 1,
        })
      }
    }
    return map
  })

  // Build full pseudonymized text (segments with entity spans replaced by replacementPreview)
  const pseudonymizedFullText = computed(() => {
    return segments.value.map(seg => {
      const segEntities = (entitiesBySegment.value.get(seg.id) || [])
        .filter(e => e.replacementPreview)
        .sort((a, b) => a.startOffset - b.startOffset)
      let result = ''
      let cursor = 0
      for (const entity of segEntities) {
        if (entity.startOffset < cursor) continue
        result += seg.textContent.slice(cursor, entity.startOffset)
        result += entity.replacementPreview
        cursor = entity.endOffset
      }
      result += seg.textContent.slice(cursor)
      return result
    }).join('\n\n')
  })

  // De-pseudonymization: reverse map from replacement token → original value + entity type
  const depseudoReplacementMap = computed(() => {
    const map = new Map<string, { original: string; entityType: string }>()
    for (const entity of entities.value) {
      if (entity.replacementPreview && !map.has(entity.replacementPreview)) {
        map.set(entity.replacementPreview, {
          original: entity.text,
          entityType: entity.entityType,
        })
      }
    }
    return map
  })

  // Parse depseudoInputText, replacing tokens with original values
  const depseudoOutputFragments = computed(() => {
    const input = depseudoInputText.value
    if (!input) return []

    const map = depseudoReplacementMap.value
    if (map.size === 0) return [{ type: 'text' as const, content: input }]

    // Build regex from map keys, escaped and sorted by length desc
    const keys = Array.from(map.keys())
      .sort((a, b) => b.length - a.length)
      .map(k => k.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'))
    const pattern = new RegExp(`(${keys.join('|')})`, 'g')

    const fragments: { type: 'text' | 'replaced'; content: string; token?: string; entityType?: string }[] = []
    let lastIndex = 0
    let match: RegExpExecArray | null

    while ((match = pattern.exec(input)) !== null) {
      if (match.index > lastIndex) {
        fragments.push({ type: 'text', content: input.slice(lastIndex, match.index) })
      }
      const token = match[1]!
      const entry = map.get(token)!
      fragments.push({
        type: 'replaced',
        content: entry.original,
        token,
        entityType: entry.entityType,
      })
      lastIndex = pattern.lastIndex
    }

    if (lastIndex < input.length) {
      fragments.push({ type: 'text', content: input.slice(lastIndex) })
    }

    return fragments
  })

  // Plain text output (for copy button)
  const depseudoOutputPlainText = computed(() =>
    depseudoOutputFragments.value.map(f => f.content).join('')
  )

  // Stats
  const depseudoStats = computed(() => ({
    replacedCount: depseudoOutputFragments.value.filter(f => f.type === 'replaced').length,
  }))

  // Internal helper
  function recomputeSummary() {
    const ents = entities.value
    summary.value = {
      totalEntities: ents.length,
      highConfidence: ents.filter((e) => e.confidenceTier === 'HIGH').length,
      mediumConfidence: ents.filter((e) => e.confidenceTier === 'MEDIUM').length,
      lowConfidence: ents.filter((e) => e.confidenceTier === 'LOW').length,
      confirmed: ents.filter((e) => e.reviewStatus === 'confirmed').length,
      manuallyAdded: ents.filter((e) => e.reviewStatus === 'addedmanual').length,
      pending: ents.filter((e) => e.reviewStatus === 'pending').length,
    }
  }

  // Actions
  async function fetchReviewData(id: string) {
    loading.value = true
    error.value = null
    try {
      const data = await getReviewData(id)
      jobId.value = data.jobId
      status.value = data.status
      reviewCompleted.value = data.status === 'pseudonymized'
      segments.value = data.segments
      entities.value = data.entities
      summary.value = data.summary
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to load review data'
    } finally {
      loading.value = false
    }
  }

  async function deleteByToken(token: string) {
    const toDelete = entities.value.filter(e => e.replacementPreview === token)
    if (toDelete.length === 0) return
    const idsToDelete = new Set(toDelete.map(e => e.id))
    entities.value = entities.value.filter(e => !idsToDelete.has(e.id))
    if (activeEntityId.value && idsToDelete.has(activeEntityId.value)) {
      activeEntityId.value = null
    }
    recomputeSummary()
    saving.value = true
    try {
      await Promise.all(
        Array.from(idsToDelete).map(eid => deleteEntity(jobId.value, eid)),
      )
      if (status.value === 'pseudonymized') {
        await fetchReviewData(jobId.value)
      }
    } catch {
      await fetchReviewData(jobId.value)
    } finally {
      saving.value = false
    }
  }

  async function changeEntityType(id: string, entityType: string) {
    const entity = entities.value.find((e) => e.id === id)
    if (!entity) return
    const previousType = entity.entityType
    entity.entityType = entityType
    saving.value = true
    try {
      await updateEntity(jobId.value, id, { entityType })
      if (status.value === 'pseudonymized') {
        await fetchReviewData(jobId.value)
      }
    } catch {
      entity.entityType = previousType
    } finally {
      saving.value = false
    }
  }

  async function changeEntitySpan(id: string, startOffset: number, endOffset: number) {
    const entity = entities.value.find((e) => e.id === id)
    if (!entity) return
    const prevStart = entity.startOffset
    const prevEnd = entity.endOffset
    const segment = segments.value.find((s) => s.id === entity.segmentId)
    if (segment) {
      entity.text = segment.textContent.slice(startOffset, endOffset)
    }
    entity.startOffset = startOffset
    entity.endOffset = endOffset
    saving.value = true
    try {
      await updateEntity(jobId.value, id, { startOffset, endOffset })
      if (status.value === 'pseudonymized') {
        await fetchReviewData(jobId.value)
      }
    } catch {
      entity.startOffset = prevStart
      entity.endOffset = prevEnd
      if (segment) {
        entity.text = segment.textContent.slice(prevStart, prevEnd)
      }
    } finally {
      saving.value = false
    }
  }

  async function addSearchMatchesAsEntities(
    matches: Array<{ segmentId: string; text: string; startOffset: number; endOffset: number }>,
    entityType: string,
    replacementText?: string,
  ) {
    saving.value = true
    try {
      for (const match of matches) {
        // Re-check overlap before each call (entities grow during batch)
        const overlaps = entities.value.some(
          (e) =>
            e.segmentId === match.segmentId &&
            e.startOffset < match.endOffset &&
            e.endOffset > match.startOffset,
        )
        if (overlaps) continue

        try {
          const request: import('../api/types').AddEntityRequest = {
            segmentId: match.segmentId,
            text: match.text,
            entityType,
            startOffset: match.startOffset,
            endOffset: match.endOffset,
          }
          if (replacementText) {
            request.replacementText = replacementText
          }
          const newEntity = await addEntity(jobId.value, request)
          entities.value.push(newEntity)
        } catch {
          // Skip failures for individual matches
        }
      }
      recomputeSummary()
      if (status.value === 'pseudonymized') {
        await fetchReviewData(jobId.value)
      }
    } finally {
      saving.value = false
    }
  }

  async function deleteAllEntities() {
    const previousEntities = [...entities.value]
    const previousSummary = { ...summary.value }

    // Optimistic clear
    entities.value = []
    activeEntityId.value = null
    recomputeSummary()

    saving.value = true
    try {
      await deleteAllEntitiesApi(jobId.value)
      if (status.value === 'pseudonymized') {
        await fetchReviewData(jobId.value)
      }
    } catch {
      // Rollback on error
      entities.value = previousEntities
      summary.value = previousSummary
    } finally {
      saving.value = false
    }
  }

  async function submitCompleteReview(id: string) {
    completeReviewLoading.value = true
    error.value = null
    try {
      await completeReview(id)
      status.value = 'pseudonymized'
      await fetchReviewData(id)
      return true
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to complete review'
      return false
    } finally {
      completeReviewLoading.value = false
    }
  }

  async function submitReopenReview(id: string) {
    error.value = null
    try {
      await reopenReview(id)
      status.value = 'inreview'
      return true
    } catch (e) {
      error.value = e instanceof Error ? e.message : 'Failed to reopen review'
      return false
    }
  }

  function setActiveEntity(id: string | null) {
    activeEntityId.value = id
  }

  function setViewMode(mode: ViewMode) {
    viewMode.value = mode
  }

  function navigateToNextEntity() {
    const active = entities.value
    if (active.length === 0) return null

    const currentIdx = activeEntityId.value
      ? active.findIndex((e) => e.id === activeEntityId.value)
      : -1
    const nextIdx = (currentIdx + 1) % active.length
    const nextEntity = active[nextIdx]!
    activeEntityId.value = nextEntity.id
    return nextEntity.id
  }

  function navigateToPrevEntity() {
    const active = entities.value
    if (active.length === 0) return null

    const currentIdx = activeEntityId.value
      ? active.findIndex((e) => e.id === activeEntityId.value)
      : 0
    const prevIdx = currentIdx <= 0 ? active.length - 1 : currentIdx - 1
    const prevEntity = active[prevIdx]!
    activeEntityId.value = prevEntity.id
    return prevEntity.id
  }

  async function updateSegmentText(segId: string, newText: string, entityOffsets: EntityOffsetUpdate[]) {
    saving.value = true
    try {
      await updateSegment(jobId.value, segId, { textContent: newText, entityOffsets })
      // Update local state
      const seg = segments.value.find(s => s.id === segId)
      if (seg) {
        seg.textContent = newText
      }
      // Update entity offsets locally
      for (const offset of entityOffsets) {
        const entity = entities.value.find(e => e.id === offset.entityId)
        if (entity) {
          entity.startOffset = offset.startOffset
          entity.endOffset = offset.endOffset
          entity.text = offset.text
        }
      }
      recomputeSummary()
    } catch (e) {
      // Refetch on failure
      await fetchReviewData(jobId.value)
    } finally {
      saving.value = false
    }
  }

  async function updateEntityReplacementText(entityId: string, replacementText: string) {
    const entity = entities.value.find((e) => e.id === entityId)
    if (!entity) return
    const prev = entity.replacementPreview
    entity.replacementPreview = replacementText
    saving.value = true
    try {
      await updateEntity(jobId.value, entityId, { replacementText })
    } catch {
      entity.replacementPreview = prev
    } finally {
      saving.value = false
    }
  }

  async function updateReplacementByToken(oldToken: string, newToken: string) {
    if (oldToken === newToken) return
    const groupEntities = entities.value.filter(e => e.replacementPreview === oldToken)
    if (groupEntities.length === 0) return

    // Optimistic update
    for (const e of groupEntities) {
      e.replacementPreview = newToken
    }

    saving.value = true
    try {
      await Promise.all(
        groupEntities.map(e => updateEntity(jobId.value, e.id, { replacementText: newToken }))
      )
    } catch {
      // Rollback
      for (const e of groupEntities) {
        e.replacementPreview = oldToken
      }
    } finally {
      saving.value = false
    }
  }

  function resetReviewState() {
    jobId.value = ''
    status.value = ''
    segments.value = []
    entities.value = []
    summary.value = {
      totalEntities: 0,
      highConfidence: 0,
      mediumConfidence: 0,
      lowConfidence: 0,
      confirmed: 0,
      manuallyAdded: 0,
      pending: 0,
    }
    loading.value = false
    error.value = null
    saving.value = false
    activeEntityId.value = null
    viewMode.value = 'pseudonymized'
    reviewCompleted.value = false
    fileName.value = ''
    completeReviewLoading.value = false
    depseudoInputText.value = ''
  }

  return {
    // State
    jobId,
    status,
    segments,
    entities,
    summary,
    loading,
    error,
    saving,
    activeEntityId,
    viewMode,
    reviewCompleted,
    fileName,
    completeReviewLoading,
    // Computed
    activeEntity,
    activeEntityToken,
    highlightedEntityIds,
    entitiesBySegment,
    pseudoTokenMappings,
    pseudonymizedFullText,
    depseudoInputText,
    depseudoReplacementMap,
    depseudoOutputFragments,
    depseudoOutputPlainText,
    depseudoStats,
    // Actions
    fetchReviewData,
    deleteByToken,
    deleteAllEntities,
    changeEntityType,
    changeEntitySpan,
    addSearchMatchesAsEntities,
    submitCompleteReview,
    submitReopenReview,
    setActiveEntity,
    setViewMode,
    navigateToNextEntity,
    navigateToPrevEntity,
    updateSegmentText,
    updateEntityReplacementText,
    updateReplacementByToken,
    resetReviewState,
  }
})
