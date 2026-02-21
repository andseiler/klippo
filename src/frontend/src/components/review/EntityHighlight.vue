<script setup lang="ts">
import { computed } from 'vue'
import type { EntityDto } from '../../api/types'
import { getHighlightClasses } from '../../constants/confidenceTiers'
import { useReviewStore } from '../../stores/review'

const props = withDefaults(defineProps<{
  entity: EntityDto
  isActive: boolean
  readonly?: boolean
  displayMode?: 'original' | 'pseudonymized'
}>(), {
  readonly: false,
  displayMode: 'original',
})

const emit = defineEmits<{
  'replacement-updated': [entityId: string, newText: string]
}>()

const reviewStore = useReviewStore()

const displayText = computed(() => {
  switch (props.displayMode) {
    case 'pseudonymized':
      return props.entity.replacementPreview || props.entity.text
    default:
      return props.entity.text
  }
})

const highlightClasses = computed(() => {
  const base = getHighlightClasses(props.entity.confidenceTier)
  const active = props.isActive ? 'ring-2 ring-blue-500' : ''
  return `${base} ${active}`
})

function handleBlur(event: FocusEvent) {
  if (props.displayMode !== 'pseudonymized') return
  const el = event.currentTarget as HTMLElement
  const currentText = el.textContent || ''
  const expected = props.entity.replacementPreview || props.entity.text
  if (currentText !== expected) {
    emit('replacement-updated', props.entity.id, currentText)
  }
}

function handleClick() {
  if (!props.readonly) {
    reviewStore.setActiveEntity(props.entity.id)
  }
}
</script>

<template>
  <mark
    :id="`entity-${entity.id}`"
    :data-entity-id="entity.id"
    :contenteditable="displayMode === 'pseudonymized' ? 'true' : undefined"
    :class="[
      'rounded-sm px-0.5 transition-all',
      readonly ? '' : 'cursor-pointer',
      highlightClasses,
    ]"
    @click="handleClick"
    @blur="handleBlur"
  >{{ displayText }}</mark>
</template>
