<script setup lang="ts">
import { useUiStore } from '../../stores/ui'
import SidebarNavItem from './SidebarNavItem.vue'

const uiStore = useUiStore()

const navItems = [
  { to: '/dashboard', icon: 'dashboard', labelKey: 'nav.dashboard' },
  { to: '/upload', icon: 'upload', labelKey: 'nav.upload' },
]
</script>

<template>
  <!-- Mobile overlay -->
  <div
    v-if="uiStore.sidebarMobileOpen"
    class="fixed inset-0 z-30 bg-black/50 lg:hidden"
    @click="uiStore.closeMobileSidebar()"
  />

  <!-- Sidebar -->
  <aside
    :class="[
      'fixed lg:static inset-y-0 left-0 z-40 flex flex-col bg-white dark:bg-gray-800 border-r border-gray-200 dark:border-gray-700 transition-all duration-200',
      uiStore.sidebarCollapsed ? 'w-16' : 'w-64',
      uiStore.sidebarMobileOpen ? 'translate-x-0' : '-translate-x-full lg:translate-x-0',
    ]"
  >
    <!-- Logo -->
    <div class="flex items-center h-16 px-4 border-b border-gray-200 dark:border-gray-700 flex-shrink-0">
      <span v-if="!uiStore.sidebarCollapsed" class="text-lg font-bold text-gray-900 dark:text-gray-100">
        Klippo
      </span>
      <span v-else class="text-lg font-bold text-gray-900 dark:text-gray-100 mx-auto">K</span>
    </div>

    <!-- Nav items -->
    <nav class="flex-1 p-3 space-y-1 overflow-y-auto">
      <SidebarNavItem
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        :icon="item.icon"
        :label-key="item.labelKey"
      />
    </nav>

    <!-- Collapse toggle (desktop only) -->
    <button
      class="hidden lg:flex items-center justify-center h-10 border-t border-gray-200 dark:border-gray-700 text-gray-500 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-700"
      @click="uiStore.toggleSidebar()"
    >
      <svg
        class="h-5 w-5 transition-transform"
        :class="{ 'rotate-180': uiStore.sidebarCollapsed }"
        fill="none"
        stroke="currentColor"
        viewBox="0 0 24 24"
      >
        <path stroke-linecap="round" stroke-linejoin="round" stroke-width="2" d="M11 19l-7-7 7-7m8 14l-7-7 7-7" />
      </svg>
    </button>
  </aside>
</template>
