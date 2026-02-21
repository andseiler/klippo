import { faker } from '@faker-js/faker/locale/de'

export function generateFakeValue(entityType: string): string {
  const type = entityType.toUpperCase()
  switch (type) {
    case 'PERSON':
    case 'NAME':
    case 'PER':
      return faker.person.fullName()
    case 'ORGANIZATION':
    case 'ORG':
    case 'COMPANY':
      return faker.company.name()
    case 'LOCATION':
    case 'LOC':
    case 'GPE':
    case 'CITY':
      return faker.location.city()
    case 'COUNTRY':
      return faker.location.country()
    case 'ADDRESS':
      return faker.location.streetAddress()
    case 'IBAN':
      return faker.finance.iban()
    case 'EMAIL':
    case 'EMAIL_ADDRESS':
      return faker.internet.email()
    case 'PHONE':
    case 'PHONE_NUMBER':
    case 'PHONE_DACH':
      return faker.phone.number()
    case 'DATE':
    case 'DATE_TIME':
    case 'DATE_OF_BIRTH':
      return faker.date.past({ years: 5 }).toLocaleDateString('de-DE')
    case 'DE_STEUER_ID':
      return `[DE_STEUER_ID_${String(faker.number.int({ min: 1, max: 999 })).padStart(3, '0')}]`
    case 'FINANCIAL_AMOUNT':
      return faker.finance.amount({ min: 100, max: 50000, dec: 2 }) + ' EUR'
    case 'LICENSE_PLATE':
      return `${faker.string.alpha({ length: 2, casing: 'upper' })}-${faker.string.alpha({ length: 2, casing: 'upper' })} ${faker.number.int({ min: 100, max: 9999 })}`
    case 'HEALTH_INSURANCE_ID':
      return `[HEALTH_INSURANCE_ID_${String(faker.number.int({ min: 1, max: 999 })).padStart(3, '0')}]`
    default: {
      const counter = String(faker.number.int({ min: 1, max: 999 })).padStart(3, '0')
      return `[${type}_${counter}]`
    }
  }
}
