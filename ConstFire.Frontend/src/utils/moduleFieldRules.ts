import type { ModuleField } from '../types/modules'
function isYesNoDataType(dataType: string): boolean {
  return dataType.toLowerCase().includes('yes / no') && !dataType.toLowerCase().includes('+')
}

/** SECP vs AOP gating field (1.1 on sheet 01, usually 1.3 on other sheets). */
export function findEntityGatingRef(fields: ModuleField[]): string | null {
  const legalStatus = fields.find((f) => f.ref === '1.2' && /legal status/i.test(f.fieldName))
  if (legalStatus) return '1.2'

  const explicit = fields.find(
    (f) =>
      isYesNoDataType(f.dataType) &&
      /gating|entity-type check|entity type check/i.test(`${f.fieldName} ${f.validation}`),
  )
  if (explicit) return explicit.ref
  if (fields.some((f) => f.ref === '1.1' && isYesNoDataType(f.dataType))) return '1.1'
  if (fields.some((f) => f.ref === '1.3' && isYesNoDataType(f.dataType))) return '1.3'
  return null
}

function gateValue(values: Record<string, string>, gateRef: string | null): string {
  if (!gateRef) return ''
  return values[gateRef]?.trim().toLowerCase() ?? ''
}

export function isCompanyTrack(values: Record<string, string>, gateRef: string | null): boolean {
  const v = gateValue(values, gateRef)
  if (!v) return false
  if (gateRef === '1.2') return v.includes('company')
  return v === 'yes'
}

export function isAopTrack(values: Record<string, string>, gateRef: string | null): boolean {
  const v = gateValue(values, gateRef)
  if (!v) return false
  if (gateRef === '1.2') return v.includes('aop') || v.includes('partnership') || v.includes('sole')
  return v === 'no'
}

function parseMandatoryIf(validation: string): { ref: string; value: string } | null {
  const match = validation.match(/mandatory if\s+([\d.]+)\s*=\s*([^.;]+)/i)
  if (!match) return null
  return { ref: match[1].trim(), value: match[2].trim() }
}

function isCompanyOnlyField(field: ModuleField): boolean {
  const m = field.mandatory.toLowerCase()
  if (m.includes('company only') || m.includes('company track')) return true
  const v = field.validation.toLowerCase()
  return (
    v.includes('if 1.2 = yes') ||
    v.includes('mandatory if 1.2 = yes') ||
    v.includes('mandatory if 1.3 = yes') ||
    (v.includes('invisible otherwise') && v.includes('if 1.2 = yes'))
  )
}

function isAopOnlyField(field: ModuleField): boolean {
  const m = field.mandatory.toLowerCase()
  if (
    m.includes('aop only') ||
    m.includes('partnership') ||
    m.includes('registered partnership')
  ) {
    return true
  }
  const v = field.validation.toLowerCase()
  return v.includes('disabled for companies') || v.includes('mandatory if 1.5 = aop')
}

export function isGenericFieldVisible(
  field: ModuleField,
  allValues: Record<string, string>,
  fields: ModuleField[],
): boolean {
  const gateRef = findEntityGatingRef(fields)
  const ref = field.ref

  if (isCompanyOnlyField(field)) return isCompanyTrack(allValues, gateRef)
  if (isAopOnlyField(field)) return isAopTrack(allValues, gateRef)

  const mandatoryIf = parseMandatoryIf(field.validation)
  if (mandatoryIf) {
    const actual = allValues[mandatoryIf.ref]?.trim() ?? ''
    const expected = mandatoryIf.value.toLowerCase()
    if (expected === 'yes' || expected === 'no') {
      return actual.toLowerCase() === expected
    }
    return actual.toLowerCase().includes(expected) || expected.includes(actual.toLowerCase())
  }

  const v = field.validation.toLowerCase()
  if (v.includes('invisible otherwise') || v.includes('disabled and left invisible')) {
    if (v.includes('if 1.2 = yes') || v.includes('if 1.3 = yes')) {
      return isCompanyTrack(allValues, gateRef)
    }
    if (v.includes('if 1.2 = no') || v.includes('if 1.3 = no')) {
      return isAopTrack(allValues, gateRef)
    }
  }

  if (v.includes('not applicable to aop') || v.includes('not applicable to partnership')) {
    return isCompanyTrack(allValues, gateRef)
  }

  if (v.includes('not applicable to company') || v.includes('not applicable to companies')) {
    return isAopTrack(allValues, gateRef)
  }

  // Multi-select follow-up (e.g. provincial tax registration numbers)
  if (ref.endsWith('.6') || ref.endsWith('.7')) {
    const base = ref.replace(/\.\d+$/, '')
    const multiRef = `${base}.5`
    const multiField = fields.find((f) => f.ref === multiRef)
    if (multiField?.dataType.toLowerCase() === 'multi-select') {
      const picked = (allValues[multiRef] ?? '').toLowerCase()
      if (picked.includes('not applicable') && picked.split(',').every((p) => !p.trim() || p.includes('not applicable'))) {
        return false
      }
    }
  }

  return true
}

export function isFieldVisibleForModule(
  _moduleCode: string,
  ref: string,
  allValues: Record<string, string>,
  fields: ModuleField[],
): boolean {
  const field = fields.find((f) => f.ref === ref)
  if (!field) return true
  return isGenericFieldVisible(field, allValues, fields)
}

export function applyModuleConditionalCascade(
  _moduleCode: string,
  data: Record<string, string>,
  flatContext: Record<string, string>,
  sectionNum: number,
  fields: ModuleField[],
): Record<string, string> {
  const result = { ...data }
  const gateRef = findEntityGatingRef(fields)
  const merged = { ...flatContext, ...result }

  if (sectionNum === 1 && gateRef === '1.1') {
    const track = (result[gateRef] ?? merged[gateRef] ?? '').trim().toLowerCase()
    const authorityField = fields.find(
      (f) =>
        f.section === 1 &&
        /registering authority/i.test(f.fieldName) &&
        f.dataType.toLowerCase().includes('dropdown'),
    )
    if (track === 'yes') {
      if (authorityField) result[authorityField.ref] = 'SECP'
      for (const f of fields) {
        if (isAopOnlyField(f)) delete result[f.ref]
      }
    } else if (track === 'no') {
      if (authorityField && (result[authorityField.ref] ?? '').toUpperCase().includes('SECP')) {
        result[authorityField.ref] = ''
      }
      for (const f of fields) {
        if (isCompanyOnlyField(f)) delete result[f.ref]
      }
    }
  }

  const context = { ...flatContext, ...result }
  for (const key of Object.keys(result)) {
    const field = fields.find((f) => f.ref === key)
    if (field && !isGenericFieldVisible(field, context, fields)) delete result[key]
  }

  return result
}

export function applyModuleRepeatingRowCascade(
  moduleCode: string,
  row: Record<string, string>,
  flatContext: Record<string, string>,
  sectionNum: number,
  fields: ModuleField[],
): Record<string, string> {
  return applyModuleConditionalCascade(moduleCode, row, flatContext, sectionNum, fields)
}
