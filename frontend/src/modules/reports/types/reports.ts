export interface DashboardMetrics {
  period: {
    id: number
    code: string
    name?: string
  }
  socialService: Record<string, number>
  internships: Record<string, number>
  pendingDocumentReviews: number
  agreementsExpiringSoon: number
  openNonConformances: number
}

export interface TrackingDocumentsChecklist {
  cp: boolean
  ca: boolean
  sol?: boolean
  cc?: boolean
  r1?: boolean
  r2?: boolean
  r3?: boolean
  ev1?: boolean
  ev2?: boolean
  ev3?: boolean
  ev?: boolean
  ct: boolean
}

export interface TrackingItem {
  id: number
  tramiteId: number
  controlNumber: string
  studentFullName: string
  careerCode: string
  careerName: string
  semesterEnrolled: number
  gender?: string | null
  shift?: string
  phoneNumber?: string | null
  companyName?: string | null
  companySector?: string | null
  supervisorName?: string | null
  agreementNumber?: string | null
  programName?: string | null
  startDate?: string | null
  endDate?: string | null
  status: string
  progressPercentage: number
  documents?: TrackingDocumentsChecklist
  presentationLetterFolio?: string | null
  releaseCertificateFolio?: string | null
  originalTerminationLetterReceived?: boolean
  finalGradeLevel?: string | null
  isPnc: boolean
  observations?: string | null
  schoolServicesActFolio?: string | null
  beneficiariesCount?: number | null
  hasInclusionActivities?: boolean | null
  targetCommunity?: string | null
}

export interface TrackingResponse {
  items: TrackingItem[]
}
