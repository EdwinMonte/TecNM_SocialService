export type ExpedientDocumentStatus = 'pending' | 'approved' | 'rejected' | 'superseded'

export interface ExpedientDocument {
  id: number
  studentId: number
  tramiteType: 'social_service' | 'internship' | 'general'
  tramiteId?: number | null
  documentCode: string
  originalFileName: string
  fileMimeType: string
  fileSizeBytes: number
  status: ExpedientDocumentStatus
  rejectionReason?: string
  reviewedByName?: string
  reviewedAt?: string
  documentDate?: string | null
  createdAt: string
}

export interface ReviewQueueItem extends ExpedientDocument {
  studentControlNumber: string
  studentFullName: string
  careerCode: string
  careerName: string
}

export interface ReviewDocumentRequest {
  status: 'approved' | 'rejected'
  documentDate?: string
  rejectionReason?: string
}


export interface DocumentRequirement { code: string; title: string; pdfKey: string | null; templateKey: string | null; canUpload: boolean; blockedReason: string | null }

export interface ImssDateSuggestion { status: "found" | "ambiguous" | "not_found" | "unavailable"; suggestedDate: string | null; pageNumber: number | null }
