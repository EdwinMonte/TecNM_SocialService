import { http } from '../../../shared/api/http'
import type {
  ActivityAssessment,
  ActivityAssessmentRequest,
  BiMonthlyReport,
  CommitmentRequest,
  CreateSocialServicePayload,
  EvaluationRequest,
  ReportRequest,
  SocialServiceDetail,
  SocialServiceEvaluation,
  SocialServiceListItem,
} from '../types/socialService'

export async function getSocialServices(params?: {
  page?: number
  pageSize?: number
  periodId?: number
  status?: string
  careerId?: number
  companyId?: number
  programId?: number
  search?: string
}): Promise<{ items: SocialServiceListItem[]; totalItems: number; page: number; pageSize: number }> {
  return (await http.get('/social-services', { params })).data
}

export async function getMySocialServiceList(): Promise<SocialServiceListItem[]> {
  return (await http.get<SocialServiceListItem[]>('/social-services/me')).data
}

export async function getMySocialService(): Promise<SocialServiceDetail | null> {
  const list = await getMySocialServiceList()
  if (!list || list.length === 0) return null
  return await getSocialServiceById(list[0].id)
}

export async function getSocialServiceById(id: number): Promise<SocialServiceDetail> {
  return (await http.get<SocialServiceDetail>(`/social-services/${id}`)).data
}

export async function requestSocialService(payload: CreateSocialServicePayload): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>('/social-services', payload)).data
}

export async function reviewSocialServiceApplication(
  id: number,
  payload: { isAccepted: boolean; rejectionReason?: string | null; officeObservations?: string | null },
): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>(`/social-services/${id}/application/review`, payload)).data
}

export async function registerSocialServiceAcceptance(
  id: number,
  acceptanceLetterDate: string,
): Promise<SocialServiceDetail> {
  return (
    await http.post<SocialServiceDetail>(`/social-services/${id}/acceptance`, {
      acceptanceLetterDate,
    })
  ).data
}

export async function upsertCommitment(
  id: number,
  payload: CommitmentRequest,
): Promise<SocialServiceDetail> {
  return (await http.put<SocialServiceDetail>(`/social-services/${id}/commitment`, payload)).data
}

export async function activateSocialService(id: number): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>(`/social-services/${id}/activate`, {})).data
}

export async function submitBimonthlyReport(
  id: number,
  reportNumber: number,
  payload: ReportRequest,
): Promise<BiMonthlyReport> {
  return (await http.put<BiMonthlyReport>(`/social-services/${id}/reports/${reportNumber}`, payload)).data
}

export async function upsertSocialServiceEvaluation(
  id: number,
  evaluatorType: 'external_supervisor' | 'student_self',
  bimonthlyPeriod: '1' | '2' | '3' | 'final',
  payload: EvaluationRequest,
): Promise<SocialServiceEvaluation> {
  return (
    await http.put<SocialServiceEvaluation>(
      `/social-services/${id}/evaluations/${evaluatorType}/${bimonthlyPeriod}`,
      payload,
    )
  ).data
}

export async function upsertActivityAssessment(
  id: number,
  payload: ActivityAssessmentRequest,
): Promise<ActivityAssessment> {
  return (await http.put<ActivityAssessment>(`/social-services/${id}/activity-assessment`, payload)).data
}

export async function completeSocialService(
  id: number,
  terminationLetterDate: string,
): Promise<SocialServiceDetail> {
  return (
    await http.post<SocialServiceDetail>(`/social-services/${id}/complete`, {
      terminationLetterDate,
    })
  ).data
}

export async function releaseSocialService(
  id: number,
  payload: {
    finalGradeLevel?: string | null
    finalScore?: number | null
    originalTerminationLetterReceived: boolean
  },
): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>(`/social-services/${id}/release`, payload)).data
}

export async function sendSocialServiceSchoolAct(id: number): Promise<SocialServiceDetail> {
  return (await http.post<SocialServiceDetail>(`/social-services/${id}/school-services-act`, {})).data
}

export async function getSocialServicePrintData(
  id: number,
  document: string,
): Promise<Record<string, any>> {
  return (await http.get<Record<string, any>>(`/social-services/${id}/print-data/${document}`)).data
}

export async function getAcceptanceLetterPrintData(id: number): Promise<Record<string, any>> {
  return getSocialServicePrintData(id, 'acceptance_letter')
}
