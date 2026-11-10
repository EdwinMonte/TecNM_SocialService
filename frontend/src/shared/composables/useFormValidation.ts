import { computed, nextTick, reactive, ref, useId } from 'vue'
import { problemFields, problemMessage } from '../api/problemDetails'

export type FieldErrors = Record<string, string[]>
export function useFormValidation(rules: () => FieldErrors = () => ({})) {
  const errors = reactive<FieldErrors>(Object.create(null))
  const general = ref('')
  const visited = new Set<string>()
  const serverKeys = new Set<string>()
  const fields = new Map<string, { id: string; label: string }>()
  const prefix = useId()
  function reset() { for (const key of Object.keys(errors)) delete errors[key]; general.value = ''; visited.clear(); serverKeys.clear() }
  function check(key: string) {
    visited.add(key)
    if (!serverKeys.has(key)) delete errors[key]
    const messages = rules()[key]
    if (messages?.length) errors[key] = messages
  }
  const entries = computed(() => Object.entries(errors).map(([key, messages]) => ({
    key, messages, id: fields.get(key)?.id, label: fields.get(key)?.label || 'Datos del formulario',
  })))
  async function focusFirst() {
    await nextTick()
    const first = entries.value.find(entry => entry.id)
    if (first?.id) document.getElementById(first.id)?.focus()
  }
  function validate() {
    reset()
    Object.assign(errors, rules())
    for (const key of Object.keys(errors)) visited.add(key)
    if (Object.keys(errors).length) { void focusFirst(); return false }
    return true
  }
  function apply(error: unknown, fallback: string) {
    for (const key of Object.keys(errors)) delete errors[key]
    Object.assign(errors, problemFields(error))
    serverKeys.clear()
    for (const key of Object.keys(errors)) serverKeys.add(key)
    general.value = Object.keys(errors).length ? '' : problemMessage(error, fallback)
    void focusFirst()
  }
  function field(key: string, label: string) {
    const id = fields.get(key)?.id || `${prefix}-${key.replace(/[^a-z0-9]/gi, '-')}`
    fields.set(key, { id, label })
    const changed = () => { serverKeys.delete(key); if (visited.has(key) || errors[key]) { delete errors[key]; check(key) } }
    return { id, error: errors[key], onBlur: () => check(key), onChange: changed, onInput: changed }
  }
  const hasErrors = computed(() => entries.value.length > 0 || !!general.value)
  return { errors, general, entries, hasErrors, field, reset, check, validate, apply, focusFirst }
}
