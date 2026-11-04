<script setup lang="ts">
import ExpedientChecklist from '../../expedients/components/ExpedientChecklist.vue'
import { http } from '../../../shared/api/http'
import { computed, onMounted, ref } from 'vue'
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
  reviewDocument,
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

// Filtros y Autocomplete
const searchQuery = ref('')
const statusFilter = ref('')
const isAutocompleteOpen = ref(false)
let debounceTimer: ReturnType<typeof setTimeout> | null = null

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
    .slice(0, 8)
})

function handleSearchInput(): void {
  isAutocompleteOpen.value = searchQuery.value.trim().length > 0
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 220)
}

function selectSuggestion(suggestion: InternshipListItem): void {
  searchQuery.value = suggestion.studentFullName
  isAutocompleteOpen.value = false
  loadData()
}

function clearSearch(): void {
  searchQuery.value = ''
  isAutocompleteOpen.value = false
  loadData()
}

function resetFilters(): void {
  searchQuery.value = ''
  statusFilter.value = ''
  isAutocompleteOpen.value = false
  loadData()
}

// Modal de Gestión de Trámite de Prácticas
const isDetailOpen = ref(false)
const selectedDetail = ref<InternshipDetail | null>(null)
const studentDocs = ref<ExpedientDocument[]>([])
const isActionRunning = ref(false)
const approveObservations = ref('Vigencia IMSS verificada. Se emite Carta de Presentación.')

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
      pageSize: 50,
      status: statusFilter.value || undefined,
      search: searchQuery.value.trim() || undefined,
    })
    items.value = res.items || []
    if (allItemsCache.value.length === 0 && items.value.length > 0) {
      allItemsCache.value = [...items.value]
    } else if (!searchQuery.value && !statusFilter.value && items.value.length > 0) {
      allItemsCache.value = [...items.value]
    }
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'Error al cargar trámites de Prácticas Profesionales.'
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
    isDetailOpen.value = true
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo cargar el detalle del trámite.'
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
  isActionRunning.value = true
  errorMessage.value = ''
  try {
    await approveInternship(selectedDetail.value.id, approveObservations.value)
    successMessage.value =
      'Solicitud aprobada. Se generó automáticamente la Carta de Presentación con folio EJ26/PR...'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo aprobar la solicitud.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleReviewDoc(doc: ExpedientDocument, status: 'approved' | 'rejected'): Promise<void> {
  let reason: string | undefined
  if (status === 'rejected') {
    const input = window.prompt('Indica el motivo de observación / rechazo del documento:')
    if (!input) return
    reason = input
  }
  isActionRunning.value = true
  errorMessage.value = ''
  try {
    await reviewDocument(doc.id, { status, rejectionReason: reason })
    successMessage.value = `Documento ${doc.documentCode} ${status === 'approved' ? 'aprobado' : 'observado'}.`
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo dictaminar el documento.'
  } finally {
    isActionRunning.value = false
  }
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
      const link = document.createElement('a'); link.href = url; link.download = `${docKey}.pdf`; link.click(); URL.revokeObjectURL(url)
      return
    }
    const data = await getInternshipPrintData(selectedDetail.value.id, docKey)
    openPrintableDocument(data)
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Plantilla aún no disponible.'
  }
}

async function handleComplete(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await completeInternship(selectedDetail.value.id, new Date().toISOString().slice(0, 10))
    successMessage.value = 'Documentación EV y CT validada. Trámite listo para liberar.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'Verifica que EV y CT estén aprobados.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleRelease(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await releaseInternship(selectedDetail.value.id)
    successMessage.value = 'Constancia de Acreditación de Prácticas (PPC) emitida exitosamente.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'Verifica que CA, EV y CT estén aprobados.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleSendSchoolAct(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await sendInternshipSchoolAct(selectedDetail.value.id)
    successMessage.value = 'Acta de Prácticas turnada a Servicios Escolares con folio DGTV.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo generar el acta.'
  } finally {
    isActionRunning.value = false
  }
}

onMounted(() => {
  void loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">
        Gestión de Prácticas Profesionales (Vinculación)
      </h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Emisión de Cartas de Presentación (`EJ26/PR...`), validación de Oficios de Aceptación
        membretados (`arturo.martinez@monclova.tecnm.mx`) y acreditación
      </p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" style="margin-bottom: 1rem;">
      {{ successMessage }}
    </UiAlert>

    <UiCard>
      <div class="filters-bar">
        <div class="search-box">
          <label class="filter-label">Buscar por Matrícula o Alumno</label>
          <div class="autocomplete-container">
            <div class="input-wrapper">
              <svg class="search-icon" viewBox="0 0 24 24" width="16" height="16" stroke="currentColor" stroke-width="2" fill="none">
                <circle cx="11" cy="11" r="8" />
                <line x1="21" y1="21" x2="16.65" y2="16.65" />
              </svg>
              <input
                v-model="searchQuery"
                type="text"
                class="modern-input"
                placeholder="Ej. I22050289 o Miguel..."
                @input="handleSearchInput"
                @focus="handleSearchFocus"
                @blur="handleSearchBlur"
                @keyup.enter="isAutocompleteOpen = false; loadData()"
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
          <label class="filter-label">Filtrar por Estatus</label>
          <div class="select-wrapper">
            <select v-model="statusFilter" class="modern-select" @change="loadData">
              <option value="">Todos los estatus</option>
              <option value="requested">Solicitud CP Recibida</option>
              <option value="approved">Carta Presentación Emitida</option>
              <option value="active">Activo en Empresa</option>
              <option value="completed_pending_review">Listo para Liberar</option>
              <option value="released">Liberado (100%)</option>
            </select>
            <svg class="select-chevron" viewBox="0 0 24 24" width="14" height="14" stroke="currentColor" stroke-width="2" fill="none">
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
          <UiButton variant="primary" class="filter-btn" :disabled="isLoading" @click="loadData">
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
          <div style="display: flex; align-items: center; gap: 0.5rem;">
            <div
              style="flex: 1; height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden; min-width: 60px;"
            >
              <div
                :style="{
                  width: `${row.documentProgressPercentage}%`,
                  height: '100%',
                  background: '#16a34a',
                }"
              ></div>
            </div>
            <span style="font-size: 0.85rem; font-weight: 600;">
              {{ row.documentProgressPercentage }}%
            </span>
          </div>
        </template>

        <template #cell-status="{ row }">
          <UiBadge :tone="row.status === 'released' ? 'success' : 'warning'">
            {{ formatStatus(row.status) }}
          </UiBadge>
        </template>

        <template #cell-actions="{ row }">
          <UiButton variant="primary" size="sm" @click="openDetailModal(row)">
            Gestionar Prácticas
          </UiButton>
        </template>
      </UiTable>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL DE GESTIÓN INTEGRAL DE PRÁCTICAS PROFESIONALES               -->
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
      <div v-if="selectedDetail" style="display: flex; flex-direction: column; gap: 1rem;">
        <ExpedientChecklist :requirements="selectedDetail.documentRequirements" :documents="studentDocs" tramite-type="internship" :tramite-id="selectedDetail.id" :student-id="selectedDetail.student.id" :can-edit="false" @changed="refreshCurrentDetail" />
        <div
          style="display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 0.75rem; background: #f8fafc; padding: 0.85rem; border-radius: 8px;"
        >
          <div>
            <small style="color: #6b7280; display: block;">Estatus Actual</small>
            <strong>{{ formatStatus(selectedDetail.status) }}</strong>
          </div>
          <div>
            <small style="color: #6b7280; display: block;">Empresa Receptora</small>
            <strong>{{ selectedDetail.company?.name || 'Pendiente de CA' }}</strong>
          </div>
          <div>
            <small style="color: #6b7280; display: block;">Folio Carta Presentación</small>
            <strong>{{ selectedDetail.presentationLetterFolio || 'Pendiente' }}</strong>
          </div>
          <div>
            <small style="color: #6b7280; display: block;">Constancia Liberación</small>
            <strong style="color: #16a34a;">
              {{ selectedDetail.releaseCertificateFolio || 'Pendiente' }}
            </strong>
          </div>
        </div>

        <!-- Botones de Impresión de Plantillas -->
        <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
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

        <!-- Si está en 'requested', botón para Aprobar y Emitir Carta de Presentación -->
        <div
          v-if="selectedDetail.status === 'requested'"
          style="padding: 0.9rem; border: 1px solid #bbf7d0; background: #f0fdf4; border-radius: 8px;"
        >
          <strong>Emisión de Carta de Presentación (Solicitud del Forms)</strong>
          <p style="margin: 0.25rem 0 0.6rem; font-size: 0.85rem;">
            Contacto confirmado: {{ selectedDetail.student.phoneNumber }} ·
            {{ selectedDetail.student.institutionalEmail }}
          </p>
          <UiField label="Observaciones de Coordinación">
            <input v-model="approveObservations" type="text" />
          </UiField>
          <div style="margin-top: 0.6rem;">
            <UiButton
              variant="primary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleApproveRequest"
            >
              Aprobar Solicitud y Emitir Carta de Presentación (EJ26/PR...)
            </UiButton>
          </div>
        </div>

        <!-- Documentos Subidos en el Expediente -->
        <div>
          <strong style="display: block; margin-bottom: 0.5rem;">
            Documentos Subidos en el Expediente ({{ studentDocs.length }})
          </strong>
          <div
            v-if="studentDocs.length === 0"
            style="padding: 0.75rem; color: #6b7280; font-size: 0.88rem;"
          >
            El alumno aún no ha subido archivos PDF a este trámite.
          </div>
          <div v-else style="display: flex; flex-direction: column; gap: 0.5rem; max-height: 240px; overflow-y: auto;">
            <div
              v-for="doc in studentDocs"
              :key="doc.id"
              style="display: flex; justify-content: space-between; align-items: center; padding: 0.6rem 0.8rem; border: 1px solid #e5e7eb; border-radius: 6px; gap: 0.5rem; flex-wrap: wrap;"
            >
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
              <div style="display: flex; gap: 0.4rem;">
                <UiButton variant="secondary" size="sm" @click="handleViewDocPdf(doc.id)">
                  Ver PDF
                </UiButton>
                <UiButton
                  v-if="doc.status === 'pending'"
                  variant="primary"
                  size="sm"
                  :disabled="isActionRunning"
                  @click="handleReviewDoc(doc, 'approved')"
                >
                  Aprobar
                </UiButton>
                <UiButton
                  v-if="doc.status === 'pending'"
                  variant="secondary"
                  size="sm"
                  :disabled="isActionRunning"
                  @click="handleReviewDoc(doc, 'rejected')"
                >
                  Observar
                </UiButton>
              </div>
            </div>
          </div>
        </div>

        <!-- Botones de Cierre y Liberación -->
        <div
          style="display: flex; flex-wrap: wrap; gap: 0.5rem; padding-top: 0.75rem; border-top: 1px solid #e5e7eb;"
        >
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
  </UiBasePage>
</template>

<style scoped>
.filters-bar {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  align-items: flex-end;
  margin-bottom: 1rem;
}

.search-box {
  flex: 1.5;
  min-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.select-box {
  flex: 1.2;
  min-width: 220px;
  display: flex;
  flex-direction: column;
  gap: 0.35rem;
}

.filter-label {
  font-size: 0.8rem;
  font-weight: 600;
  color: #475569;
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
  height: 38px;
  border-radius: 8px;
  border: 1px solid #cbd5e1;
  background: #ffffff;
  padding: 0 1rem 0 2.25rem;
  font-size: 0.85rem;
  color: #0f172a;
  outline: none;
  transition: all 0.15s ease;
  box-sizing: border-box;
}

.modern-input:focus {
  border-color: #0284c7;
  box-shadow: 0 0 0 3px rgba(2, 132, 199, 0.15);
}

.modern-select {
  width: 100%;
  height: 38px;
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
  height: 38px;
}

.reset-btn {
  height: 38px;
  padding: 0 0.85rem;
  font-size: 0.85rem;
}

.filter-btn {
  height: 38px;
  padding: 0 1.25rem;
  font-size: 0.85rem;
}

.autocomplete-container {
  position: relative;
  width: 100%;
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

.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: #ffffff;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  box-shadow: 0 12px 28px -4px rgba(15, 23, 42, 0.2);
  z-index: 999;
  max-height: 250px;
  overflow-y: auto;
  box-sizing: border-box;
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
</style>
