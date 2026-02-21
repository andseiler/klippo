import { ref, onUnmounted } from 'vue'

export function useEstimatedProgress(defaultEstimatedMs = 10000, maxProgress = 95) {
  const progress = ref(0)
  const elapsedSeconds = ref(0)
  const startTime = ref(0)
  let tau = defaultEstimatedMs / 3
  let intervalId: ReturnType<typeof setInterval> | null = null
  let completing = false

  function tick() {
    if (completing) return
    const elapsed = Date.now() - startTime.value
    elapsedSeconds.value = Math.floor(elapsed / 1000)
    progress.value = Math.round(maxProgress * (1 - Math.exp(-elapsed / tau)))
  }

  function start(wordCount?: number) {
    reset()
    if (wordCount != null) {
      const estimatedSeconds = Math.max(5, 3 + wordCount / 400)
      tau = (estimatedSeconds * 1000) / 3
    } else {
      tau = defaultEstimatedMs / 3
    }
    startTime.value = Date.now()
    intervalId = setInterval(tick, 100)
  }

  function complete() {
    completing = true
    if (intervalId) {
      clearInterval(intervalId)
      intervalId = null
    }
    progress.value = 100
  }

  function reset() {
    completing = false
    if (intervalId) {
      clearInterval(intervalId)
      intervalId = null
    }
    progress.value = 0
    elapsedSeconds.value = 0
    startTime.value = 0
  }

  onUnmounted(() => {
    if (intervalId) {
      clearInterval(intervalId)
      intervalId = null
    }
  })

  return { progress, elapsedSeconds, start, complete, reset }
}
