import { http } from '../../../shared/api/http'
import type { DashboardMetrics, TrackingItem, TrackingResponse } from '../types/reports'

export async function getDashboardMetrics(periodId?: number): Promise<DashboardMetrics> {
  return (
    await http.get<DashboardMetrics>('/reports/dashboard', {
      params: periodId ? { periodId } : undefined,
    })
  ).data
}

export async function getTrackingData(
  periodId?: number,
  tramiteType: 'social_service' | 'internship' = 'social_service',
  careerId?: number,
  search?: string,
): Promise<TrackingResponse> {
  const res = await http.get<TrackingItem[] | TrackingResponse>('/reports/tracking', {
    params: {
      periodId: periodId || undefined,
      tramiteType,
      careerId: careerId || undefined,
      search: search || undefined,
    },
  })
  const items = Array.isArray(res.data) ? res.data : (res.data.items ?? [])
  return { items }
}

export async function getSemesterStatistics(
  periodId?: number,
  tramiteType: 'social_service' | 'internship' = 'social_service',
  comparePeriodId?: number,
): Promise<any> {
  return (
    await http.get('/reports/statistics', {
      params: {
        periodId: periodId || undefined,
        tramiteType,
        comparePeriodId: comparePeriodId || undefined,
      },
    })
  ).data
}

export async function get911Report(
  periodId?: number,
  tramiteType: 'social_service' | 'internship' = 'social_service',
  comparePeriodId?: number,
): Promise<any> {
  return (
    await http.get('/reports/911', {
      params: {
        periodId: periodId || undefined,
        tramiteType,
        comparePeriodId: comparePeriodId || undefined,
      },
    })
  ).data
}
