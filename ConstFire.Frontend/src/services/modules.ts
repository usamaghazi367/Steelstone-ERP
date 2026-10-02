import { api } from './api'
import type { ModuleDetail, ModuleSummary, RecordItem, RecordListResponse } from '../types/modules'

export async function getModules(): Promise<ModuleSummary[]> {
  const { data } = await api.get<ModuleSummary[]>('/api/modules')
  return data
}

export async function getModule(code: string): Promise<ModuleDetail> {
  const { data } = await api.get<ModuleDetail>(`/api/modules/${code}`)
  return data
}

export async function getRecords(
  code: string,
  params: {
    search?: string
    sortBy?: string
    sortDir?: string
    page?: number
    pageSize?: number
    full?: boolean
  },
): Promise<RecordListResponse> {
  const { data } = await api.get<RecordListResponse>(`/api/modules/${code}/records`, { params })
  return data
}

export async function exportModuleRecords(code: string, search?: string): Promise<Blob> {
  const { data } = await api.get<Blob>(`/api/modules/${code}/records/export`, {
    params: search ? { search } : undefined,
    responseType: 'blob',
    timeout: 300000,
  })
  return data
}

export async function downloadRecordPdf(code: string, recordId: number): Promise<{ blob: Blob; fileName: string }> {
  const response = await api.get<Blob>(`/api/modules/${code}/records/${recordId}/pdf`, {
    responseType: 'blob',
    timeout: 120000,
  })
  const disposition = response.headers['content-disposition'] as string | undefined
  let fileName = `steelstone-record-${recordId}.pdf`
  if (disposition) {
    const match = /filename\*?=(?:UTF-8''|")?([^";]+)/i.exec(disposition)
    if (match?.[1]) fileName = decodeURIComponent(match[1].replace(/"/g, ''))
  }
  return { blob: response.data, fileName }
}

export async function getRecord(code: string, id: number): Promise<RecordItem> {
  const { data } = await api.get<RecordItem>(`/api/modules/${code}/records/${id}`)
  return data
}

export async function createRecord(code: string, recordData: Record<string, string> = {}): Promise<RecordItem> {
  const { data } = await api.post<RecordItem>(`/api/modules/${code}/records`, { data: recordData })
  return data
}

export async function saveSection(
  code: string,
  recordId: number,
  sectionNum: number,
  payload: { data?: Record<string, string>; rows?: Record<string, string>[]; markComplete?: boolean },
): Promise<RecordItem> {
  const { data } = await api.patch<RecordItem>(
    `/api/modules/${code}/records/${recordId}/sections/${sectionNum}`,
    {
      sectionNum,
      data: payload.data ?? {},
      rows: payload.rows,
      markComplete: payload.markComplete ?? true,
    },
  )
  return data
}

export async function updateRecord(
  code: string,
  id: number,
  recordData: Record<string, string>,
): Promise<RecordItem> {
  const { data } = await api.put<RecordItem>(`/api/modules/${code}/records/${id}`, { data: recordData })
  return data
}

export async function deleteRecord(code: string, id: number): Promise<void> {
  await api.delete(`/api/modules/${code}/records/${id}`)
}

export interface FieldOptionsResponse {
  fieldRef: string
  dataType: string
  options: { value: string; label: string }[]
  sourceModules?: string[]
}

export async function getFieldOptions(
  code: string,
  fieldRef: string,
  params: { search?: string; parentValue?: string; limit?: number },
): Promise<FieldOptionsResponse> {
  const { data } = await api.get<FieldOptionsResponse>(`/api/modules/${code}/fields/${fieldRef}/options`, {
    params,
  })
  return data
}
