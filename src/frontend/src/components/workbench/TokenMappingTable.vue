<script setup lang="ts">
import { ref, computed, watch, nextTick } from 'vue'
import { useI18n } from 'vue-i18n'
import { useReviewStore } from '../../stores/review'
import { getEntityTypeHighlightClass } from '../../constants/entityTypeColors'
import InlineDeleteButton from '../ui/InlineDeleteButton.vue'

const vFocus = { mounted: (el: HTMLElement) => el.focus() }

const { t } = useI18n()
const reviewStore = useReviewStore()
const editingToken = ref<string | null>(null)
const editValue = ref('')
const highlightedRow = ref<HTMLElement | null>(null)

const hasTokens = computed(() => reviewStore.pseudoTokenMappings.size > 0)

watch(
  () => reviewStore.activeEntityToken,
  async () => {
    await nextTick()
    highlightedRow.value?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  },
)

function startEdit(token: string) {
  editingToken.value = token
  editValue.value = token
}

function commitEdit(oldToken: string) {
  const newToken = editValue.value.trim()
  editingToken.value = null
  if (newToken && newToken !== oldToken) {
    reviewStore.updateReplacementByToken(oldToken, newToken)
  }
}

function handleKeydown(event: KeyboardEvent, oldToken: string) {
  if (event.key === 'Enter') {
    event.preventDefault()
    commitEdit(oldToken)
  } else if (event.key === 'Escape') {
    editingToken.value = null
  }
}
</script>

<template>
  <!-- Token mapping mode -->
  <div v-if="hasTokens">
    <table class="w-full text-xs border-collapse">
      <thead>
        <tr class="border-b border-gray-200 dark:border-gray-700">
          <th class="text-left py-1.5 pr-2 font-medium text-gray-500 dark:text-gray-400">
            {{ t('review.workbench.export.originalColumn') }}
          </th>
          <th class="text-left py-1.5 font-medium text-gray-500 dark:text-gray-400">
            {{ t('review.workbench.export.tokenColumn') }}
          </th>
          <th class="w-8"></th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="[token, info] in reviewStore.pseudoTokenMappings"
          :key="token"
          :ref="el => { if (reviewStore.activeEntityToken === token) highlightedRow = el as HTMLElement }"
          :class="[
            'border-b border-gray-100 dark:border-gray-800',
            reviewStore.activeEntityToken === token
              ? 'ring-2 ring-blue-400 bg-blue-50 dark:bg-blue-900/20 rounded'
              : ''
          ]"
        >
          <td class="py-1.5 pr-2">
            <div class="flex items-center gap-1.5">
              <div class="flex flex-col gap-0.5">
                <span
                  v-for="orig in info.originals"
                  :key="orig"
                  class="text-gray-700 dark:text-gray-300"
                >{{ orig }}</span>
              </div>
              <span
                v-if="info.count > 1"
                class="inline-flex items-center px-1.5 py-0 text-[10px] font-medium rounded-full bg-gray-200 dark:bg-gray-600 text-gray-700 dark:text-gray-300 flex-shrink-0"
              >
                {{ t('review.occurrences', { count: info.count }) }}
              </span>
            </div>
          </td>
          <td class="py-1.5">
            <div class="flex items-center gap-1">
              <input
                v-if="editingToken === token"
                v-model="editValue"
                class="w-full rounded border border-blue-400 bg-white dark:bg-gray-800 px-1.5 py-0.5 text-xs text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-1 focus:ring-blue-500"
                @blur="commitEdit(token)"
                @keydown="handleKeydown($event, token)"
                v-focus
              />
              <span
                v-else
                :class="['inline-flex items-center rounded px-1.5 py-0.5 cursor-pointer hover:ring-1 hover:ring-blue-400', getEntityTypeHighlightClass(info.entityType)]"
                :title="info.entityType"
                @click="startEdit(token)"
              >{{ token }}</span>
            </div>
          </td>
          <td class="py-1.5 pl-1">
            <InlineDeleteButton @confirm="reviewStore.deleteByToken(token)" />
          </td>
        </tr>
      </tbody>
    </table>
  </div>

  <!-- Empty state -->
  <p v-else class="text-xs text-gray-400 dark:text-gray-500 italic py-4">
    {{ t('review.workbench.export.noEntities') }}
  </p>
</template>
