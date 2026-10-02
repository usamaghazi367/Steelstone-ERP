import XLSX from 'xlsx';
import { readFileSync, writeFileSync } from 'fs';
import { fileURLToPath } from 'url';
import { dirname, join } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));

const EXCEL_PATH = 'C:/Users/Engr Kamran Ghazi/Downloads/Steelstone IT (1).xlsx';
const SCHEMA_PATH = join(
  __dirname,
  '../ConstFire.Backend/Data/modules-schema.json',
);
const REPORT_PATH = join(__dirname, 'audit-report.txt');

/** @param {unknown} value */
function normalize(value) {
  if (value == null) return '';
  return String(value).replace(/\s+/g, ' ').trim();
}

/** @param {unknown} ref */
function normalizeRef(ref) {
  const s = normalize(ref);
  if (/^\d+\.\d+$/.test(s)) return s;
  const n = Number(ref);
  if (!Number.isFinite(n)) return s;
  const parts = String(n).split('.');
  if (parts.length === 2) return `${parts[0]}.${parts[1]}`;
  return s;
}

/** @param {string} ref */
function isFieldRef(ref) {
  return /^\d+\.\d+$/.test(ref);
}

/**
 * @param {unknown[][]} rows
 * @returns {Map<string, { ref: string, field: string, dataType: string, mandatory: string, validation: string }>}
 */
function parseModuleSheet(rows) {
  let headerIdx = -1;
  for (let i = 0; i < rows.length; i++) {
    const c0 = normalize(rows[i]?.[0]).toLowerCase();
    const c1 = normalize(rows[i]?.[1]).toLowerCase();
    if ((c0 === 'ref.' || c0 === 'ref') && c1.startsWith('field')) {
      headerIdx = i;
      break;
    }
  }
  if (headerIdx < 0) return new Map();

  /** @type {Map<string, { ref: string, field: string, dataType: string, mandatory: string, validation: string }>} */
  const fields = new Map();
  for (let i = headerIdx + 1; i < rows.length; i++) {
    const row = rows[i] ?? [];
    const ref = normalizeRef(row[0]);
    if (!isFieldRef(ref)) continue;
    fields.set(ref, {
      ref,
      field: normalize(row[1]),
      dataType: normalize(row[2]),
      mandatory: normalize(row[3]),
      validation: normalize(row[4]),
    });
  }
  return fields;
}

/**
 * @param {{ ref: string, field: string, dataType: string, mandatory: string, validation: string }} excel
 * @param {{ ref: string, field: string, dataType: string, mandatory: string, validation: string }} app
 */
function fieldDiffs(excel, app) {
  /** @type {string[]} */
  const diffs = [];
  if (excel.field !== app.field) diffs.push('field');
  if (excel.dataType !== app.dataType) diffs.push('dataType');
  if (excel.mandatory !== app.mandatory) diffs.push('mandatory');
  if (excel.validation !== app.validation) diffs.push('validation');
  return diffs;
}

function main() {
  const schema = JSON.parse(readFileSync(SCHEMA_PATH, 'utf8'));
  /** @type {Map<string, { code: string, name: string, fields: Map<string, object> }>} */
  const appModules = new Map();
  for (const mod of schema) {
    const code = normalize(mod.code);
    const fields = new Map();
    for (const f of mod.fields ?? []) {
      const ref = normalizeRef(f.ref);
      if (!isFieldRef(ref)) continue;
      fields.set(ref, {
        ref,
        field: normalize(f.field),
        dataType: normalize(f.dataType),
        mandatory: normalize(f.mandatory),
        validation: normalize(f.validation),
      });
    }
    appModules.set(code, { code, name: normalize(mod.name), fields });
  }

  const wb = XLSX.readFile(EXCEL_PATH);
  const sheetNames = wb.SheetNames.filter((n) => normalize(n) !== '00');

  const totals = {
    modulesCompared: 0,
    excelFields: 0,
    appFields: 0,
    missingInApp: 0,
    extraInApp: 0,
    fieldNameDiff: 0,
    dataTypeDiff: 0,
    mandatoryDiff: 0,
    validationDiff: 0,
    modulesMissingInApp: 0,
  };

  /** @type {Array<{ module: string, name: string, top: string[] }>} */
  const moduleSummaries = [];

  const lines = [];
  lines.push('Audit: Excel vs modules-schema.json');
  lines.push(`Excel: ${EXCEL_PATH}`);
  lines.push(`Schema: ${SCHEMA_PATH}`);
  lines.push(`Generated: ${new Date().toISOString()}`);
  lines.push('');

  for (const sheetName of sheetNames) {
    const code = normalize(sheetName).padStart(2, '0');
    const rows = XLSX.utils.sheet_to_json(wb.Sheets[sheetName], {
      header: 1,
      defval: '',
    });
    const excelFields = parseModuleSheet(rows);
    const appMod = appModules.get(code);

    lines.push('='.repeat(72));
    lines.push(`Module ${code} (sheet "${sheetName}")`);
    if (!appMod) {
      totals.modulesMissingInApp++;
      lines.push('  ERROR: No matching module in app schema.');
      lines.push(`  Excel field refs: ${excelFields.size}`);
      moduleSummaries.push({
        module: code,
        name: '(missing in app)',
        top: [`missing module in app; ${excelFields.size} excel refs`],
      });
      continue;
    }

    lines.push(`  App name: ${appMod.name}`);
    totals.modulesCompared++;
    totals.excelFields += excelFields.size;
    totals.appFields += appMod.fields.size;

    /** @type {string[]} */
    const missingInApp = [];
    /** @type {string[]} */
    const extraInApp = [];
    /** @type {string[]} */
    const fieldNameDiff = [];
    /** @type {string[]} */
    const dataTypeDiff = [];
    /** @type {string[]} */
    const mandatoryDiff = [];
    /** @type {string[]} */
    const validationDiff = [];

    for (const [ref, ex] of excelFields) {
      const ap = appMod.fields.get(ref);
      if (!ap) {
        missingInApp.push(ref);
        continue;
      }
      if (ex.field !== ap.field) {
        fieldNameDiff.push(
          `${ref}: excel="${ex.field}" | app="${ap.field}"`,
        );
      }
      if (ex.dataType !== ap.dataType) {
        dataTypeDiff.push(
          `${ref}: excel="${ex.dataType}" | app="${ap.dataType}"`,
        );
      }
      if (ex.mandatory !== ap.mandatory) {
        mandatoryDiff.push(
          `${ref}: excel="${ex.mandatory}" | app="${ap.mandatory}"`,
        );
      }
      if (ex.validation !== ap.validation) {
        validationDiff.push(`${ref} (validation differs)`);
      }
    }

    for (const ref of appMod.fields.keys()) {
      if (!excelFields.has(ref)) extraInApp.push(ref);
    }

    totals.missingInApp += missingInApp.length;
    totals.extraInApp += extraInApp.length;
    totals.fieldNameDiff += fieldNameDiff.length;
    totals.dataTypeDiff += dataTypeDiff.length;
    totals.mandatoryDiff += mandatoryDiff.length;
    totals.validationDiff += validationDiff.length;

    lines.push(
      `  Counts: excel=${excelFields.size} app=${appMod.fields.size} | missingInApp=${missingInApp.length} extraInApp=${extraInApp.length} fieldDiff=${fieldNameDiff.length} dataTypeDiff=${dataTypeDiff.length} mandatoryDiff=${mandatoryDiff.length} validationDiff=${validationDiff.length}`,
    );

    const emit = (label, items, limit = 50) => {
      if (!items.length) return;
      lines.push(`  ${label} (${items.length}):`);
      for (const item of items.slice(0, limit)) lines.push(`    - ${item}`);
      if (items.length > limit) {
        lines.push(`    ... and ${items.length - limit} more`);
      }
    };

    emit('Missing in app', missingInApp);
    emit('Extra in app', extraInApp);
    emit('Field name mismatch', fieldNameDiff);
    emit('Data type mismatch', dataTypeDiff);
    emit('Mandatory mismatch', mandatoryDiff);
    emit('Validation mismatch', validationDiff, 30);

    /** @type {string[]} */
    const top = [];
    const pushTop = (label, n, sample) => {
      if (n > 0) top.push(`${label}: ${n}${sample ? ` e.g. ${sample}` : ''}`);
    };
    pushTop('missingInApp', missingInApp.length, missingInApp.slice(0, 3).join(', '));
    pushTop('extraInApp', extraInApp.length, extraInApp.slice(0, 3).join(', '));
    pushTop('fieldName', fieldNameDiff.length, fieldNameDiff[0]?.split(':')[0]);
    pushTop('dataType', dataTypeDiff.length, dataTypeDiff[0]?.split(':')[0]);
    pushTop('mandatory', mandatoryDiff.length, mandatoryDiff[0]?.split(':')[0]);
    pushTop('validation', validationDiff.length, validationDiff[0]?.split(':')[0]);

    moduleSummaries.push({
      module: code,
      name: appMod.name,
      top: top.slice(0, 6),
    });
  }

  const appOnlyCodes = [...appModules.keys()].filter(
    (c) => !sheetNames.some((s) => normalize(s).padStart(2, '0') === c),
  );
  if (appOnlyCodes.length) {
    lines.push('');
    lines.push('Modules in app with no Excel sheet:');
    for (const c of appOnlyCodes) {
      lines.push(`  - ${c} ${appModules.get(c)?.name ?? ''}`);
    }
  }

  lines.push('');
  lines.push('='.repeat(72));
  lines.push('TOTALS');
  lines.push(`  modulesCompared: ${totals.modulesCompared}`);
  lines.push(`  excelFields (module sheets): ${totals.excelFields}`);
  lines.push(`  appFields (matched modules): ${totals.appFields}`);
  lines.push(`  missingInApp: ${totals.missingInApp}`);
  lines.push(`  extraInApp: ${totals.extraInApp}`);
  lines.push(`  fieldNameDiff: ${totals.fieldNameDiff}`);
  lines.push(`  dataTypeDiff: ${totals.dataTypeDiff}`);
  lines.push(`  mandatoryDiff: ${totals.mandatoryDiff}`);
  lines.push(`  validationDiff: ${totals.validationDiff}`);
  lines.push('');
  lines.push('TOP MISMATCHES PER MODULE (short)');
  for (const m of moduleSummaries) {
    lines.push(`  ${m.module} ${m.name}`);
    if (!m.top.length) lines.push('    (no mismatches)');
    else for (const t of m.top) lines.push(`    - ${t}`);
  }

  writeFileSync(REPORT_PATH, lines.join('\n'), 'utf8');
  console.log(JSON.stringify({ totals, moduleSummaries }, null, 2));
  console.log(`Report written: ${REPORT_PATH}`);
}

main();
