export interface ModuleSummary {
  code: string
  name: string
  category: string
}

export interface ModuleField {
  ref: string
  fieldName: string
  dataType: string
  mandatory: string
  validation: string
  sortOrder: number
  section?: number
}

export interface ModuleSection {
  num: number
  title: string
  repeating: boolean
}

export interface ModuleDetail extends ModuleSummary {
  fields: ModuleField[]
  listColumns: string[]
  sections?: ModuleSection[]
}

export interface RecordItem {
  id: number
  recordCode?: string
  data: Record<string, string>
  completedSections?: number[]
  createdAt: string
  updatedAt: string
}

export interface RecordListResponse {
  items: RecordItem[]
  totalCount: number
  page: number
  pageSize: number
}

export type ModuleAction = 'view' | 'add' | 'edit' | 'delete'
