import type { ConfidenceTier } from '../api/types'

export interface TierStyle {
  colorLight: string
  colorDark: string
  borderColor: string
  textColor: string
  label: string
}

export const CONFIDENCE_TIERS: Record<ConfidenceTier, TierStyle> = {
  HIGH: {
    colorLight: 'bg-green-200/60',
    colorDark: 'dark:bg-green-800/40',
    borderColor: 'border-green-400 dark:border-green-600',
    textColor: 'text-green-700 dark:text-green-300',
    label: 'HIGH',
  },
  MEDIUM: {
    colorLight: 'bg-yellow-200/60',
    colorDark: 'dark:bg-yellow-800/40',
    borderColor: 'border-yellow-400 dark:border-yellow-600',
    textColor: 'text-yellow-700 dark:text-yellow-300',
    label: 'MEDIUM',
  },
  LOW: {
    colorLight: 'bg-orange-200/60',
    colorDark: 'dark:bg-orange-800/40',
    borderColor: 'border-orange-400 dark:border-orange-600',
    textColor: 'text-orange-700 dark:text-orange-300',
    label: 'LOW',
  },
}

export function getTierStyle(tier: ConfidenceTier): TierStyle {
  return CONFIDENCE_TIERS[tier]
}

export function getHighlightClasses(tier: ConfidenceTier): string {
  const style = CONFIDENCE_TIERS[tier]
  return `${style.colorLight} ${style.colorDark}`
}
