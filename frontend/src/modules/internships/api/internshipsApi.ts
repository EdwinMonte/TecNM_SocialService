import { http } from '../../../shared/api/http'
import type {
  CreateInternshipPayload,
  InternshipDetail,
  InternshipEvaluation,
  InternshipEvaluationRequest,
  InternshipListItem,
  RegisterInternshipAcceptanceRequest,
} from '../types/internships'

export async function getInternships(params?: {
  page?: number
  pageSize?: number
  periodId?: number
  status?: string
  careerId?: number
  companyId?: number
  isExpedientComplete?: boolean
  search?: string
}): Promise<{ items: InternshipListItem[]; totalItems: number; page: number; pageSize: number }> {
  return (await http.get('/internships', { params })).data
}

export async function getMyInternshipList(): Promise<InternshipListItem[]> {
  return (await http.get<InternshipListItem[]>('/internships/me')).data
}

export async function getMyInternship(): Promise<InternshipDetail | null> {
  const list = await getMyInternshipList()
  if (!list || list.length === 0) return null
  return await getInternshipById(list[0].id)
}

export async function getInternshipById(id: number): Promise<InternshipDetail> {
  return (await http.get<InternshipDetail>(`/internships/${id}`)).data
}

export async function requestInternship(payload: CreateInternshipPayload): Promise<InternshipDetail> {
  return (await http.post<InternshipDetail>('/internships', payload)).data
}

export async function approveInternship(
  id: number,
  observations?: string | null,
): Promise<InternshipDetail> {
  return (await http.post<InternshipDetail>(`/internships/${id}/approve`, { observations })).data
}

export async function registerInternshipAcceptance(
  id: number,
  payload: RegisterInternshipAcceptanceRequest,
): Promise<InternshipDetail> {
  return (await http.post<InternshipDetail>(`/internships/${id}/acceptance`, payload)).data
}

export async function upsertInternshipEvaluation(
  id: number,
  payload: InternshipEvaluationRequest,
): Promise<InternshipEvaluation> {
  return (await http.put<InternshipEvaluation>(`/internships/${id}/evaluation`, payload)).data
}

export async function completeInternship(
  id: number,
  terminationLetterDate: string,
): Promise<InternshipDetail> {
  return (
    await http.post<InternshipDetail>(`/internships/${id}/complete`, {
      terminationLetterDate,
    })
  ).data
}

export async function releaseInternship(
  id: number,
  releaseDate?: string,
): Promise<InternshipDetail> {
  const today = releaseDate || new Date().toISOString().slice(0, 10)
  return (await http.post<InternshipDetail>(`/internships/${id}/release`, { releaseDate: today })).data
}

export async function sendInternshipSchoolAct(id: number): Promise<InternshipDetail> {
  return (await http.post<InternshipDetail>(`/internships/${id}/school-services-act`, {})).data
}

export async function getInternshipPrintData(
  id: number,
  document: 'presentation_letter' | 'evaluation' | 'release_certificate',
): Promise<Record<string, any>> {
  return (await http.get<Record<string, any>>(`/internships/${id}/print-data/${document}`)).data
}
