<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { ModuleDetail } from '../types/modules'
import DynamicForm from './DynamicForm.vue'
import {
  applyConditionalCascade,
  applyOwnerToDirector,
  applyRepeatingRowCascade,
  emptyEnterpriseState,
  fieldsForSection,
  getOwnerOptions,
  getSectionData,
  parseEnterpriseState,
  trackKey,
  type EnterpriseFormState,
  type RepeatingSectionData,
  type SectionData,
} from '../utils/enterpriseRules'
import { saveSection } from '../services/modules'

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
const formState = ref<EnterpriseFormState>(emptyEnterpriseState())
const saving = ref(false)
const error = ref('')
const branchRows = ref<RepeatingSectionData>([{}])

const currentSection = computed(() => sections.value.find((s) => s.num === activeSection.value))
const completedSections = computed(() => formState.value.completedSections)

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

const conditionalKey = computed(() => trackKey(flatValues.value))

const sectionFields = computed(() => {
  if (!currentSection.value) return []
  const all = fieldsForSection(props.module.fields, currentSection.value.num)
  if (currentSection.value.num === 3) {
    return all.filter((f) => f.ref !== '3.10')
  }
  return all
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
  formState.value = parseEnterpriseState(props.initialData, sections.value, props.module.fields)
  if (props.recordCode) formState.value.recordCode = props.recordCode
  const packed = props.initialData['__section_3_sites']
  if (packed) {
    try {
      branchRows.value = JSON.parse(packed)
    } catch {
      branchRows.value = [{}]
    }
  }
}

watch(() => props.initialData, loadState, { immediate: true, deep: true })

function onSectionFormUpdate(data: Record<string, string>) {
  const sectionNum = currentSection.value?.num ?? 1
  sectionFlatData.value = applyConditionalCascade(data, flatValues.value, sectionNum)
}

function onRepeatingRowUpdate(index: number, data: Record<string, string>) {
  const sectionNum = currentSection.value?.num ?? 0
  const rows = [...repeatingRows.value]
  rows[index] = applyRepeatingRowCascade(data, flatValues.value, sectionNum)
  repeatingRows.value = rows
}

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

function updateRepeatingField(index: number, ref: string, value: string) {
  const rows = [...repeatingRows.value]
  rows[index] = { ...rows[index], [ref]: value }

  if (currentSection.value?.num === 5 && ref === '5.1' && value.toLowerCase() === 'yes') {
    // user picks owner via 5.2 dropdown separately
  }
  if (currentSection.value?.num === 5 && ref === '5.2_ownerIndex') {
    const owners = getOwnerOptions(formState.value)
    const owner = owners.find((o) => o.value === value)
    if (owner) rows[index] = applyOwnerToDirector(rows[index], owner.row)
  }

  rows[index] = applyRepeatingRowCascade(rows[index], flatValues.value, currentSection.value?.num ?? 0)
  repeatingRows.value = rows
}

function ownerOptionsForRow() {
  return getOwnerOptions(formState.value)
}

async function saveCurrentSection() {
  if (!currentSection.value) return
  saving.value = true
  error.value = ''
  try {
    const sectionNum = currentSection.value.num
    let payload: { data?: Record<string, string>; rows?: Record<string, string>[] }

    if (currentSection.value.repeating) {
      payload = { rows: repeatingRows.value }
    } else {
      const data = { ...sectionFlatData.value }
      if (sectionNum === 3) {
        data['__section_3_sites'] = JSON.stringify(branchRows.value)
      }
      payload = { data }
    }

    const saved = await saveSection('01', props.recordId, sectionNum, payload)
    formState.value = parseEnterpriseState(saved.data, sections.value, props.module.fields)
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

function addBranchRow() {
  branchRows.value = [...branchRows.value, {}]
}

function removeBranchRow(index: number) {
  if (branchRows.value.length <= 1) return
  branchRows.value = branchRows.value.filter((_, i) => i !== index)
}
</script>

<template>
  <div class="enterprise-form">
    <div v-if="formState.recordCode" class="record-code-banner">
      Enterprise ID: <strong>{{ formState.recordCode }}</strong>
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
        <span class="num">{{ section.num }}</span>
        <span class="title">{{ section.title }}</span>
      </button>
    </nav>

    <div v-if="currentSection" class="section-panel">
      <header class="section-header">
        <h2>Section {{ currentSection.num }} — {{ currentSection.title }}</h2>
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

          <div v-if="currentSection.num === 5" class="owner-link">
            <label>
              Is this director also an owner from Section 4?
              <select
                :value="row['5.1'] ?? ''"
                @change="updateRepeatingField(index, '5.1', ($event.target as HTMLSelectElement).value)"
              >
                <option value="">— Select —</option>
                <option value="Yes">Yes</option>
                <option value="No">No</option>
              </select>
            </label>
            <label v-if="(row['5.1'] ?? '').toLowerCase() === 'yes'">
              Select owner to auto-fill
              <select
                :value="row['5.2_ownerIndex'] ?? ''"
                @change="updateRepeatingField(index, '5.2_ownerIndex', ($event.target as HTMLSelectElement).value)"
              >
                <option value="">— Select owner —</option>
                <option v-for="owner in ownerOptionsForRow()" :key="owner.value" :value="owner.value">
                  {{ owner.label }}
                </option>
              </select>
            </label>
          </div>

          <DynamicForm
            :key="`repeat-${index}-${conditionalKey}`"
            :fields="sectionFields"
            :model-value="row"
            module-code="01"
            :flat-context="flatValues"
            hide-section-headers
            @update:model-value="(val) => onRepeatingRowUpdate(index, val)"
          />
        </div>
        <button type="button" class="btn-add" @click="addRepeatingRow">+ Add another entry</button>
      </template>

      <template v-else>
        <DynamicForm
          :key="`section-${activeSection}-${conditionalKey}`"
          :fields="sectionFields"
          :model-value="sectionFlatData"
          module-code="01"
          :flat-context="flatValues"
          hide-section-headers
          @update:model-value="onSectionFormUpdate"
        />

        <div v-if="currentSection.num === 3" class="repeat-card">
          <div class="repeat-card-head">
            <strong>Branch / factory / warehouse address(es)</strong>
          </div>
          <div v-for="(row, index) in branchRows" :key="'b' + index" class="branch-row">
            <input
              placeholder="Site type"
              :value="row.siteType ?? ''"
              @input="branchRows[index] = { ...row, siteType: ($event.target as HTMLInputElement).value }"
            />
            <input
              placeholder="Address"
              :value="row.address ?? ''"
              @input="branchRows[index] = { ...row, address: ($event.target as HTMLInputElement).value }"
            />
            <input
              placeholder="City"
              :value="row.city ?? ''"
              @input="branchRows[index] = { ...row, city: ($event.target as HTMLInputElement).value }"
            />
            <input
              placeholder="Province"
              :value="row.province ?? ''"
              @input="branchRows[index] = { ...row, province: ($event.target as HTMLInputElement).value }"
            />
            <button v-if="branchRows.length > 1" type="button" class="btn-remove" @click="removeBranchRow(index)">
              Remove
            </button>
          </div>
          <button type="button" class="btn-add" @click="addBranchRow">+ Add site</button>
        </div>
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
.enterprise-form {
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

.owner-link {
  display: grid;
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.owner-link label {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  font-size: 0.85rem;
}

.owner-link select {
  min-height: 44px;
  padding: 0.5rem;
}

.branch-row {
  display: grid;
  gap: 0.5rem;
  margin-bottom: 0.75rem;
}

.branch-row input {
  min-height: 44px;
  padding: 0.5rem 0.75rem;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
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

@media (min-width: 768px) {
  .branch-row {
    grid-template-columns: 1fr 2fr 1fr 1fr auto;
    align-items: center;
  }
}
</style>
