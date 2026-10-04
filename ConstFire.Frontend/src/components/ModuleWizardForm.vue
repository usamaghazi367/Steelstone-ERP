<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ModuleDetail } from '../types/modules'
import DynamicForm from './DynamicForm.vue'
import {
  fieldsForSection,
  getSectionData,
  parseModuleState,
  type ModuleFormState,
  type RepeatingSectionData,
  type SectionData,
} from '../utils/moduleWizardRules'
import { saveSection } from '../services/modules'
import {
  applyModuleConditionalCascade,
  applyModuleRepeatingRowCascade,
} from '../utils/moduleFieldRules'

const props = defineProps<{
  module: ModuleDetail
  recordId: number
  recordCode?: string
  initialData: Record<string, string>
  initialSection?: number
}>()

const emit = defineEmits<{
  saved: [sectionNum: number]
  complete: []
}>()

const sections = computed(() => props.module.sections ?? [])
const activeSection = ref(props.initialSection ?? sections.value[0]?.num ?? 1)
const formState = ref<ModuleFormState>({ completedSections: [], sections: {} })
const saving = ref(false)
const error = ref('')

const currentSection = computed(() => sections.value.find((s) => s.num === activeSection.value))
const completedSections = computed(() => formState.value.completedSections)

const sectionFields = computed(() => {
  if (!currentSection.value) return []
  return fieldsForSection(props.module.fields, currentSection.value.num)
})

const flatValues = computed(() => {
  const flat: Record<string, string> = {}
  for (const [, value] of Object.entries(formState.value.sections)) {
    if (Array.isArray(value)) {
      value.forEach((row, index) => {
        for (const [ref, val] of Object.entries(row)) flat[`${ref}#${index}`] = val
      })
    } else {
      Object.assign(flat, value)
    }
  }
  return flat
})

const repeatingRows = computed<RepeatingSectionData>({
  get() {
    if (!currentSection.value?.repeating) return [{}]
    return getSectionData(formState.value, currentSection.value.num, true) as RepeatingSectionData
  },
  set(rows) {
    if (!currentSection.value) return
    formState.value.sections[String(currentSection.value.num)] = rows
  },
})

const sectionFlatData = computed<SectionData>({
  get() {
    if (!currentSection.value || currentSection.value.repeating) return {}
    return getSectionData(formState.value, currentSection.value.num, false) as SectionData
  },
  set(data) {
    if (!currentSection.value) return
    formState.value.sections[String(currentSection.value.num)] = data
  },
})

function loadState() {
  formState.value = parseModuleState(props.initialData, sections.value, props.module.fields)
  if (props.recordCode) formState.value.recordCode = props.recordCode
}

watch(() => props.initialData, loadState, { immediate: true, deep: true })

function goSection(num: number) {
  activeSection.value = num
}

function addRepeatingRow() {
  repeatingRows.value = [...repeatingRows.value, {}]
}

function removeRepeatingRow(index: number) {
  if (repeatingRows.value.length <= 1) return
  repeatingRows.value = repeatingRows.value.filter((_, i) => i !== index)
}

function rowHasContent(row: Record<string, string>) {
  return Object.values(row).some((v) => String(v ?? '').trim().length > 0)
}

const savedRepeatingTableRows = computed(() => {
  if (!currentSection.value?.repeating) return [] as Record<string, string>[]
  return repeatingRows.value.filter(rowHasContent)
})

function sanitizeRepeatingRows(rows: RepeatingSectionData): RepeatingSectionData {
  const filled = rows.filter(rowHasContent)
  return filled.length > 0 ? filled : [{}]
}

function onSectionFormUpdate(data: Record<string, string>) {
  const sectionNum = currentSection.value?.num ?? 1
  sectionFlatData.value = applyModuleConditionalCascade(
    props.module.code,
    data,
    flatValues.value,
    sectionNum,
    props.module.fields,
  )
}

function onRepeatingRowUpdate(index: number, data: Record<string, string>) {
  const sectionNum = currentSection.value?.num ?? 0
  const rows = [...repeatingRows.value]
  rows[index] = applyModuleRepeatingRowCascade(
    props.module.code,
    data,
    flatValues.value,
    sectionNum,
    props.module.fields,
  )
  repeatingRows.value = rows
}

async function saveCurrentSection() {
  if (!currentSection.value) return
  saving.value = true
  error.value = ''
  try {
    const sectionNum = currentSection.value.num
    const payload = currentSection.value.repeating
      ? { rows: sanitizeRepeatingRows(repeatingRows.value) }
      : { data: { ...sectionFlatData.value } }

    const saved = await saveSection(props.module.code, props.recordId, sectionNum, payload)
    formState.value = parseModuleState(saved.data, sections.value, props.module.fields)
    formState.value.recordCode = saved.recordCode
    if (!completedSections.value.includes(sectionNum)) {
      formState.value.completedSections = [...completedSections.value, sectionNum]
    }
    emit('saved', sectionNum)

    const next = sections.value.find((s) => s.num > sectionNum)
    if (next) activeSection.value = next.num
    else emit('complete')
  } catch (e: unknown) {
    const err = e as { response?: { data?: { message?: string } } }
    error.value = err.response?.data?.message ?? 'Failed to save section.'
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <div class="module-wizard">
    <div v-if="formState.recordCode" class="record-code-banner">
      Record ID: <strong>{{ formState.recordCode }}</strong>
      <span class="auto-note">(assigned automatically)</span>
    </div>

    <nav class="section-nav">
      <button
        v-for="section in sections"
        :key="section.num"
        type="button"
        class="section-tab"
        :class="{
          active: activeSection === section.num,
          done: completedSections.includes(section.num),
        }"
        @click="goSection(section.num)"
      >
        <span class="title">{{ section.title }}</span>
      </button>
    </nav>

    <div v-if="currentSection" class="section-panel">
      <header class="section-header">
        <h2>{{ currentSection.title }}</h2>
        <p v-if="currentSection.repeating">Add one or more entries in this section.</p>
      </header>

      <template v-if="currentSection.repeating">
        <div v-for="(row, index) in repeatingRows" :key="index" class="repeat-card">
          <div class="repeat-card-head">
            <strong>Entry {{ index + 1 }}</strong>
            <button
              v-if="repeatingRows.length > 1"
              type="button"
              class="btn-remove"
              @click="removeRepeatingRow(index)"
            >
              Remove
            </button>
          </div>
          <DynamicForm
            :fields="sectionFields"
            :all-fields="module.fields"
            :model-value="row"
            :module-code="module.code"
            :flat-context="flatValues"
            hide-section-headers
            @update:model-value="(val) => onRepeatingRowUpdate(index, val)"
          />
        </div>
        <button type="button" class="btn-add" @click="addRepeatingRow">+ Add another entry</button>

        <div v-if="savedRepeatingTableRows.length" class="saved-entries">
          <h3>Saved entries in this section ({{ savedRepeatingTableRows.length }})</h3>
          <div class="saved-table-wrap">
            <table class="saved-table">
              <thead>
                <tr>
                  <th>#</th>
                  <th v-for="field in sectionFields" :key="field.ref">{{ field.fieldName }}</th>
                </tr>
              </thead>
              <tbody>
                <tr v-for="(row, index) in savedRepeatingTableRows" :key="index">
                  <td>{{ index + 1 }}</td>
                  <td v-for="field in sectionFields" :key="field.ref">
                    {{ row[field.ref] || '—' }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </template>

      <template v-else>
        <DynamicForm
          :fields="sectionFields"
          :all-fields="module.fields"
          :model-value="sectionFlatData"
          :module-code="module.code"
          :flat-context="flatValues"
          hide-section-headers
          @update:model-value="onSectionFormUpdate"
        />
      </template>

      <p v-if="error" class="error">{{ error }}</p>

      <div class="section-actions">
        <button type="button" class="btn-secondary" :disabled="activeSection <= 1" @click="goSection(activeSection - 1)">
          Previous section
        </button>
        <button type="button" class="btn-primary" :disabled="saving" @click="saveCurrentSection">
          {{ saving ? 'Saving…' : 'Save section & continue' }}
        </button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.module-wizard {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.record-code-banner {
  background: #fff7ed;
  border: 1px solid #fed7aa;
  border-radius: 8px;
  padding: 0.75rem 1rem;
  font-size: 0.9rem;
}

.auto-note {
  color: #64748b;
  font-size: 0.8rem;
  margin-left: 0.5rem;
}

.section-nav {
  display: flex;
  gap: 0.5rem;
  overflow-x: auto;
  padding-bottom: 0.25rem;
}

.section-tab {
  min-width: 180px;
  text-align: left;
  padding: 0.65rem 0.75rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #fff;
  cursor: pointer;
}

.section-tab.active {
  border-color: #ea580c;
  background: #fff7ed;
}

.section-tab.done .num {
  background: #16a34a;
}

.section-tab .num {
  display: inline-flex;
  width: 1.5rem;
  height: 1.5rem;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: #64748b;
  color: #fff;
  font-size: 0.75rem;
  margin-right: 0.35rem;
}

.section-tab .title {
  display: block;
  font-size: 0.72rem;
  color: #475569;
  margin-top: 0.25rem;
  line-height: 1.3;
}

.section-panel {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 1.25rem;
}

.section-header h2 {
  margin: 0 0 0.35rem;
  font-size: 1rem;
  color: #0f172a;
}

.section-header p {
  margin: 0 0 1rem;
  color: #64748b;
  font-size: 0.85rem;
}

.repeat-card {
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1rem;
  margin-bottom: 0.75rem;
  background: #f8fafc;
}

.repeat-card-head {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 0.75rem;
}

.btn-add,
.btn-remove,
.btn-primary,
.btn-secondary {
  min-height: 44px;
  padding: 0.55rem 1rem;
  border-radius: 8px;
  cursor: pointer;
}

.btn-add {
  border: 1px dashed #ea580c;
  background: #fff;
  color: #ea580c;
  width: 100%;
}

.btn-remove {
  border: 1px solid #fecaca;
  background: #fff;
  color: #dc2626;
}

.section-actions {
  display: flex;
  gap: 0.75rem;
  margin-top: 1rem;
  flex-wrap: wrap;
}

.btn-primary {
  background: #ea580c;
  color: #fff;
  border: none;
  flex: 1;
}

.btn-secondary {
  background: #fff;
  border: 1px solid #e2e8f0;
}

.error {
  color: #dc2626;
  background: #fef2f2;
  border: 1px solid #fecaca;
  padding: 0.75rem;
  border-radius: 8px;
}

.saved-entries {
  margin-top: 1rem;
  border: 1px solid #cbd5e1;
  border-radius: 10px;
  padding: 0.75rem;
  background: #fff;
}

.saved-entries h3 {
  margin: 0 0 0.5rem;
  font-size: 0.9rem;
  color: #0f172a;
}

.saved-table-wrap {
  overflow-x: auto;
}

.saved-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.8rem;
}

.saved-table th,
.saved-table td {
  border: 1px solid #e2e8f0;
  padding: 0.4rem 0.5rem;
  text-align: left;
  vertical-align: top;
}

.saved-table th {
  background: #f1f5f9;
  font-weight: 600;
}
</style>
