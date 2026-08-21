<script setup lang="ts">
import { computed } from 'vue'
import { joinMultiValue, splitMultiValue } from '../utils/fieldOptions'

const props = withDefaults(
  defineProps<{
    modelValue: string
    options: string[]
    disabled?: boolean
    id?: string
  }>(),
  {
    disabled: false,
  },
)

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const selectedValues = computed(() => splitMultiValue(props.modelValue))

function isChecked(option: string): boolean {
  return selectedValues.value.some((value) => value.toLowerCase() === option.toLowerCase())
}

function toggleOption(option: string) {
  if (props.disabled) return
  const next = isChecked(option)
    ? selectedValues.value.filter((value) => value.toLowerCase() !== option.toLowerCase())
    : [...selectedValues.value, option]
  emit('update:modelValue', joinMultiValue(next))
}
</script>

<template>
  <div class="checkbox-multi" :class="{ disabled }" :id="id">
    <label
      v-for="option in options"
      :key="option"
      class="checkbox-option"
      :class="{ checked: isChecked(option) }"
    >
      <input
        type="checkbox"
        :checked="isChecked(option)"
        :disabled="disabled"
        @change="toggleOption(option)"
      />
      <span>{{ option }}</span>
    </label>
    <p v-if="options.length === 0" class="empty">No options available.</p>
  </div>
</template>

<style scoped>
.checkbox-multi {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  padding: 0.5rem 0.65rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #fff;
  max-height: 220px;
  overflow-y: auto;
}

.checkbox-option {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.35rem 0.25rem;
  border-radius: 6px;
  cursor: pointer;
  font-size: 0.875rem;
  color: #334155;
}

.checkbox-option:hover {
  background: #fff7ed;
}

.checkbox-option.checked {
  background: #fff7ed;
  color: #9a3412;
  font-weight: 500;
}

.checkbox-option input {
  width: 1rem;
  height: 1rem;
  accent-color: #ea580c;
  cursor: pointer;
}

.empty {
  margin: 0;
  font-size: 0.8rem;
  color: #94a3b8;
}

.disabled {
  opacity: 0.6;
  pointer-events: none;
}
</style>
