<script setup lang="ts">
import { computed, ref, watch } from 'vue'

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
    placeholder: 'Type to search…',
    loading: false,
    disabled: false,
  },
)

const emit = defineEmits<{
  'update:modelValue': [string]
  focus: []
  search: [string]
}>()

const open = ref(false)
const query = ref('')
const inputRef = ref<HTMLInputElement | null>(null)

const displayValue = computed(() => (open.value ? query.value : props.modelValue))

const filteredOptions = computed(() => {
  const term = query.value.trim().toLowerCase()
  const list = props.options
  if (!term) return list.slice(0, 100)
  return list.filter((option) => option.toLowerCase().includes(term)).slice(0, 100)
})

watch(
  () => props.modelValue,
  (value) => {
    if (!open.value) query.value = value
  },
)

function openList() {
  if (props.disabled) return
  open.value = true
  query.value = props.modelValue
  emit('focus')
}

function closeList() {
  open.value = false
  query.value = props.modelValue
}

function selectOption(option: string) {
  emit('update:modelValue', option)
  query.value = option
  open.value = false
}

function onInput(event: Event) {
  query.value = (event.target as HTMLInputElement).value
  open.value = true
  emit('search', query.value)
}

function onBlur() {
  window.setTimeout(() => {
    closeList()
  }, 150)
}

function clearValue() {
  emit('update:modelValue', '')
  query.value = ''
  inputRef.value?.focus()
}
</script>

<template>
  <div class="searchable-select" :class="{ open, disabled }">
    <input
      :id="id"
      ref="inputRef"
      type="text"
      class="select-input"
      :value="displayValue"
      :placeholder="placeholder"
      :disabled="disabled"
      autocomplete="off"
      @focus="openList"
      @input="onInput"
      @blur="onBlur"
    />
    <button
      v-if="modelValue && !disabled"
      type="button"
      class="clear-btn"
      aria-label="Clear selection"
      @mousedown.prevent
      @click="clearValue"
    >
      ×
    </button>
    <div v-if="open" class="options-panel">
      <p v-if="loading" class="status">Loading options…</p>
      <p v-else-if="filteredOptions.length === 0" class="status">No matches found.</p>
      <button
        v-for="option in filteredOptions"
        :key="option"
        type="button"
        class="option"
        :class="{ selected: option === modelValue }"
        @mousedown.prevent
        @click="selectOption(option)"
      >
        {{ option }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.searchable-select {
  position: relative;
}

.select-input {
  width: 100%;
  padding: 0.55rem 2rem 0.55rem 0.75rem;
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

.clear-btn {
  position: absolute;
  right: 0.35rem;
  top: 50%;
  transform: translateY(-50%);
  width: 28px;
  height: 28px;
  border: none;
  border-radius: 999px;
  background: #f1f5f9;
  color: #64748b;
  cursor: pointer;
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

.option:hover,
.option.selected {
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
