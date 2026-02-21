<script setup lang="ts">
import { useUiStore } from '../../stores/ui'
import { useDarkMode } from '../../composables/useDarkMode'
import LanguageToggle from './LanguageToggle.vue'
import UserMenu from './UserMenu.vue'

const uiStore = useUiStore()
const { isDark, toggleDark } = useDarkMode()
</script>

<template>
  <header class="flex items-center h-16 px-4 sm:px-6 bg-white dark:bg-gray-800 border-b border-gray-200 dark:border-gray-700">
    <!-- Mobile hamburger -->
    <button
      class="lg:hidden mr-3 rounded-md p-2 text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700"
      @click="uiStore.toggleMobileSidebar()"
    >
      <svg class="h-6 w-6" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6h16M4 12h16M4 18h16" />
      </svg>
    </button>

    <div class="flex-1" />

    <!-- Right side controls -->
    <div class="flex items-center gap-2">
      <!-- Dark mode toggle -->
      <button
        class="rounded-md p-2 text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700 transition-colors"
        @click="toggleDark()"
      >
        <svg v-if="isDark" class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364 6.364l-.707-.707M6.343 6.343l-.707-.707m12.728 0l-.707.707M6.343 17.657l-.707.707M16 12a4 4 0 11-8 0 4 4 0 018 0z" />
        </svg>
        <svg v-else class="h-5 w-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
          <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M20.354 15.354A9 9 0 018.646 3.646 9.003 9.003 0 0012 21a9.003 9.003 0 008.354-5.646z" />
        </svg>
      </button>

      <LanguageToggle />
      <UserMenu />
    </div>
  </header>
</template>
