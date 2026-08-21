export interface FieldOption {
  value: string
  label: string
}

export function isYesNoField(dataType: string): boolean {
  return dataType.toLowerCase().includes('yes / no') && !dataType.toLowerCase().includes('+')
}

export function isDropdownField(dataType: string): boolean {
  const dt = dataType.toLowerCase()
  return dt === 'dropdown' || dt === 'dependent dropdown'
}

export function isMultiSelectField(dataType: string): boolean {
  return dataType.toLowerCase() === 'multi-select'
}

export function isFileUploadField(dataType: string): boolean {
  const dt = dataType.toLowerCase()
  return dt === 'file upload' || dt === 'file upload / e-sign'
}

export function hasDropdownOptions(
  dataType: string,
  validation: string,
  modelValue: Record<string, string>,
  fieldRef?: string,
): boolean {
  return getStaticFieldOptions(dataType, validation, modelValue, fieldRef).length > 0
}

export function isLookupField(dataType: string): boolean {
  return dataType.toLowerCase() === 'lookup'
}

export function getDependentParentRef(validation: string): string | null {
  const match = validation.match(/if\s+([\d.]+)\s*=/i)
  return match?.[1] ?? null
}

export function resolveParentValue(
  validation: string,
  modelValue: Record<string, string>,
): string {
  const yesNo = modelValue['1.1']?.trim().toLowerCase()
  if (validation.includes('1.2 = Yes') || validation.includes('1.3 = Company')) {
    if (yesNo === 'yes') return 'Company'
    if (yesNo === 'no') return 'AOP'
  }

  const explicit = getDependentParentRef(validation)
  const candidates = explicit ? [explicit, '1.1', '1.3', '1.5'] : ['1.1', '1.3', '1.5']

  for (const ref of candidates) {
    const value = modelValue[ref]?.trim()
    if (!value) continue
    if (ref === '1.1') return value.toLowerCase() === 'yes' ? 'Company' : 'AOP'
    return value
  }
  return ''
}

function cleanOption(option: string): string {
  return option.trim().replace(/\s+/g, ' ').replace(/\.$/, '')
}

function startsWithInstruction(option: string): boolean {
  const lower = option.toLowerCase()
  const blocked = [
    'must ',
    'mandatory',
    'disabled',
    'required',
    'defaults',
    'drives',
    'enables',
    'only applied',
    'not in',
    'cannot',
    'should',
    'will ',
    'if ',
    'e.g.',
    'for example',
    'linked to',
    'select ',
    'pulled from',
    'from the',
    'overrides',
  ]
  return blocked.some((prefix) => lower.startsWith(prefix))
}

export function parseSlashSeparatedOptions(text: string): string[] {
  if (!text) return []

  let segment = text
  const periodIndex = text.indexOf('. ')
  if (periodIndex > 0 && text.includes('/')) {
    segment = text.slice(0, periodIndex)
  }

  return segment
    .split(/\s*\/\s*/)
    .map(cleanOption)
    .filter((o) => o.length > 0 && o.length <= 120)
    .filter((o) => !startsWithInstruction(o))
    .filter((value, index, array) => array.findIndex((v) => v.toLowerCase() === value.toLowerCase()) === index)
}

function parentMatches(parent: string, key: string): boolean {
  if (parent.includes(key) || key.includes(parent)) return true
  if (key.includes('yes') || key === 'company') return parent.includes('company')
  if (key.includes('no') || key === 'aop') return parent.includes('aop') || parent.includes('partnership')
  if (key === 'individual') return parent.includes('individual') || parent.includes('sole')
  if (key === 'other') return parent.includes('other') || parent.includes('sole')
  return false
}

export function parseDependentOptions(validation: string, parentValue: string): string[] {
  if (!parentValue.trim()) return []
  const parent = parentValue.trim().toLowerCase()

  const ifRegex = /if\s+[\d.]+\s*=\s*([^:]+):\s*([\s\S]+?)(?=if\s+[\d.]+\s*=|$)/gi
  let match: RegExpExecArray | null
  while ((match = ifRegex.exec(validation)) !== null) {
    const key = match[1].trim().toLowerCase()
    if (parentMatches(parent, key)) {
      return parseSlashSeparatedOptions(match[2])
    }
  }

  const groupRegex = /(Company|AOP|Individual|Other|Sole Proprietorship?)\s*:/gi
  const parts = validation.split(groupRegex).filter(Boolean)
  for (let i = 0; i + 1 < parts.length; i += 2) {
    const key = parts[i].trim().toLowerCase()
    const optionsText = parts[i + 1]
    if (parentMatches(parent, key)) {
      return parseSlashSeparatedOptions(optionsText)
    }
  }

  return []
}

export function getStaticFieldOptions(
  dataType: string,
  validation: string,
  modelValue: Record<string, string>,
  fieldRef?: string,
): string[] {
  if (fieldRef === '4.2') {
    return ['Subscriber', 'Partner', 'Sole Proprietor']
  }

  if (fieldRef === '1.2') {
    if (isCompanyTrack(modelValue)) return ['SECP']
    if (isAopTrack(modelValue)) {
      return ['Registrar of Firms (Provincial)', 'FBR only (Sole Proprietor)']
    }
  }

  if (fieldRef === '2.5') {
    return ['PRA (Punjab)', 'SRB (Sindh)', 'KPRA', 'BRA (Balochistan)', 'ICT', 'Not applicable']
  }

  if (dataType.toLowerCase() === 'dependent dropdown') {
    return parseDependentOptions(validation, resolveParentValue(validation, modelValue))
  }
  return parseSlashSeparatedOptions(validation)
}

function isCompanyTrack(values: Record<string, string>): boolean {
  return (values['1.1'] ?? '').trim().toLowerCase() === 'yes'
}

function isAopTrack(values: Record<string, string>): boolean {
  return (values['1.1'] ?? '').trim().toLowerCase() === 'no'
}

export function splitMultiValue(value: string): string[] {
  if (!value.trim()) return []
  return value
    .split(',')
    .map((part) => part.trim())
    .filter(Boolean)
}

export function joinMultiValue(values: string[]): string {
  return values.join(', ')
}
