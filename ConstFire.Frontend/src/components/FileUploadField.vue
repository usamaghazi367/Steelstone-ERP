<script setup lang="ts">
import { computed, ref } from 'vue'

const props = withDefaults(
  defineProps<{
    modelValue: string
    accept?: string
    maxSizeMb?: number
    disabled?: boolean
    id?: string
    buttonLabel?: string
  }>(),
  {
    accept: '.pdf,.jpg,.jpeg,.png,.doc,.docx',
    maxSizeMb: 10,
    disabled: false,
    buttonLabel: 'Choose file',
  },
)

const emit = defineEmits<{ 'update:modelValue': [string] }>()

const error = ref('')
const fileInput = ref<HTMLInputElement | null>(null)

const fileName = computed(() => {
  if (!props.modelValue) return ''
  if (props.modelValue.startsWith('data:')) {
    const match = props.modelValue.match(/;name=([^;]+)/)
    return match?.[1] ?? 'Uploaded file'
  }
  return props.modelValue.split('|').pop() ?? props.modelValue
})

function openPicker() {
  if (props.disabled) return
  fileInput.value?.click()
}

function clearFile() {
  error.value = ''
  emit('update:modelValue', '')
  if (fileInput.value) fileInput.value.value = ''
}

async function onFileChange(event: Event) {
  error.value = ''
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  const maxBytes = props.maxSizeMb * 1024 * 1024
  if (file.size > maxBytes) {
    error.value = `File must be ${props.maxSizeMb} MB or smaller.`
    input.value = ''
    return
  }

  const reader = new FileReader()
  reader.onload = () => {
    const dataUrl = String(reader.result ?? '')
    emit('update:modelValue', `${dataUrl};name=${file.name}|${file.type}|${file.size}`)
  }
  reader.onerror = () => {
    error.value = 'Could not read the selected file.'
  }
  reader.readAsDataURL(file)
}
</script>

<template>
  <div class="file-upload" :class="{ disabled }">
    <input
      :id="id"
      ref="fileInput"
      type="file"
      class="hidden-input"
      :accept="accept"
      :disabled="disabled"
      @change="onFileChange"
    />
    <div v-if="modelValue" class="file-meta">
      <span class="file-name">{{ fileName }}</span>
      <button type="button" class="btn-clear" :disabled="disabled" @click="clearFile">Remove</button>
    </div>
    <button type="button" class="btn-choose" :disabled="disabled" @click="openPicker">
      {{ modelValue ? 'Replace file' : buttonLabel }}
    </button>
    <p v-if="error" class="error">{{ error }}</p>
  </div>
</template>

<style scoped>
.file-upload {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.hidden-input {
  display: none;
}

.btn-choose {
  align-self: flex-start;
  min-height: 44px;
  padding: 0.55rem 1rem;
  border: 1px dashed #ea580c;
  border-radius: 8px;
  background: #fff;
  color: #ea580c;
  cursor: pointer;
  font-size: 0.875rem;
}

.file-meta {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
  padding: 0.5rem 0.65rem;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  background: #f8fafc;
}

.file-name {
  font-size: 0.85rem;
  color: #334155;
  word-break: break-word;
}

.btn-clear {
  border: 1px solid #fecaca;
  background: #fff;
  color: #dc2626;
  border-radius: 6px;
  padding: 0.35rem 0.65rem;
  cursor: pointer;
  font-size: 0.8rem;
}

.error {
  margin: 0;
  color: #dc2626;
  font-size: 0.75rem;
}

.disabled .btn-choose,
.disabled .btn-clear {
  opacity: 0.5;
  cursor: not-allowed;
}
</style>
