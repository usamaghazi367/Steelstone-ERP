import fs from 'fs'
import path from 'path'
import { fileURLToPath } from 'url'
import XLSX from 'xlsx'

const __dirname = path.dirname(fileURLToPath(import.meta.url))
const root = path.resolve(__dirname, '..')
const xlsxPath = 'C:/Users/Engr Kamran Ghazi/Downloads/Steelstone IT (1).xlsx'
const schemaPath = path.join(root, 'ConstFire.Backend/Data/modules-schema.json')
const configPath = path.join(root, 'ConstFire.Backend/Data/module-01-config.json')

const wb = XLSX.readFile(xlsxPath)
const rows = XLSX.utils.sheet_to_json(wb.Sheets['01'], { header: 1, defval: '' })

const sections = []
const fields = []
let sectionNum = 0
let sectionTitle = ''
let sectionRepeating = false

for (const row of rows) {
  const c0 = String(row[0] ?? '').trim()
  const c1 = String(row[1] ?? '').trim()
  if (c0.startsWith('SECTION')) {
    const match = c0.match(/SECTION\s+(\d+)/i)
    sectionNum = match ? Number(match[1]) : sectionNum + 1
    sectionTitle = c1 || c0
    sectionRepeating = /repeating block|repeating table|multiple options/i.test(`${c0} ${c1}`)
    sections.push({ num: sectionNum, title: sectionTitle, repeating: sectionRepeating })
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

const module01 = {
  code: '01',
  name: 'ENTERPRISE REGISTRATION',
  category: 'Enterprise Onboarding',
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

const allModules = JSON.parse(fs.readFileSync(schemaPath, 'utf8'))
const idx = allModules.findIndex((m) => m.code === '01')
if (idx >= 0) allModules[idx] = module01
else allModules.unshift(module01)

fs.writeFileSync(schemaPath, JSON.stringify(allModules, null, 2))
fs.writeFileSync(configPath, JSON.stringify(module01, null, 2))
console.log(`Updated module 01: ${fields.length} fields, ${sections.length} sections`)
