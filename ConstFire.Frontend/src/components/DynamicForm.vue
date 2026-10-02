<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, watch } from 'vue'
import type { ModuleField } from '../types/modules'
import SearchableMultiSelect from './SearchableMultiSelect.vue'
import SearchableSelect from './SearchableSelect.vue'
import FileUploadField from './FileUploadField.vue'
import { getFieldOptions } from '../services/modules'
import {
  getStaticFieldOptions,
  isBankNameField,
  isDropdownField,
  isFileUploadField,
  isLookupField,
  isMultiSelectField,
  isSearchableSelectField,
  isYesNoField,
  resolveParentValue,
} from '../utils/fieldOptions'
import { findEntityGatingRef, isFieldVisibleForModule } from '../utils/moduleFieldRules'

const props = withDefaults(
  defineProps<{
    fields: ModuleField[]
    modelValue: Record<string, string>
    moduleCode: string
    flatContext?: Record<string, string>
    hideSectionHeaders?: boolean
    allFields?: ModuleField[]
  }>(),
  {
    flatContext: () => ({}),
    hideSectionHeaders: false,
    allFields: undefined,
  },
)

const emit = defineEmits<{ 'update:modelValue': [Record<string, string>] }>()

const lookupOptions = reactive<Record<string, string[]>>({})
const lookupLoading = reactive<Record<string, boolean>>({})
const lookupTimers = reactive<Record<string, number>>({})

const contextValues = computed(() => ({ ...props.flatContext, ...props.modelValue }))

const ruleFields = computed(() => props.allFields ?? props.fields)

const editableFields = computed(() =>
  props.fields.filter(
    (f) =>
      !f.dataType.toLowerCase().includes('read only') &&
      !f.dataType.toLowerCase().includes('formula') &&
      !f.dataType.toLowerCase().includes('calculated') &&
      isFieldVisibleForModule(props.moduleCode, f.ref, contextValues.value, ruleFields.value),
  ),
)

const sections = computed(() => {
  const map = new Map<string, ModuleField[]>()
  for (const field of editableFields.value) {
    const section = field.ref.split('.')[0] ?? '0'
    if (!map.has(section)) map.set(section, [])
    map.get(section)!.push(field)
  }
  return [...map.entries()].sort((a, b) => Number(a[0]) - Number(b[0]))
})

function updateField(ref: string, value: string) {
  emit('update:modelValue', { ...props.modelValue, [ref]: value })
}

function inputType(dataType: string) {
  const dt = dataType.toLowerCase()
  if (dt.includes('date')) return 'date'
  if (dt.includes('number') || dt.includes('numeric') || dt.includes('amount')) return 'number'
  if (dt.includes('email')) return 'email'
  if (dt.includes('phone')) return 'tel'
  return 'text'
}

function isTextArea(dataType: string) {
  return dataType.toLowerCase().includes('text (') && parseInt(dataType.match(/\((\d+)\)/)?.[1] ?? '0') > 100
}

function staticOptions(field: ModuleField): string[] {
  return getStaticFieldOptions(field.dataType, field.validation, contextValues.value, field.ref, ruleFields.value)
}

function fieldInputId(field: ModuleField): string {
  return `field-${field.ref.replace('.', '-')}`
}

function fileMaxSizeMb(validation: string): number {
  const match = validation.match(/max\s+(\d+)\s*mb/i)
  return match ? Number(match[1]) : 10
}

function fileAccept(validation: string): string {
  if (/pdf only|\.pdf|pdf,/i.test(validation)) return '.pdf,application/pdf'
  if (/pdf/i.test(validation)) return '.pdf,application/pdf'
  return '.pdf,.jpg,.jpeg,.png,.doc,.docx'
}

function uploadButtonLabel(field: ModuleField): string {
  if (/pdf/i.test(field.validation)) return 'Upload PDF'
  return 'Choose file'
}

const formRenderKey = computed(() => 'stable')

async function loadLookupOptions(field: ModuleField, search = '') {
  lookupLoading[field.ref] = true
  try {
    const parentValue = resolveParentValue(field.validation, contextValues.value, findEntityGatingRef(ruleFields.value))
    const response = await getFieldOptions(props.moduleCode, field.ref, {
      search: search || undefined,
      parentValue: parentValue || undefined,
      limit: 100,
    })
    lookupOptions[field.ref] = response.options.map((option) => option.label)
  } catch {
    lookupOptions[field.ref] = []
  } finally {
    lookupLoading[field.ref] = false
  }
}

function scheduleLookupLoad(field: ModuleField, search = '') {
  if (lookupTimers[field.ref]) {
    window.clearTimeout(lookupTimers[field.ref])
  }
  lookupTimers[field.ref] = window.setTimeout(() => {
    loadLookupOptions(field, search)
  }, 250)
}

function onLookupFocus(field: ModuleField) {
  loadLookupOptions(field)
}

function onLookupSearch(field: ModuleField, search: string) {
  scheduleLookupLoad(field, search)
}

watch(
  () => props.modelValue,
  () => {
    for (const field of editableFields.value) {
      if (isDropdownField(field.dataType) && field.dataType.toLowerCase() === 'dependent dropdown') {
        lookupOptions[field.ref] = []
      }
    }
  },
  { deep: true },
)

onBeforeUnmount(() => {
  Object.values(lookupTimers).forEach((timer) => window.clearTimeout(timer))
})
</script>

<template>
  <div class="dynamic-form" :key="formRenderKey">
    <section v-for="[sectionNum, sectionFields] in sections" :key="sectionNum" class="form-section">
      <div class="fields-grid">
        <div v-for="field in sectionFields" :key="field.ref" class="field">
          <label :for="fieldInputId(field)">
            {{ field.fieldName }}
            <span v-if="field.mandatory === 'Yes'" class="req">*</span>
          </label>

          <textarea
            v-if="isTextArea(field.dataType)"
            :id="field.ref"
            :value="modelValue[field.ref] ?? ''"
            rows="3"
            @input="updateField(field.ref, ($event.target as HTMLTextAreaElement).value)"
          />

          <select
            v-else-if="isYesNoField(field.dataType)"
            :id="field.ref"
            :value="modelValue[field.ref] ?? ''"
            @change="updateField(field.ref, ($event.target as HTMLSelectElement).value)"
          >
            <option value="">— Select —</option>
            <option value="Yes">Yes</option>
            <option value="No">No</option>
          </select>

          <SearchableMultiSelect
            v-else-if="isMultiSelectField(field.dataType)"
            :id="field.ref"
            :model-value="modelValue[field.ref] ?? ''"
            :options="staticOptions(field)"
            :placeholder="'Search ' + field.fieldName.toLowerCase() + '…'"
            @update:model-value="updateField(field.ref, $event)"
          />

          <SearchableSelect
            v-else-if="isBankNameField(field)"
            :id="fieldInputId(field)"
            :model-value="modelValue[field.ref] ?? ''"
            :options="lookupOptions[field.ref] ?? []"
            :loading="lookupLoading[field.ref]"
            :placeholder="'Search bank…'"
            @focus="loadLookupOptions(field)"
            @search="onLookupSearch(field, $event)"
            @update:model-value="updateField(field.ref, $event)"
          />

          <SearchableSelect
            v-else-if="isSearchableSelectField(field) && !isLookupField(field.dataType)"
            :id="fieldInputId(field)"
            :model-value="modelValue[field.ref] ?? ''"
            :options="staticOptions(field)"
            :placeholder="'Search ' + field.fieldName.toLowerCase() + '…'"
            @update:model-value="updateField(field.ref, $event)"
          />

          <SearchableSelect
            v-else-if="isLookupField(field.dataType)"
            :id="fieldInputId(field)"
            :model-value="modelValue[field.ref] ?? ''"
            :options="lookupOptions[field.ref] ?? []"
            :loading="lookupLoading[field.ref]"
            :placeholder="'Search ' + field.fieldName.toLowerCase() + '…'"
            @focus="onLookupFocus(field)"
            @search="onLookupSearch(field, $event)"
            @update:model-value="updateField(field.ref, $event)"
          />

          <FileUploadField
            v-else-if="isFileUploadField(field.dataType, field.ref, field.validation)"
            :id="fieldInputId(field)"
            :model-value="modelValue[field.ref] ?? ''"
            :accept="fileAccept(field.validation)"
            :max-size-mb="fileMaxSizeMb(field.validation)"
            :button-label="uploadButtonLabel(field)"
            @update:model-value="updateField(field.ref, $event)"
          />

          <input
            v-else
            :id="field.ref"
            :type="inputType(field.dataType)"
            :value="modelValue[field.ref] ?? ''"
            @input="updateField(field.ref, ($event.target as HTMLInputElement).value)"
          />

        </div>
      </div>
    </section>
  </div>
</template>

<style scoped>
.dynamic-form {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.form-section {
  background: transparent;
  border: none;
  border-radius: 0;
  padding: 0;
}

.dynamic-form:not(.nested) .form-section {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 10px;
  padding: 1.25rem;
}

.form-section h3 {
  margin: 0 0 1rem;
  font-size: 0.95rem;
  color: #334155;
  border-bottom: 1px solid #f1f5f9;
  padding-bottom: 0.5rem;
}

.fields-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

@media (max-width: 640px) {
  .fields-grid {
    grid-template-columns: 1fr;
  }

  .form-section {
    padding: 1rem;
  }

  .field input,
  .field select,
  .field textarea {
    font-size: 1rem;
    min-height: 44px;
  }
}

.field label {
  display: block;
  font-size: 0.8rem;
  font-weight: 500;
  color: #475569;
  margin-bottom: 0.35rem;
}

.ref {
  color: #ea580c;
  font-weight: 700;
  margin-right: 0.35rem;
}

.req {
  color: #dc2626;
}

.field input,
.field select,
.field textarea {
  width: 100%;
  padding: 0.55rem 0.75rem;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  font-size: 0.875rem;
  box-sizing: border-box;
}

.field input:focus,
.field select:focus,
.field textarea:focus {
  outline: none;
  border-color: #ea580c;
  box-shadow: 0 0 0 2px rgba(234, 88, 12, 0.12);
}

.hint {
  display: block;
  margin-top: 0.25rem;
  font-size: 0.7rem;
  color: #94a3b8;
  line-height: 1.3;
}
</style>
