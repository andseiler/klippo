<script setup lang="ts">
import { ref, computed } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModal from '../ui/AppModal.vue'
import AppButton from '../ui/AppButton.vue'

interface Props {
  open: boolean
}

defineProps<Props>()
const emit = defineEmits<{
  'update:open': [value: boolean]
  continue: []
  end: []
}>()

const { t } = useI18n()

const checked1 = ref(false)
const checked2 = ref(false)
const checked3 = ref(false)

const allChecked = computed(() => checked1.value && checked2.value && checked3.value)

function resetState() {
  checked1.value = false
  checked2.value = false
  checked3.value = false
}
</script>

<template>
  <AppModal
    :open="open"
    :title="t('review.completeReviewModal.title')"
    persistent
    @update:open="emit('update:open', $event)"
  >
    <div class="space-y-4">
      <!-- Checkbox 1 -->
      <label class="flex items-start gap-3 cursor-pointer">
        <input
          v-model="checked1"
          type="checkbox"
          class="mt-0.5 rounded border-gray-300 dark:border-gray-600"
        />
        <span class="text-sm text-gray-700 dark:text-gray-300">
          {{ t('review.completeReviewModal.checkbox1') }}
        </span>
      </label>

      <!-- Checkbox 2 -->
      <label class="flex items-start gap-3 cursor-pointer">
        <input
          v-model="checked2"
          type="checkbox"
          class="mt-0.5 rounded border-gray-300 dark:border-gray-600"
        />
        <span class="text-sm text-gray-700 dark:text-gray-300">
          {{ t('review.completeReviewModal.checkbox2') }}
        </span>
      </label>

      <!-- Checkbox 3 -->
      <label class="flex items-start gap-3 cursor-pointer">
        <input
          v-model="checked3"
          type="checkbox"
          class="mt-0.5 rounded border-gray-300 dark:border-gray-600"
        />
        <span class="text-sm text-gray-700 dark:text-gray-300">
          {{ t('review.completeReviewModal.checkbox3') }}
        </span>
      </label>

      <!-- Actions -->
      <div class="flex justify-end gap-3 pt-2">
        <AppButton variant="secondary" @click="resetState(); emit('continue')">
          {{ t('review.completeReviewModal.continueReview') }}
        </AppButton>
        <AppButton :disabled="!allChecked" @click="emit('end')">
          {{ t('review.completeReviewModal.endReview') }}
        </AppButton>
      </div>
    </div>
  </AppModal>
</template>
