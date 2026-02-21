<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import AppModal from '../ui/AppModal.vue'
import AppButton from '../ui/AppButton.vue'
import AppProgress from '../ui/AppProgress.vue'
import TrainingStepIntro from './training/TrainingStepIntro.vue'
import TrainingStepPiiTypes from './training/TrainingStepPiiTypes.vue'
import TrainingStepManualAdd from './training/TrainingStepManualAdd.vue'
import TrainingStepAutomationBias from './training/TrainingStepAutomationBias.vue'

const props = defineProps<{
  training: ReturnType<typeof import('../../composables/useReviewerTraining').useReviewerTraining>
}>()

const { t } = useI18n()

const stepComponents = [
  TrainingStepIntro,
  TrainingStepPiiTypes,
  TrainingStepManualAdd,
  TrainingStepAutomationBias,
]

const currentComponent = computed(() => stepComponents[props.training.trainingStep.value - 1])
const progressValue = computed(() => (props.training.trainingStep.value / props.training.totalSteps) * 100)
</script>

<template>
  <AppModal
    :open="training.isTrainingModalOpen.value"
    :title="t('review.training.title')"
    persistent
    @update:open="() => {}"
  >
    <div class="space-y-4">
      <!-- Progress -->
      <div class="space-y-1">
        <div class="flex justify-between text-xs text-gray-500 dark:text-gray-400">
          <span>{{ t('review.training.step', { current: training.trainingStep.value, total: training.totalSteps }) }}</span>
        </div>
        <AppProgress :value="progressValue" />
      </div>

      <!-- Step content -->
      <component :is="currentComponent" />

      <!-- Navigation -->
      <div class="flex justify-between pt-2">
        <AppButton
          v-if="training.trainingStep.value > 1"
          variant="ghost"
          size="sm"
          @click="training.prevStep"
        >
          {{ t('review.training.back') }}
        </AppButton>
        <div v-else />

        <AppButton
          v-if="!training.isLastStep.value"
          variant="primary"
          size="sm"
          @click="training.nextStep"
        >
          {{ t('review.training.next') }}
        </AppButton>
        <AppButton
          v-else
          variant="primary"
          size="sm"
          @click="training.completeTraining"
        >
          {{ t('review.training.complete') }}
        </AppButton>
      </div>
    </div>
  </AppModal>
</template>
