<script setup lang="ts">
import TokenMappingTable from './TokenMappingTable.vue'
import AddPiiByTextPanel from './AddPiiByTextPanel.vue'

defineProps<{
  textSelection: ReturnType<typeof import('../../composables/useTextSelection').useTextSelection>
}>()
</script>

<template>
  <div class="space-y-4">
    <!-- Add PII by text -->
    <AddPiiByTextPanel :text-selection="textSelection" />

    <!-- Token mapping table with delete buttons -->
    <TokenMappingTable />
  </div>
</template>
