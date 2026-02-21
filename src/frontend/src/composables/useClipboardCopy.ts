import { ref } from 'vue'
import { useClipboard } from '@vueuse/core'

export function useClipboardCopy() {
  const { copy } = useClipboard()
  const justCopied = ref(false)
  let timeoutId: ReturnType<typeof setTimeout> | null = null

  async function copyText(text: string) {
    await copy(text)
    justCopied.value = true
    if (timeoutId) clearTimeout(timeoutId)
    timeoutId = setTimeout(() => {
      justCopied.value = false
    }, 2000)
  }

  return { copyText, justCopied }
}
