import type { ModuleField } from '../types/modules'

export interface SectionDef {
  num: number
  title: string
  repeating: boolean
}

export type SectionData = Record<string, string>
export type RepeatingSectionData = SectionData[]
export type EnterpriseFormState = {
  recordCode?: string
  completedSections: number[]
  sections: Record<string, SectionData | RepeatingSectionData>
}

const COMPANY_ONLY_FIELDS = ['1.6', '1.7', '1.8', '1.15', '1.18']
const AOP_ONLY_FIELDS = ['1.9', '1.10', '1.11']

export function emptyEnterpriseState(): EnterpriseFormState {
  return { completedSections: [], sections: {} }
}

export function getSectionFieldRefs(fields: ModuleField[], sectionNum: number): string[] {
  return fields
    .filter((f) => (f.section ?? Number(f.ref.split('.')[0])) === sectionNum)
    .map((f) => f.ref)
}

export function getSectionData(state: EnterpriseFormState, sectionNum: number, repeating: boolean) {
  const key = String(sectionNum)
  if (repeating) {
    const rows = state.sections[key]
    if (Array.isArray(rows)) return rows as RepeatingSectionData
    return [{}]
  }
  const data = state.sections[key]
  if (data && !Array.isArray(data)) return data as SectionData
  return {}
}

export function isCompanyTrack(values: Record<string, string>): boolean {
  return (values['1.1'] ?? '').trim().toLowerCase() === 'yes'
}

export function isAopTrack(values: Record<string, string>): boolean {
  return (values['1.1'] ?? '').trim().toLowerCase() === 'no'
}

function ownerType(values: Record<string, string>): string {
  return (values['4.3'] ?? '').trim().toLowerCase()
}

function section1Values(allValues: Record<string, string>): Record<string, string> {
  const data: SectionData = {}
  for (const ref of Object.keys(allValues)) {
    if (ref.startsWith('1.') && !ref.includes('#')) data[ref] = allValues[ref]
  }
  return data
}

export function isFieldVisible(ref: string, allValues: Record<string, string>): boolean {
  const hiddenAlways = new Set(['1.17', '1.18'])
  if (hiddenAlways.has(ref)) return false

  const s1 = section1Values(allValues)

  if (COMPANY_ONLY_FIELDS.includes(ref)) {
    return isCompanyTrack(s1)
  }
  if (AOP_ONLY_FIELDS.includes(ref)) {
    return isAopTrack(s1)
  }

  if (ref === '2.6') {
    const services = (allValues['2.5'] ?? '').toLowerCase()
    return services.length > 0 && !services.includes('not applicable')
  }

  if (ref === '3.9') {
    return (allValues['3.8'] ?? '').trim().toLowerCase() === 'no'
  }

  if (ref.startsWith('4.')) {
    const type = ownerType(allValues)
    if (ref === '4.5') return type.includes('individual')
    if (ref === '4.6') return type.includes('individual (pakistani)')
    if (ref === '4.7') return type.includes('foreign')
    if (ref === '4.8') return type.includes('body corporate') || type.includes('corporate')
  }

  if (ref === '5.9') {
    return isCompanyTrack(s1)
  }

  if (ref.startsWith('11.')) {
    if (['11.1', '11.2', '11.3', '11.4', '11.10'].includes(ref)) return isCompanyTrack(s1)
    if (['11.5', '11.6'].includes(ref)) return isAopTrack(s1)
    if (ref === '11.8') {
      return (allValues['2.3'] ?? '').toLowerCase() === 'yes' || Boolean(allValues['2.6']?.trim())
    }
    if (ref === '11.14') return true
  }

  if (ref === '9.7') {
    const currency = (allValues['9.6'] ?? '').trim().toUpperCase()
    return currency !== '' && currency !== 'PKR'
  }

  return true
}

/** Remove hidden values and apply defaults when gating fields change. */
export function applyConditionalCascade(
  data: SectionData,
  flatContext: Record<string, string>,
  sectionNum: number,
): SectionData {
  const result = { ...data }
  const merged = { ...flatContext, ...result }

  if (sectionNum === 1) {
    const track = (result['1.1'] ?? merged['1.1'] ?? '').trim().toLowerCase()
    if (track === 'yes') {
      result['1.2'] = 'SECP'
      for (const ref of AOP_ONLY_FIELDS) delete result[ref]
    } else if (track === 'no') {
      if ((result['1.2'] ?? '').toUpperCase().includes('SECP')) result['1.2'] = ''
      for (const ref of COMPANY_ONLY_FIELDS) delete result[ref]
      if (!result['1.2']) result['1.2'] = ''
    }
  }

  if (sectionNum === 2 && isCompanyTrack({ ...flatContext, ...result })) {
    if (!result['2.7']) result['2.7'] = 'Yes'
  }

  if (sectionNum === 3 && (result['3.8'] ?? '').toLowerCase() === 'yes') {
    delete result['3.9']
  }

  if (sectionNum === 4) {
    const type = (result['4.3'] ?? '').toLowerCase()
    if (type.includes('individual (pakistani)')) {
      delete result['4.7']
      delete result['4.8']
    } else if (type.includes('foreign')) {
      delete result['4.5']
      delete result['4.6']
      delete result['4.8']
    } else if (type.includes('corporate')) {
      delete result['4.5']
      delete result['4.6']
      delete result['4.7']
    }
  }

  const context = { ...flatContext, ...result }
  for (const ref of Object.keys(result)) {
    if (!isFieldVisible(ref, context)) delete result[ref]
  }

  return result
}

export function applyRepeatingRowCascade(
  row: SectionData,
  flatContext: Record<string, string>,
  sectionNum: number,
): SectionData {
  if (sectionNum === 4) {
    return applyConditionalCascade(row, flatContext, 4)
  }
  if (sectionNum === 5) {
    const result = { ...row }
    if ((result['5.1'] ?? '').toLowerCase() !== 'yes') {
      delete result['5.2_ownerIndex']
    }
    return result
  }
  return row
}

export function flattenEnterpriseState(state: EnterpriseFormState): Record<string, string> {
  const flat: Record<string, string> = {}
  if (state.recordCode) flat['_recordCode'] = state.recordCode

  for (const [sectionKey, sectionValue] of Object.entries(state.sections)) {
    if (Array.isArray(sectionValue)) {
      flat[`__section_${sectionKey}`] = JSON.stringify(sectionValue)
      sectionValue.forEach((row, index) => {
        for (const [ref, value] of Object.entries(row)) {
          flat[`${ref}#${index}`] = value
        }
      })
    } else {
      Object.assign(flat, sectionValue)
    }
  }

  const owners = state.sections['4']
  const directors = state.sections['5']
  if (Array.isArray(owners)) flat['1.17'] = String(owners.filter((r) => r['4.4']?.trim()).length)
  if (Array.isArray(directors)) flat['1.18'] = String(directors.filter((r) => r['5.2']?.trim()).length)

  return flat
}

export function parseEnterpriseState(
  data: Record<string, string>,
  sections: SectionDef[],
  fields: ModuleField[],
): EnterpriseFormState {
  const state = emptyEnterpriseState()
  state.recordCode = data['_recordCode']

  const completed = data['_completedSections']
  if (completed) {
    try {
      state.completedSections = JSON.parse(completed)
    } catch {
      state.completedSections = completed.split(',').map(Number).filter(Boolean)
    }
  }

  for (const section of sections) {
    const key = String(section.num)
    const refs = getSectionFieldRefs(fields, section.num)
    const packed = data[`__section_${key}`]

    if (packed) {
      try {
        state.sections[key] = JSON.parse(packed)
        continue
      } catch {
        /* fall through */
      }
    }

    if (section.repeating) {
      const rowIndexes = new Set<number>()
      for (const ref of refs) {
        for (const dataKey of Object.keys(data)) {
          const match = dataKey.match(new RegExp(`^${escapeRef(ref)}#(\\d+)$`))
          if (match) rowIndexes.add(Number(match[1]))
        }
      }
      const sorted = [...rowIndexes].sort((a, b) => a - b)
      if (sorted.length === 0) {
        state.sections[key] = [{}]
      } else {
        state.sections[key] = sorted.map((index) => {
          const row: SectionData = {}
          for (const ref of refs) {
            const v = data[`${ref}#${index}`]
            if (v !== undefined) row[ref] = v
          }
          return row
        })
      }
    } else {
      const row: SectionData = {}
      for (const ref of refs) {
        if (data[ref] !== undefined) row[ref] = data[ref]
      }
      state.sections[key] = row
    }
  }

  return state
}

function escapeRef(ref: string): string {
  return ref.replace('.', '\\.')
}

export function getOwnerOptions(state: EnterpriseFormState): { value: string; label: string; row: SectionData }[] {
  const owners = state.sections['4']
  if (!Array.isArray(owners)) return []
  return owners
    .filter((row) => row['4.4']?.trim())
    .map((row, index) => ({
      value: String(index),
      label: row['4.4'].trim(),
      row,
    }))
}

export function applyOwnerToDirector(row: SectionData, owner: SectionData): SectionData {
  return {
    ...row,
    '5.1': 'Yes',
    '5.2': owner['4.4'] ?? row['5.2'] ?? '',
    '5.3': owner['4.5'] ?? row['5.3'] ?? '',
    '5.4': owner['4.6'] || owner['4.7'] || row['5.4'] || '',
    '5.10': owner['4.10'] ?? row['5.10'] ?? '',
    '5.11': owner['4.11'] ?? row['5.11'] ?? '',
    '5.12': owner['4.12'] ?? row['5.12'] ?? '',
  }
}

export function fieldsForSection(fields: ModuleField[], sectionNum: number): ModuleField[] {
  return fields.filter((f) => (f.section ?? Number(f.ref.split('.')[0])) === sectionNum)
}

export function trackKey(flat: Record<string, string>): string {
  return `${flat['1.1'] ?? ''}|${flat['1.2'] ?? ''}|${flat['4.3'] ?? ''}|${flat['3.8'] ?? ''}`
}
