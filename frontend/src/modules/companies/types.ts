export const companyTypeLabels = {
  public: 'Sector Público', micro: 'Microempresas', small: 'Pequeñas empresas',
  medium: 'Medianas empresas', large: 'Grandes empresas', social: 'Sector Social',
} as const
export type CompanyType = keyof typeof companyTypeLabels
export interface Page<T> { items: T[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export interface CompanyRequest {
  name: string; rfc: string | null; commercialName: string | null
  companyType: CompanyType | ''; sector: string; companySize: string; isTecnmInternal: boolean
  address: string | null; city: string; state: string; postalCode: string | null
  phoneNumber: string | null; email: string | null
}
export interface Company extends CompanyRequest {
  id: number; isActive: boolean; activeSupervisorsCount: number; hasValidAgreement: boolean
}
