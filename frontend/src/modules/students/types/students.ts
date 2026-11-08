export type StudentShift = 'morning' | 'afternoon' | 'night'

export interface StudentListItem {
  id: number
  controlNumber: string
  fullName: string
  careerId: number
  careerCode: string
  currentSemester: number | null
  curriculumProgressPercentage: number | null
  shift: StudentShift | null
  isEnrolled: boolean | null
  hasValidImss: boolean | null
  canReadProfile?: boolean
  socialServiceFollowUp?: SocialServiceFollowUp | null
  hasUserAccount: boolean | null
  accountUsername: string | null
  accountStatus: 'missing' | 'active' | 'inactive' | 'inconsistent' | null
}

export interface StudentDetail {
  socialServiceEligiblePeriodId: number | null
  id: number
  userId: number | null
  accountUsername: string | null
  accountStatus: 'missing' | 'active' | 'inactive' | 'inconsistent'
  careerId: number
  careerName: string
  importBatchId: number | null
  controlNumber: string
  curp: string | null
  firstName: string
  firstSurname: string
  secondSurname: string | null
  gender: 'M' | 'F' | null
  shift: StudentShift
  hasDisability: boolean
  disabilityDescription: string | null
  institutionalEmail: string | null
  personalEmail: string | null
  phoneNumber: string | null
  currentSemester: number
  completedCredits: number
  curriculumProgressPercentage: number
  isEnrolled: boolean
  hasValidImss: boolean
  hasImssFile: boolean
  imssStatus: string
  imssDocumentId: number | null
  imssDocumentDate: string | null
  imssRejectionReason: string | null
  imssValidatedAt: string | null
  imssValidatedByName: string | null
  createdAt: string
  updatedAt: string
}

export type Student = StudentListItem

export interface StudentFilterParams {
  followUpStatus?: string
  followUpProgress?: string
  page?: number
  pageSize?: number
  search?: string
  careerId?: number
  shift?: string
  isEnrolled?: boolean
  hasValidImss?: boolean
  hasDisability?: boolean
  sort?: string
}

export interface PagedResponse<T> {
  items: T[]
  page: number
  pageSize: number
  totalItems: number
  totalPages: number
}

export interface StudentRequest {
  socialServiceEligiblePeriodId?: number | null
  careerId: number
  controlNumber: string
  curp?: string | null
  firstName: string
  firstSurname: string
  secondSurname?: string | null
  gender?: 'M' | 'F' | null
  shift: StudentShift
  hasDisability?: boolean
  disabilityDescription?: string | null
  institutionalEmail?: string | null
  personalEmail?: string | null
  phoneNumber?: string | null
  currentSemester: number
  completedCredits: number
  isEnrolled: boolean
}

export type CreateStudentRequest = StudentRequest

export interface StudentContactRequest {
  personalEmail?: string | null
  institutionalEmail?: string | null
  phoneNumber?: string | null
  currentSemester?: number | null
  shift?: StudentShift | null
}

export interface EligibilityReason {
  code: string
  message: string
}

export interface EligibilityResult {
  eligible: boolean
  reasons: EligibilityReason[]
}

export interface StudentEligibility {
  socialService: EligibilityResult
  internship: EligibilityResult
}

export interface CreateStudentAccountResponse {
  userId: number
  username: string
  accountStatus: string
}

export interface StudentImportRowError {
  row: number
  controlNumber?: string | null
  field?: string | null
  message: string
}

export interface StudentImportResult {
  id: number
  periodId: number
  periodCode: string
  fileName: string
  totalRecords: number
  createdAccounts: number
  existingAccounts: number
  importedRecords: number
  failedRecords: number
  status: string
  notes?: string | null
  createdAt: string
  createdByName?: string | null
  rowErrors: StudentImportRowError[]
}

export interface SocialServiceFollowUp {
 socialServiceId: number | null
 eligiblePeriodId: number | null
 eligiblePeriodCode: string | null
 trackingPeriodId: number | null
 trackingPeriodCode: string | null
 elapsedSemesters: number | null
 approvedDocumentsCount: number
 lastApprovedAt: string | null
 status: string
 progressAssessment: 'consistent' | 'low_progress' | null
 notes: string | null
 reviewedAt: string | null
}
