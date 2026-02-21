<script setup lang="ts">
import AppSidebar from './AppSidebar.vue'
import AppTopBar from './AppTopBar.vue'
</script>

<template>
  <div class="flex h-screen bg-gray-50 dark:bg-gray-900">
    <AppSidebar />
    <div class="flex flex-col flex-1 min-w-0">
      <AppTopBar />
      <main class="flex-1 overflow-y-auto p-4 sm:p-6 lg:p-8">
        <slot />
      </main>
    </div>
  </div>
</template>
