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
    <div class="text-sm text-gray-700 dark:text-gray-300 space-y-4">
      <p class="font-semibold">Angaben gemäß § 5 ECG und Offenlegung gemäß § 25 MedienG</p>

      <section>
        <p class="font-medium mb-1">Diensteanbieter / Medieninhaber:</p>
        <p>Andreas Seiler (EPU)</p>
      </section>

      <section>
        <p class="font-medium mb-1">Anschrift:</p>
        <p>
          Weisstraße 9<br />
          6112 Wattens<br />
          Österreich
        </p>
      </section>

      <section>
        <p class="font-medium mb-1">Kontakt:</p>
        <p>
          Telefon: <a href="tel:+436603245617" class="underline">+43 660 3245617</a><br />
          E-Mail: <a href="mailto:mail@andreas-seiler.net" class="underline">mail@andreas-seiler.net</a>
        </p>
      </section>

      <section>
        <p class="font-medium mb-1">UID-Nummer:</p>
        <p>ATU75761105</p>
      </section>

      <section>
        <p class="font-medium mb-1">Unternehmensgegenstand:</p>
        <p>Dienstleistungen in der automatischen Datenverarbeitung und Informationstechnik</p>
      </section>

      <section>
        <p class="font-medium mb-1">Mitgliedschaft / Kammerzugehörigkeit:</p>
        <p>
          Mitglied der Wirtschaftskammer Tirol (WKO)<br />
          Fachgruppe: Unternehmensberatung, Buchhaltung und Informationstechnologie (UBIT)
        </p>
      </section>

      <section>
        <p class="font-medium mb-1">Berufsrechtliche Vorschriften:</p>
        <p>Gewerbeordnung (GewO), abrufbar im <a href="https://www.ris.bka.gv.at/GeltendeFassung.wxe?Abfrage=Bundesnormen&Gesetzesnummer=10007517" target="_blank" rel="noopener noreferrer" class="underline">Rechtsinformationssystem des Bundes (RIS)</a></p>
      </section>

      <section>
        <p class="font-medium mb-1">Hosting:</p>
        <p>Hetzner Online GmbH, Industriestr. 25, 91710 Gunzenhausen, Deutschland</p>
      </section>

      <section>
        <p class="font-medium mb-1">Grundlegende Richtung der Website („Blattlinie"):</p>
        <p>Information über das Online-Tool „Klippo" zur Erkennung und Maskierung personenbezogener Daten sowie Bereitstellung von Kontakt- und Supportinformationen.</p>
      </section>
    </div>
  </AppModal>

  <AppModal :open="showDatenschutz" title="Datenschutzerklärung" size="wide" @update:open="showDatenschutz = $event">
    <div class="text-sm text-gray-700 dark:text-gray-300 space-y-4">
      <p class="text-xs text-gray-500 dark:text-gray-400">Stand: 23.02.2026</p>

      <section>
        <h3 class="font-semibold mb-1">1. Verantwortlicher</h3>
        <p>
          Andreas Seiler (EPU)<br />
          Weisstraße 9, 6112 Wattens, Österreich<br />
          Telefon: <a href="tel:+436603245617" class="underline">+43 660 3245617</a><br />
          E-Mail: <a href="mailto:mail@andreas-seiler.net" class="underline">mail@andreas-seiler.net</a>
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">2. Zweck der Verarbeitung</h3>
        <p>
          Wir betreiben „Klippo", ein Online-Tool, das Texte/Dokumente verarbeitet, um personenbezogene
          Daten (z.&nbsp;B. Namen, Adressen, Telefonnummern) zu erkennen und durch Platzhalter zu ersetzen.
          Ziel ist es, Nutzern eine verfremdete Version für die Weiterverwendung (z.&nbsp;B. in Chatbots)
          bereitzustellen.
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">3. Welche Daten wir verarbeiten</h3>

        <div class="ml-2 space-y-2">
          <div>
            <p class="font-medium">3.1 Beim Aufruf der Website</p>
            <p>
              Beim Aufruf der Website verarbeitet unser Server technisch notwendige Verbindungsdaten
              (insbesondere IP-Adresse, Datum/Uhrzeit, aufgerufene Seite/Request), um die Website
              bereitzustellen und zu schützen.
            </p>
          </div>

          <div>
            <p class="font-medium">3.2 Bei Nutzung des Playgrounds (Upload/Verarbeitung)</p>
            <p>
              Wenn Sie Text eingeben oder Dokumente hochladen, verarbeiten wir den Inhalt ausschließlich
              zur Erkennung und Maskierung personenbezogener Daten und zur Ausgabe der verfremdeten Version.
            </p>
            <p class="mt-1 italic">
              Hinweis: In hochgeladenen Inhalten können auch personenbezogene Daten Dritter enthalten sein
              (z.&nbsp;B. Kund:innen, Mitarbeiter:innen). Bitte laden Sie nur Inhalte hoch, zu deren
              Verarbeitung Sie berechtigt sind.
            </p>
          </div>

          <div>
            <p class="font-medium">3.3 LocalStorage (technischer Session-Token)</p>
            <p>
              Zur technischen Bereitstellung des Dienstes wird ein Session-Token lokal im Browser
              (localStorage) gespeichert. Der Token enthält keine Klartextinhalte aus Ihren Uploads.
              Sie können ihn jederzeit durch Löschen Ihrer Browserdaten entfernen.
            </p>
          </div>
        </div>
      </section>

      <section>
        <h3 class="font-semibold mb-1">4. Rechtsgrundlagen</h3>
        <ul class="list-disc list-inside space-y-1">
          <li>Bereitstellung des Dienstes und Verarbeitung von Uploads: Art.&nbsp;6 Abs.&nbsp;1 lit.&nbsp;b DSGVO (Vertrag / vorvertragliche Maßnahmen)</li>
          <li>Betrieb, Sicherheit und Missbrauchs-/Fehlerprävention (Server-Logs): Art.&nbsp;6 Abs.&nbsp;1 lit.&nbsp;f DSGVO (berechtigtes Interesse an IT-Sicherheit und Stabilität)</li>
        </ul>
      </section>

      <section>
        <h3 class="font-semibold mb-1">5. Empfänger / Auftragsverarbeiter (Hosting)</h3>
        <p>
          Die Anwendung wird bei Hetzner Online GmbH (Deutschland) gehostet. Hetzner verarbeitet dabei
          personenbezogene Daten in unserem Auftrag (Auftragsverarbeiter). Es findet keine Übermittlung
          in Drittländer außerhalb EU/EWR statt.
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">6. Speicherdauer</h3>
        <ul class="list-disc list-inside space-y-1">
          <li>Upload-Inhalte: Speicherung nur so lange, wie für die Verarbeitung erforderlich, standardmäßig automatische Löschung nach 1&nbsp;Stunde.</li>
          <li>Server-Logs: Löschung nach 7&nbsp;Tagen.</li>
          <li>LocalStorage-Token: bis zur Löschung durch den Nutzer (Browserdaten) bzw. bis zur technischen Erneuerung/Überschreibung durch die Anwendung.</li>
        </ul>
      </section>

      <section>
        <h3 class="font-semibold mb-1">7. Keine Weitergabe / kein Tracking</h3>
        <p>
          Wir verwenden keine Tracking-Cookies und keine Analyse-Tools. Eine Weitergabe von
          Upload-Inhalten an Dritte erfolgt nicht; ausgenommen sind technische Dienstleister im
          Rahmen der Auftragsverarbeitung (z.&nbsp;B. Hosting).
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">8. Automatisierte Entscheidungen</h3>
        <p>Es erfolgt keine automatisierte Entscheidungsfindung im Sinne von Art.&nbsp;22 DSGVO.</p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">9. Ihre Rechte</h3>
        <p>
          Sie haben (je nach Voraussetzungen) das Recht auf Auskunft, Berichtigung, Löschung,
          Einschränkung der Verarbeitung, Datenübertragbarkeit sowie Widerspruch gegen Verarbeitungen,
          die auf Art.&nbsp;6 Abs.&nbsp;1 lit.&nbsp;f DSGVO beruhen.
        </p>
        <p class="mt-1">
          Anfragen richten Sie bitte an:
          <a href="mailto:mail@andreas-seiler.net" class="underline">mail@andreas-seiler.net</a>
        </p>
      </section>

      <section>
        <h3 class="font-semibold mb-1">10. Beschwerderecht</h3>
        <p>
          Sie haben das Recht, Beschwerde bei einer Datenschutzaufsichtsbehörde einzulegen.
          Zuständig in Österreich ist:
        </p>
        <p class="mt-1">
          Österreichische Datenschutzbehörde (DSB)<br />
          Barichgasse 40–42, 1030 Wien<br />
          E-Mail: <a href="mailto:dsb@dsb.gv.at" class="underline">dsb@dsb.gv.at</a>
        </p>
      </section>
    </div>
  </AppModal>
</template>
