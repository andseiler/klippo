<script setup lang="ts">
import { DropdownMenuRoot, DropdownMenuTrigger, DropdownMenuPortal, DropdownMenuContent, DropdownMenuItem, DropdownMenuSeparator } from 'radix-vue'
import { useRouter } from 'vue-router'
import { useI18n } from 'vue-i18n'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const { t } = useI18n()
const authStore = useAuthStore()

async function handleLogout() {
  await authStore.logout()
  router.push('/login')
}
</script>

<template>
  <DropdownMenuRoot>
    <DropdownMenuTrigger
      class="flex items-center gap-2 rounded-md px-3 py-1.5 text-sm text-gray-700 dark:text-gray-300 hover:bg-gray-100 dark:hover:bg-gray-700 focus:outline-none transition-colors"
    >
      <span class="hidden sm:inline">{{ authStore.email }}</span>
      <svg class="h-4 w-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M19 9l-7 7-7-7" />
      </svg>
    </DropdownMenuTrigger>
    <DropdownMenuPortal>
      <DropdownMenuContent
        class="z-50 min-w-[200px] rounded-md border border-gray-200 dark:border-gray-600 bg-white dark:bg-gray-800 p-1 shadow-lg"
        :side-offset="8"
        align="end"
      >
        <div class="px-3 py-2 text-sm">
          <div class="font-medium text-gray-900 dark:text-gray-100">{{ authStore.name }}</div>
          <div class="text-gray-500 dark:text-gray-400 text-xs mt-0.5">{{ authStore.email }}</div>
        </div>
        <DropdownMenuSeparator class="h-px bg-gray-200 dark:bg-gray-600 my-1" />
        <DropdownMenuItem
          class="flex cursor-pointer items-center rounded-md px-3 py-2 text-sm text-red-600 dark:text-red-400 hover:bg-gray-100 dark:hover:bg-gray-700 outline-none data-[highlighted]:bg-gray-100 dark:data-[highlighted]:bg-gray-700"
          @select="handleLogout"
        >
          {{ t('common.logout') }}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenuPortal>
  </DropdownMenuRoot>
</template>
