<script setup lang="ts">
import { ref } from 'vue'
import AppLayout from '../layouts/AppLayout.vue'
import { exportDataBackup, exportFormattedWorkbook, importDataBackup } from '../services/dataBackup'
import { getModules } from '../services/modules'
import type { ModuleSummary } from '../types/modules'

const modules = ref<ModuleSummary[]>([])
const loading = ref(false)
const message = ref('')
const error = ref('')
const fileInput = ref<HTMLInputElement | null>(null)

async function loadModules() {
  modules.value = await getModules()
}

loadModules()

async function onExport() {
  error.value = ''
  message.value = ''
  loading.value = true
  try {
    const blob = await exportDataBackup()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `steelstone-erp-backup-${new Date().toISOString().slice(0, 10)}.xlsx`
    a.click()
    URL.revokeObjectURL(url)
    message.value = 'Backup downloaded successfully.'
  } catch {
    error.value = 'Export failed. Make sure you are logged in as Admin.'
  } finally {
    loading.value = false
  }
}

async function onExportFormatted() {
  error.value = ''
  message.value = ''
  loading.value = true
  try {
    const blob = await exportFormattedWorkbook()
    const url = URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = `steelstone-erp-forms-${new Date().toISOString().slice(0, 10)}.xlsx`
    a.click()
    URL.revokeObjectURL(url)
    message.value = 'Formatted Excel workbook downloaded (T00 index + T01–T15 module tabs).'
  } catch {
    error.value = 'Formatted export failed. Make sure you are logged in as Admin.'
  } finally {
    loading.value = false
  }
}

function pickImport() {
  fileInput.value?.click()
}

async function onFileSelected(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return

  if (!confirm('Import will replace app data (records, fields, lookups) from this backup. Continue?')) return

  error.value = ''
  message.value = ''
  loading.value = true
  try {
    const result = await importDataBackup(file)
    message.value = result.message
  } catch {
    error.value = 'Import failed. Use a backup file exported from this app (.xlsx).'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppLayout :modules="modules">
    <div class="page">
      <header class="page-header">
        <h1>Import / Export</h1>
        <p>
          Download ERP data as a technical backup, as a specification-style workbook (T00–T15 tabs), or restore
          from a backup file.
        </p>
      </header>

      <div class="cards">
        <section class="card">
          <h2>Export data</h2>
          <p>
            Full database backup: users, modules, field definitions, all ERP records (JSON),
            lookup lists, plus one sheet per module with readable columns.
          </p>
          <button type="button" class="btn primary" :disabled="loading" @click="onExport">
            {{ loading ? 'Working…' : 'Download Excel backup' }}
          </button>
        </section>

        <section class="card card-wide">
          <h2>Download formatted Excel (T00–T15)</h2>
          <p>
            Uses the same master Excel template as <strong>TEST DATE.xlsx</strong> (coloured headers, section bands,
            tab layout). Only your live data and entry counts are updated; formatting stays identical.
          </p>
          <button type="button" class="btn primary" :disabled="loading" @click="onExportFormatted">
            {{ loading ? 'Working…' : 'Download formatted workbook' }}
          </button>
        </section>

        <section class="card">
          <h2>Import data</h2>
          <p>
            Restores from a v2 backup: users, modules, fields, records, and lookup options.
            SQL server login is unchanged; app data tables are replaced from the file.
          </p>
          <input ref="fileInput" type="file" accept=".xlsx" class="hidden" @change="onFileSelected" />
          <button type="button" class="btn secondary" :disabled="loading" @click="pickImport">
            Choose backup file…
          </button>
        </section>
      </div>

      <p v-if="message" class="ok">{{ message }}</p>
      <p v-if="error" class="err">{{ error }}</p>
    </div>
  </AppLayout>
</template>

<style scoped>
.page {
  max-width: 880px;
  margin: 0 auto;
  padding: 1.5rem 1rem 3rem;
}

.page-header h1 {
  margin: 0 0 0.35rem;
  font-size: 1.5rem;
  color: #0f172a;
}

.page-header p {
  margin: 0 0 1.5rem;
  color: #64748b;
}

.cards {
  display: grid;
  gap: 1rem;
}

@media (min-width: 720px) {
  .cards {
    grid-template-columns: 1fr 1fr;
  }

  .card-wide {
    grid-column: 1 / -1;
  }
}

.card {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
}

.card h2 {
  margin: 0 0 0.5rem;
  font-size: 1.1rem;
}

.card p {
  margin: 0 0 1rem;
  color: #475569;
  font-size: 0.92rem;
  line-height: 1.45;
}

.btn {
  border: none;
  border-radius: 8px;
  padding: 0.65rem 1rem;
  font-weight: 600;
  cursor: pointer;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn.primary {
  background: linear-gradient(135deg, #ea580c, #dc2626);
  color: #fff;
}

.btn.secondary {
  background: #f1f5f9;
  color: #0f172a;
  border: 1px solid #cbd5e1;
}

.hidden {
  display: none;
}

.ok {
  margin-top: 1rem;
  color: #15803d;
}

.err {
  margin-top: 1rem;
  color: #dc2626;
}
</style>
