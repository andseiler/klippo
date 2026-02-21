import { ref, computed } from 'vue'

const MAX_FILE_SIZE = 50 * 1024 * 1024 // 50 MB

export function useFileUpload() {
  const file = ref<File | null>(null)
  const validationError = ref<string | null>(null)
  const isDragging = ref(false)

  function validateFile(f: File): boolean {
    validationError.value = null

    if (f.size > MAX_FILE_SIZE) {
      validationError.value = 'fileTooLarge'
      return false
    }

    return true
  }

  function selectFile(f: File) {
    if (validateFile(f)) {
      file.value = f
    }
  }

  function clearFile() {
    file.value = null
    validationError.value = null
  }

  const formattedSize = computed(() => {
    if (!file.value) return ''
    const bytes = file.value.size
    if (bytes < 1024) return `${bytes} B`
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`
  })

  return {
    file,
    validationError,
    isDragging,
    selectFile,
    clearFile,
    formattedSize,
  }
}
