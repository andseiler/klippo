<script setup lang="ts">
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '../stores/auth'
import AppAlert from '../components/ui/AppAlert.vue'
import AppButton from '../components/ui/AppButton.vue'
import LanguageToggle from '../components/layout/LanguageToggle.vue'

const router = useRouter()
const route = useRoute()
const { t } = useI18n()
const authStore = useAuthStore()

const email = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)

async function handleLogin() {
  error.value = ''
  loading.value = true
  try {
    await authStore.login({ email: email.value, password: password.value })
    const redirect = route.query.redirect as string | undefined
    router.push(redirect || '/dashboard')
  } catch (e: unknown) {
    error.value = e instanceof Error ? e.message : t('auth.loginFailed')
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
      <p class="text-center text-sm text-gray-500 dark:text-gray-400 mb-6">
        {{ t('auth.loginDescription') }}
      </p>
      <form @submit.prevent="handleLogin" class="space-y-4">
        <div>
          <label for="email" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.email') }}
          </label>
          <input
            id="email"
            v-model="email"
            type="email"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.emailPlaceholder')"
          />
        </div>
        <div>
          <label for="password" class="block text-sm font-medium text-gray-700 dark:text-gray-300">
            {{ t('auth.password') }}
          </label>
          <input
            id="password"
            v-model="password"
            type="password"
            required
            class="mt-1 block w-full rounded-md border border-gray-300 dark:border-gray-600 bg-white dark:bg-gray-700 text-gray-900 dark:text-gray-100 px-3 py-2 shadow-sm focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            :placeholder="t('auth.passwordPlaceholder')"
          />
        </div>
        <AppAlert v-if="error" variant="error">{{ error }}</AppAlert>
        <AppButton type="submit" :loading="loading" :disabled="loading" class="w-full">
          {{ loading ? t('auth.signingIn') : t('auth.login') }}
        </AppButton>
      </form>
      <p class="mt-4 text-center text-sm text-gray-600 dark:text-gray-400">
        {{ t('auth.noAccount') }}
        <RouterLink to="/register" class="text-blue-600 hover:text-blue-500 dark:text-blue-400">
          {{ t('auth.registerLink') }}
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
