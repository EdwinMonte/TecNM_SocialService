import type { FieldErrors } from '../composables/useFormValidation'

export interface FieldRule { required?: boolean; max?: number; pattern?: RegExp; min?: number; maxNumber?: number; integer?: boolean; choices?: readonly string[]; message?: string }
export function validateFields(values: object, rules: Record<string, FieldRule>): FieldErrors {
  const errors: FieldErrors = {}
  for (const [key, rule] of Object.entries(rules)) {
    const raw = key.split('.').reduce<any>((value, part) => value?.[part], values)
    const text = typeof raw === 'string' ? raw.trim() : raw == null ? '' : String(raw)
    let message = ''
    if (rule.required && !text) message = 'Este campo es obligatorio.'
    else if (text) {
      if (rule.max !== undefined && text.length > rule.max) message = `Admite máximo ${rule.max} caracteres.`
      else if (rule.pattern && !rule.pattern.test(text)) message = rule.message || 'Revisa el formato de este campo.'
      else if (rule.choices && !rule.choices.includes(text)) message = 'Selecciona una opción válida.'
      else if ((rule.integer && !Number.isInteger(Number(raw))) || (rule.min !== undefined && Number(raw) < rule.min) || (rule.maxNumber !== undefined && Number(raw) > rule.maxNumber)) message = rule.message || 'Indica un número dentro del rango permitido.'
    }
    if (message) errors[key] = [message]
  }
  return errors
}
export const optionalEmail: FieldRule = { max: 255, pattern: /@/, message: 'El correo debe contener @.' }
export function dateOrder(errors: FieldErrors, values: any, start: string, end: string) {
  if (values[start] && values[end] && values[end] < values[start]) errors[end] = ['La fecha final debe ser mayor o igual a la inicial.']
  return errors
}
