import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import XLSX from 'xlsx'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(__dirname, '..')
const xlsxPath = 'C:/Users/Engr Kamran Ghazi/Downloads/Steelstone IT (1).xlsx'
const dataDir = path.join(root, 'ConstFire.Backend/Data')
const schemaPath = path.join(dataDir, 'modules-schema.json')

const prefixes = {
  '01': 'ENT',
  '02': 'MFR',
  '03': 'DST',
  '04': 'CUS',
  '05': 'TRN',
  '06': 'VDR',
  '07': 'EMP',
  '08': 'BIL',
  '09': 'QOT',
  '10': 'PAY',
  '11': 'INV',
  '12': 'PRL',
  '13': 'ITM',
  '14': 'CHL',
  '15': 'RCT',
}

const wb = XLSX.readFile(xlsxPath)
const allModules = []

for (const sheetName of wb.SheetNames.filter((s) => s !== '00')) {
  const rows = XLSX.utils.sheet_to_json(wb.Sheets[sheetName], { header: 1, defval: '' })
  const sections = []
  const fields = []
  let sectionNum = 0
  let sectionTitle = ''

  for (const row of rows) {
    const c0 = String(row[0] ?? '').trim()
    const c1 = String(row[1] ?? '').trim()
    if (c0.startsWith('SECTION')) {
      const match = c0.match(/SECTION\s+(\d+)/i)
      sectionNum = match ? Number(match[1]) : sectionNum + 1
      sectionTitle = c1 || c0
      const repeating = /repeating block|repeating table|multiple options/i.test(`${c0} ${c1}`)
      sections.push({ num: sectionNum, title: sectionTitle, repeating })
      continue
    }
    if (!/^\d+\.\d+/.test(c0)) continue

    fields.push({
      ref: c0,
      section: sectionNum,
      field: c1,
      dataType: String(row[2] ?? '').trim(),
      mandatory: String(row[3] ?? '').trim(),
      validation: String(row[4] ?? '').trim(),
      sampleEntry: row[5] === undefined || row[5] === null ? '' : String(row[5]),
    })
  }

  const moduleName = String(rows[0]?.[0] ?? sheetName).trim()
  const category = String(rows[0]?.[1] ?? '').trim() || 'ERP Module'

  const sectionOneFields = fields
    .filter((f) => f.section === 1)
    .filter((f) => !/read only|formula|calculated|note -/i.test(f.dataType))
    .slice(0, 3)
    .map((f) => f.ref)

  const listColumns = ['_recordCode', ...sectionOneFields]

  const moduleConfig = {
    code: sheetName,
    name: moduleName,
    category,
    recordCodePrefix: prefixes[sheetName] ?? `MOD${sheetName}`,
    listColumns,
    sections,
    fields: fields.map(({ ref, field, dataType, mandatory, validation, sampleEntry, section }) => ({
      ref,
      field,
      dataType,
      mandatory,
      validation,
      sampleEntry,
      section,
    })),
  }

  if (sheetName === '01') {
    moduleConfig.fields = moduleConfig.fields.map((f) =>
      f.ref === '3.3' ? { ...f, dataType: 'Text (100)' } : f,
    )
    moduleConfig.listColumns = ['_recordCode', '1.4', '1.13', '1.21']
  }

  allModules.push({
    code: sheetName,
    name: moduleName,
    category,
    fields: moduleConfig.fields.map(({ ref, field, dataType, mandatory, validation, sampleEntry }) => ({
      ref,
      field,
      dataType,
      mandatory,
      validation,
      sampleEntry,
    })),
  })

  fs.writeFileSync(path.join(dataDir, `module-${sheetName}-config.json`), JSON.stringify(moduleConfig, null, 2))
  console.log(`module-${sheetName}-config.json: ${fields.length} fields, ${sections.length} sections`)
}

fs.writeFileSync(schemaPath, JSON.stringify(allModules, null, 2))
console.log(`Updated ${allModules.length} modules in modules-schema.json`)
