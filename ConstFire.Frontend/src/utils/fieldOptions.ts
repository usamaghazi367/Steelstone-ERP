export interface FieldOption {
  value: string
  label: string
}

export function isYesNoField(dataType: string): boolean {
  return dataType.toLowerCase().includes('yes / no') && !dataType.toLowerCase().includes('+')
}

export function isDropdownField(dataType: string): boolean {
  const dt = dataType.toLowerCase()
  if (dt.includes('lookup')) return false
  return dt === 'dropdown' || dt.includes('dependent dropdown')
}

export function isMultiSelectField(dataType: string): boolean {
  return dataType.toLowerCase() === 'multi-select'
}

export function isLookupField(dataType: string): boolean {
  const dt = dataType.toLowerCase()
  return dt === 'lookup' || dt.includes('lookup')
}

export function isFileUploadField(dataType: string, fieldRef?: string, validation?: string): boolean {
  const dt = dataType.toLowerCase()
  if (dt === 'file upload' || dt === 'file upload / e-sign') return true
  if (fieldRef?.startsWith('11.') && (validation ?? '').toLowerCase().includes('pdf')) return true
  return false
}

export function getDependentParentRef(validation: string): string | null {
  const match = validation.match(/if\s+([\d.]+)\s*=/i)
  return match?.[1] ?? null
}

export function resolveParentValue(
  validation: string,
  modelValue: Record<string, string>,
  gateRef?: string | null,
): string {
  const gate = gateRef ?? (modelValue['1.1'] ? '1.1' : modelValue['1.3'] ? '1.3' : null)
  const yesNo = gate ? modelValue[gate]?.trim().toLowerCase() : ''
  if (validation.includes('1.2 = Yes') || validation.includes('1.3 = Yes') || validation.includes('1.3 = Company')) {
    if (yesNo === 'yes') return 'Company'
    if (yesNo === 'no') return 'AOP'
  }

  const explicit = getDependentParentRef(validation)
  const candidates = explicit ? [explicit, gate ?? '', '1.1', '1.3', '1.5'].filter(Boolean) : [gate ?? '', '1.1', '1.3', '1.5'].filter(Boolean)

  for (const ref of candidates) {
    const value = modelValue[ref]?.trim()
    if (!value) continue
    if (ref === '1.1' || ref === '1.3') return value.toLowerCase() === 'yes' ? 'Company' : 'AOP'
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

import { getPakistaniCities, isPakistaniCityField } from './pakistaniCities'
import { findEntityGatingRef, isAopTrack, isCompanyTrack } from './moduleFieldRules'
import type { ModuleField } from '../types/modules'

function isRegisteringAuthorityField(field: { fieldName?: string; validation: string }): boolean {
  return (
    /registering authority/i.test(field.fieldName ?? '') ||
    /must be secp if/i.test(field.validation) ||
    /secp \/ registrar of firms/i.test(field.validation)
  )
}

export function isPhoneField(dataType: string, fieldName?: string): boolean {
  const dt = dataType.toLowerCase()
  const name = (fieldName ?? '').toLowerCase()
  return dt.includes('phone') || name.includes('mobile') || name.includes('phone')
}

export function isBankNameField(field: { ref: string; fieldName?: string }): boolean {
  const name = field.fieldName ?? ''
  return (
    field.ref === '4.1' ||
    /bank name/i.test(name) ||
    /payee bank name/i.test(name)
  )
}

export function isSearchableSelectField(field: {
  ref: string
  dataType: string
  validation: string
  fieldName?: string
}): boolean {
  if (isPhoneField(field.dataType, field.fieldName)) return false
  if (isBankNameField(field)) return true
  if (isDropdownField(field.dataType) || isLookupField(field.dataType)) return true
  return isPakistaniCityField(field.validation, field.ref, field.dataType, field.fieldName)
}

export function getStaticFieldOptions(
  dataType: string,
  validation: string,
  modelValue: Record<string, string>,
  fieldRef?: string,
  allFields?: ModuleField[],
): string[] {
  const fieldMeta = fieldRef && allFields ? allFields.find((f) => f.ref === fieldRef) : undefined
  if (
    isPakistaniCityField(
      validation,
      fieldRef,
      fieldMeta?.dataType ?? dataType,
      fieldMeta?.fieldName,
    )
  ) {
    return getPakistaniCities()
  }

  if (fieldRef === '4.2') {
    return ['Subscriber', 'Partner', 'Sole Proprietor']
  }

  const gateRef = allFields ? findEntityGatingRef(allFields) : null
  if (fieldRef && allFields) {
    const fieldMeta = allFields.find((f) => f.ref === fieldRef)
    if (fieldMeta && isRegisteringAuthorityField(fieldMeta)) {
      if (isCompanyTrack(modelValue, gateRef)) return ['SECP']
      if (isAopTrack(modelValue, gateRef)) {
        return ['Registrar of Firms (Provincial)', 'FBR only (Sole Proprietor)']
      }
    }
  }

  if (fieldRef === '1.2') {
    if (isCompanyTrack(modelValue, gateRef ?? '1.1')) return ['SECP']
    if (isAopTrack(modelValue, gateRef ?? '1.1')) {
      return ['Registrar of Firms (Provincial)', 'FBR only (Sole Proprietor)']
    }
  }

  if (fieldRef === '2.5') {
    return ['PRA (Punjab)', 'SRB (Sindh)', 'KPRA', 'BRA (Balochistan)', 'ICT', 'Not applicable']
  }

  if (dataType.toLowerCase() === 'dependent dropdown') {
    return parseDependentOptions(validation, resolveParentValue(validation, modelValue, gateRef))
  }
  return parseSlashSeparatedOptions(validation)
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
