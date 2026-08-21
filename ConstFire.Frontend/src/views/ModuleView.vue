<script setup lang="ts">
import { ref, watch, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import AppLayout from '../layouts/AppLayout.vue'
import DynamicForm from '../components/DynamicForm.vue'
import EnterpriseRegistrationForm from '../components/EnterpriseRegistrationForm.vue'
import ModuleWizardForm from '../components/ModuleWizardForm.vue'
import {
  getModules,
  getModule,
  getRecords,
  getRecord,
  createRecord,
  updateRecord,
  deleteRecord,
} from '../services/modules'
import type { ModuleDetail, ModuleSummary, RecordItem } from '../types/modules'

const route = useRoute()
const router = useRouter()

const modules = ref<ModuleSummary[]>([])
const moduleDetail = ref<ModuleDetail | null>(null)
const records = ref<RecordItem[]>([])
const totalCount = ref(0)
const loading = ref(false)
const error = ref('')
const search = ref('')
const sortBy = ref('id')
const sortDir = ref<'asc' | 'desc'>('asc')
const page = ref(1)
const pageSize = 25

const formData = ref<Record<string, string>>({})
const saving = ref(false)
const deleteTarget = ref<RecordItem | null>(null)
const deleting = ref(false)
const wizardRecordId = ref<number | null>(null)
const wizardRecordCode = ref<string | undefined>()
const creatingWizardDraft = ref(false)

const hasSectionWizard = computed(() => (moduleDetail.value?.sections?.length ?? 0) > 0)
const isEnterpriseModule = computed(() => code.value === '01')
const showWizard = computed(
  () => hasSectionWizard.value && (action.value === 'add' || action.value === 'edit') && wizardRecordId.value,
)

const code = computed(() => route.params.code as string)
const action = computed(() => (route.query.action as string) || 'view')
const editId = computed(() => (route.query.id ? Number(route.query.id) : null))

const fieldLabelMap = computed(() => {
  const map: Record<string, string> = {}
  moduleDetail.value?.fields.forEach((f) => {
    map[f.ref] = f.fieldName
  })
  return map
})

const listColumns = computed(() => {
  const cols = moduleDetail.value?.listColumns ?? []
  return cols.map((ref) => ({
    ref,
    label: ref === '_recordCode' ? (code.value === '01' ? 'Enterprise ID' : 'Record ID') : (fieldLabelMap.value[ref] ?? ref),
  }))
})

const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / pageSize)))

onMounted(async () => {
  modules.value = await getModules()
  if (!route.params.code && modules.value.length > 0) {
    router.replace({ name: 'module', params: { code: modules.value[0].code }, query: { action: 'view' } })
  }
})

watch(
  () => [route.params.code, route.query.action, route.query.id],
  () => loadPage(),
  { immediate: true },
)

async function loadPage() {
  if (!code.value) return
  loading.value = true
  error.value = ''
  try {
    moduleDetail.value = await getModule(code.value)
    if (action.value === 'view') {
      await loadRecords()
    } else if (action.value === 'add') {
      formData.value = {}
      wizardRecordId.value = null
      wizardRecordCode.value = undefined
      if (hasSectionWizard.value) {
        creatingWizardDraft.value = true
        try {
          const draft = await createRecord(code.value, {})
          wizardRecordId.value = draft.id
          wizardRecordCode.value = draft.recordCode
          formData.value = { ...draft.data }
          router.replace({
            name: 'module',
            params: { code: code.value },
            query: { action: 'edit', id: String(draft.id), section: '1' },
          })
        } finally {
          creatingWizardDraft.value = false
        }
      }
    } else if (action.value === 'edit' && editId.value) {
      const record = await getRecord(code.value, editId.value)
      formData.value = { ...record.data }
      if (hasSectionWizard.value) {
        wizardRecordId.value = record.id
        wizardRecordCode.value = record.recordCode
      }
    }
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    error.value = err.response?.data?.message ?? 'Failed to load module.'
  } finally {
    loading.value = false
  }
}

async function loadRecords() {
  const result = await getRecords(code.value, {
    search: search.value || undefined,
    sortBy: sortBy.value,
    sortDir: sortDir.value,
    page: page.value,
    pageSize,
  })
  records.value = result.items
  totalCount.value = result.totalCount
}

function setAction(newAction: string, id?: number) {
  const query: Record<string, string> = { action: newAction }
  if (id) query.id = String(id)
  router.push({ name: 'module', params: { code: code.value }, query })
}

function toggleSort(column: string) {
  if (sortBy.value === column) {
    sortDir.value = sortDir.value === 'asc' ? 'desc' : 'asc'
  } else {
    sortBy.value = column
    sortDir.value = 'asc'
  }
  page.value = 1
  loadRecords()
}

async function onSearch() {
  page.value = 1
  await loadRecords()
}

async function saveRecord() {
  saving.value = true
  error.value = ''
  try {
    if (action.value === 'add') {
      await createRecord(code.value, formData.value)
    } else if (action.value === 'edit' && editId.value) {
      await updateRecord(code.value, editId.value, formData.value)
    }
    setAction('view')
    await loadRecords()
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    error.value = err.response?.data?.message ?? 'Save failed.'
  } finally {
    saving.value = false
  }
}

function confirmDelete(record: RecordItem) {
  deleteTarget.value = record
  setAction('delete', record.id)
}

async function performDelete() {
  if (!deleteTarget.value) return
  deleting.value = true
  error.value = ''
  try {
    await deleteRecord(code.value, deleteTarget.value.id)
    deleteTarget.value = null
    setAction('view')
    await loadRecords()
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    error.value = err.response?.data?.message ?? 'Delete failed.'
  } finally {
    deleting.value = false
  }
}

function formatDate(value: string) {
  return new Date(value).toLocaleString()
}

function sortIcon(column: string) {
  if (sortBy.value !== column) return '↕'
  return sortDir.value === 'asc' ? '↑' : '↓'
}
</script>

<template>
  <AppLayout :modules="modules">
    <template #title>
      <div v-if="moduleDetail" class="page-title">
        <span class="page-code">{{ moduleDetail.code }}</span>
        <div>
          <h1>{{ moduleDetail.name }}</h1>
          <p>{{ moduleDetail.category }}</p>
        </div>
      </div>
    </template>

    <div v-if="loading && !moduleDetail" class="loading">Loading…</div>

    <template v-else-if="moduleDetail">
      <nav class="action-bar">
        <button :class="{ active: action === 'view' }" @click="setAction('view')">View Data</button>
        <button :class="{ active: action === 'add' }" @click="setAction('add')">Add Data</button>
        <button
          :class="{ active: action === 'edit' }"
          :disabled="!editId && action !== 'edit'"
          @click="editId && setAction('edit', editId)"
        >
          Edit Data
        </button>
        <button
          :class="{ active: action === 'delete' }"
          :disabled="!deleteTarget && action !== 'delete'"
        >
          Delete Data
        </button>
      </nav>

      <p v-if="error" class="error">{{ error }}</p>

      <!-- VIEW -->
      <div v-if="action === 'view'" class="panel">
        <div class="toolbar">
          <input
            v-model="search"
            type="search"
            placeholder="Search records…"
            class="search-input"
            @keyup.enter="onSearch"
          />
          <button class="btn-secondary" @click="onSearch">Search</button>
          <span class="count">{{ totalCount }} record(s)</span>
        </div>

        <div v-if="records.length === 0" class="empty-state">No records found.</div>

        <div v-else class="record-cards">
          <article v-for="row in records" :key="'card-' + row.id" class="record-card">
            <div class="record-card-head">
              <span class="record-id">#{{ row.id }}</span>
              <span class="record-date">{{ formatDate(row.createdAt) }}</span>
            </div>
            <dl class="record-fields">
              <div v-for="col in listColumns" :key="col.ref" class="record-field">
                <dt>{{ col.label }}</dt>
                <dd>{{ row.data[col.ref] || '—' }}</dd>
              </div>
            </dl>
            <div class="record-card-actions">
              <button type="button" @click="setAction('edit', row.id)">Edit</button>
              <button type="button" class="danger" @click="confirmDelete(row)">Delete</button>
            </div>
          </article>
        </div>

        <div class="table-wrap desktop-table">
          <table>
            <thead>
              <tr>
                <th class="sortable" @click="toggleSort('id')">ID {{ sortIcon('id') }}</th>
                <th
                  v-for="col in listColumns"
                  :key="col.ref"
                  class="sortable"
                  @click="toggleSort(col.ref)"
                >
                  {{ col.label }} {{ sortIcon(col.ref) }}
                </th>
                <th class="sortable" @click="toggleSort('createdAt')">
                  Created {{ sortIcon('createdAt') }}
                </th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in records" :key="row.id">
                <td>{{ row.id }}</td>
                <td v-for="col in listColumns" :key="col.ref">{{ row.data[col.ref] || '—' }}</td>
                <td>{{ formatDate(row.createdAt) }}</td>
                <td class="actions">
                  <button type="button" @click="setAction('edit', row.id)">Edit</button>
                  <button type="button" class="danger" @click="confirmDelete(row)">Delete</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <div v-if="totalPages > 1" class="pagination">
          <button :disabled="page <= 1" @click="page--; loadRecords()">Previous</button>
          <span>Page {{ page }} of {{ totalPages }}</span>
          <button :disabled="page >= totalPages" @click="page++; loadRecords()">Next</button>
        </div>
      </div>

      <!-- SECTION WIZARD (all modules with sections) -->
      <div v-else-if="showWizard && moduleDetail" class="panel enterprise-panel">
        <div v-if="creatingWizardDraft" class="loading">Creating registration draft…</div>
        <EnterpriseRegistrationForm
          v-else-if="isEnterpriseModule"
          :module="moduleDetail"
          :record-id="wizardRecordId!"
          :record-code="wizardRecordCode"
          :initial-data="formData"
          :initial-section="Number(route.query.section) || 1"
          @complete="setAction('view')"
        />
        <ModuleWizardForm
          v-else
          :module="moduleDetail"
          :record-id="wizardRecordId!"
          :record-code="wizardRecordCode"
          :initial-data="formData"
          :initial-section="Number(route.query.section) || 1"
          @complete="setAction('view')"
        />
      </div>

      <!-- ADD / EDIT (other modules) -->
      <div v-else-if="action === 'add' || action === 'edit'" class="panel">
        <div class="form-header">
          <h2>{{ action === 'add' ? 'Add New Record' : `Edit Record #${editId}` }}</h2>
          <div class="form-actions">
            <button class="btn-secondary" @click="setAction('view')">Cancel</button>
            <button class="btn-primary" :disabled="saving" @click="saveRecord">
              {{ saving ? 'Saving…' : 'Save' }}
            </button>
          </div>
        </div>
        <DynamicForm v-model="formData" :fields="moduleDetail.fields" :module-code="code" />
      </div>

      <!-- DELETE -->
      <div v-else-if="action === 'delete' && deleteTarget" class="panel delete-panel">
        <h2>Delete Record #{{ deleteTarget.id }}?</h2>
        <p>This action cannot be undone.</p>
        <dl>
          <template v-for="col in listColumns" :key="col.ref">
            <dt>{{ col.label }}</dt>
            <dd>{{ deleteTarget.data[col.ref] || '—' }}</dd>
          </template>
        </dl>
        <div class="form-actions">
          <button class="btn-secondary" @click="setAction('view')">Cancel</button>
          <button class="btn-danger" :disabled="deleting" @click="performDelete">
            {{ deleting ? 'Deleting…' : 'Confirm Delete' }}
          </button>
        </div>
      </div>

      <div v-else-if="action === 'edit'" class="panel hint-panel">
        Select a record from <strong>View Data</strong> and click Edit, or use the row action buttons.
      </div>

      <div v-else-if="action === 'delete'" class="panel hint-panel">
        Select a record from <strong>View Data</strong> and click Delete.
      </div>
    </template>
  </AppLayout>
</template>

<style scoped>
.page-title {
  display: flex;
  align-items: center;
  gap: 0.875rem;
}

.page-code {
  background: #ea580c;
  color: #fff;
  font-weight: 700;
  font-size: 0.85rem;
  padding: 0.35rem 0.6rem;
  border-radius: 6px;
}

.page-title h1 {
  margin: 0;
  font-size: 1.1rem;
  color: #0f172a;
}

.page-title p {
  margin: 0;
  font-size: 0.8rem;
  color: #64748b;
}

.action-bar {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 1rem;
  flex-wrap: nowrap;
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
  padding-bottom: 0.25rem;
}

.action-bar button {
  padding: 0.65rem 1rem;
  min-height: 44px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #fff;
  color: #475569;
  font-size: 0.875rem;
  font-weight: 500;
  cursor: pointer;
  white-space: nowrap;
  flex-shrink: 0;
}

.action-bar button.active {
  background: #ea580c;
  border-color: #ea580c;
  color: #fff;
}

.action-bar button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.panel {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
}

.toolbar {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 1rem;
  flex-wrap: wrap;
}

.search-input {
  flex: 1 1 100%;
  min-width: 0;
  padding: 0.65rem 0.875rem;
  min-height: 44px;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  font-size: 1rem;
}

.count {
  font-size: 0.8rem;
  color: #64748b;
  width: 100%;
}

@media (min-width: 640px) {
  .search-input {
    flex: 1;
    min-width: 200px;
    font-size: 0.875rem;
  }

  .count {
    width: auto;
    margin-left: auto;
  }
}

.record-cards {
  display: none;
}

.desktop-table {
  display: block;
}

.table-wrap {
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.record-card {
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1rem;
  margin-bottom: 0.75rem;
  background: #f8fafc;
}

.record-card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.record-id {
  font-weight: 700;
  color: #ea580c;
}

.record-date {
  font-size: 0.75rem;
  color: #64748b;
}

.record-fields {
  margin: 0;
}

.record-field {
  margin-bottom: 0.5rem;
}

.record-field dt {
  font-size: 0.7rem;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: #64748b;
  margin-bottom: 0.15rem;
}

.record-field dd {
  margin: 0;
  font-size: 0.9rem;
  color: #0f172a;
  word-break: break-word;
}

.record-card-actions {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.record-card-actions button {
  flex: 1;
  min-height: 44px;
  padding: 0.5rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #fff;
  font-size: 0.875rem;
  cursor: pointer;
}

.record-card-actions button.danger {
  color: #dc2626;
  border-color: #fecaca;
}

.empty-state {
  text-align: center;
  color: #94a3b8;
  padding: 2rem 1rem;
}

@media (max-width: 768px) {
  .record-cards {
    display: block;
  }

  .desktop-table {
    display: none;
  }

  .panel {
    padding: 1rem;
    border-radius: 10px;
  }

  .page-title h1 {
    font-size: 1rem;
  }

  .page-title p {
    display: none;
  }

  .delete-panel dl {
    grid-template-columns: 1fr;
  }

  .form-header {
    flex-direction: column;
    align-items: stretch;
  }

  .form-actions {
    width: 100%;
  }

  .form-actions button {
    flex: 1;
    min-height: 44px;
  }

  .btn-primary,
  .btn-secondary,
  .btn-danger {
    min-height: 44px;
  }

  .pagination button {
    min-height: 44px;
    min-width: 44px;
  }
}

table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.85rem;
}

th,
td {
  padding: 0.65rem 0.75rem;
  text-align: left;
  border-bottom: 1px solid #f1f5f9;
}

th {
  background: #f8fafc;
  color: #475569;
  font-weight: 600;
  white-space: nowrap;
}

.sortable {
  cursor: pointer;
  user-select: none;
}

.sortable:hover {
  background: #f1f5f9;
}

.actions {
  white-space: nowrap;
}

.actions button {
  padding: 0.3rem 0.6rem;
  margin-right: 0.35rem;
  border: 1px solid #e2e8f0;
  border-radius: 5px;
  background: #fff;
  font-size: 0.75rem;
  cursor: pointer;
}

.actions button.danger {
  color: #dc2626;
  border-color: #fecaca;
}

.empty {
  text-align: center;
  color: #94a3b8;
  padding: 2rem !important;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
  margin-top: 1rem;
}

.pagination button {
  padding: 0.4rem 0.875rem;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  background: #fff;
  cursor: pointer;
}

.pagination button:disabled {
  opacity: 0.4;
  cursor: not-allowed;
}

.form-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1.25rem;
  flex-wrap: wrap;
  gap: 0.75rem;
}

.form-header h2 {
  margin: 0;
  font-size: 1rem;
  color: #334155;
}

.form-actions {
  display: flex;
  gap: 0.5rem;
}

.btn-primary {
  padding: 0.55rem 1.25rem;
  background: #ea580c;
  color: #fff;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
}

.btn-secondary {
  padding: 0.55rem 1rem;
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  cursor: pointer;
}

.btn-danger {
  padding: 0.55rem 1.25rem;
  background: #dc2626;
  color: #fff;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
}

.delete-panel dl {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 0.5rem 1rem;
  margin: 1rem 0;
}

.delete-panel dt {
  color: #64748b;
  font-size: 0.85rem;
}

.delete-panel dd {
  margin: 0;
  font-weight: 500;
}

.hint-panel {
  color: #64748b;
  text-align: center;
  padding: 2rem;
}

.error {
  color: #dc2626;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem 1rem;
  border-radius: 8px;
  margin-bottom: 1rem;
  font-size: 0.875rem;
}

.loading {
  text-align: center;
  padding: 3rem;
  color: #64748b;
}
</style>
