<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
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
  createStudent,
  createStudentAccount,
  downloadImportTemplate,
  getStudentById,
  getStudentImssBlob,
  getStudents,
  updateStudent,
  uploadStudentImssFile,
  uploadStudentsExcel,
  validateStudentImss,
} from '../api/studentsApi'
import type {
  CreateStudentAccountResponse,
  StudentDetail,
  StudentImportResult,
  StudentListItem,
  StudentRequest,
  StudentShift,
} from '../types/students'

interface StudentRow extends StudentListItem {
  actions?: string
}

const students = ref<StudentRow[]>([])
const allStudentsCache = ref<StudentRow[]>([])
const careerOptions = ref<OptionItem[]>([])
const periodOptions = ref<OptionItem[]>([])

const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')

// Filtros
const searchQuery = ref('')
const selectedCareerId = ref<number | undefined>(undefined)
const selectedShift = ref<string>('')
const selectedImssFilter = ref<string>('')
const currentPage = ref(1)
const totalItems = ref(0)

// Autocomplete
const isSearchFocused = ref(false)
const isAutocompleteOpen = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | null = null

const columns: { key: keyof StudentRow & string; label: string }[] = [
  { key: 'controlNumber', label: 'No. Control' },
  { key: 'fullName', label: 'Nombre Completo' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'currentSemester', label: 'Sem.' },
  { key: 'curriculumProgressPercentage', label: '% Créditos' },
  { key: 'shift', label: 'Turno' },
  { key: 'hasValidImss', label: 'Vigencia IMSS' },
  { key: 'hasUserAccount', label: 'Cuenta' },
  { key: 'actions', label: 'Acciones' },
]

// Modal de Alta / Edición de Alumno (Caso Específico)
const isStudentModalOpen = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)
const isSubmittingStudent = ref(false)
const selectedDetail = ref<StudentDetail | null>(null)
const imssDocumentDate = ref('')
const imssDeclaredDate = ref('')
const imssRejectionReason = ref('')
const staffImssFile = ref<File | null>(null)

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
const autocompleteSuggestions = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return []
  const map = new Map<number, StudentRow>()
  for (const s of allStudentsCache.value) map.set(s.id, s)
  for (const s of students.value) map.set(s.id, s)
  const pool = Array.from(map.values())
  return pool
    .filter(
      (s) =>
        s.controlNumber.toLowerCase().includes(query) ||
        s.fullName.toLowerCase().includes(query) ||
        s.careerCode.toLowerCase().includes(query),
    )
    .slice(0, 8)
})

const hasActiveFilters = computed(() => {
  return (
    searchQuery.value.trim() !== '' ||
    selectedCareerId.value !== undefined ||
    selectedShift.value !== '' ||
    selectedImssFilter.value !== ''
  )
})

function formatShift(shift: StudentShift | string): string {
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

async function loadData(): Promise<void> {
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
      careerId: selectedCareerId.value || undefined,
      shift: selectedShift.value || undefined,
      hasValidImss,
    })
    students.value = res.items || []
    totalItems.value = res.totalItems || students.value.length

    // Guardar en cache para sugerencias de autocomplete
    if (allStudentsCache.value.length === 0 && students.value.length > 0) {
      allStudentsCache.value = [...students.value]
    } else if (!hasActiveFilters.value && students.value.length > 0) {
      allStudentsCache.value = [...students.value]
    }
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Error al cargar padrón de alumnos.'
  } finally {
    isLoading.value = false
  }
}

function handleSearchInput(): void {
  isAutocompleteOpen.value = searchQuery.value.trim().length > 0
}

function handleSearchFocus(): void {
  isSearchFocused.value = true
  if (searchQuery.value.trim().length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  // Retraso para permitir click en sugerencia
  setTimeout(() => {
    isSearchFocused.value = false
    isAutocompleteOpen.value = false
  }, 220)
}

function selectSuggestion(suggestion: StudentListItem): void {
  searchQuery.value = suggestion.controlNumber
  isAutocompleteOpen.value = false
  loadData()
}

function clearSearch(): void {
  searchQuery.value = ''
  isAutocompleteOpen.value = false
  loadData()
}

function resetAllFilters(): void {
  searchQuery.value = ''
  selectedCareerId.value = undefined
  selectedShift.value = ''
  selectedImssFilter.value = ''
  isAutocompleteOpen.value = false
  loadData()
}

function openCreateModal(): void {
  isEditing.value = false
  editingId.value = null
  selectedDetail.value = null
  staffImssFile.value = null
  imssDeclaredDate.value = ''; imssDocumentDate.value = ''; imssRejectionReason.value = ''
  Object.assign(studentForm, {
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
  try {
    const detail = await getStudentById(row.id)
    isEditing.value = true
    editingId.value = detail.id
    selectedDetail.value = detail
    staffImssFile.value = null
    imssDeclaredDate.value = ''; imssDocumentDate.value = detail.imssDocumentDate || ''; imssRejectionReason.value = ''
    Object.assign(studentForm, {
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
    errorMessage.value = err?.response?.data?.message || 'No se pudo cargar el detalle del estudiante.'
  }
}

async function handleSaveStudent(): Promise<void> {
  isSubmittingStudent.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    if (staffImssFile.value && !imssDeclaredDate.value) throw new Error('Indica la fecha de expedición de la constancia IMSS.')
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
      if (staffImssFile.value) {
        await uploadStudentImssFile(editingId.value, staffImssFile.value, imssDeclaredDate.value)
      }
      successMessage.value = `Expediente de ${payload.controlNumber} actualizado correctamente.`
    } else {
      const created = await createStudent(payload)
      if (staffImssFile.value) {
        await uploadStudentImssFile(created.id, staffImssFile.value, imssDeclaredDate.value)
      }
      successMessage.value = `Alumno ${payload.controlNumber} agregado al padrón exitosamente.`
    }
    isStudentModalOpen.value = false
    await loadData()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.detail || err?.message || 'Error al guardar los datos del alumno.'
  } finally {
    isSubmittingStudent.value = false
  }
}

async function handleToggleImssValidation(id: number, isValid: boolean): Promise<void> {
  try {
    if (isValid && !imssDocumentDate.value) {
      errorMessage.value = 'Confirma la fecha de expedición que aparece en el PDF.'
      return
    }
    if (!isValid && !imssRejectionReason.value.trim()) {
      errorMessage.value = 'Indica el motivo de rechazo.'
      return
    }
    selectedDetail.value = await validateStudentImss(id, isValid, imssDocumentDate.value || undefined, imssRejectionReason.value || undefined)
    successMessage.value = isValid
      ? 'Vigencia de Derechos IMSS marcada como válida.'
      : 'Constancia IMSS rechazada con observaciones.'
    await loadData()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo actualizar la validación de IMSS.'
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
    isAccountModalOpen.value = true
    await loadData()
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'No se pudo generar la cuenta de usuario para el alumno.'
  }
}

async function handleDownloadTemplate(): Promise<void> {
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
  }
}

function handleExcelFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  importFile.value = input.files?.[0] ?? null
}

function handleStaffImssFileChange(event: Event): void {
  const input = event.target as HTMLInputElement
  staffImssFile.value = input.files?.[0] ?? null
}

async function handleRunImport(): Promise<void> {
  if (!importFile.value || !importPeriodId.value) return
  isImporting.value = true
  errorMessage.value = ''
  try {
    importResult.value = await uploadStudentsExcel(importPeriodId.value, importFile.value)
    successMessage.value = `Importación finalizada: ${importResult.value.importedRecords} alumnos procesados.`
    await loadData()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Error al procesar el archivo Excel (.xlsx).'
  } finally {
    isImporting.value = false
  }
}

onMounted(async () => {
  await Promise.all([loadCatalogs(), loadData()])
})
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

      <div class="actions-group">
        <UiButton variant="secondary" class="action-btn" @click="handleDownloadTemplate">
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
        <UiButton variant="primary" class="action-btn primary" @click="openCreateModal">
          <svg viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2.2" fill="none">
            <line x1="12" y1="5" x2="12" y2="19" />
            <line x1="5" y1="12" x2="19" y2="12" />
          </svg>
          Agregar Alumno (Caso Específico)
        </UiButton>
      </div>
    </div>

    <!-- Feedback Alerts -->
    <UiAlert v-if="errorMessage" tone="danger" class="mb-4">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" class="mb-4">
      {{ successMessage }}
    </UiAlert>

    <!-- Main Container -->
    <UiCard class="main-card">
      <!-- Toolbar de Filtros Modernos con Autocomplete y Comboboxes -->
      <div class="filters-toolbar">
        <!-- 1. Buscador con Autocomplete -->
        <div class="filter-col search-col">
          <label class="filter-label">Buscar alumno</label>
          <div class="autocomplete-container">
            <div class="search-input-wrapper">
              <svg class="search-icon" viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              <input
                v-model="searchQuery"
                type="text"
                class="modern-input"
                placeholder="No. Control o Nombre..."
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="loadData"
              />
              <button
                v-if="searchQuery"
                type="button"
                class="clear-search-btn"
                aria-label="Limpiar búsqueda"
                @click="clearSearch"
              >
                ✕
              </button>
            </div>

            <!-- Sugerencias Flotantes Autocomplete -->
            <div v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0" class="suggestions-dropdown">
              <div
                v-for="item in autocompleteSuggestions"
                :key="item.id"
                class="suggestion-item"
                @mousedown.prevent="selectSuggestion(item)"
              >
                <div class="suggestion-left">
                  <span class="suggestion-control">{{ item.controlNumber }}</span>
                  <span class="suggestion-name">{{ item.fullName }}</span>
                </div>
                <div class="suggestion-right">
                  <span class="suggestion-career">{{ item.careerCode }}</span>
                  <span class="suggestion-progress">{{ item.curriculumProgressPercentage }}%</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- 2. Combobox Carrera -->
        <div class="filter-col">
          <label class="filter-label">Carrera</label>
          <div class="select-wrapper">
            <select v-model="selectedCareerId" class="modern-select" @change="loadData">
              <option :value="undefined">Todas las carreras</option>
              <option v-for="c in careerOptions" :key="c.id" :value="c.id">
                {{ c.label }}
              </option>
            </select>
            <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <!-- 3. Combobox Turno -->
        <div class="filter-col">
          <label class="filter-label">Turno</label>
          <div class="select-wrapper">
            <select v-model="selectedShift" class="modern-select" @change="loadData">
              <option value="">Todos los turnos</option>
              <option value="morning">Matutino</option>
              <option value="afternoon">Vespertino</option>
              <option value="night">Nocturno</option>
            </select>
            <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <!-- 4. Combobox Vigencia IMSS -->
        <div class="filter-col">
          <label class="filter-label">Vigencia IMSS</label>
          <div class="select-wrapper">
            <select v-model="selectedImssFilter" class="modern-select" @change="loadData">
              <option value="">Todos los estados</option>
              <option value="true">Con IMSS Vigente</option>
              <option value="false">Pendiente de IMSS</option>
            </select>
            <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <!-- Botones de Acción de Filtro -->
        <div class="filter-actions">
          <UiButton
            v-if="hasActiveFilters"
            variant="secondary"
            class="reset-filters-btn"
            title="Restablecer filtros"
            @click="resetAllFilters"
          >
            Limpiar
          </UiButton>
          <UiButton variant="primary" class="apply-filter-btn" :disabled="isLoading" @click="loadData">
            {{ isLoading ? 'Buscando...' : 'Filtrar' }}
          </UiButton>
        </div>
      </div>

      <!-- Tabla de Estudiantes con Alineación Perfecta -->
      <UiTable label="Listado del Padrón de Alumnos" :columns="columns" :rows="students">
        <template #cell-controlNumber="{ row }">
          <span class="control-number-cell">{{ row.controlNumber }}</span>
        </template>

        <template #cell-fullName="{ row }">
          <span class="fullname-cell">{{ row.fullName }}</span>
        </template>

        <template #cell-careerCode="{ row }">
          <span class="career-pill">{{ row.careerCode }}</span>
        </template>

        <template #cell-currentSemester="{ row }">
          <span class="semester-cell">{{ row.currentSemester }}°</span>
        </template>

        <template #cell-curriculumProgressPercentage="{ row }">
          <span
            class="progress-badge"
            :class="{ qualified: row.curriculumProgressPercentage >= 70 }"
          >
            {{ row.curriculumProgressPercentage }}%
          </span>
        </template>

        <template #cell-shift="{ row }">
          <span class="shift-cell">{{ formatShift(row.shift) }}</span>
        </template>

        <template #cell-hasValidImss="{ row }">
          <UiBadge :tone="row.hasValidImss ? 'success' : 'warning'">
            {{ row.hasValidImss ? 'Vigente' : 'Pendiente' }}
          </UiBadge>
        </template>

        <template #cell-hasUserAccount="{ row }">
          <UiBadge :tone="row.hasUserAccount ? 'info' : 'warning'">
            {{ row.hasUserAccount ? 'Activa' : 'Sin cuenta' }}
          </UiBadge>
        </template>

        <template #cell-actions="{ row }">
          <div class="row-actions">
            <button
              type="button"
              class="table-btn secondary"
              title="Ver expediente y vigencia IMSS"
              @click="openEditModal(row)"
            >
              Editar / IMSS
            </button>
            <button
              v-if="!row.hasUserAccount"
              type="button"
              class="table-btn primary"
              title="Generar credenciales para el alumno"
              @click="handleCreateAccount(row)"
            >
              Crear Cuenta
            </button>
          </div>
        </template>
      </UiTable>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL 1: ALTA / EDICIÓN INDIVIDUAL DE ALUMNO (CASO ESPECÍFICO)      -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isStudentModalOpen"
      size="lg"
      :title="isEditing ? `Editar Expediente · ${studentForm.controlNumber}` : 'Agregar Alumno (Caso Específico)'"
    >
      <div class="modal-form-grid">
        <UiField label="No. de Control *" hint="Ej. I23050363">
          <input v-model="studentForm.controlNumber" type="text" class="modern-input" placeholder="I23050999" />
        </UiField>

        <UiField label="Carrera *">
          <select v-model.number="studentForm.careerId" class="modern-select">
            <option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
          </select>
        </UiField>

        <UiField label="Nombre(s) *">
          <input v-model="studentForm.firstName" type="text" class="modern-input" placeholder="Nombre del alumno" />
        </UiField>

        <UiField label="Primer Apellido *">
          <input v-model="studentForm.firstSurname" type="text" class="modern-input" placeholder="Apellido paterno" />
        </UiField>

        <UiField label="Segundo Apellido">
          <input v-model="studentForm.secondSurname" type="text" class="modern-input" placeholder="Apellido materno" />
        </UiField>

        <UiField label="CURP">
          <input v-model="studentForm.curp" type="text" class="modern-input" maxlength="18" placeholder="18 caracteres" />
        </UiField>

        <UiField label="Semestre Actual (1-15) *">
          <input v-model.number="studentForm.currentSemester" type="number" class="modern-input" min="1" max="15" />
        </UiField>

        <UiField label="Créditos Aprobados *" hint="Total retícula: 260 (≥182 = 70%)">
          <input v-model.number="studentForm.completedCredits" type="number" class="modern-input" min="0" max="300" />
        </UiField>

        <UiField label="Turno *">
          <select v-model="studentForm.shift" class="modern-select">
            <option value="morning">Matutino</option>
            <option value="afternoon">Vespertino</option>
            <option value="night">Nocturno</option>
          </select>
        </UiField>

        <UiField label="Sexo (Estadístico 911)">
          <select v-model="studentForm.gender" class="modern-select">
            <option value="M">Hombre (M)</option>
            <option value="F">Mujer (F)</option>
          </select>
        </UiField>

        <UiField label="Correo Institucional">
          <input
            v-model="studentForm.institutionalEmail"
            type="email"
            class="modern-input"
            placeholder="L23050999@monclova.tecnm.mx"
          />
        </UiField>

        <UiField label="Teléfono de Contacto">
          <input v-model="studentForm.phoneNumber" type="text" class="modern-input" placeholder="8661234567" />
        </UiField>
      </div>

      <!-- Sección de Constancia de Vigencia IMSS en edición o alta -->
      <div class="imss-box">
        <div class="imss-box-header">
          <strong>Constancia de Vigencia de Derechos del IMSS (PDF)</strong>
          <span v-if="selectedDetail" class="imss-status-tag" :class="{ valid: selectedDetail.hasValidImss }">
            {{ selectedDetail.hasValidImss ? 'Vigente y Validado' : 'Pendiente de Validación' }}
          </span>
        </div>
        <p class="imss-box-desc">
          Revisa el PDF y confirma su fecha de expedición antes de aprobarlo.
        </p>
        <input type="file" accept="application/pdf" class="file-input" @change="handleStaffImssFileChange" />
        <UiField v-if="staffImssFile" v-model="imssDeclaredDate" type="date" label="Fecha de expedición declarada para el nuevo PDF IMSS" />

        <label>Fecha de expedición confirmada en el PDF <input v-model="imssDocumentDate" type="date" /></label>
        <label>Observaciones de rechazo <textarea v-model="imssRejectionReason" maxlength="1000" /></label>
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
            variant="secondary"
            size="sm"
            @click="handleToggleImssValidation(selectedDetail.id, !selectedDetail.hasValidImss)"
          >
            {{ selectedDetail.hasValidImss ? 'Rechazar IMSS' : 'Validar Vigencia IMSS' }}
          </UiButton>
        </div>
      </div>

      <template #footer>
        <UiButton variant="secondary" @click="isStudentModalOpen = false">Cancelar</UiButton>
        <UiButton variant="primary" :disabled="isSubmittingStudent" @click="handleSaveStudent">
          {{ isSubmittingStudent ? 'Guardando...' : isEditing ? 'Guardar Cambios' : 'Registrar Alumno' }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 2: IMPORTACIÓN MASIVA DESDE EXCEL (.XLSX)                     -->
    <!-- ================================================================== -->
    <UiDialog v-model="isImportModalOpen" size="md" title="Importar Padrón de Alumnos desde Excel (.xlsx)">
      <div class="import-modal-content">
        <p class="import-intro-text">
          Carga el archivo Excel con los alumnos que están en condiciones de realizar su Servicio
          Social o Prácticas Profesionales en el periodo seleccionado.
        </p>

        <UiField label="Periodo Escolar Destino *">
          <select v-model.number="importPeriodId" class="modern-select">
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </UiField>

        <UiField label="Archivo Excel (.xlsx) *">
          <input type="file" accept=".xlsx" class="file-input" @change="handleExcelFileChange" />
        </UiField>

        <div v-if="importResult" class="import-summary-box">
          <strong>Resultado de la Importación ({{ importResult.fileName }}):</strong>
          <ul>
            <li>Total de filas leídas: <strong>{{ importResult.totalRecords }}</strong></li>
            <li>Alumnos importados/actualizados: <strong>{{ importResult.importedRecords }}</strong></li>
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
          <span class="cred-label">Contraseña Temporal:</span>
          <code class="cred-pass">{{ generatedAccount.temporaryPassword }}</code>
        </div>
      </div>
      <template #footer>
        <UiButton variant="primary" @click="isAccountModalOpen = false">Entendido</UiButton>
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
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.title-row {
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
  height: 38px;
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
