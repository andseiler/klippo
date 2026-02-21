import { createI18n } from 'vue-i18n'

import deCommon from './de/common.json'
import deAuth from './de/auth.json'
import deUpload from './de/upload.json'
import deDashboard from './de/dashboard.json'
import deJob from './de/job.json'
import deReview from './de/review.json'
import dePlayground from './de/playground.json'

import enCommon from './en/common.json'
import enAuth from './en/auth.json'
import enUpload from './en/upload.json'
import enDashboard from './en/dashboard.json'
import enJob from './en/job.json'
import enReview from './en/review.json'
import enPlayground from './en/playground.json'

const i18n = createI18n({
  legacy: false,
  locale: localStorage.getItem('locale') || 'de',
  fallbackLocale: 'en',
  messages: {
    de: {
      ...deCommon,
      ...deAuth,
      ...deUpload,
      ...deDashboard,
      ...deJob,
      ...deReview,
      ...dePlayground,
    },
    en: {
      ...enCommon,
      ...enAuth,
      ...enUpload,
      ...enDashboard,
      ...enJob,
      ...enReview,
      ...enPlayground,
    },
  },
})

export default i18n
