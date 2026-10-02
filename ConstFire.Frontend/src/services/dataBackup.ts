import { api } from './api'

export async function exportDataBackup(): Promise<Blob> {
  const { data } = await api.get<Blob>('/api/data/export', {
    responseType: 'blob',
    timeout: 300000,
  })
  return data
}

export async function exportFormattedWorkbook(): Promise<Blob> {
  const { data } = await api.get<Blob>('/api/data/export-formatted', {
    responseType: 'blob',
    timeout: 300000,
  })
  return data
}

export async function importDataBackup(file: File): Promise<{ recordsImported: number; fieldOptionsImported: number; message: string }> {
  const form = new FormData()
  form.append('file', file)
  const { data } = await api.post('/api/data/import', form, {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}
