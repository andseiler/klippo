export interface EntityTypeMeta {
  key: string
  regexValidated: boolean
  quickTag: boolean
}

export const ENTITY_TYPES: Record<string, EntityTypeMeta> = {
  PERSON: { key: 'PERSON', regexValidated: false, quickTag: true },
  ADDRESS: { key: 'ADDRESS', regexValidated: false, quickTag: true },
  EMAIL: { key: 'EMAIL', regexValidated: true, quickTag: false },
  PHONE_DACH: { key: 'PHONE_DACH', regexValidated: true, quickTag: true },
  IBAN: { key: 'IBAN', regexValidated: true, quickTag: false },
  DE_STEUER_ID: { key: 'DE_STEUER_ID', regexValidated: true, quickTag: true },
  DATE_OF_BIRTH: { key: 'DATE_OF_BIRTH', regexValidated: true, quickTag: false },
  DATE: { key: 'DATE', regexValidated: true, quickTag: false },
  FINANCIAL_AMOUNT: { key: 'FINANCIAL_AMOUNT', regexValidated: false, quickTag: false },
  COMPANY: { key: 'COMPANY', regexValidated: false, quickTag: false },
  LICENSE_PLATE: { key: 'LICENSE_PLATE', regexValidated: true, quickTag: false },
  HEALTH_INSURANCE_ID: { key: 'HEALTH_INSURANCE_ID', regexValidated: true, quickTag: false },
  CITY: { key: 'CITY', regexValidated: false, quickTag: false },
  COUNTRY: { key: 'COUNTRY', regexValidated: false, quickTag: false },
}

export const QUICK_TAG_TYPES = Object.values(ENTITY_TYPES).filter((t) => t.quickTag)
export const REGEX_VALIDATED_TYPES = Object.values(ENTITY_TYPES).filter((t) => t.regexValidated)

export const ALL_ENTITY_TYPE_KEYS = Object.keys(ENTITY_TYPES)
