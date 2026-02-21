export const ENTITY_TYPE_COLORS: Record<string, string> = {
  PERSON: 'bg-blue-200/70 dark:bg-blue-800/40',
  ADDRESS: 'bg-purple-200/70 dark:bg-purple-800/40',
  EMAIL: 'bg-cyan-200/70 dark:bg-cyan-800/40',
  PHONE_DACH: 'bg-green-200/70 dark:bg-green-800/40',
  IBAN: 'bg-orange-200/70 dark:bg-orange-800/40',
  DE_STEUER_ID: 'bg-red-200/70 dark:bg-red-800/40',
  DATE_OF_BIRTH: 'bg-pink-200/70 dark:bg-pink-800/40',
  DATE: 'bg-teal-200/70 dark:bg-teal-800/40',
  FINANCIAL_AMOUNT: 'bg-amber-200/70 dark:bg-amber-800/40',
  COMPANY: 'bg-indigo-200/70 dark:bg-indigo-800/40',
  LICENSE_PLATE: 'bg-lime-200/70 dark:bg-lime-800/40',
  HEALTH_INSURANCE_ID: 'bg-rose-200/70 dark:bg-rose-800/40',
  CITY: 'bg-violet-200/70 dark:bg-violet-800/40',
  COUNTRY: 'bg-emerald-200/70 dark:bg-emerald-800/40',
}

export function getEntityTypeHighlightClass(entityType?: string): string {
  return ENTITY_TYPE_COLORS[entityType ?? ''] ?? 'bg-yellow-200/70 dark:bg-yellow-800/40'
}
