<script setup lang="ts">
import { ref } from 'vue'
import AppModal from '../ui/AppModal.vue'

const showImpressum = ref(false)
const showDatenschutz = ref(false)
</script>

<template>
  <footer class="flex-shrink-0 flex items-center justify-center gap-4 px-4 py-3 text-xs text-gray-500 dark:text-gray-400 border-t border-gray-200 dark:border-gray-800">
    <button class="hover:text-gray-700 dark:hover:text-gray-300 underline" @click="showImpressum = true">
      Impressum
    </button>
    <span class="text-gray-300 dark:text-gray-600">|</span>
    <button class="hover:text-gray-700 dark:hover:text-gray-300 underline" @click="showDatenschutz = true">
      Datenschutz
    </button>
  </footer>

  <AppModal :open="showImpressum" title="Impressum" size="wide" @update:open="showImpressum = $event">
    <div class="text-sm text-gray-700 dark:text-gray-300 space-y-2">
      <p class="font-medium">Angaben gemäß § 5 ECG und § 25 MedienG</p>
      <p>
        Andreas Seiler<br />
        Weisstraße 9<br />
        6112 Wattens<br />
        Österreich
      </p>
      <p>
        Kontakt:
        <a href="mailto:mail@andreas-seiler.net" class="underline">mail@andreas-seiler.net</a>
      </p>
      <p>Hosting: Hetzner Online GmbH, Deutschland</p>
    </div>
  </AppModal>

  <AppModal :open="showDatenschutz" title="Datenschutzerklärung" size="wide" @update:open="showDatenschutz = $event">
    <div class="text-sm text-gray-700 dark:text-gray-300 space-y-4">
      <section>
        <h3 class="font-semibold mb-1">1. Verantwortlicher</h3>
        <p>
          Andreas Seiler<br />
          Weisstraße 9, 6112 Wattens, Österreich<br />
          <a href="mailto:mail@andreas-seiler.net" class="underline">mail@andreas-seiler.net</a>
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">2. Erhobene Daten</h3>
        <p>
          Der Playground speichert keine personenbezogenen Nutzerdaten. Hochgeladene Dokumente werden
          ausschließlich zur PII-Erkennung verarbeitet und nach 1 Stunde automatisch gelöscht.
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">3. Hosting</h3>
        <p>
          Die Anwendung wird bei Hetzner Online GmbH in Deutschland gehostet. Server-Logs (IP-Adresse,
          Zeitstempel) werden für den sicheren Betrieb gespeichert und nach 7 Tagen gelöscht.
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">4. Cookies und Tracking</h3>
        <p>
          Es werden keine Tracking-Cookies und keine Analyse-Tools eingesetzt. Lediglich ein technischer
          Session-Token wird in localStorage gespeichert.
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">5. Datenverarbeitung</h3>
        <p>
          Die PII-Erkennung erfolgt lokal auf dem Server mittels Regex und Named Entity Recognition (NER).
          Optional kann ein lokales LLM zur erweiterten Erkennung eingesetzt werden. Es werden keine Daten
          an Dritte weitergegeben.
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">6. Ihre Rechte</h3>
        <p>
          Sie haben das Recht auf Auskunft, Berichtigung und Löschung Ihrer Daten. Kontaktieren Sie uns
          per E-Mail:
          <a href="mailto:mail@andreas-seiler.net" class="underline">mail@andreas-seiler.net</a>
        </p>
      </section>
    </div>
  </AppModal>
</template>
