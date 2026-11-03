import { http } from '../../../shared/api/http'
import type {
  AcademicPeriod,
  Career,
  CareerFilterParams,
  CareerRequest,
  OptionItem,
  PagedResponse,
  UpdateStatusRequest,
} from '../types/careers'

export async function getCareers(params?: CareerFilterParams): Promise<PagedResponse<Career>> {
  const queryParams: Record<string, string | number | boolean> = {}

  if (params?.page) queryParams.page = params.page
  if (params?.pageSize) queryParams.pageSize = params.pageSize
  if (params?.search) queryParams.search = params.search
  if (params?.isActive !== undefined && params?.isActive !== null) queryParams.isActive = params.isActive
  if (params?.sort) queryParams.sort = params.sort

  const response = await http.get<PagedResponse<Career>>('/careers', { params: queryParams })
  return response.data
}

export async function getCareerOptions(): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>('/careers/options')).data
}

export async function getPeriodOptions(): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>('/academic-periods/options')).data
}

export async function getCurrentPeriod(): Promise<AcademicPeriod> {
  return (await http.get<AcademicPeriod>('/academic-periods/current')).data
}

export async function getProgramOptions(): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>('/social-service-programs/options')).data
}

export async function getCompanyOptions(isTecnmInternal?: boolean): Promise<OptionItem[]> {
  const params = isTecnmInternal !== undefined ? { isTecnmInternal } : undefined
  return (await http.get<OptionItem[]>('/companies/options', { params })).data
}

export async function getSupervisorOptions(companyId: number): Promise<OptionItem[]> {
  return (await http.get<OptionItem[]>(`/companies/${companyId}/supervisors/options`)).data
}

export async function getAgreementOptions(companyId?: number): Promise<OptionItem[]> {
  const params = companyId ? { companyId } : undefined
  return (await http.get<OptionItem[]>('/agreements/options', { params })).data
}

export async function getCareerById(id: number): Promise<Career> {
  const response = await http.get<Career>(`/careers/${id}`)
  return response.data
}

export async function createCareer(request: CareerRequest): Promise<Career> {
  const response = await http.post<Career>('/careers', request)
  return response.data
}

export async function updateCareer(id: number, request: CareerRequest): Promise<Career> {
  const response = await http.put<Career>(`/careers/${id}`, request)
  return response.data
}

export async function updateCareerStatus(id: number, isActive: boolean): Promise<Career> {
  const payload: UpdateStatusRequest = { isActive }
  const response = await http.patch<Career>(`/careers/${id}/status`, payload)
  return response.data
}

export async function deleteCareer(id: number): Promise<void> {
  await http.delete(`/careers/${id}`)
}
