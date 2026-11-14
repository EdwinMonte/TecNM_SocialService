<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import { problemMessage } from '../../../shared/api/problemDetails'
import ExpedientChecklist from '../../expedients/components/ExpedientChecklist.vue'
import DocumentReviewDialog from '../../expedients/components/DocumentReviewDialog.vue'
import { http } from '../../../shared/api/http'
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
import { openPrintableDocument } from '../../../shared/utils/printDocument'
import {
  getDocumentBlob,
  getStudentDocuments,
} from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import {
  approveInternship,
  completeInternship,
  getInternshipById,
  getInternshipPrintData,
  getInternships,
  releaseInternship,
  sendInternshipSchoolAct,
  updateInternshipExpedient,
} from '../api/internshipsApi'
import type { InternshipDetail, InternshipListItem } from '../types/internships'

interface InternshipRow extends InternshipListItem {
  actions?: string
}

const items = ref<InternshipRow[]>([])
const allItemsCache = ref<InternshipRow[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')

// Paginación estandarizada (10 por página)
const currentPage = ref(1)
const pageSize = 10
const totalItems = ref(0)
const totalPages = ref(1)

// Filtros y Autocomplete con Debounce
const searchQuery = ref('')
const statusFilter = ref('')
const isAutocompleteOpen = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | null = null

// KPIs animados compactos
const animatedKpis = reactive({
  total: 0,
  requested: 0,
  active: 0,
  released: 0,
})

function updateAnimatedKpis(): void {
  const source = allItemsCache.value.length > 0 ? allItemsCache.value : items.value
  const targets = {
    total: totalItems.value || source.length,
    requested: source.filter((s) => s.status === 'requested').length,
    active: source.filter((s) => s.status === 'active' || s.status === 'approved' || s.status === 'accepted').length,
    released: source.filter((s) => s.status === 'released').length,
  }

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (prefersReduced) {
    Object.assign(animatedKpis, targets)
    return
  }

  const duration = 520
  const start = performance.now()
  function tick(now: number): void {
    const progress = Math.min((now - start) / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)
    animatedKpis.total = Math.round(targets.total * ease)
    animatedKpis.requested = Math.round(targets.requested * ease)
    animatedKpis.active = Math.round(targets.active * ease)
    animatedKpis.released = Math.round(targets.released * ease)
    if (progress < 1) requestAnimationFrame(tick)
  }
  requestAnimationFrame(tick)
}

const autocompleteSuggestions = computed(() => {
  const query = searchQuery.value.trim().toLowerCase()
  if (!query) return []
  const map = new Map<number, InternshipRow>()
  for (const s of allItemsCache.value) map.set(s.id, s)
  for (const s of items.value) map.set(s.id, s)
  const pool = Array.from(map.values())

  return pool
    .filter(
      (s) =>
        s.studentControlNumber.toLowerCase().includes(query) ||
        s.studentFullName.toLowerCase().includes(query) ||
        s.careerCode.toLowerCase().includes(query) ||
        (s.companyName && s.companyName.toLowerCase().includes(query)),
    )
    .slice(0, 7)
})

function handleSearchInput(): void {
  isAutocompleteOpen.value = searchQuery.value.trim().length > 0
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    currentPage.value = 1
    void loadData()
  }, 260)
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 200)
}

function selectSuggestion(suggestion: InternshipListItem): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = suggestion.studentFullName
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function clearSearch(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = ''
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function resetFilters(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  searchQuery.value = ''
  statusFilter.value = ''
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadData()
}

function toggleKpiStatus(targetStatus: string): void {
  statusFilter.value = statusFilter.value === targetStatus ? '' : targetStatus
  currentPage.value = 1
  void loadData()
}

// Modal de Gestión de Trámite de Prácticas
const isDetailOpen = ref(false)
const selectedDetail = ref<InternshipDetail | null>(null)
const studentDocs = ref<ExpedientDocument[]>([])
const isActionRunning = ref(false)

// Modal de Observaciones de Coordinación y Emisión de CP + Observaciones Generales del Expediente
const isObservationsModalOpen = ref(false)
const observationsModalMode = ref<'approve' | 'general'>('general')
const approveObservations = ref('Vigencia IMSS verificada. Se emite Carta de Presentación.')
const generalObservations = ref('')
const isSavingObservations = ref(false)

const observationsValidation = useFormValidation(() => {
  const errors: Record<string, string[]> = {}
  if (observationsModalMode.value === 'approve') {
    if (approveObservations.value.trim().length > 1000) {
      errors.observations = ['Las observaciones no pueden exceder 1000 caracteres.']
    }
  } else {
    if (generalObservations.value.trim().length > 2000) {
      errors.observations = ['Las observaciones del expediente admiten máximo 2000 caracteres.']
    }
  }
  return errors
})

// Modal de Dictamen / Observación de Documento individual
const isDocReviewOpen = ref(false)
const selectedDocForReview = ref<ExpedientDocument | null>(null)

function openObservationsModal(mode: 'approve' | 'general'): void {
  if (!selectedDetail.value) return
  observationsModalMode.value = mode
  observationsValidation.reset()
  if (mode === 'approve') {
    approveObservations.value =
      selectedDetail.value.observations ||
      'Vigencia IMSS verificada. Se emite Carta de Presentación.'
  } else {
    generalObservations.value = selectedDetail.value.observations || ''
  }
  isObservationsModalOpen.value = true
}

function openDocReviewModal(doc: ExpedientDocument): void {
  selectedDocForReview.value = doc
  isDocReviewOpen.value = true
}

async function handleSaveGeneralObservations(): Promise<void> {
  if (!selectedDetail.value || isSavingObservations.value) return
  if (!observationsValidation.validate()) return
  isSavingObservations.value = true
  errorMessage.value = ''
  try {
    await updateInternshipExpedient(selectedDetail.value.id, {
      isExpedientComplete: Boolean(selectedDetail.value.isExpedientComplete),
      originalTerminationLetterReceived: Boolean(selectedDetail.value.originalTerminationLetterReceived),
      observations: generalObservations.value.trim() || null,
    })
    successMessage.value = 'Observaciones de coordinación guardadas correctamente.'
    isObservationsModalOpen.value = false
    await refreshCurrentDetail()
  } catch (err: any) {
    observationsValidation.apply(err, 'No se pudieron guardar las observaciones.')
  } finally {
    isSavingObservations.value = false
  }
}

const columns: { key: keyof InternshipRow & string; label: string }[] = [
  { key: 'studentControlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'companyName', label: 'Empresa' },
  { key: 'presentationLetterFolio', label: 'Folio CP' },
  { key: 'documentProgressPercentage', label: 'Avance Doc.' },
  { key: 'status', label: 'Estatus' },
  { key: 'actions', label: 'Acciones' },
]

const statusLabels: Record<string, string> = {
  requested: 'Solicitud CP Recibida',
  approved: 'CP Emitida (Esperando CA)',
  accepted: 'CA Validada',
  active: 'En Curso (Activo)',
  completed_pending_review: 'Listo para Liberar',
  released: 'Liberado (PPC)',
  cancelled: 'Cancelado',
  non_compliant: 'PNC',
}

function formatStatus(status: string): string {
  return statusLabels[status] || status
}

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getInternships({
      page: currentPage.value,
      pageSize,
      status: statusFilter.value || undefined,
      search: searchQuery.value.trim() || undefined,
    })
    items.value = res.items || []
    totalItems.value = res.totalItems ?? items.value.length
    totalPages.value = Math.max(1, res.totalPages ?? Math.ceil(totalItems.value / pageSize))

    if (allItemsCache.value.length === 0 && items.value.length > 0) {
      allItemsCache.value = [...items.value]
    } else if (!searchQuery.value && !statusFilter.value && items.value.length > 0) {
      allItemsCache.value = [...items.value]
    }
    updateAnimatedKpis()
  } catch (err: any) {
    errorMessage.value =
      problemMessage(err, 'Error al cargar trámites de Prácticas Profesionales.')
  } finally {
    isLoading.value = false
  }
}

async function openDetailModal(row: InternshipListItem): Promise<void> {
  errorMessage.value = ''
  try {
    const [detail, docs] = await Promise.all([
      getInternshipById(row.id),
      getStudentDocuments(row.studentId, { tramiteType: 'internship', tramiteId: row.id }).catch(
        () => [],
      ),
    ])
    selectedDetail.value = detail
    studentDocs.value = docs
    approveObservations.value =
      detail.observations || 'Vigencia IMSS verificada. Se emite Carta de Presentación.'
    generalObservations.value = detail.observations || ''
    isDetailOpen.value = true
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo cargar el detalle del trámite.')
  }
}

async function refreshCurrentDetail(): Promise<void> {
  if (!selectedDetail.value) return
  const id = selectedDetail.value.id
  const studentId = selectedDetail.value.student.id
  const [detail, docs] = await Promise.all([
    getInternshipById(id),
    getStudentDocuments(studentId, { tramiteType: 'internship', tramiteId: id }).catch(() => []),
  ])
  selectedDetail.value = detail
  studentDocs.value = docs
  await loadData()
}

async function handleApproveRequest(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  observationsModalMode.value = 'approve'
  if (!observationsValidation.validate()) return
  actionValidation.reset()
  isActionRunning.value = true
  errorMessage.value = ''
  try {
    await approveInternship(selectedDetail.value.id, approveObservations.value.trim())
    successMessage.value =
      'Solicitud aprobada. Se generó automáticamente la Carta de Presentación con folio EJ26/PR...'
    isObservationsModalOpen.value = false
    await refreshCurrentDetail()
  } catch (err: any) {
    observationsValidation.apply(err, 'No se pudo aprobar la solicitud.')
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'No se pudo aprobar la solicitud.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleDocReviewed(reviewedDoc: ExpedientDocument): Promise<void> {
  successMessage.value = `Documento ${reviewedDoc.documentCode} ${reviewedDoc.status === 'approved' ? 'aprobado' : 'observado'}.`
  await refreshCurrentDetail()
}

async function handleViewDocPdf(docId: number): Promise<void> {
  try {
    const blob = await getDocumentBlob(docId)
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    errorMessage.value = 'Archivo PDF de demostración (sin binario en disco local).'
  }
}

async function handlePrint(docKey: 'presentation_letter' | 'evaluation' | 'release_certificate'): Promise<void> {
  if (!selectedDetail.value) return
  try {
    if (docKey !== 'release_certificate' && docKey !== 'presentation_letter') {
      const response = await http.get(`/internships/${selectedDetail.value.id}/pdf/${docKey}`, { responseType: 'blob' })
      const url = URL.createObjectURL(response.data)
      const link = document.createElement('a')
      link.href = url
      link.download = `${docKey}.pdf`
      link.click()
      URL.revokeObjectURL(url)
      return
    }
    const data = await getInternshipPrintData(selectedDetail.value.id, docKey)
    openPrintableDocument(data)
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'Plantilla aún no disponible.')
  }
}

async function handleComplete(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await completeInternship(selectedDetail.value.id, new Date().toISOString().slice(0, 10))
    successMessage.value = 'Documentación EV y CT validada. Trámite listo para liberar.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value =
      problemMessage(err, 'Verifica que EV y CT estén aprobados.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleRelease(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await releaseInternship(selectedDetail.value.id)
    successMessage.value = 'Constancia de Acreditación de Prácticas (PPC) emitida exitosamente.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value =
      problemMessage(err, 'Verifica que CA, EV y CT estén aprobados.')
  } finally {
    isActionRunning.value = false
  }
}

async function handleSendSchoolAct(): Promise<void> {
  if (!selectedDetail.value) return
  if (isActionRunning.value) return
  actionValidation.reset()
  isActionRunning.value = true
  try {
    await sendInternshipSchoolAct(selectedDetail.value.id)
    successMessage.value = 'Acta de Prácticas turnada a Servicios Escolares con folio DGTV.'
    await refreshCurrentDetail()
  } catch (err: any) {
    actionValidation.apply(err, 'No se pudo completar la operación.')
    errorMessage.value = problemMessage(err, 'No se pudo generar el acta.')
  } finally {
    isActionRunning.value = false
  }
}

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
})

onMounted(() => {
  void loadData()
})

const actionValidation = useFormValidation()
watchValidation(() => [isDetailOpen.value, selectedDetail.value?.id], () => actionValidation.reset())
watchValidation(isObservationsModalOpen, () => observationsValidation.reset())
</script>

<template>
  <UiBasePage :fluid="true">
    <!-- Encabezado Ejecutivo Compacto -->
    <header class="exec-header reveal-step reveal-1">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">COORDINACIÓN DE PRÁCTICAS PROFESIONALES</span>
          <span class="exec-period">· Sector Productivo (200 h)</span>
        </div>
        <h1 class="exec-title">Gestión de Prácticas Profesionales (Vinculación)</h1>
        <p class="exec-subtitle">
          Emisión de Cartas de Presentación (`EJ26/PR...`), validación de Oficios de Aceptación membretados (`arturo.martinez@monclova.tecnm.mx`) y acreditación
        </p>
      </div>

      <div class="exec-kpis">
        <button
          type="button"
          class="mini-kpi"
          :class="{ 'mini-kpi--active': statusFilter === '' }"
          @click="resetFilters"
        >
          <span class="mini-kpi__label">Total Padrón</span>
          <strong class="mini-kpi__value">{{ animatedKpis.total }}</strong>
        </button>
        <button
          type="button"
          class="mini-kpi mini-kpi--gold"
          :class="{ 'mini-kpi--active': statusFilter === 'requested' }"
          @click="toggleKpiStatus('requested')"
        >
          <span class="mini-kpi__label">Solicitudes CP</span>
          <strong class="mini-kpi__value">{{ animatedKpis.requested }}</strong>
        </button>
        <button
          type="button"
          class="mini-kpi mini-kpi--navy"
          :class="{ 'mini-kpi--active': statusFilter === 'active' }"
          @click="toggleKpiStatus('active')"
        >
          <span class="mini-kpi__label">En Empresa</span>
          <strong class="mini-kpi__value">{{ animatedKpis.active }}</strong>
        </button>
        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': statusFilter === 'released' }"
          @click="toggleKpiStatus('released')"
        >
          <span class="mini-kpi__label">Liberados</span>
          <strong class="mini-kpi__value">{{ animatedKpis.released }}</strong>
        </button>
      </div>
    </header>

    <UiAlert v-if="errorMessage && !(actionValidation.hasErrors.value || observationsValidation.hasErrors.value)" tone="danger" class="compact-alert">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" class="compact-alert">
      {{ successMessage }}
    </UiAlert>

    <UiCard class="workspace-card reveal-step reveal-2">
      <div class="filters-bar">
        <div class="search-box">
          <label class="filter-label" for="pp-search-input">Buscar por Matrícula, Alumno, Carrera o Empresa</label>
          <div class="autocomplete-container">
            <div class="input-wrapper">
              <svg class="search-icon" viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              <input
                id="pp-search-input"
                v-model="searchQuery"
                type="text"
                class="modern-input"
                placeholder="Ej. I22050289, Miguel, IIND o empresa..."
                autocomplete="off"
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="isAutocompleteOpen = false; currentPage = 1; loadData()"
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
            <div v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0" class="suggestions-dropdown" role="listbox">
              <div
                v-for="item in autocompleteSuggestions"
                :key="item.id"
                class="suggestion-item"
                role="option"
                @mousedown.prevent="selectSuggestion(item)"
              >
                <div class="suggestion-left">
                  <span class="suggestion-control">{{ item.studentControlNumber }}</span>
                  <span class="suggestion-name">{{ item.studentFullName }}</span>
                </div>
                <div class="suggestion-right">
                  <span class="suggestion-career">{{ item.careerCode }}</span>
                  <span class="suggestion-progress">{{ item.documentProgressPercentage }}%</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <div class="select-box">
          <label class="filter-label" for="pp-status-select">Filtrar por Estatus</label>
          <div class="select-wrapper">
            <select
              id="pp-status-select"
              v-model="statusFilter"
              class="modern-select"
              @change="currentPage = 1; loadData()"
            >
              <option value="">Todos los estatus</option>
              <option value="requested">Solicitud CP Recibida</option>
              <option value="approved">Carta Presentación Emitida</option>
              <option value="active">Activo en Empresa</option>
              <option value="completed_pending_review">Listo para Liberar</option>
              <option value="released">Liberado (100%)</option>
            </select>
            <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none" aria-hidden="true">
              <polyline points="6 9 12 15 18 9" />
            </svg>
          </div>
        </div>

        <div class="filter-actions">
          <UiButton
            v-if="searchQuery || statusFilter"
            variant="secondary"
            class="reset-btn"
            title="Restablecer filtros"
            @click="resetFilters"
          >
            Limpiar
          </UiButton>
          <UiButton variant="primary" class="filter-btn" :disabled="isLoading" @click="currentPage = 1; loadData()">
            {{ isLoading ? 'Buscando...' : 'Buscar' }}
          </UiButton>
        </div>
      </div>

      <UiTable label="Trámites de Prácticas Profesionales" :columns="columns" :rows="items">
        <template #cell-companyName="{ row }">
          <span>{{ row.companyName || 'En espera de Oficio CA' }}</span>
        </template>

        <template #cell-presentationLetterFolio="{ row }">
          <strong>{{ row.presentationLetterFolio || 'Por emitir' }}</strong>
        </template>

        <template #cell-documentProgressPercentage="{ row }">
          <div class="progress-cell">
            <div class="progress-track">
              <div
                class="progress-fill"
                :style="{ width: `${row.documentProgressPercentage}%` }"
              />
            </div>
            <span class="progress-text">{{ row.documentProgressPercentage }}%</span>
          </div>
        </template>

        <template #cell-status="{ row }">
          <UiBadge :tone="row.status === 'released' ? 'success' : row.status === 'requested' ? 'info' : 'warning'">
            {{ formatStatus(row.status) }}
          </UiBadge>
        </template>

        <template #cell-actions="{ row }">
          <UiButton variant="primary" size="sm" @click="openDetailModal(row)">
            Gestionar Prácticas
          </UiButton>
        </template>
      </UiTable>

      <!-- Barra de Paginación Estandarizada (10 por página) -->
      <nav class="pagination-bar" aria-label="Paginación de trámites de Prácticas Profesionales">
        <span class="pagination-info">
          Mostrando <strong>{{ items.length }}</strong> de <strong>{{ totalItems }}</strong> trámites · Página <strong>{{ currentPage }}</strong> de <strong>{{ totalPages }}</strong>
        </span>
        <div class="pagination-controls">
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="isLoading || currentPage <= 1"
            @click="currentPage--; loadData()"
          >
            Anterior
          </UiButton>
          <UiButton
            variant="secondary"
            size="sm"
            :disabled="isLoading || currentPage >= totalPages"
            @click="currentPage++; loadData()"
          >
            Siguiente
          </UiButton>
        </div>
      </nav>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL 1: GESTIÓN INTEGRAL DE PRÁCTICAS PROFESIONALES               -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isDetailOpen"
      size="xl"
      :title="
        selectedDetail
          ? `Expediente PP · ${selectedDetail.student.controlNumber} — ${selectedDetail.student.fullName}`
          : 'Detalle de Prácticas Profesionales'
      "
    >
      <div v-if="selectedDetail" class="detail-modal-stack">
        <UiFormErrors :validation="actionValidation" />

        <!-- Resumen de Folios y Estado -->
        <div class="summary-grid">
          <div class="summary-item">
            <small>Estatus Actual</small>
            <strong>{{ formatStatus(selectedDetail.status) }}</strong>
          </div>
          <div class="summary-item">
            <small>Empresa Receptora</small>
            <strong>{{ selectedDetail.company?.name || 'Pendiente de CA' }}</strong>
          </div>
          <div class="summary-item">
            <small>Folio Carta Presentación</small>
            <strong>{{ selectedDetail.presentationLetterFolio || 'Pendiente' }}</strong>
          </div>
          <div class="summary-item">
            <small>Constancia Liberación</small>
            <strong class="text-emerald">{{ selectedDetail.releaseCertificateFolio || 'Pendiente' }}</strong>
          </div>
        </div>

        <!-- Barra de Acciones de Plantillas y Modal de Observaciones -->
        <div class="detail-toolbar">
          <div class="detail-toolbar__group">
            <UiButton
              v-if="selectedDetail.presentationLetterFolio"
              variant="secondary"
              size="sm"
              @click="handlePrint('presentation_letter')"
            >
              Imprimir Carta Presentación ({{ selectedDetail.presentationLetterFolio }})
            </UiButton>
            <UiButton variant="secondary" size="sm" @click="handlePrint('evaluation')">
              Ver Formato Anexo XVII
            </UiButton>
            <UiButton
              v-if="selectedDetail.status === 'released'"
              variant="secondary"
              size="sm"
              @click="handlePrint('release_certificate')"
            >
              Imprimir Constancia Liberación (PPC)
            </UiButton>
          </div>

          <div class="detail-toolbar__group">
            <UiButton
              v-if="selectedDetail.status === 'requested'"
              variant="primary"
              size="sm"
              @click="openObservationsModal('approve')"
            >
              Dictaminar y Emitir Carta de Presentación
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              @click="openObservationsModal('general')"
            >
              Observaciones de Coordinación
            </UiButton>
          </div>
        </div>

        <!-- Resumen de Observaciones vigentes (si existen) -->
        <div v-if="selectedDetail.observations" class="observations-banner">
          <div class="observations-banner__text">
            <strong>Observaciones registradas:</strong>
            <span>{{ selectedDetail.observations }}</span>
          </div>
          <UiButton variant="secondary" size="sm" @click="openObservationsModal(selectedDetail.status === 'requested' ? 'approve' : 'general')">
            Editar observaciones
          </UiButton>
        </div>

        <!-- Checklist Documental -->
        <ExpedientChecklist
          :requirements="selectedDetail.documentRequirements"
          :documents="studentDocs"
          tramite-type="internship"
          :tramite-id="selectedDetail.id"
          :student-id="selectedDetail.student.id"
          :can-edit="false"
          @changed="refreshCurrentDetail"
        />

        <!-- Documentos Subidos en el Expediente (con revisión mediante modal) -->
        <div class="uploaded-docs-section">
          <strong class="section-heading">
            Documentos Subidos en el Expediente ({{ studentDocs.length }})
          </strong>
          <div
            v-if="studentDocs.length === 0"
            class="empty-docs-note"
          >
            El alumno aún no ha subido archivos PDF a este trámite.
          </div>
          <div v-else class="uploaded-docs-list">
            <div
              v-for="doc in studentDocs"
              :key="doc.id"
              class="uploaded-doc-row"
            >
              <div class="uploaded-doc-info">
                <div>
                  <strong>{{ doc.documentCode }}</strong> — {{ doc.originalFileName }}
                  <UiBadge
                    :tone="
                      doc.status === 'approved'
                        ? 'success'
                        : doc.status === 'rejected'
                          ? 'danger'
                          : 'warning'
                    "
                    style="margin-left: 0.4rem;"
                  >
                    {{
                      doc.status === 'approved'
                        ? 'Aprobado (SI)'
                        : doc.status === 'rejected'
                          ? 'Observado'
                          : 'En Revisión'
                    }}
                  </UiBadge>
                </div>
                <small v-if="doc.rejectionReason" class="doc-rejection-note">
                  Observación: {{ doc.rejectionReason }}
                </small>
              </div>
              <div class="uploaded-doc-actions">
                <UiButton variant="secondary" size="sm" @click="handleViewDocPdf(doc.id)">
                  Ver PDF
                </UiButton>
                <UiButton
                  v-if="doc.status === 'pending'"
                  variant="primary"
                  size="sm"
                  :disabled="isActionRunning"
                  @click="openDocReviewModal(doc)"
                >
                  Dictaminar / Observar
                </UiButton>
                <UiButton
                  v-else
                  variant="secondary"
                  size="sm"
                  @click="openDocReviewModal(doc)"
                >
                  Ver Dictamen
                </UiButton>
              </div>
            </div>
          </div>
        </div>

        <!-- Botones de Cierre y Liberación -->
        <div class="stage-actions-bar">
          <UiButton
            v-if="selectedDetail.status === 'active'"
            variant="primary"
            size="sm"
            :disabled="isActionRunning"
            @click="handleComplete"
          >
            Validar Cierre Documental (EV y CT)
          </UiButton>

          <UiButton
            v-if="
              selectedDetail.status === 'completed_pending_review' ||
              selectedDetail.status === 'active'
            "
            variant="primary"
            size="sm"
            :disabled="isActionRunning"
            @click="handleRelease"
          >
            Emitir Constancia de Liberación (Folio PPC)
          </UiButton>

          <UiButton
            v-if="selectedDetail.status === 'released' && !selectedDetail.schoolServicesActSent"
            variant="secondary"
            size="sm"
            :disabled="isActionRunning"
            @click="handleSendSchoolAct"
          >
            Generar Acta para Servicios Escolares (DGTV)
          </UiButton>
        </div>
      </div>

      <template #footer>
        <UiButton variant="secondary" @click="isDetailOpen = false">Cerrar</UiButton>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 2: GESTIÓN DE OBSERVACIONES DE COORDINACIÓN Y EMISIÓN CP     -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isObservationsModalOpen"
      size="md"
      :title="
        observationsModalMode === 'approve'
          ? 'Dictaminar Solicitud y Emitir Carta de Presentación'
          : 'Observaciones de Coordinación (Prácticas Profesionales)'
      "
    >
      <div v-if="selectedDetail" class="observations-modal-body">
        <UiFormErrors :validation="observationsValidation" />

        <div class="observations-student-pill">
          <strong>{{ selectedDetail.student.controlNumber }}</strong> · {{ selectedDetail.student.fullName }}
          <span>({{ selectedDetail.student.careerName }})</span>
        </div>

        <template v-if="observationsModalMode === 'approve'">
          <p class="observations-helper">
            Contacto confirmado: <strong>{{ selectedDetail.student.phoneNumber || 'Sin teléfono' }}</strong> ·
            <strong>{{ selectedDetail.student.institutionalEmail || 'Sin correo' }}</strong>.
            Registra las observaciones de coordinación para emitir la Carta de Presentación (`EJ26/PR...`).
          </p>

          <UiField
            v-model="approveObservations"
            as="textarea"
            label="Observaciones de Coordinación"
            placeholder="Ej. Vigencia IMSS verificada. Se emite Carta de Presentación."
            maxlength="1000"
            v-bind="observationsValidation.field('observations', 'Observaciones de Coordinación')"
            :disabled="isActionRunning"
          />
        </template>

        <template v-else>
          <p class="observations-helper">
            Consulta, redacta o actualiza las observaciones de seguimiento del expediente de Prácticas Profesionales.
          </p>

          <UiField
            v-model="generalObservations"
            as="textarea"
            label="Observaciones Administrativas del Expediente"
            placeholder="Escribe notas sobre el oficio de aceptación, seguimiento en empresa o acreditación..."
            maxlength="2000"
            v-bind="observationsValidation.field('observations', 'Observaciones del expediente')"
            :disabled="isSavingObservations"
          />
        </template>
      </div>

      <template #footer>
        <UiButton
          variant="secondary"
          :disabled="isActionRunning || isSavingObservations"
          @click="isObservationsModalOpen = false"
        >
          Cancelar
        </UiButton>

        <template v-if="observationsModalMode === 'approve'">
          <UiButton
            variant="primary"
            :disabled="isActionRunning"
            :loading="isActionRunning"
            @click="handleApproveRequest"
          >
            {{ isActionRunning ? 'Emitiendo...' : 'Aprobar Solicitud y Emitir Carta de Presentación (EJ26/PR...)' }}
          </UiButton>
        </template>

        <template v-else>
          <UiButton
            variant="primary"
            :disabled="isSavingObservations"
            :loading="isSavingObservations"
            @click="handleSaveGeneralObservations"
          >
            {{ isSavingObservations ? 'Guardando...' : 'Guardar Observaciones' }}
          </UiButton>
        </template>
      </template>
    </UiDialog>

    <!-- ================================================================== -->
    <!-- MODAL 3: DICTAMEN Y OBSERVACIONES DE DOCUMENTO INDIVIDUAL (PDF)    -->
    <!-- ================================================================== -->
    <DocumentReviewDialog
      v-model="isDocReviewOpen"
      :document="selectedDocForReview"
      :student-name="selectedDetail?.student.fullName"
      @reviewed="handleDocReviewed"
    />
  </UiBasePage>
</template>

<style scoped>
@keyframes slideUpFade {
  from {
    opacity: 0;
    transform: translateY(6px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.reveal-step {
  animation: slideUpFade 320ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

.reveal-1 {
  animation-delay: 20ms;
}

.reveal-2 {
  animation-delay: 60ms;
}

.exec-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  gap: 1rem;
  margin-bottom: 0.85rem;
  flex-wrap: wrap;
}

.exec-header__left {
  flex: 1;
  min-width: 280px;
}

.exec-eyebrow {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  margin-bottom: 0.25rem;
  flex-wrap: wrap;
}

.exec-badge {
  font-size: 0.68rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  padding: 0.15rem 0.5rem;
  border-radius: 4px;
  background: rgba(197, 160, 89, 0.16);
  color: #926d27;
}

.exec-period {
  font-size: 0.76rem;
  font-weight: 600;
  color: var(--color-text-muted, #64748b);
}

.exec-title {
  font-size: 1.35rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary, #0f172a);
  letter-spacing: -0.015em;
}

.exec-subtitle {
  color: var(--color-text-muted, #64748b);
  margin: 0.2rem 0 0;
  font-size: 0.84rem;
}

.exec-kpis {
  display: flex;
  gap: 0.55rem;
  flex-wrap: wrap;
}

.mini-kpi {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  padding: 0.45rem 0.85rem;
  min-width: 112px;
  border-radius: 10px;
  border: 1px solid var(--color-border, #e2e8f0);
  background: var(--surface-card, #ffffff);
  cursor: pointer;
  transition: transform 0.16s ease, border-color 0.16s ease, box-shadow 0.16s ease;
}

.mini-kpi:hover {
  transform: translateY(-2px);
  border-color: #94a3b8;
  box-shadow: 0 6px 14px -4px rgba(15, 23, 42, 0.08);
}

.mini-kpi--active {
  border-color: #1b396a;
  box-shadow: 0 0 0 2px rgba(27, 57, 106, 0.15);
}

.mini-kpi__label {
  font-size: 0.68rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-muted, #64748b);
}

.mini-kpi__value {
  font-size: 1.2rem;
  font-weight: 800;
  color: var(--color-text-primary, #0f172a);
  line-height: 1.2;
}

.mini-kpi--gold .mini-kpi__value {
  color: #b45309;
}

.mini-kpi--navy .mini-kpi__value {
  color: #1b396a;
}

.mini-kpi--emerald .mini-kpi__value {
  color: #15803d;
}

.compact-alert {
  margin-bottom: 0.75rem;
}

.filters-bar {
  display: flex;
  gap: 0.85rem;
  flex-wrap: wrap;
  align-items: flex-end;
  margin-bottom: 0.75rem;
}

.search-box {
  flex: 1.6;
  min-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.select-box {
  flex: 1.1;
  min-width: 210px;
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.filter-label {
  font-size: 0.78rem;
  font-weight: 600;
  color: var(--color-text-secondary, #475569);
}

.input-wrapper,
.select-wrapper {
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
  height: 36px;
  border-radius: 8px;
  border: 1px solid var(--field-border, #cbd5e1);
  background: var(--field-bg, #ffffff);
  padding: 0 2rem 0 2.25rem;
  font-size: 0.84rem;
  color: var(--field-text, #0f172a);
  outline: none;
  transition: all 0.15s ease;
  box-sizing: border-box;
}

.modern-input:focus {
  border-color: #1b396a;
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.14);
}

.modern-select {
  width: 100%;
  height: 36px;
  border-radius: 8px;
  border: 1px solid var(--field-border, #cbd5e1);
  background: var(--field-bg, #ffffff);
  padding: 0 2rem 0 0.75rem;
  font-size: 0.84rem;
  color: var(--field-text, #0f172a);
  outline: none;
  appearance: none;
  cursor: pointer;
  transition: all 0.15s ease;
  box-sizing: border-box;
}

.modern-select:focus {
  border-color: #1b396a;
  box-shadow: 0 0 0 3px rgba(27, 57, 106, 0.14);
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
  height: 36px;
}

.reset-btn,
.filter-btn {
  height: 36px;
  padding: 0 0.95rem;
  font-size: 0.84rem;
}

.autocomplete-container {
  position: relative;
  width: 100%;
}

.clear-search-btn {
  position: absolute;
  right: 0.55rem;
  background: transparent;
  border: none;
  color: #94a3b8;
  cursor: pointer;
  font-size: 0.82rem;
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

.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: var(--surface-card, #ffffff);
  border: 1px solid var(--color-border, #cbd5e1);
  border-radius: 8px;
  box-shadow: 0 12px 28px -4px rgba(15, 23, 42, 0.2);
  z-index: 999;
  max-height: 240px;
  overflow-y: auto;
  box-sizing: border-box;
}

.suggestion-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.55rem 0.85rem;
  cursor: pointer;
  border-bottom: 1px solid var(--color-border-subtle, #f1f5f9);
  transition: background 0.12s ease;
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: rgba(27, 57, 106, 0.06);
}

.suggestion-left {
  display: flex;
  flex-direction: column;
  gap: 0.1rem;
}

.suggestion-control {
  font-family: monospace;
  font-weight: 700;
  font-size: 0.8rem;
  color: #1b396a;
}

.suggestion-name {
  font-size: 0.82rem;
  color: var(--color-text-primary, #1e293b);
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
  font-weight: 700;
  color: var(--color-text-primary, #0f172a);
}

.progress-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.progress-track {
  flex: 1;
  height: 7px;
  background: #e2e8f0;
  border-radius: 999px;
  overflow: hidden;
  min-width: 56px;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(90deg, #15803d, #16a34a);
  border-radius: 999px;
  transition: width 0.35s ease;
}

.progress-text {
  font-size: 0.8rem;
  font-weight: 700;
}

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding-top: 0.75rem;
  margin-top: 0.5rem;
  border-top: 1px solid var(--color-border-subtle, #e2e8f0);
  flex-wrap: wrap;
}

.pagination-info {
  font-size: 0.82rem;
  color: var(--color-text-muted, #64748b);
}

.pagination-controls {
  display: flex;
  gap: 0.5rem;
}

/* Modal Detalle y Observaciones */
.detail-modal-stack {
  display: flex;
  flex-direction: column;
  gap: 0.9rem;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.75rem;
  background: var(--surface-subtle, #f8fafc);
  padding: 0.8rem 0.95rem;
  border-radius: 8px;
  border: 1px solid var(--color-border-subtle, #e2e8f0);
}

.summary-item small {
  color: var(--color-text-muted, #64748b);
  display: block;
  font-size: 0.75rem;
}

.text-emerald {
  color: #16a34a;
}

.detail-toolbar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.65rem;
  flex-wrap: wrap;
}

.detail-toolbar__group {
  display: flex;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.observations-banner {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.65rem 0.9rem;
  border-radius: 8px;
  background: rgba(197, 160, 89, 0.12);
  border: 1px solid rgba(197, 160, 89, 0.4);
  font-size: 0.84rem;
  flex-wrap: wrap;
}

.observations-banner__text {
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
}

.section-heading {
  display: block;
  margin-bottom: 0.45rem;
  font-size: 0.9rem;
}

.empty-docs-note {
  padding: 0.65rem;
  color: var(--color-text-muted, #64748b);
  font-size: 0.85rem;
}

.uploaded-docs-list {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  max-height: 220px;
  overflow-y: auto;
}

.uploaded-doc-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0.55rem 0.8rem;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.uploaded-doc-info {
  display: flex;
  flex-direction: column;
  gap: 0.2rem;
}

.doc-rejection-note {
  color: #b91c1c;
  font-size: 0.78rem;
}

.uploaded-doc-actions {
  display: flex;
  gap: 0.4rem;
}

.stage-actions-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  padding-top: 0.75rem;
  border-top: 1px solid var(--color-border, #e5e7eb);
}

.observations-modal-body {
  display: flex;
  flex-direction: column;
  gap: 0.85rem;
}

.observations-student-pill {
  padding: 0.55rem 0.75rem;
  background: var(--surface-subtle, #f8fafc);
  border: 1px solid var(--color-border-subtle, #e2e8f0);
  border-radius: 8px;
  font-size: 0.84rem;
  display: flex;
  gap: 0.4rem;
  flex-wrap: wrap;
}

.observations-helper {
  margin: 0;
  font-size: 0.85rem;
  color: var(--color-text-secondary, #475569);
}
</style>
