<script setup lang="ts">
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { validateFields } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import DocumentReviewDialog from '../../expedients/components/DocumentReviewDialog.vue'
import { getStudentDocuments } from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import { problemMessage } from '../../../shared/api/problemDetails'
import { hasRole } from '../../../shared/composables/useSession'
import { getStudentsCsv } from '../utils/exportStudents'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiTable,
} from '../../../shared/components'
import { getCareerOptions, getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import {
  reviewSocialServiceFollowUp,
  createStudent,
  createStudentAccount,
  downloadImportTemplate,
  getStudentById,
  getStudentImssBlob,
  getStudents,
  updateStudent,
  uploadStudentsExcel,
  resetStudentPassword,
} from '../api/studentsApi'
import type {
  CreateStudentAccountResponse,
  StudentDetail,
  StudentImportResult,
  StudentListItem,
  StudentRequest,
  StudentShift,
} from '../types/students'

const canFollowUp = computed(() => hasRole('admin', 'social_service_officer', 'department_head'))
const canAssess = computed(() => hasRole('admin', 'social_service_officer'))
const followUpStatus = ref(''), followUpProgress = ref('')
const followUpLabels: Record<string, string> = { first_notice: 'Primer aviso', second_notice: 'Segundo aviso', third_notice: 'Tercer aviso', non_compliant: 'Producto no conforme', in_progress: 'En proceso de realización del servicio', released: 'Liberado', pending_setup: 'Periodo de aptitud pendiente' }
const assessmentLabels: Record<string, string> = {consistent: 'Constante', low_progress: 'Con poco avance', pending: 'Pendiente de valoración'}
const assessmentCandidate = ref<StudentListItem | null>(null)
const assessmentOpen = ref(false), savingAssessment = ref(false)
const assessment = ref('consistent'), assessmentNotes = ref(''), assessmentError = ref('')
function openAssessment(row: StudentListItem) {
 assessmentCandidate.value = row; assessment.value = row.socialServiceFollowUp?.progressAssessment || 'consistent'; assessmentNotes.value = row.socialServiceFollowUp?.notes || ''; assessmentError.value = ''; assessmentOpen.value = true
}
async function saveAssessment() {
 if (savingAssessment.value || !assessmentValidation.validate()) return
 const row = assessmentCandidate.value, periodId = row?.socialServiceFollowUp?.trackingPeriodId
 if (!row || !periodId || savingAssessment.value) return
 savingAssessment.value = true; assessmentError.value = ''
 try { await reviewSocialServiceFollowUp(row.id, {periodId, assessment: assessment.value, notes: assessmentNotes.value}); assessmentOpen.value = false; await loadData() }
 catch (error) { assessmentValidation.apply(error, 'No se pudo guardar la valoración.'); assessmentError.value = problemMessage(error, 'No se pudo guardar la valoración.') }
 finally {savingAssessment.value = false}
}
const canCreate = computed(() => hasRole('admin', 'social_service_officer', 'internship_coordinator', 'department_head'))
const isExporting = ref(false)
const isDownloadingTemplate = ref(false)
const canRegister = computed(() => hasRole('admin'))
const canReview = computed(() => hasRole('admin', 'social_service_officer', 'internship_coordinator'))

interface StudentRow extends StudentListItem {
  actions?: string
}

const students = ref<StudentRow[]>([])
const careerOptions = ref<OptionItem[]>([])
const periodOptions = ref<OptionItem[]>([])

const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')

// Filtros
const searchQuery = ref('')
const selectedCareerId = ref<number | string>('')
const selectedShift = ref<string>('')
const selectedImssFilter = ref<string>('')
const currentPage = ref(1)
const totalItems = ref(0)

// Autocomplete
let debounceTimer: ReturnType<typeof setTimeout> | null = null

const columns: { key: keyof StudentRow & string; label: string }[] = [
  { key: 'fullName', label: 'Alumno' },
  { key: 'controlNumber', label: 'Matrícula' },
  { key: 'careerCode', label: 'Carrera / semestre' },
  ...(hasRole('admin', 'social_service_officer', 'department_head') ? [{ key: 'socialServiceFollowUp' as const, label: 'Seguimiento SS' }] : []),
  { key: 'actions', label: 'Detalle' },
]

// Modal de Alta / Edición de Alumno (Caso Específico)
const isStudentModalOpen = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)
const isSubmittingStudent = ref(false)
const selectedDetail = ref<StudentDetail | null>(null)
const imssError = ref('')
const reviewOpen = ref(false)
const reviewDocument = ref<ExpedientDocument | null>(null)
const reviewStudentName = ref('')
const resetCandidate = ref<StudentListItem | null>(null)
const resetOpen = ref(false), resetting = ref(false)
const resetError = ref('')
const generatedControlNumber = ref('')

function openDocumentReview(document: ExpedientDocument, name: string) {
  reviewDocument.value = document
  reviewStudentName.value = name
  isStudentModalOpen.value = false
  detailOpen.value = false
  reviewOpen.value = true
}
async function reviewStudentImss() {
  if (!selectedDetail.value) return
  imssError.value = ''
  try {
    const docs = await getStudentDocuments(selectedDetail.value.id, { tramiteType: 'general' })
    const document = docs.find(d => d.documentCode === 'IMSS' && d.status !== 'superseded')
    if (!document) { imssError.value = 'El alumno debe cargar una constancia actual en su portal. Un archivo histórico no acredita revisión documental.'; return }
    openDocumentReview(document, `${selectedDetail.value.firstName} ${selectedDetail.value.firstSurname}`)
  } catch (error) { imssError.value = problemMessage(error, 'No se pudo consultar la constancia IMSS.') }
}
async function afterReview() { await loadData() }
async function confirmReset() {
  if (!resetCandidate.value || resetting.value) return
  resetting.value = true; resetError.value = ''
  try {
    await resetStudentPassword(resetCandidate.value.id)
    successMessage.value = 'Contraseña restablecida a la matrícula. Las sesiones anteriores fueron invalidadas.'
    resetOpen.value = false
  } catch (error) { resetError.value = problemMessage(error, 'No se pudo restablecer la contraseña.') }
  finally { resetting.value = false }
}

const detailOpen = ref(false)
const detailRow = ref<StudentListItem | null>(null)
const detailLoading = ref(false)
const detailError = ref('')
let detailRequest = 0
async function openDetail(row: StudentListItem) {
  const request = ++detailRequest
  detailRow.value = row
  selectedDetail.value = null
  detailError.value = ''; imssError.value = ''
  detailOpen.value = true
  detailLoading.value = row.canReadProfile !== false
  if (row.canReadProfile === false) return
  try {
    const result = await getStudentById(row.id)
    if (request === detailRequest) selectedDetail.value = result
  } catch (error) {
    if (request === detailRequest) detailError.value = problemMessage(error, 'No se pudo cargar el detalle del alumno.')
  } finally { if (request === detailRequest) detailLoading.value = false }
}
function displayDate(value: string | null | undefined) {
  return value ? new Date(value).toLocaleString('es-MX', { timeZone: 'America/Mexico_City' }) : 'Sin registro'
}
function followUpTone(row: StudentListItem): 'success' | 'warning' | 'danger' | 'info' {
  const status = row.socialServiceFollowUp?.status
  if (status === 'non_compliant' || status === 'third_notice') return 'danger'
  if (status === 'released' || row.socialServiceFollowUp?.progressAssessment === 'consistent') return 'success'
  return status === 'second_notice' || row.socialServiceFollowUp?.progressAssessment === 'low_progress' ? 'warning' : 'info'
}
const detailSections = computed(() => {
  const d = selectedDetail.value
  if (!d) return []
  const accountLabels = {missing: 'Sin cuenta', active: 'Activa', inactive: 'Inactiva', inconsistent: 'Revisar cuenta'}
  return [
    { title: 'Datos personales', fields: [['Nombre', d.firstName], ['Primer apellido', d.firstSurname], ['Segundo apellido', d.secondSurname], ['CURP', d.curp], ['Sexo', d.gender === 'M' ? 'Hombre' : d.gender === 'F' ? 'Mujer' : null], ['Discapacidad', d.hasDisability ? d.disabilityDescription || 'Sí' : 'No']] },
    { title: 'Datos académicos', fields: [['Carrera', d.careerName], ['Semestre', String(d.currentSemester)], ['Turno', formatShift(d.shift)], ['Créditos aprobados', String(d.completedCredits)], ['Avance reticular', d.curriculumProgressPercentage + '%'], ['Inscripción', d.isEnrolled ? 'Inscrito' : 'No inscrito']] },
    { title: 'Contacto y cuenta', fields: [['Correo institucional', d.institutionalEmail], ['Correo personal', d.personalEmail], ['Teléfono', d.phoneNumber], ['Cuenta de acceso', accountLabels[d.accountStatus]], ['Usuario', d.accountUsername]] },
    { title: 'Registro', fields: [['Alta', displayDate(d.createdAt)], ['Última actualización', displayDate(d.updatedAt)], ['Lote de importación', d.importBatchId ? String(d.importBatchId) : 'Alta individual']] },
  ]
})
function applyFilters() { currentPage.value = 1; void loadData() }


const studentForm = reactive<StudentRequest>({
  careerId: 0,
  controlNumber: '',
  curp: '',
  firstName: '',
  firstSurname: '',
  secondSurname: '',
  gender: 'M',
  shift: 'morning',
  hasDisability: false,
  disabilityDescription: '',
  institutionalEmail: '',
  personalEmail: '',
  phoneNumber: '',
  currentSemester: 6,
  completedCredits: 190,
  isEnrolled: true,
})

// Modal de Importación por Excel
const isImportModalOpen = ref(false)
const importPeriodId = ref<number>(0)
const importFile = ref<File | null>(null)
const isImporting = ref(false)
const importResult = ref<StudentImportResult | null>(null)

// Modal de Credenciales Generadas
const isAccountModalOpen = ref(false)
const generatedAccount = ref<CreateStudentAccountResponse | null>(null)

// Autocomplete Suggestions

const hasActiveFilters = computed(() => {
  return (
    searchQuery.value.trim() !== '' ||
    Boolean(selectedCareerId.value) ||
    selectedShift.value !== '' ||
    selectedImssFilter.value !== '' || followUpStatus.value !== '' || followUpProgress.value !== ''
  )
})

function formatShift(shift: StudentShift | string | null): string {
  if (!shift) return "—"
  if (shift === 'morning') return 'Matutino'
  if (shift === 'afternoon') return 'Vespertino'
  if (shift === 'night') return 'Nocturno'
  return shift
}

async function loadCatalogs(): Promise<void> {
  try {
    const [careers, periods] = await Promise.all([
      getCareerOptions().catch(() => []),
      getPeriodOptions().catch(() => []),
    ])
    careerOptions.value = careers
    periodOptions.value = periods
    if (careers.length > 0 && !studentForm.careerId) {
      studentForm.careerId = careers[0].id
    }
    if (periods.length > 0 && !importPeriodId.value) {
      importPeriodId.value = periods[0].id
    }
  } catch {
    // silencioso
  }
}

let listRequest = 0
async function loadData(): Promise<void> {
  const request = ++listRequest
  isLoading.value = true
  errorMessage.value = ''
  try {
    const hasValidImss =
      selectedImssFilter.value === 'true'
        ? true
        : selectedImssFilter.value === 'false'
          ? false
          : undefined

    const res = await getStudents({
      page: currentPage.value,
      pageSize: 50,
      search: searchQuery.value.trim() || undefined,
      careerId: Number(selectedCareerId.value) || undefined,
      shift: selectedShift.value || undefined,
      hasValidImss,
      followUpStatus: followUpStatus.value || undefined,
      followUpProgress: followUpProgress.value || undefined,
    })
    if (request !== listRequest) return
    students.value = res.items || []
    totalItems.value = res.totalItems || students.value.length

  } catch (err: any) {
    if (request !== listRequest) return
    students.value = []; totalItems.value = 0
    errorMessage.value = problemMessage(err, 'Error al cargar padrón de alumnos.')
  } finally {
    if (request === listRequest) isLoading.value = false
  }
}

function handleSearchInput(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(applyFilters, 300)
}

function resetAllFilters(): void {
  followUpStatus.value = ''; followUpProgress.value = ''; currentPage.value = 1
  searchQuery.value = ''
  selectedCareerId.value = ''
  selectedShift.value = ''
  selectedImssFilter.value = ''
  loadData()
}

function openCreateModal(): void {
  errorMessage.value = ''
  isEditing.value = false
  editingId.value = null
  selectedDetail.value = null
  Object.assign(studentForm, {
    socialServiceEligiblePeriodId: undefined,
    careerId: careerOptions.value[0]?.id ?? 1,
    controlNumber: '',
    curp: '',
    firstName: '',
    firstSurname: '',
    secondSurname: '',
    gender: 'M',
    shift: 'morning',
    hasDisability: false,
    disabilityDescription: '',
    institutionalEmail: '',
    personalEmail: '',
    phoneNumber: '',
    currentSemester: 6,
    completedCredits: 190,
    isEnrolled: true,
  })
  isStudentModalOpen.value = true
}

async function openEditModal(row: StudentListItem): Promise<void> {
  errorMessage.value = ''
  imssError.value = ''
  try {
    const detail = await getStudentById(row.id)
    isEditing.value = true
    editingId.value = detail.id
    selectedDetail.value = detail
    Object.assign(studentForm, {
      socialServiceEligiblePeriodId: detail.socialServiceEligiblePeriodId,
      careerId: detail.careerId,
      controlNumber: detail.controlNumber,
      curp: detail.curp || '',
      firstName: detail.firstName,
      firstSurname: detail.firstSurname,
      secondSurname: detail.secondSurname || '',
      gender: detail.gender || 'M',
      shift: detail.shift,
      hasDisability: detail.hasDisability,
      disabilityDescription: detail.disabilityDescription || '',
      institutionalEmail: detail.institutionalEmail || '',
      personalEmail: detail.personalEmail || '',
      phoneNumber: detail.phoneNumber || '',
      currentSemester: detail.currentSemester,
      completedCredits: detail.completedCredits,
      isEnrolled: detail.isEnrolled,
    })
    isStudentModalOpen.value = true
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo cargar el detalle del estudiante.')
  }
}

function studentRules(): Record<string, string[]> {
  const errors: Record<string, string[]> = {}
  const check = (key: string, invalid: boolean, message: string) => { if (invalid) errors[key] = [message] }
  check('controlNumber', !/^[A-Z0-9_-]{1,32}$/.test(studentForm.controlNumber.trim().toUpperCase()), 'Indica una matrícula de 1 a 32 caracteres: letras, números, guion o guion bajo.')
  check('curp', !!studentForm.curp?.trim() && !/^[A-Z]{4}\d{6}[HM][A-Z]{5}[A-Z0-9]\d$/.test(studentForm.curp.trim().toUpperCase()), 'La CURP debe tener 18 caracteres con el formato correcto.')
  for (const [key, max] of [['firstName',100], ['firstSurname',70]] as const) check(key, !studentForm[key].trim() || studentForm[key].trim().length > max, `Este campo es obligatorio (máximo ${max} caracteres).`)
  check('secondSurname', (studentForm.secondSurname?.trim().length || 0) > 70, 'Máximo 70 caracteres.')
  check('careerId', !studentForm.careerId, 'Selecciona una carrera.')
  check('currentSemester', !Number.isInteger(studentForm.currentSemester) || studentForm.currentSemester < 1 || studentForm.currentSemester > 15, 'Indica un semestre entero entre 1 y 15.')
  const total = careerOptions.value.find(c => c.id === studentForm.careerId)?.meta?.totalCredits
  check('completedCredits', !Number.isInteger(studentForm.completedCredits) || studentForm.completedCredits < 0 || (total !== undefined && studentForm.completedCredits > total), 'Los créditos deben ser un entero entre cero y el total de la carrera.')
  for (const key of ['institutionalEmail','personalEmail'] as const) check(key, !!studentForm[key]?.trim() && (studentForm[key]!.trim().length > 255 || !studentForm[key]!.includes('@')), 'Indica un correo con @ y máximo 255 caracteres.')
  check('phoneNumber', (studentForm.phoneNumber?.trim().length || 0) > 20, 'El teléfono admite máximo 20 caracteres.')
  check('gender', !!studentForm.gender && !['M','F'].includes(studentForm.gender.trim().toUpperCase()), 'Selecciona una opción válida.')
  check('shift', !['morning','afternoon','night'].includes(studentForm.shift.trim().toLowerCase()), 'Selecciona un turno válido.')
  check('disabilityDescription', (studentForm.hasDisability && !studentForm.disabilityDescription?.trim()) || (studentForm.disabilityDescription?.trim().length || 0) > 150, 'Describe la discapacidad (máximo 150 caracteres).')
  return errors
}
async function handleSaveStudent(): Promise<void> {
  if (isSubmittingStudent.value || !studentValidation.validate()) return

  if (isSubmittingStudent.value || (isEditing.value ? !canRegister.value : !canCreate.value)) return
  isSubmittingStudent.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    const payload: StudentRequest = {
      ...studentForm,
      controlNumber: studentForm.controlNumber.trim().toUpperCase(),
      curp: studentForm.curp?.trim().toUpperCase() || null,
      secondSurname: studentForm.secondSurname?.trim() || null,
      institutionalEmail: studentForm.institutionalEmail?.trim() || null,
      personalEmail: studentForm.personalEmail?.trim() || null,
      phoneNumber: studentForm.phoneNumber?.trim() || null,
      disabilityDescription: studentForm.hasDisability
        ? studentForm.disabilityDescription?.trim() || null
        : null,
    }

    if (isEditing.value && editingId.value) {
      await updateStudent(editingId.value, payload)
      successMessage.value = `Expediente de ${payload.controlNumber} actualizado correctamente.`
    } else {
      await createStudent(payload)
      successMessage.value = `Alumno ${payload.controlNumber} agregado al padrón exitosamente.`
    }
    isStudentModalOpen.value = false
    await loadData()
  } catch (err: any) {
    studentValidation.apply(err, 'No se pudo guardar el formulario.')
    errorMessage.value = problemMessage(err, 'Error al guardar los datos del alumno.')
  } finally {
    isSubmittingStudent.value = false
  }
}

async function handleDownloadStudentImss(id: number, controlNumber: string): Promise<void> {
  try {
    const blob = await getStudentImssBlob(id)
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    errorMessage.value = `El alumno ${controlNumber} aún no tiene un archivo PDF de vigencia IMSS almacenado.`
  }
}

async function handleCreateAccount(row: StudentListItem): Promise<void> {
  errorMessage.value = ''
  try {
    const res = await createStudentAccount(row.id)
    generatedAccount.value = res
    generatedControlNumber.value = row.controlNumber
    isAccountModalOpen.value = true
    await loadData()
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'No se pudo generar la cuenta de usuario para el alumno.')
  }
}

async function handleExportStudents(): Promise<void> {
  if (isExporting.value || isLoading.value) return
  isExporting.value = true
  errorMessage.value = ''
  try {
    const csv = await getStudentsCsv({
      search: searchQuery.value.trim() || undefined,
      careerId: Number(selectedCareerId.value) || undefined,
      shift: selectedShift.value || undefined,
      hasValidImss: selectedImssFilter.value === '' ? undefined : selectedImssFilter.value === 'true',
      followUpStatus: followUpStatus.value || undefined,
      followUpProgress: followUpProgress.value || undefined,
    })
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }))
    const link = document.createElement('a')
    link.href = url
    link.download = 'Alumnos_TecNM.csv'
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (error) {
    errorMessage.value = problemMessage(error, 'No se pudo exportar el listado de alumnos. Vuelve a intentar.')
  } finally {
    isExporting.value = false
  }
}

async function handleDownloadTemplate(): Promise<void> {
  if (isDownloadingTemplate.value || !canCreate.value) return
  isDownloadingTemplate.value = true
  try {
    const blob = await downloadImportTemplate()
    const url = window.URL.createObjectURL(blob)
    const a = document.createElement('a')
    a.href = url
    a.download = 'Plantilla_Importacion_Estudiantes_TecNM.xlsx'
    a.click()
    window.URL.revokeObjectURL(url)
  } catch {
    errorMessage.value = 'No se pudo descargar la plantilla oficial de Excel.'
  } finally {
    isDownloadingTemplate.value = false
  }
}

function handleExcelFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
}



async function handleRunImport(): Promise<void> {
  if (isImporting.value || !importValidation.validate()) return
  if (!importFile.value || !importPeriodId.value) return
  isImporting.value = true
  errorMessage.value = ''
  try {
    importResult.value = await uploadStudentsExcel(importPeriodId.value, importFile.value)
    successMessage.value = `Importación finalizada: ${importResult.value.importedRecords} alumnos; ${importResult.value.createdAccounts} cuentas creadas; ${importResult.value.existingAccounts} existentes; ${importResult.value.failedRecords} filas rechazadas.`
    await loadData()
  } catch (err: any) {
    importValidation.apply(err, 'No se pudo importar el archivo.')
    errorMessage.value = problemMessage(err, 'Error al procesar el archivo Excel (.xlsx).')
  } finally {
    isImporting.value = false
  }
}

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  listRequest++; detailRequest++
})
onMounted(async () => {
  await Promise.all([loadCatalogs(), loadData()])
})
const studentTotalCredits = computed(() => careerOptions.value.find(c => c.id === studentForm.careerId)?.meta?.totalCredits)
const assessmentValidation = useFormValidation(() => validateFields({ assessment: assessment.value, notes: assessmentNotes.value }, {assessment:{required:true,choices:['consistent','low_progress']},notes:{max:1000}}))
const importValidation = useFormValidation(() => validateFields({periodId:importPeriodId.value,file:importFile.value},{periodId:{required:true,min:1},file:{required:true}}))
watchValidation(assessmentOpen, () => assessmentValidation.reset())
watchValidation(isImportModalOpen, () => importValidation.reset())
const studentValidation = useFormValidation(studentRules)
watchValidation(isStudentModalOpen, () => { studentValidation.reset(); errorMessage.value = '' })
</script>

<template>
  <UiBasePage :fluid="true">
    <!-- Header Bar -->
    <div class="header-bar">
      <div class="title-block">
        <div class="title-row">
          <h1 class="page-title">Padrón de Estudiantes Elegibles</h1>
          <span class="count-badge">{{ totalItems }} candidatos</span>
        </div>
        <p class="page-subtitle">
          Carga de alumnos candidatos por archivo Excel o registro individual para casos específicos
        </p>
      </div>

      <div class="actions-group" role="group" aria-label="Acciones del padrón">
        <UiButton variant="secondary" class="action-btn" :loading="isExporting" :disabled="isLoading || !totalItems" @click="handleExportStudents">{{ isExporting ? 'Exportando…' : 'Exportar alumnos (.csv)' }}</UiButton>
        <UiButton v-if="canCreate" variant="secondary" class="action-btn" :loading="isDownloadingTemplate" @click="handleDownloadTemplate">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="7 10 12 15 17 10" />
            <line x1="12" y1="15" x2="12" y2="3" />
          </svg>
          Descargar Plantilla Excel (.xlsx)
        </UiButton>
        <UiButton
          variant="secondary"
          class="action-btn"
          v-if="canRegister"
          @click="
            importResult = null;
            importFile = null;
            isImportModalOpen = true
          "
        >
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none">
            <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
            <polyline points="17 8 12 3 7 8" />
            <line x1="12" y1="3" x2="12" y2="15" />
          </svg>
          Importar Padrón Excel
        </UiButton>
        <UiButton v-if="canCreate" variant="primary" class="action-btn primary" @click="openCreateModal">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2.2" fill="none">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          Agregar alumno manualmente
        </UiButton>
      </div>
    </div>

    <!-- Feedback Alerts -->
    <UiAlert v-if="errorMessage && !(studentValidation.hasErrors.value || assessmentValidation.hasErrors.value || importValidation.hasErrors.value)" tone="danger" class="mb-4">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" class="mb-4">
      {{ successMessage }}
    </UiAlert>

    <!-- Main Container -->
    <UiCard class="main-card">
      <div class="roster-toolbar">
        <p v-if="canFollowUp" class="tracking-period">Periodo de seguimiento: <strong>{{ students[0]?.socialServiceFollowUp?.trackingPeriodCode || periodOptions.find(p => p.meta?.isActive)?.label || 'Sin periodo activo' }}</strong></p>
        <div class="roster-filters">
          <UiField id="roster-search" v-model="searchQuery" label="Buscar alumno" type="search" placeholder="Nombre o matrícula" @input="handleSearchInput" />
          <UiField id="roster-career" label="Carrera" as="select" v-model="selectedCareerId" @change="applyFilters"><template #options><option value="">Todas las carreras</option><option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option></template></UiField>
          <UiField v-if="canFollowUp" id="follow-up-status" label="Seguimiento SS" as="select" v-model="followUpStatus" @change="applyFilters"><template #options><option value="">Todos</option><option v-for="(label, value) in followUpLabels" :key="value" :value="value">{{ label }}</option></template></UiField>
        </div>
        <div class="roster-filter-actions">
          <details class="extra-filters">
            <summary>Más filtros</summary>
            <div class="extra-filter-fields">
              <UiField id="roster-shift" label="Turno" as="select" v-model="selectedShift" @change="applyFilters"><template #options><option value="">Todos los turnos</option><option value="morning">Matutino</option><option value="afternoon">Vespertino</option><option value="night">Nocturno</option></template></UiField>
              <UiField id="roster-imss" label="Vigencia IMSS" as="select" v-model="selectedImssFilter" @change="applyFilters"><template #options><option value="">Todos los estados</option><option value="true">Vigente</option><option value="false">Pendiente</option></template></UiField>
              <UiField v-if="canFollowUp" id="follow-up-progress" label="Constancia documental" as="select" v-model="followUpProgress" @change="applyFilters"><template #options><option value="">Todas</option><option v-for="(label, value) in assessmentLabels" :key="value" :value="value">{{ label }}</option></template></UiField>
            </div>
          </details>
          <UiButton v-if="hasActiveFilters" variant="secondary" @click="resetAllFilters">Limpiar filtros</UiButton>
        </div>
      </div>
      <p v-if="isLoading" class="roster-feedback" role="status">Cargando alumnos…</p>
      <div class="roster-table" :aria-busy="isLoading">
        <UiTable label="Listado del Padrón de Alumnos" :columns="columns" :rows="students">
          <template #cell-fullName="{ row }"><span class="roster-name">{{ row.fullName }}</span></template>
          <template #cell-controlNumber="{ row }"><span class="roster-control">{{ row.controlNumber }}</span></template>
          <template #cell-careerCode="{ row }"><div class="roster-career"><span>{{ row.careerCode }}</span><small v-if="row.currentSemester != null">Semestre {{ row.currentSemester }}</small></div></template>
          <template #cell-socialServiceFollowUp="{ row }"><div v-if="row.socialServiceFollowUp" class="roster-status"><UiBadge :tone="followUpTone(row)">{{ followUpLabels[row.socialServiceFollowUp.status] }}</UiBadge><small v-if="row.socialServiceFollowUp.status === 'in_progress'">{{ assessmentLabels[row.socialServiceFollowUp.progressAssessment || 'pending'] }}</small></div></template>
          <template #cell-actions="{ row }"><UiButton variant="secondary" @click="openDetail(row)" :aria-label="'Ver detalle de ' + row.fullName">Ver detalle</UiButton></template>
        </UiTable>
      </div>
      <p v-if="!isLoading && !students.length && !errorMessage" class="roster-feedback" role="status">No hay alumnos que coincidan con los filtros.</p>
      <div class="roster-pagination">
        <span>{{ totalItems }} alumnos · Página {{ currentPage }} de {{ Math.max(1, Math.ceil(totalItems / 50)) }}</span>
        <div><UiButton variant="secondary" :disabled="isLoading || currentPage <= 1" @click="currentPage--; loadData()">Anterior</UiButton><UiButton variant="secondary" :disabled="isLoading || currentPage * 50 >= totalItems" @click="currentPage++; loadData()">Siguiente</UiButton></div>
      </div>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL 1: ALTA / EDICIÓN INDIVIDUAL DE ALUMNO (CASO ESPECÍFICO)      -->
    <!-- ================================================================== -->
    <UiDialog v-model="detailOpen" size="lg" title="Detalle del alumno">
      <div v-if="detailRow" class="student-detail">
        <header class="student-detail-heading"><h2>{{ detailRow.fullName }}</h2><p>{{ detailRow.controlNumber }} · {{ detailRow.careerCode }}</p></header>
        <p v-if="detailLoading" role="status">Cargando expediente…</p>
        <UiAlert v-if="detailError" tone="danger"><p>{{ detailError }}</p><UiButton variant="secondary" @click="openDetail(detailRow)">Reintentar</UiButton></UiAlert>
        <section v-if="detailRow.socialServiceFollowUp" class="detail-section">
          <h3>Seguimiento de Servicio Social</h3>
          <UiBadge :tone="followUpTone(detailRow)">{{ followUpLabels[detailRow.socialServiceFollowUp.status] }}</UiBadge>
          <dl class="detail-fields">
            <div v-if="detailRow.socialServiceFollowUp.status === 'in_progress'"><dt>Constancia documental</dt><dd>{{ assessmentLabels[detailRow.socialServiceFollowUp.progressAssessment || 'pending'] }}</dd></div>
            <div><dt>Primer periodo de aptitud</dt><dd>{{ detailRow.socialServiceFollowUp.eligiblePeriodCode || 'Pendiente de confirmar' }}</dd></div>
            <div><dt>Periodo de seguimiento</dt><dd>{{ detailRow.socialServiceFollowUp.trackingPeriodCode || 'Sin periodo activo' }}</dd></div>
            <div><dt>Semestres desde la aptitud</dt><dd>{{ detailRow.socialServiceFollowUp.elapsedSemesters ?? 'Pendiente' }}</dd></div>
            <div><dt>Evidencias aprobadas</dt><dd>{{ detailRow.socialServiceFollowUp.approvedDocumentsCount }}</dd></div>
            <div><dt>Última evidencia aprobada</dt><dd>{{ displayDate(detailRow.socialServiceFollowUp.lastApprovedAt) }}</dd></div>
            <div><dt>Observación de seguimiento</dt><dd>{{ detailRow.socialServiceFollowUp.notes || 'Sin observaciones' }}</dd></div>
            <div><dt>Última valoración</dt><dd>{{ displayDate(detailRow.socialServiceFollowUp.reviewedAt) }}</dd></div>
          </dl>
          <UiButton v-if="canAssess && detailRow.socialServiceFollowUp.status === 'in_progress' && detailRow.socialServiceFollowUp.trackingPeriodId" variant="secondary" @click="detailOpen = false; openAssessment(detailRow)">Valorar seguimiento</UiButton>
        </section>
        <UiAlert v-if="detailRow.canReadProfile === false" tone="info">Este candidato dispone de un resumen de seguimiento. Los datos personales y académicos completos requieren acceso a su expediente.</UiAlert>
        <section v-for="section in detailSections" :key="section.title" class="detail-section">
          <h3>{{ section.title }}</h3><dl class="detail-fields"><div v-for="field in section.fields" :key="field[0] ?? ''"><dt>{{ field[0] }}</dt><dd>{{ field[1] || 'Sin registro' }}</dd></div></dl>
        </section>
        <section v-if="selectedDetail" class="detail-section">
          <h3>Vigencia IMSS</h3>
          <dl class="detail-fields"><div><dt>Estado documental</dt><dd>{{ ({missing:'Sin documento',pending:'Pendiente de revisión',approved:'Aprobado',rejected:'Rechazado'})[selectedDetail.imssStatus] || selectedDetail.imssStatus }}</dd></div><div><dt>Validó</dt><dd>{{ selectedDetail.imssValidatedByName || 'Sin registro' }}</dd></div><div><dt>Fecha de validación</dt><dd>{{ displayDate(selectedDetail.imssValidatedAt) }}</dd></div><div v-if="selectedDetail.imssRejectionReason"><dt>Motivo de rechazo</dt><dd>{{ selectedDetail.imssRejectionReason }}</dd></div></dl>
          <UiAlert v-if="imssError" tone="danger">{{ imssError }}</UiAlert>
          <div class="detail-actions"><UiButton v-if="selectedDetail.hasImssFile" variant="secondary" @click="handleDownloadStudentImss(selectedDetail.id, selectedDetail.controlNumber)">Ver PDF IMSS</UiButton><UiButton v-if="canReview" variant="secondary" :disabled="!selectedDetail.imssDocumentId" @click="reviewStudentImss">{{ selectedDetail.imssStatus === 'pending' ? 'Revisar IMSS' : 'Ver dictamen IMSS' }}</UiButton></div>
        </section>
        <section v-if="canRegister && selectedDetail" class="detail-section"><h3>Administración</h3><div class="detail-actions"><UiButton variant="secondary" @click="detailOpen = false; openEditModal(detailRow)">Editar datos</UiButton><UiButton v-if="!detailRow.hasUserAccount" variant="secondary" @click="detailOpen = false; handleCreateAccount(detailRow)">Crear cuenta</UiButton><UiButton v-else variant="secondary" @click="detailOpen = false; resetCandidate = detailRow; resetError = ''; resetOpen = true">Restablecer contraseña</UiButton></div></section>
      </div>
      <template #footer><UiButton variant="secondary" @click="detailOpen = false">Cerrar detalle</UiButton></template>
    </UiDialog>

    <UiDialog v-model="assessmentOpen" title="Valorar seguimiento SS">
      <p>{{ assessmentCandidate?.fullName }}</p>
      <UiFormErrors :validation="assessmentValidation" />
      <UiField v-bind="assessmentValidation.field('assessment', 'Constancia')" label="Constancia" v-slot="{ controlAttrs }"><select v-bind="controlAttrs" v-model="assessment" :disabled="savingAssessment"><option value="consistent">Constante</option><option value="low_progress">Con poco avance</option></select></UiField>
      <UiField v-bind="assessmentValidation.field('notes', 'Observación')" label="Observación" v-slot="{ controlAttrs }"><textarea v-bind="controlAttrs" v-model="assessmentNotes" maxlength="1000" :disabled="savingAssessment" /></UiField>
      <p v-if="assessmentError && !assessmentValidation.hasErrors.value" role="alert">{{ assessmentError }}</p>
      <UiButton :disabled="savingAssessment" @click="saveAssessment">{{ savingAssessment ? 'Guardando…' : 'Guardar valoración' }}</UiButton>
    </UiDialog>
    <UiDialog
      v-model="isStudentModalOpen"
      size="lg"
      :title="isEditing ? `Expediente · ${studentForm.controlNumber}` : 'Agregar Alumno (Caso Específico)'"
    >
      <UiAlert v-if="errorMessage && !(studentValidation.hasErrors.value || assessmentValidation.hasErrors.value || importValidation.hasErrors.value)" tone="danger">{{ errorMessage }}</UiAlert>
      <fieldset class="modal-form-grid" :disabled="isEditing ? !canRegister : !canCreate" style="border:0;padding:0;margin:0">
        <UiFormErrors :validation="studentValidation" />
        <UiField label="Primer periodo de aptitud SS" hint="Primer semestre como candidato" v-bind="studentValidation.field('socialServiceEligiblePeriodId', 'Primer periodo de aptitud SS')" v-slot="{ controlAttrs }"><select v-bind="controlAttrs" v-model="studentForm.socialServiceEligiblePeriodId" class="modern-select"><option :value="undefined">Activo al crear / conservar al editar</option><option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option></select></UiField>
        <UiField label="No. de Control *" hint="Ej. I23050363" v-bind="studentValidation.field('controlNumber', 'No. de Control *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="studentForm.controlNumber" type="text" class="modern-input" placeholder="I23050999" />
        </UiField>

        <UiField label="Carrera *" v-bind="studentValidation.field('careerId', 'Carrera *')" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model.number="studentForm.careerId" class="modern-select">
            <option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
        </UiField>

        <UiField label="Nombre(s) *" v-bind="studentValidation.field('firstName', 'Nombre(s) *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="studentForm.firstName" type="text" class="modern-input" placeholder="Nombre del alumno" />
        </UiField>

        <UiField label="Primer Apellido *" v-bind="studentValidation.field('firstSurname', 'Primer Apellido *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="studentForm.firstSurname" type="text" class="modern-input" placeholder="Apellido paterno" />
        </UiField>

        <UiField label="Segundo Apellido" v-bind="studentValidation.field('secondSurname', 'Segundo Apellido')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="studentForm.secondSurname" type="text" class="modern-input" placeholder="Apellido materno" />
        </UiField>

        <UiField label="CURP" v-bind="studentValidation.field('curp', 'CURP')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="studentForm.curp" type="text" class="modern-input" maxlength="18" placeholder="18 caracteres" />
        </UiField>

        <UiField label="Semestre Actual (1-15) *" v-bind="studentValidation.field('currentSemester', 'Semestre Actual (1-15) *')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model.number="studentForm.currentSemester" type="number" class="modern-input" min="1" max="15" />
        </UiField>

        <UiField label="Créditos Aprobados *" :hint="`Total retícula: ${studentTotalCredits ?? 'Consulta la carrera'}`" v-bind="studentValidation.field('completedCredits', 'Créditos aprobados')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model.number="studentForm.completedCredits" type="number" class="modern-input" min="0" :max="careerOptions.find(c => c.id === studentForm.careerId)?.meta?.totalCredits" />
        </UiField>

        <UiField label="Turno *" v-bind="studentValidation.field('shift', 'Turno *')" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="studentForm.shift" class="modern-select">
            <option value="morning">Matutino</option>
            <option value="afternoon">Vespertino</option>
            <option value="night">Nocturno</option>
          </select>
        </UiField>

        <UiField label="Sexo (Estadístico 911)" v-bind="studentValidation.field('gender', 'Sexo (Estadístico 911)')" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="studentForm.gender" class="modern-select">
            <option value="M">Hombre (M)</option>
            <option value="F">Mujer (F)</option>
          </select>
        </UiField>

        <UiField label="Correo Institucional" v-bind="studentValidation.field('institutionalEmail', 'Correo Institucional')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs"
            v-model="studentForm.institutionalEmail"
            type="email"
            class="modern-input"
            placeholder="L23050999@monclova.tecnm.mx"
          />
        </UiField>

        <UiField label="Teléfono de Contacto" v-bind="studentValidation.field('phoneNumber', 'Teléfono de Contacto')" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" v-model="studentForm.phoneNumber" type="text" class="modern-input" placeholder="8661234567" />
        </UiField>
      </fieldset>

      <!-- Sección de Constancia de Vigencia IMSS en edición o alta -->
      <div class="imss-box">
        <div class="imss-box-header">
          <strong>Constancia de Vigencia de Derechos del IMSS (PDF)</strong>
          <span v-if="selectedDetail" class="imss-status-tag" :class="{ valid: selectedDetail.hasValidImss }">
            {{ selectedDetail.hasValidImss ? 'Vigente y Validado' : 'Pendiente de Validación' }}
          </span>
        </div>
        <p class="imss-box-desc">
          Revisa que el PDF corresponda al alumno y muestre estado VIGENTE antes de aprobarlo.
        </p>
        <p>El alumno debe cargar su constancia desde su portal.</p>

        <UiAlert v-if="imssError" tone="danger" role="alert">{{ imssError }}</UiAlert>
        <p v-if="selectedDetail?.imssStatus === 'missing'">{{ selectedDetail.hasImssFile ? 'Archivo histórico: requiere nueva carga documental.' : 'Sin constancia documental: el alumno debe cargarla desde su perfil.' }}</p>
        <p v-if="selectedDetail?.imssRejectionReason">{{ selectedDetail.imssRejectionReason }}</p>
        <div
          v-if="isEditing && selectedDetail"
          class="imss-actions-row"
        >
          <UiButton
            v-if="selectedDetail.hasImssFile"
            variant="secondary"
            size="sm"
            @click="handleDownloadStudentImss(selectedDetail.id, selectedDetail.controlNumber)"
          >
            Ver PDF IMSS Almacenado
          </UiButton>
          <UiButton
            v-if="canReview"
            variant="secondary"
            size="sm"
            :disabled="!selectedDetail.imssDocumentId"
            @click="reviewStudentImss"
          >
            {{ selectedDetail.imssStatus === 'pending' ? 'Revisar IMSS' : 'Ver dictamen IMSS' }}
          </UiButton>
        </div>
      </div>

      <template #footer>
        <UiButton variant="secondary" @click="isStudentModalOpen = false">Cancelar</UiButton>
        <UiButton v-if="isEditing ? canRegister : canCreate" variant="primary" :disabled="isSubmittingStudent" @click="handleSaveStudent">
          {{ isSubmittingStudent ? 'Guardando...' : isEditing ? 'Guardar Cambios' : 'Registrar Alumno' }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 2: IMPORTACIÓN MASIVA DESDE EXCEL (.XLSX)                     -->
    <!-- ================================================================== -->
    <UiDialog v-model="isImportModalOpen" size="md" title="Importar Padrón de Alumnos desde Excel (.xlsx)">
      <div class="import-modal-content">
        <p>Las cuentas nuevas usarán el correo institucional y su matrícula como contraseña. Las cuentas existentes conservan su contraseña.</p>
        <p class="import-intro-text">
          Carga el archivo Excel con los alumnos que están en condiciones de realizar su Servicio
          Social o Prácticas Profesionales en el periodo seleccionado.
        </p>

        <UiFormErrors :validation="importValidation" />
        <UiField v-bind="importValidation.field('periodId', 'Periodo escolar destino')" label="Periodo Escolar Destino *" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model.number="importPeriodId" class="modern-select">
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </UiField>

        <UiField v-bind="importValidation.field('file', 'Archivo Excel')" label="Archivo Excel (.xlsx) *" v-slot="{ controlAttrs }">
          <input v-bind="controlAttrs" type="file" accept=".xlsx" class="file-input" @change="handleExcelFileChange" />
        </UiField>

        <div v-if="importResult" class="import-summary-box">
          <strong>Resultado de la Importación ({{ importResult.fileName }}):</strong>
          <ul>
            <li>Total de filas leídas: <strong>{{ importResult.totalRecords }}</strong></li>
            <li>Alumnos importados/actualizados: <strong>{{ importResult.importedRecords }}</strong></li>
            <li>Cuentas creadas: <strong>{{ importResult.createdAccounts }}</strong></li>
            <li>Cuentas existentes: <strong>{{ importResult.existingAccounts }}</strong></li>
            <li>Filas con error: <strong>{{ importResult.failedRecords }}</strong></li>
          </ul>
          <div v-if="importResult.rowErrors?.length" class="import-errors-list">
            <div v-for="(err, idx) in importResult.rowErrors.slice(0, 5)" :key="idx">
              Fila {{ err.row }} ({{ err.controlNumber || 'S/N' }}): {{ err.message }}
            </div>
          </div>
        </div>
      </div>

      <template #footer>
        <div class="import-modal-footer">
          <UiButton variant="secondary" @click="handleDownloadTemplate">
            Descargar Plantilla Oficial
          </UiButton>
          <div class="footer-actions">
            <UiButton variant="secondary" @click="isImportModalOpen = false">Cerrar</UiButton>
            <UiButton
              variant="primary"
              :disabled="!importFile || isImporting"
              @click="handleRunImport"
            >
              {{ isImporting ? 'Importando...' : 'Subir y Procesar Excel' }}
            </UiButton>
          </div>
        </div>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 3: CREDENCIALES DE ACCESO GENERADAS PARA ALUMNO               -->
    <!-- ================================================================== -->
    <UiDialog v-model="isAccountModalOpen" size="sm" title="Cuenta de Acceso Creada">
      <div v-if="generatedAccount" class="account-modal-body">
        <UiAlert tone="success">
          Se ha creado la cuenta de acceso institucional para el estudiante. Comparte estas credenciales:
        </UiAlert>
        <div class="credential-item">
          <span class="cred-label">Usuario:</span>
          <strong class="cred-value">{{ generatedAccount.username }}</strong>
        </div>
        <div class="credential-item">
          <span class="cred-label">Contraseña habitual:</span>
          <code class="cred-pass">{{ generatedControlNumber }}</code>
        </div>
      </div>
      <template #footer>
        <UiButton variant="primary" @click="isAccountModalOpen = false">Entendido</UiButton>
      </template>
    </UiDialog>
    <DocumentReviewDialog v-model="reviewOpen" :document="reviewDocument" :student-name="reviewStudentName" @reviewed="afterReview" />
    <UiDialog v-model="resetOpen" title="Restablecer contraseña">
      <UiAlert v-if="resetError" tone="danger" role="alert">{{ resetError }}</UiAlert>
      <p>La contraseña de {{ resetCandidate?.accountUsername }} será su matrícula {{ resetCandidate?.controlNumber }}. Las sesiones actuales se cerrarán.</p>
      <template #footer>
        <UiButton variant="secondary" :disabled="resetting" @click="resetOpen = false">Cancelar</UiButton>
        <UiButton :loading="resetting" :disabled="resetting" @click="confirmReset">Restablecer contraseña</UiButton>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
.header-bar {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  gap: 1.5rem;
  margin-bottom: 1.5rem;
  flex-wrap: wrap;
}

.title-block {
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.title-row {
  flex-wrap: wrap;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.page-title {
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--color-text-primary, #0f172a);
  margin: 0;
  letter-spacing: -0.02em;
}

.count-badge {
  display: inline-flex;
  align-items: center;
  padding: 0.2rem 0.6rem;
  border-radius: 9999px;
  background: rgba(2, 132, 199, 0.1);
  color: #0284c7;
  font-size: 0.8rem;
  font-weight: 600;
}

.page-subtitle {
  color: #64748b;
  font-size: 0.88rem;
  margin: 0;
}

.actions-group {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.action-btn {
  display: inline-flex;
  align-items: center;
  gap: 0.45rem;
  font-size: 0.85rem;
  font-weight: 500;
  min-height: 44px;
  height: auto;
  max-width: 100%;
  white-space: normal;
  text-align: center;
  padding: 0 1rem;
}

.main-card {
  border-radius: 12px;
  border: 1px solid var(--color-border-light, #e2e8f0);
}

/* Toolbar de Filtros Modernos */
.filters-toolbar {
  display: grid;
  grid-template-columns: minmax(260px, 1.8fr) repeat(3, minmax(170px, 1.2fr)) auto;
  gap: 1rem;
  align-items: flex-end;
  padding: 0.25rem 0 1.25rem;
  border-bottom: 1px solid var(--color-border-light, #e2e8f0);
  margin-bottom: 1rem;
}

@media (max-width: 1100px) {
  .filters-toolbar {
    grid-template-columns: 1fr 1fr;
  }
}

@media (max-width: 640px) {
  .filters-toolbar {
    grid-template-columns: 1fr;
  }
}

.filter-col {
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
  position: relative;
}

.filter-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: #475569;
  letter-spacing: -0.01em;
}

.search-col {
  position: relative;
}

.autocomplete-container {
  position: relative;
  width: 100%;
}

.search-input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  color: #94a3b8;
  pointer-events: none;
}

.modern-input {
  width: 100%;
  height: 40px;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  padding: 0 2rem 0 2.25rem;
  font-size: 0.875rem;
  color: #0f172a;
  outline: none;
  transition: all 0.15s ease;
  box-sizing: border-box;
}

.modern-input:focus {
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

.clear-search-btn {
  position: absolute;
  right: 0.6rem;
  background: transparent;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 0.85rem;
  padding: 0.2rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
}

.clear-search-btn:hover {
  color: #0f172a;
  background: #f1f5f9;
}

/* Sugerencias Autocomplete Flotantes */
.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: #ffffff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  box-shadow: 0 12px 28px -4px rgba(15, 23, 42, 0.2);
  z-index: 999;
  max-height: 250px;
  overflow-y: auto;
}

.suggestion-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.6rem 0.85rem;
  cursor: pointer;
  border-bottom: 1px solid #f1f5f9;
  transition: background 0.12s ease;
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: #f8fafc;
}

.suggestion-left {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.suggestion-control {
  font-family: monospace;
  font-weight: 700;
  font-size: 0.82rem;
  color: #0284c7;
}

.suggestion-name {
  font-size: 0.82rem;
  color: #1e293b;
}

.suggestion-right {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.suggestion-career {
  font-size: 0.72rem;
  font-weight: 600;
  padding: 0.1rem 0.4rem;
  background: #f1f5f9;
  border-radius: 4px;
  color: #475569;
}

.suggestion-progress {
  font-size: 0.78rem;
  font-weight: 600;
  color: #0f172a;
}

/* Comboboxes con Chevrons */
.select-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.modern-select {
  width: 100%;
  height: 40px;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  padding: 0 2rem 0 0.75rem;
  font-size: 0.85rem;
  color: #0f172a;
  outline: none;
  appearance: none;
  cursor: pointer;
  transition: all 0.15s ease;
  box-sizing: border-box;
}

.modern-select:focus {
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

.select-chevron {
  position: absolute;
  right: 0.75rem;
  color: #64748b;
  pointer-events: none;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  height: 40px;
}

.reset-filters-btn {
  height: 40px;
  padding: 0 0.85rem;
  font-size: 0.85rem;
}

.apply-filter-btn {
  height: 40px;
  padding: 0 1.15rem;
  font-size: 0.85rem;
  font-weight: 600;
}

/* Celda de Tabla y Tipografía */
.control-number-cell {
  font-family: monospace;
  font-weight: 600;
  font-size: 0.875rem;
  color: #0f172a;
}

.fullname-cell {
  font-weight: 500;
  color: #1e293b;
  font-size: 0.875rem;
}

.career-pill {
  display: inline-block;
  padding: 0.15rem 0.5rem;
  border-radius: 6px;
  background: #f1f5f9;
  font-size: 0.75rem;
  font-weight: 600;
  color: #334155;
  text-align: center;
}

.semester-cell {
  font-weight: 500;
  color: #475569;
}

.progress-badge {
  display: inline-block;
  font-weight: 700;
  font-size: 0.85rem;
  color: #d97706;
}

.progress-badge.qualified {
  color: #0284c7;
}

.shift-cell {
  color: #475569;
  font-size: 0.85rem;
}

.row-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.4rem;
  min-width: 190px;
}

.table-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  height: 30px;
  padding: 0 0.65rem;
  border-radius: 6px;
  font-size: 0.78rem;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.12s ease;
  white-space: nowrap;
}

.table-btn.secondary {
  border: 1px solid #cbd5e1;
  background: #ffffff;
  color: #334155;
}

.table-btn.secondary:hover {
  background: #f8fafc;
  border-color: #94a3b8;
}

.table-btn.primary {
  border: 1px solid transparent;
  background: #0f172a;
  color: #ffffff;
}

.table-btn.primary:hover {
  background: #1e293b;
}

/* Modales */
.modal-form-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.85rem 1rem;
}

@media (max-width: 600px) {
  .modal-form-grid {
    grid-template-columns: 1fr;
  }
}

.imss-box {
  margin-top: 1.25rem;
  padding: 1rem;
  border-radius: 8px;
  background: #f8fafc;
  border: 1px solid #e2e8f0;
}

.imss-box-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.imss-status-tag {
  font-size: 0.75rem;
  font-weight: 600;
  padding: 0.15rem 0.5rem;
  border-radius: 9999px;
  background: #fef3c7;
  color: #92400e;
}

.imss-status-tag.valid {
  background: #dcfce7;
  color: #166534;
}

.imss-box-desc {
  margin: 0.25rem 0 0.75rem;
  font-size: 0.82rem;
  color: #64748b;
}

.file-input {
  font-size: 0.82rem;
  color: #475569;
}

.imss-actions-row {
  display: flex;
  gap: 0.5rem;
  margin-top: 0.75rem;
  flex-wrap: wrap;
}

.import-modal-content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.import-intro-text {
  margin: 0;
  font-size: 0.875rem;
  color: #475569;
  line-height: 1.4;
}

.import-summary-box {
  padding: 0.85rem;
  border-radius: 8px;
  background: #f0fdf4;
  border: 1px solid #bbf7d0;
  font-size: 0.88rem;
}

.import-summary-box ul {
  margin: 0.4rem 0;
  padding-left: 1.2rem;
}

.import-errors-list {
  margin-top: 0.5rem;
  font-size: 0.8rem;
  color: #dc2626;
}

.import-modal-footer {
  display: flex;
  justify-content: space-between;
  width: 100%;
  gap: 0.75rem;
}

.footer-actions {
  display: flex;
  gap: 0.5rem;
}

.account-modal-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.credential-item {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.cred-label {
  font-size: 0.8rem;
  color: #64748b;
}

.cred-value {
  font-size: 1.05rem;
  color: #0f172a;
}

.cred-pass {
  font-size: 1.1rem;
  font-weight: 700;
  color: #0284c7;
  background: #f0f9ff;
  padding: 0.25rem 0.5rem;
  border-radius: 4px;
  display: inline-block;
  width: fit-content;
}
</style>

<style scoped>
.main-card { padding: 0; overflow: hidden; border-color: var(--color-border); }
.page-subtitle { color: var(--color-text-secondary); }
.roster-toolbar { padding: 24px; }
.tracking-period { margin: 0 0 16px; font-size: 14px; color: var(--color-text-secondary); }
.roster-filters { display: grid; grid-template-columns: 1.3fr 1fr 1.3fr; gap: 16px; }
.roster-filters :deep(.field), .extra-filter-fields :deep(.field) { margin-bottom: 0; }
.roster-filter-actions { display: flex; align-items: start; gap: 12px; margin-top: 12px; }
.extra-filters { flex: 1; }
.extra-filters summary { cursor: pointer; min-height: 44px; display: flex; align-items: center; color: var(--color-primary); font-weight: 600; }
.extra-filters summary::before { content: '+'; margin-right: 8px; }
.extra-filters[open] summary::before { content: '−'; }
.extra-filter-fields { display: grid; grid-template-columns: repeat(3, minmax(0,1fr)); gap: 16px; padding-top: 8px; }
.roster-table :deep(.table-wrap) { margin: 0; border-radius: 0; }
.roster-table :deep(table) { width: 100%; table-layout: fixed; white-space: normal; }
.roster-table :deep(th), .roster-table :deep(td) { padding: 16px; overflow-wrap: anywhere; vertical-align: middle; }
.roster-table :deep(th) { font-size: 12px; text-transform: none; letter-spacing: 0; }
.roster-table :deep(th:nth-child(1)) { width: 27%; }
.roster-table :deep(th:nth-child(2)) { width: 15%; }
.roster-table :deep(th:nth-child(3)) { width: 17%; }
.roster-table :deep(th:nth-child(4)) { width: 27%; }
.roster-table :deep(th:last-child) { width: 14%; }
.roster-table :deep(button) { min-height: 44px; padding: 8px 12px; white-space: normal; }
.roster-name { font-weight: 600; color: var(--color-text-primary); line-height: 1.5; }
.roster-control { font-variant-numeric: tabular-nums; }
.roster-career, .roster-status { display: flex; flex-direction: column; align-items: start; gap: 6px; }
.roster-career small, .roster-status small { color: var(--color-text-secondary); font-size: 13px; }
.roster-status :deep(.badge) { max-width: 100%; white-space: normal; line-height: 1.4; }
.roster-feedback { margin: 0; padding: 24px; color: var(--color-text-secondary); }
.roster-pagination { display: flex; flex-wrap: wrap; justify-content: space-between; align-items: center; gap: 12px; padding: 16px 24px; border-top: 1px solid var(--color-border); color: var(--color-text-secondary); font-size: 14px; }
.roster-pagination > div, .detail-actions { display: flex; flex-wrap: wrap; gap: 8px; }
.student-detail { min-width: 0; }
.student-detail-heading h2 { font-size: 22px; margin: 0 0 8px; overflow-wrap: anywhere; }
.student-detail-heading p { margin: 0; color: var(--color-text-secondary); }
.detail-section { padding-top: 20px; margin-top: 20px; border-top: 1px solid var(--color-border); }
.detail-section h3 { font-size: 16px; margin: 0 0 16px; }
.detail-fields { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 16px 24px; margin: 16px 0; }
.detail-fields > div { min-width: 0; }
.detail-fields dt { font-size: 13px; color: var(--color-text-secondary); margin-bottom: 4px; }
.detail-fields dd { margin: 0; overflow-wrap: anywhere; white-space: pre-wrap; color: var(--color-text-primary); }
@media(max-width:800px) {
 .actions-group { width: 100%; }
 .action-btn { flex: 1 1 220px; justify-content: center; padding: 10px 12px; }
 .roster-filters, .extra-filter-fields { grid-template-columns: 1fr; }
 .roster-toolbar { padding: 16px; }
 .roster-table :deep(thead) { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); }
 .roster-table :deep(table), .roster-table :deep(tbody) { display: block; }
 .roster-table :deep(tr) { display: grid; grid-template-columns: repeat(2,minmax(0,1fr)); gap: 12px; padding: 20px 16px; border-bottom: 1px solid var(--color-border); }
 .roster-table :deep(td) { display: block; padding: 0; border: 0; }
 .roster-table :deep(td:first-child), .roster-table :deep(td:nth-child(4)), .roster-table :deep(td:last-child) { grid-column: 1 / -1; }
 .roster-table :deep(td:last-child) { text-align: right; }
 .detail-fields { grid-template-columns: 1fr; }
 .roster-pagination { padding: 16px; }
}
</style>
