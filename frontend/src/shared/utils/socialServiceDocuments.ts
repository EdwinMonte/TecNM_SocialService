import source from '../../../../backend/Modules/Expedients/social-service-documents.json'

const catalog: { number: string; title: string; codes: string[]; trackingKey: string | null }[] = source

export const socialServiceDocuments = catalog
export function socialDocument(code: string) {
  return catalog.find(item => item.codes.includes(code))
}
export function socialDocumentLabel(code: string): string {
  const item = socialDocument(code)
  return item ? `${item.number}${item.number.includes('.') ? '' : '.'} ${item.title}` : code
}
export function socialDocumentOrder(code: string): number {
  const index = catalog.findIndex(item => item.codes.includes(code))
  return index < 0 ? catalog.length : index
}
export function socialDocumentPeriod(code: string): string {
  return /^EV_(SUP|SELF)[123]$/.test(code) ? `Bimestre ${code.slice(-1)}`
    : /^EV[123]$/.test(code) ? `Paquete histórico · Bimestre ${code.slice(-1)}` : ''
}
