export function problemFields(error: any): Record<string, string[]> {
  const raw = error?.response?.data?.errors
  if (!raw || typeof raw !== 'object' || Array.isArray(raw)) return {}
  const fields: Record<string, string[]> = Object.create(null)
  for (const [key, value] of Object.entries(raw)) {
    const path = key.replace(/^\$\.?/, '').split('.').map(part => part.charAt(0).toLowerCase() + part.slice(1)).join('.')
    const messages = (Array.isArray(value) ? value : [value]).filter((v): v is string => typeof v === 'string' && !!v.trim())
    if (messages.length) fields[path] = [...new Set([...(fields[path] || []), ...messages])]
  }
  return fields
}
export function problemMessage(error: any, fallback: string): string {
  const problem = error?.response?.data
  const fields = Object.values(problemFields(error)).flat()
  return fields.length ? fields.join(' ') : problem?.detail || problem?.message || fallback
}
