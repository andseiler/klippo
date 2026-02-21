<script setup lang="ts">
import { computed } from 'vue'
import type { SegmentDto, EntityDto, EntityOffsetUpdate } from '../../api/types'
import type { SearchMatch } from '../../composables/useDocumentSearch'
import { useReviewStore } from '../../stores/review'
import EntityHighlight from './EntityHighlight.vue'

interface TextFragment {
  type: 'text' | 'entity' | 'search-match'
  content: string
  entity?: EntityDto
  originalStart?: number
  originalEnd?: number
  isActiveMatch?: boolean
}

const props = withDefaults(defineProps<{
  segment: SegmentDto
  entities: EntityDto[]
  readonly?: boolean
  editable?: boolean
  displayMode?: 'original' | 'pseudonymized'
  searchMatches?: SearchMatch[]
  activeMatchIndex?: number
  globalMatchOffset?: number
}>(), {
  readonly: false,
  editable: false,
  displayMode: 'original',
  searchMatches: () => [],
  activeMatchIndex: -1,
  globalMatchOffset: 0,
})

const emit = defineEmits<{
  'segment-updated': [segmentId: string, newText: string, entityOffsets: EntityOffsetUpdate[]]
  'entity-replacement-updated': [entityId: string, newText: string]
}>()

const reviewStore = useReviewStore()

const fragments = computed<TextFragment[]>(() => {
  const text = props.segment.textContent
  const sorted = [...props.entities].sort((a, b) => a.startOffset - b.startOffset)

  // Phase 1: Build entity/text fragments
  const baseFragments: TextFragment[] = []
  let cursor = 0

  for (const entity of sorted) {
    if (entity.startOffset < cursor) continue

    if (entity.startOffset > cursor) {
      baseFragments.push({
        type: 'text',
        content: text.slice(cursor, entity.startOffset),
        originalStart: cursor,
        originalEnd: entity.startOffset,
      })
    }

    baseFragments.push({
      type: 'entity',
      content: text.slice(entity.startOffset, entity.endOffset),
      entity,
      originalStart: entity.startOffset,
      originalEnd: entity.endOffset,
    })

    cursor = entity.endOffset
  }

  if (cursor < text.length) {
    baseFragments.push({
      type: 'text',
      content: text.slice(cursor),
      originalStart: cursor,
      originalEnd: text.length,
    })
  }

  // Phase 2: Split text fragments to interleave search matches
  if (props.searchMatches.length === 0) return baseFragments

  const result: TextFragment[] = []
  for (const fragment of baseFragments) {
    if (fragment.type !== 'text') {
      result.push(fragment)
      continue
    }

    const fragStart = fragment.originalStart!
    const fragEnd = fragment.originalEnd!

    // Find matches that overlap this text fragment
    const overlapping = props.searchMatches.filter(
      (m) => m.startOffset < fragEnd && m.endOffset > fragStart,
    )

    if (overlapping.length === 0) {
      result.push(fragment)
      continue
    }

    let innerCursor = fragStart
    for (const match of overlapping) {
      const matchStart = Math.max(match.startOffset, fragStart)
      const matchEnd = Math.min(match.endOffset, fragEnd)

      if (matchStart > innerCursor) {
        result.push({
          type: 'text',
          content: text.slice(innerCursor, matchStart),
          originalStart: innerCursor,
          originalEnd: matchStart,
        })
      }

      // Determine if this is the active/current match
      const matchGlobalIdx = props.searchMatches.indexOf(match)
      const isActive = props.activeMatchIndex === props.globalMatchOffset + matchGlobalIdx

      result.push({
        type: 'search-match',
        content: text.slice(matchStart, matchEnd),
        originalStart: matchStart,
        originalEnd: matchEnd,
        isActiveMatch: isActive,
      })

      innerCursor = matchEnd
    }

    if (innerCursor < fragEnd) {
      result.push({
        type: 'text',
        content: text.slice(innerCursor, fragEnd),
        originalStart: innerCursor,
        originalEnd: fragEnd,
      })
    }
  }

  return result
})

function extractStateFromDOM(el: HTMLElement): { text: string; entityOffsets: EntityOffsetUpdate[] } {
  let fullText = ''
  const entityOffsets: EntityOffsetUpdate[] = []

  function walk(node: Node) {
    if (node.nodeType === Node.TEXT_NODE) {
      fullText += node.textContent || ''
    } else if (node.nodeType === Node.ELEMENT_NODE) {
      const element = node as HTMLElement
      const entityId = element.getAttribute('data-entity-id')
      if (entityId) {
        const start = fullText.length
        for (const child of Array.from(node.childNodes)) {
          walk(child)
        }
        const end = fullText.length
        entityOffsets.push({
          entityId,
          startOffset: start,
          endOffset: end,
          text: fullText.slice(start, end),
        })
      } else {
        for (const child of Array.from(node.childNodes)) {
          walk(child)
        }
      }
    }
  }

  for (const child of Array.from(el.childNodes)) {
    walk(child)
  }

  return { text: fullText, entityOffsets }
}

function handleBlur(event: FocusEvent) {
  if (!props.editable) return
  const el = event.currentTarget as HTMLElement

  const { text, entityOffsets } = extractStateFromDOM(el)
  if (text !== props.segment.textContent) {
    emit('segment-updated', props.segment.id, text, entityOffsets)
  }
}
</script>

<template>
  <span
    :data-segment-id="segment.id"
    :data-segment-text="segment.textContent"
    :contenteditable="editable && displayMode !== 'pseudonymized' ? 'true' : undefined"
    :class="editable ? 'outline-none focus:ring-2 focus:ring-blue-300 focus:ring-offset-1 rounded' : ''"
    @blur="handleBlur"
  >
    <template v-for="(fragment, idx) in fragments" :key="idx">
      <span
        v-if="fragment.type === 'text'"
        :data-offset-start="fragment.originalStart"
        :data-offset-end="fragment.originalEnd"
      >{{ fragment.content }}</span>
      <mark
        v-else-if="fragment.type === 'search-match'"
        :class="[
          'rounded px-0.5',
          fragment.isActiveMatch
            ? 'bg-orange-300 dark:bg-orange-600'
            : 'bg-yellow-200 dark:bg-yellow-700/50',
        ]"
        :data-offset-start="fragment.originalStart"
        :data-offset-end="fragment.originalEnd"
        data-search-match
      >{{ fragment.content }}</mark>
      <EntityHighlight
        v-else-if="fragment.entity"
        :entity="fragment.entity"
        :is-active="reviewStore.highlightedEntityIds.has(fragment.entity.id)"
        :readonly="readonly"
        :display-mode="displayMode"
        @replacement-updated="(entityId: string, newText: string) => emit('entity-replacement-updated', entityId, newText)"
      />
    </template>
  </span>
</template>
