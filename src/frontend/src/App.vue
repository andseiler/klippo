<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import AppLayout from './components/layout/AppLayout.vue'

const route = useRoute()
const useBareLayout = computed(() => !route.name || route.meta.layout === 'bare')
</script>

<template>
  <AppLayout v-if="!useBareLayout">
    <RouterView />
  </AppLayout>
  <RouterView v-else />
</template>
