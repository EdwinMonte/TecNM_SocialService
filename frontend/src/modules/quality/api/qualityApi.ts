import { http } from '../../../shared/api/http'
import type {
  CreateNonConformanceRequest,
  NonConformance,
  NonConformanceFilterParams,
  PagedResponse,
  UpdateNonConformanceRequest,
  UpdateNonConformanceStatusRequest,
} from '../types/quality'

export async function getNonConformances(
  params?: NonConformanceFilterParams,
): Promise<PagedResponse<NonConformance>> {
  return (await http.get<PagedResponse<NonConformance>>('/non-conformances', { params })).data
}

export async function getNonConformanceById(id: number): Promise<NonConformance> {
  return (await http.get<NonConformance>(`/non-conformances/${id}`)).data
}

export async function createNonConformance(
  request: CreateNonConformanceRequest,
): Promise<NonConformance> {
  return (await http.post<NonConformance>('/non-conformances', request)).data
}

export async function updateNonConformance(
  id: number,
  request: UpdateNonConformanceRequest,
): Promise<NonConformance> {
  return (await http.put<NonConformance>(`/non-conformances/${id}`, request)).data
}

export async function updateNonConformanceStatus(
  id: number,
  request: UpdateNonConformanceStatusRequest,
): Promise<NonConformance> {
  return (
    await http.post<NonConformance>(`/non-conformances/${id}/status`, request)
  ).data
}
