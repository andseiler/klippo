<script setup lang="ts">
import { RouterLink, useRoute } from 'vue-router'
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { useUiStore } from '../../stores/ui'

interface Props {
  to: string
  icon: string
  labelKey: string
}

const props = defineProps<Props>()
const route = useRoute()
const { t } = useI18n()
const uiStore = useUiStore()

const isActive = computed(() => route.path === props.to || route.path.startsWith(props.to + '/'))
</script>

<template>
  <RouterLink
    :to="props.to"
    :class="[
      'flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors',
      isActive
        ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
        : 'text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-700',
    ]"
    @click="uiStore.closeMobileSidebar()"
  >
    <!-- Dashboard icon -->
    <svg v-if="props.icon === 'dashboard'" class="h-5 w-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2V6zM14 6a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2V6zM4 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2H6a2 2 0 01-2-2v-2zM14 16a2 2 0 012-2h2a2 2 0 012 2v2a2 2 0 01-2 2h-2a2 2 0 01-2-2v-2z" />
    </svg>
    <!-- Upload icon -->
    <svg v-else-if="props.icon === 'upload'" class="h-5 w-5 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
      <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
    </svg>
    <span v-if="!uiStore.sidebarCollapsed">{{ t(props.labelKey) }}</span>
  </RouterLink>
</template>
