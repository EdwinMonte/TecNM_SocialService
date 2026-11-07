import { http } from '../../shared/api/http'
import type { Company, CompanyRequest, Page } from './types'

export async function getCompanies(params: { page: number; search?: string; companyType?: string; isActive?: boolean }, signal?: AbortSignal) {
  return (await http.get<Page<Company>>('/companies', { params, signal })).data
}
export async function saveCompany(id: number | null, payload: CompanyRequest) {
  return id ? (await http.put<Company>(`/companies/${id}`, payload)).data : (await http.post<Company>('/companies', payload)).data
}
export async function setCompanyStatus(id: number, isActive: boolean) { await http.patch(`/companies/${id}/status`, { isActive }) }
export async function deleteCompany(id: number) { await http.delete(`/companies/${id}`) }
