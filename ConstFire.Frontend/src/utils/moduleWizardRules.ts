import type { ModuleField } from '../types/modules'
import type { SectionDef } from './enterpriseRules'

export type SectionData = Record<string, string>
export type RepeatingSectionData = SectionData[]
export type ModuleFormState = {
  recordCode?: string
  completedSections: number[]
  sections: Record<string, SectionData | RepeatingSectionData>
}

export function emptyModuleState(): ModuleFormState {
  return { completedSections: [], sections: {} }
}

export function getSectionFieldRefs(fields: ModuleField[], sectionNum: number): string[] {
  return fields
    .filter((f) => (f.section ?? Number(f.ref.split('.')[0])) === sectionNum)
    .map((f) => f.ref)
}

export function getSectionData(state: ModuleFormState, sectionNum: number, repeating: boolean) {
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

export function fieldsForSection(fields: ModuleField[], sectionNum: number): ModuleField[] {
  return fields.filter((f) => (f.section ?? Number(f.ref.split('.')[0])) === sectionNum)
}

function escapeRef(ref: string): string {
  return ref.replace('.', '\\.')
}

export function parseModuleState(
  data: Record<string, string>,
  sections: SectionDef[],
  fields: ModuleField[],
): ModuleFormState {
  const state = emptyModuleState()
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
        const flatRow: SectionData = {}
        for (const ref of refs) {
          if (data[ref] !== undefined && data[ref] !== '') flatRow[ref] = data[ref]
        }
        state.sections[key] = Object.keys(flatRow).length > 0 ? [flatRow] : [{}]
      } else {
        state.sections[key] = sorted.map((index) => {
          const row: SectionData = {}
          for (const ref of refs) {
            const value = data[`${ref}#${index}`]
            if (value !== undefined) row[ref] = value
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

export function flattenModuleState(state: ModuleFormState): Record<string, string> {
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

  return flat
}
