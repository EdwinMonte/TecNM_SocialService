import { http } from '../../../shared/api/http'
import type {
  CreateStudentAccountResponse,
  PagedResponse,
  StudentContactRequest,
  StudentDetail,
  StudentEligibility,
  StudentFilterParams,
  StudentImportResult,
  StudentListItem,
  StudentRequest,
} from '../types/students'

export async function getStudents(params?: StudentFilterParams): Promise<PagedResponse<StudentListItem>> {
  return (await http.get<PagedResponse<StudentListItem>>('/students', { params })).data
}

export async function getStudentById(id: number): Promise<StudentDetail> {
  return (await http.get<StudentDetail>(`/students/${id}`)).data
}

export async function getMyStudentProfile(): Promise<StudentDetail> {
  return (await http.get<StudentDetail>('/students/me')).data
}

export async function getMyEligibility(): Promise<StudentEligibility> {
  return (await http.get<StudentEligibility>('/students/me/eligibility')).data
}

export async function updateMyContact(data: StudentContactRequest): Promise<StudentDetail> {
  return (await http.put<StudentDetail>('/students/me/contact', data)).data
}

export async function uploadMyImssFile(file: File, documentDate: string): Promise<StudentDetail> {
  if (!documentDate) throw new Error('Indica la fecha de expedición de la constancia IMSS.')
  const formData = new FormData()
  formData.append('file', file)
  formData.append('documentDate', documentDate)
  return (
    await http.put<StudentDetail>('/students/me/imss', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  ).data
}

export async function getMyImssBlob(): Promise<Blob> {
  const response = await http.get('/students/me/imss', { responseType: 'blob' })
  return response.data
}

export async function createStudent(data: StudentRequest): Promise<StudentDetail> {
  return (await http.post<StudentDetail>('/students', data)).data
}

export async function updateStudent(id: number, data: StudentRequest): Promise<StudentDetail> {
  return (await http.put<StudentDetail>(`/students/${id}`, data)).data
}

export async function deleteStudent(id: number): Promise<void> {
  await http.delete(`/students/${id}`)
}

export async function createStudentAccount(id: number): Promise<CreateStudentAccountResponse> {
  return (await http.post<CreateStudentAccountResponse>(`/students/${id}/account`)).data
}

export async function uploadStudentImssFile(id: number, file: File, documentDate: string): Promise<StudentDetail> {
  if (!documentDate) throw new Error('Indica la fecha de expedición de la constancia IMSS.')
  const formData = new FormData()
  formData.append('file', file)
  formData.append('documentDate', documentDate)
  return (
    await http.put<StudentDetail>(`/students/${id}/imss`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  ).data
}

export async function getStudentImssBlob(id: number): Promise<Blob> {
  const response = await http.get(`/students/${id}/imss`, { responseType: 'blob' })
  return response.data
}

export async function validateStudentImss(id: number, isValid: boolean, documentDate?: string, rejectionReason?: string): Promise<StudentDetail> {
  return (await http.post<StudentDetail>(`/students/${id}/imss/validation`, { isValid, documentDate, rejectionReason })).data
}

export async function downloadImportTemplate(): Promise<Blob> {
  const response = await http.get('/student-imports/template', { responseType: 'blob' })
  return response.data
}

export async function uploadStudentsExcel(periodId: number, file: File): Promise<StudentImportResult> {
  const formData = new FormData()
  formData.append('periodId', String(periodId))
  formData.append('file', file)
  return (
    await http.post<StudentImportResult>('/student-imports', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
  ).data
}
