<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '../stores/auth'
import AppAlert from '../components/ui/AppAlert.vue'
import AppButton from '../components/ui/AppButton.vue'
import LanguageToggle from '../components/layout/LanguageToggle.vue'

const router = useRouter()
const { t } = useI18n()
const authStore = useAuthStore()

const email = ref('')
const name = ref('')
const password = ref('')
const confirmPassword = ref('')
const organizationName = ref('')
const error = ref('')
const loading = ref(false)

async function handleRegister() {
  error.value = ''

  if (password.value !== confirmPassword.value) {
    error.value = t('auth.passwordMismatch')
    return
  }

  loading.value = true
  try {
    await authStore.register({
      email: email.value,
      name: name.value,
      password: password.value,
      organizationName: organizationName.value,
    })
    router.push('/dashboard')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('auth.registerFailed')
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="relative min-h-screen flex items-center justify-center bg-gray-100 dark:bg-gray-900">
    <div class="absolute top-4 right-4">
      <LanguageToggle />
    </div>
    <div class="max-w-md w-full bg-white dark:bg-gray-800 rounded-lg shadow-md p-8">
      <h1 class="text-2xl font-bold text-center text-gray-900 dark:text-gray-100 mb-2">
        {{ t('common.appName') }}
      </h1>
      <h2 class="text-lg font-medium text-center text-gray-700 dark:text-gray-300 mb-2">
        {{ t('auth.register') }}
      </h2>
      <p class="text-center text-sm text-gray-500 dark:text-gray-400 mb-4">
        {{ t('auth.registerDescription') }}
      </p>
      <form @submit.prevent="handleRegister" class="space-y-4">
        <div>
          <label for="reg-email" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.email') }}
          </label>
          <input
            id="reg-email"
            v-model="email"
            type="email"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.emailPlaceholder')"
          />
        </div>
        <div>
          <label for="reg-name" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.name') }}
          </label>
          <input
            id="reg-name"
            v-model="name"
            type="text"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.namePlaceholder')"
          />
        </div>
        <div>
          <label for="reg-org" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.organizationName') }}
          </label>
          <input
            id="reg-org"
            v-model="organizationName"
            type="text"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.organizationPlaceholder')"
          />
        </div>
        <div>
          <label for="reg-password" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.password') }}
          </label>
          <input
            id="reg-password"
            v-model="password"
            type="password"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.passwordPlaceholder')"
          />
        </div>
        <div>
          <label for="reg-confirm" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.confirmPassword') }}
          </label>
          <input
            id="reg-confirm"
            v-model="confirmPassword"
            type="password"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.confirmPasswordPlaceholder')"
          />
        </div>
        <AppAlert v-if="error" variant="error">{{ error }}</AppAlert>
        <AppButton type="submit" :loading="loading" :disabled="loading" class="w-full">
          {{ loading ? t('auth.registering') : t('auth.register') }}
        </AppButton>
      </form>
      <p class="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
        {{ t('auth.hasAccount') }}
        <RouterLink to="/login" class="text-blue-600 hover:text-blue-500 dark:text-blue-400">
          {{ t('auth.loginLink') }}
        </RouterLink>
      </p>
      <p class="mt-2 text-center text-sm">
        <RouterLink to="/playground" class="text-gray-500 hover:text-gray-700 dark:text-gray-400 dark:hover:text-gray-300">
          {{ t('auth.tryPlayground') }}
        </RouterLink>
      </p>
    </div>
  </div>
</template>
