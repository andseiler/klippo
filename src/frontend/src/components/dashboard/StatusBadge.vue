<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { JobStatus } from '../../api/types'
import { getJobStatusMeta } from '../../constants/jobStatuses'
import AppBadge from '../ui/AppBadge.vue'

interface Props {
  status: JobStatus
}

const props = defineProps<Props>()
const { t } = useI18n()
</script>

<template>
  <AppBadge :color-class="getJobStatusMeta(props.status).colorClass">
    {{ t(`job.status.${props.status}`) }}
  </AppBadge>
</template>
