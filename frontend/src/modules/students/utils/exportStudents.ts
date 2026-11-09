import { getStudents } from '../api/studentsApi'
import type { StudentFilterParams } from '../types/students'

function cell(value: unknown): string {
  let text = value == null ? '' : String(value)
  if (/^\s*[=+@-]/.test(text)) text = `'${text}`
  return `"${text.replace(/"/g, '""')}"`
}

export async function getStudentsCsv(filters: StudentFilterParams): Promise<string> {
  const rows: unknown[][] = [['Matrícula', 'Alumno', 'Carrera', 'Semestre', 'Avance académico (%)', 'Turno', 'Inscrito', 'Vigencia IMSS']]
  let page = 1
  let totalPages = 1
  do {
    const result = await getStudents({ ...filters, page, pageSize: 100 })
    totalPages = result.totalPages
    for (const student of result.items) {
      rows.push([student.controlNumber, student.fullName, student.careerCode, student.currentSemester,
        student.curriculumProgressPercentage,
        student.shift ? ({ morning: 'Matutino', afternoon: 'Vespertino', night: 'Nocturno' })[student.shift] : '',
        student.isEnrolled == null ? '' : student.isEnrolled ? 'Sí' : 'No',
        student.hasValidImss == null ? '' : student.hasValidImss ? 'Sí' : 'No'])
    }
    page++
  } while (page <= totalPages)
  return '\uFEFF' + rows.map(row => row.map(cell).join(',')).join('\r\n')
}
