<script setup lang="ts">
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useFileUpload } from '../composables/useFileUpload'
import { useJobsStore } from '../stores/jobs'
import FileDropZone from '../components/upload/FileDropZone.vue'
import AppButton from '../components/ui/AppButton.vue'
import AppAlert from '../components/ui/AppAlert.vue'
import AppProgress from '../components/ui/AppProgress.vue'

const router = useRouter()
const { t } = useI18n()
const jobsStore = useJobsStore()

const { file, validationError, selectFile, clearFile, formattedSize } = useFileUpload()

const submitting = ref(false)
const uploadProgress = ref(0)
const submitError = ref('')

// Tab state
const activeTab = ref<'file' | 'text'>('file')
const pastedText = ref('')
const textDocName = ref(generateRandomName())

function generateRandomName(): string {
  const chars = 'abcdefghijklmnopqrstuvwxyz0123456789'
  let result = 'doc-'
  for (let i = 0; i < 8; i++) {
    result += chars.charAt(Math.floor(Math.random() * chars.length))
  }
  return result
}

const textFileSize = computed(() =>
  pastedText.value ? `${new Blob([pastedText.value]).size} B` : '0 B'
)
const hasTextContent = computed(() => pastedText.value.trim().length > 0)
const hasInput = computed(() =>
  activeTab.value === 'file' ? !!file.value : hasTextContent.value
)

async function handleSubmit() {
  let submitFile: File

  if (activeTab.value === 'text') {
    const text = pastedText.value.trim()
    if (!text) {
      submitError.value = t('upload.errors.noText')
      return
    }
    const blob = new Blob([text], { type: 'text/plain' })
    submitFile = new File([blob], textDocName.value + '.txt', { type: 'text/plain' })
  } else {
    if (!file.value) {
      submitError.value = t('upload.errors.noFile')
      return
    }
    submitFile = file.value
  }

  submitting.value = true
  submitError.value = ''
  uploadProgress.value = 0

  // Simulate upload progress
  const progressInterval = setInterval(() => {
    if (uploadProgress.value < 90) {
      uploadProgress.value += Math.random() * 15
    }
  }, 200)

  try {
    const job = await jobsStore.submitJob({
      file: submitFile,
    })
    uploadProgress.value = 100
    clearInterval(progressInterval)
    setTimeout(() => {
      router.push('/dashboard')
    }, 500)
  } catch (e) {
    clearInterval(progressInterval)
    submitError.value = e instanceof Error ? e.message : t('common.error')
  } finally {
    submitting.value = false
  }
}

function handleFileSelected(f: File) {
  selectFile(f)
}

function handleReset() {
  clearFile()
  pastedText.value = ''
  textDocName.value = generateRandomName()
  submitError.value = ''
  uploadProgress.value = 0
}

function switchTab(tab: 'file' | 'text') {
  activeTab.value = tab
}
</script>

<template>
  <div class="max-w-2xl mx-auto space-y-6">
    <h1 class="text-2xl font-bold text-gray-900 dark:text-gray-100">
      {{ t('upload.title') }}
    </h1>

    <!-- Tab bar -->
    <div class="flex border-b border-gray-200 dark:border-gray-700">
      <button
        :class="[
          'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
          activeTab === 'file'
            ? 'border-blue-500 text-blue-600 dark:text-blue-400'
            : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300',
        ]"
        @click="switchTab('file')"
      >
        {{ t('upload.tabs.file') }}
      </button>
      <button
        :class="[
          'px-4 py-2 text-sm font-medium border-b-2 transition-colors',
          activeTab === 'text'
            ? 'border-blue-500 text-blue-600 dark:text-blue-400'
            : 'border-transparent text-gray-500 dark:text-gray-400 hover:text-gray-700 dark:hover:text-gray-300',
        ]"
        @click="switchTab('text')"
      >
        {{ t('upload.tabs.text') }}
      </button>
    </div>

    <!-- Step 1: File selection tab -->
    <div v-if="activeTab === 'file'">
      <div v-if="!file">
        <FileDropZone @file-selected="handleFileSelected" />
        <AppAlert v-if="validationError" variant="error" class="mt-3">
          {{ t(`upload.${validationError}`) }}
        </AppAlert>
      </div>
      <div v-else class="flex items-center justify-between bg-white dark:bg-gray-800 rounded-lg border border-gray-200 dark:border-gray-700 p-4">
        <div>
          <p class="text-sm font-medium text-gray-900 dark:text-gray-100">{{ file.name }}</p>
          <p class="text-xs text-gray-500 dark:text-gray-400">{{ formattedSize }}</p>
        </div>
        <AppButton variant="ghost" size="sm" @click="handleReset">
          {{ t('common.reset') }}
        </AppButton>
      </div>
    </div>

    <!-- Step 1: Text input tab -->
    <div v-if="activeTab === 'text'" class="space-y-4">
      <div>
        <label class="block text-sm font-medium text-gray-700 dark:text-gray-300 mb-1">
          {{ t('upload.textInput.nameLabel') }}
        </label>
        <input
          v-model="textDocName"
          type="text"
          class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 px-3 py-2 text-sm text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        />
      </div>
      <div>
        <textarea
          v-model="pastedText"
          :placeholder="t('upload.textInput.textPlaceholder')"
          class="w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-800 p-4 text-sm text-gray-900 dark:text-gray-100 leading-relaxed resize-y min-h-[200px] focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
        />
      </div>
    </div>

    <!-- Submit -->
    <div v-if="hasInput" class="space-y-4">
      <AppAlert v-if="submitError" variant="error">{{ submitError }}</AppAlert>

      <div v-if="submitting" class="space-y-2">
        <p class="text-sm text-gray-600 dark:text-gray-400">{{ t('upload.uploadProgress') }}</p>
        <AppProgress :value="Math.min(uploadProgress, 100)" />
      </div>

      <AppButton
        :loading="submitting"
        :disabled="submitting"
        class="w-full"
        @click="handleSubmit"
      >
        {{ submitting ? t('upload.submitting') : t('upload.submit') }}
      </AppButton>
    </div>
  </div>
</template>
