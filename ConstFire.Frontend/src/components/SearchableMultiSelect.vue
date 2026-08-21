<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { joinMultiValue, splitMultiValue } from '../utils/fieldOptions'

const props = withDefaults(
  defineProps<{
    modelValue: string
    options: string[]
    placeholder?: string
    loading?: boolean
    disabled?: boolean
    id?: string
  }>(),
  {
    placeholder: 'Type to search and select…',
    loading: false,
    disabled: false,
  },
)

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const open = ref(false)
const query = ref('')
const inputRef = ref<HTMLInputElement | null>(null)

const selectedValues = computed(() => splitMultiValue(props.modelValue))

const filteredOptions = computed(() => {
  const term = query.value.trim().toLowerCase()
  const available = props.options.filter(
    (option) => !selectedValues.value.some((selected) => selected.toLowerCase() === option.toLowerCase()),
  )
  if (!term) return available.slice(0, 100)
  return available.filter((option) => option.toLowerCase().includes(term)).slice(0, 100)
})

watch(
  () => props.modelValue,
  () => {
    if (!open.value) query.value = ''
  },
)

function openList() {
  if (props.disabled) return
  open.value = true
}

function closeList() {
  open.value = false
  query.value = ''
}

function addOption(option: string) {
  const next = [...selectedValues.value, option]
  emit('update:modelValue', joinMultiValue(next))
  query.value = ''
  inputRef.value?.focus()
}

function removeOption(option: string) {
  const next = selectedValues.value.filter((value) => value !== option)
  emit('update:modelValue', joinMultiValue(next))
}

function onInput(event: Event) {
  query.value = (event.target as HTMLInputElement).value
  open.value = true
}

function onBlur() {
  window.setTimeout(closeList, 150)
}
</script>

<template>
  <div class="searchable-multi" :class="{ open, disabled }">
    <div class="chips" v-if="selectedValues.length">
      <span v-for="value in selectedValues" :key="value" class="chip">
        {{ value }}
        <button type="button" aria-label="Remove" @mousedown.prevent @click="removeOption(value)">×</button>
      </span>
    </div>
    <input
      :id="id"
      ref="inputRef"
      type="text"
      class="select-input"
      :value="query"
      :placeholder="placeholder"
      :disabled="disabled"
      autocomplete="off"
      @focus="openList"
      @input="onInput"
      @blur="onBlur"
    />
    <div v-if="open" class="options-panel">
      <p v-if="loading" class="status">Loading options…</p>
      <p v-else-if="filteredOptions.length === 0" class="status">No matches found.</p>
      <button
        v-for="option in filteredOptions"
        :key="option"
        type="button"
        class="option"
        @mousedown.prevent
        @click="addOption(option)"
      >
        {{ option }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.searchable-multi {
  position: relative;
}

.chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  margin-bottom: 0.35rem;
}

.chip {
  display: inline-flex;
  align-items: center;
  gap: 0.25rem;
  padding: 0.2rem 0.45rem;
  border-radius: 999px;
  background: #fff7ed;
  color: #9a3412;
  font-size: 0.75rem;
}

.chip button {
  border: none;
  background: transparent;
  color: inherit;
  cursor: pointer;
  font-size: 0.95rem;
  line-height: 1;
}

.select-input {
  width: 100%;
  padding: 0.55rem 0.75rem;
  min-height: 44px;
  border: 1px solid #e2e8f0;
  border-radius: 6px;
  font-size: 0.875rem;
  box-sizing: border-box;
}

.select-input:focus {
  outline: none;
  border-color: #ea580c;
  box-shadow: 0 0 0 2px rgba(234, 88, 12, 0.12);
}

.options-panel {
  position: absolute;
  z-index: 30;
  left: 0;
  right: 0;
  top: calc(100% + 4px);
  max-height: 240px;
  overflow-y: auto;
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  box-shadow: 0 10px 24px rgba(15, 23, 42, 0.12);
}

.option {
  display: block;
  width: 100%;
  padding: 0.65rem 0.75rem;
  border: none;
  background: #fff;
  text-align: left;
  cursor: pointer;
  font-size: 0.875rem;
}

.option:hover {
  background: #fff7ed;
  color: #9a3412;
}

.status {
  margin: 0;
  padding: 0.75rem;
  font-size: 0.8rem;
  color: #64748b;
}

.disabled .select-input {
  background: #f8fafc;
  color: #94a3b8;
}
</style>
