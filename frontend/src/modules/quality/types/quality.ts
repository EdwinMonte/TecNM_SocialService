export type NonConformanceStatus = 'open' | 'under_review' | 'resolved' | 'cancelled'

export type NonConformanceCategory =
  | 'desertion'
  | 'delayed_reports'
  | 'company_dismissal'
  | 'insufficient_grade'
  | 'invalid_documents'
  | 'other'

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface NonConformance {
  id: number
  incidentCode: string
  tramiteType: 'social_service' | 'internship'
  tramiteId: number
  studentId: number
  studentControlNumber: string
  studentFullName: string
  periodId: number
  periodCode: string
  category: NonConformanceCategory | string
  description: string
  status: NonConformanceStatus
  actionPlan: string | null
  resolvedAt: string | null
  resolvedByName: string | null
  createdAt: string
  updatedAt: string
}

export interface NonConformanceFilterParams {
  page?: number
  pageSize?: number
  periodId?: number
  tramiteType?: string
  status?: string
  category?: string
  search?: string
}

export interface CreateNonConformanceRequest {
  tramiteType: 'social_service' | 'internship'
  tramiteId: number
  category: string
  description: string
  actionPlan?: string | null
}

export interface UpdateNonConformanceRequest {
  category: string
  description: string
  actionPlan?: string | null
}

export interface UpdateNonConformanceStatusRequest {
  status: 'under_review' | 'resolved' | 'cancelled'
  actionPlan?: string | null
}
