export function problemMessage(error: any, fallback: string): string {
  const problem = error?.response?.data
  const fields = problem?.errors && typeof problem.errors === 'object'
    ? Object.values(problem.errors).flat().filter(value => typeof value === 'string').join(' ')
    : ''
  return [problem?.detail || problem?.message || fallback, fields, problem?.code ? `(${problem.code})` : '']
    .filter(Boolean).join(' ')
}
