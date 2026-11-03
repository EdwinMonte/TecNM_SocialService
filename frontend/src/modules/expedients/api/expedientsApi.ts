import { http } from '../../../shared/api/http'
import type { ExpedientDocument, ReviewDocumentRequest, ReviewQueueItem } from '../types/expedients'

export async function getStudentDocuments(
  studentId: number,
  params?: { tramiteType?: string; tramiteId?: number; status?: string },
): Promise<ExpedientDocument[]> {
  return (await http.get<ExpedientDocument[]>(`/students/${studentId}/documents`, { params })).data
}

export async function getMyDocuments(params?: {
  tramiteType?: string
  tramiteId?: number
  status?: string
}): Promise<ExpedientDocument[]> {
  return (await http.get<ExpedientDocument[]>('/students/me/documents', { params })).data
}

export async function uploadMyDocument(payload: {
  file: File
  tramiteType: 'social_service' | 'internship' | 'general'
  tramiteId?: number | null
  documentCode: string
}): Promise<ExpedientDocument> {
  const formData = new FormData()
  formData.append('File', payload.file)
  formData.append('TramiteType', payload.tramiteType)
  if (payload.tramiteId) formData.append('TramiteId', String(payload.tramiteId))
  formData.append('DocumentCode', payload.documentCode)

  return (
    await http.post<ExpedientDocument>('/students/me/documents', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  ).data
}

export async function uploadStudentDocument(
  studentId: number,
  formData: FormData,
): Promise<ExpedientDocument> {
  return (
    await http.post<ExpedientDocument>(`/students/${studentId}/documents`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  ).data
}

export async function getDocumentBlob(documentId: number): Promise<Blob> {
  const response = await http.get(`/documents/${documentId}/file`, { responseType: 'blob' })
  return response.data
}

export async function getReviewQueue(
  status?: string,
  params?: { page?: number; pageSize?: number; tramiteType?: string; documentCode?: string; search?: string },
): Promise<ReviewQueueItem[]> {
  const res = await http.get<{ items: ReviewQueueItem[]; totalItems: number }>('/documents/review-queue', {
    params: {
      page: params?.page ?? 1,
      pageSize: params?.pageSize ?? 100,
      status: status || undefined,
      tramiteType: params?.tramiteType,
      documentCode: params?.documentCode,
      search: params?.search,
    },
  })
  return Array.isArray(res.data) ? res.data : (res.data.items ?? [])
}

export async function reviewDocument(
  documentId: number,
  request: ReviewDocumentRequest,
): Promise<ExpedientDocument> {
  return (await http.post<ExpedientDocument>(`/documents/${documentId}/review`, request)).data
}

export async function deleteDocument(documentId: number): Promise<void> {
  await http.delete(`/documents/${documentId}`)
}
