import { ref, computed } from 'vue'

const TOTAL_STEPS = 4

export function useReviewerTraining() {
  const trainingStep = ref(1)
  const isTrainingModalOpen = ref(false)

  const totalSteps = TOTAL_STEPS

  const isLastStep = computed(() => trainingStep.value >= TOTAL_STEPS)

  function getStorageKey(): string {
    const userId = localStorage.getItem('userId') || 'anonymous'
    return `reviewerTrainingCompleted_${userId}`
  }

  function checkTrainingNeeded() {
    const completed = localStorage.getItem(getStorageKey())
    if (!completed) {
      isTrainingModalOpen.value = true
      trainingStep.value = 1
    }
  }

  function nextStep() {
    if (trainingStep.value < TOTAL_STEPS) {
      trainingStep.value++
    }
  }

  function prevStep() {
    if (trainingStep.value > 1) {
      trainingStep.value--
    }
  }

  function completeTraining() {
    localStorage.setItem(getStorageKey(), new Date().toISOString())
    isTrainingModalOpen.value = false
  }

  return {
    trainingStep,
    totalSteps,
    isLastStep,
    isTrainingModalOpen,
    checkTrainingNeeded,
    nextStep,
    prevStep,
    completeTraining,
  }
}
