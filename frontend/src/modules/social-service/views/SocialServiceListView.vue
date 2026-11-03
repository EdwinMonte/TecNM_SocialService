<script setup lang="ts">
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
  activateSocialService,
  completeSocialService,
  getSocialServiceById,
  getSocialServicePrintData,
  getSocialServices,
  registerSocialServiceAcceptance,
  releaseSocialService,
  reviewSocialServiceApplication,
  sendSocialServiceSchoolAct,
} from '../api/socialServiceApi'
import type { SocialServiceDetail, SocialServiceListItem } from '../types/socialService'

interface SocialServiceRow extends SocialServiceListItem {
  actions?: string
}

const items = ref<SocialServiceRow[]>([])
const allItemsCache = ref<SocialServiceRow[]>([])
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
  const map = new Map<number, SocialServiceRow>()
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

function selectSuggestion(suggestion: SocialServiceListItem): void {
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

// Modal de Gestión de Trámite
const isDetailOpen = ref(false)
const selectedDetail = ref<SocialServiceDetail | null>(null)
const studentDocs = ref<ExpedientDocument[]>([])
const isActionRunning = ref(false)
const reviewObservations = ref('Cumple con requisitos y vigencia IMSS.')

const columns: { key: keyof SocialServiceRow & string; label: string }[] = [
  { key: 'studentControlNumber', label: 'Control' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'careerCode', label: 'Carrera' },
  { key: 'companyName', label: 'Dependencia' },
  { key: 'modality', label: 'Modalidad' },
  { key: 'documentProgressPercentage', label: 'Avance Doc.' },
  { key: 'status', label: 'Estatus' },
  { key: 'actions', label: 'Acciones' },
]

const statusLabels: Record<string, string> = {
  requested: 'Solicitud Recibida',
  approved: 'Carta Pres. Emitida',
  accepted: 'Carta Aceptación OK',
  active: 'En Curso (Activo)',
  completed_pending_review: 'Listo para Liberar',
  released: 'Liberado (SSC)',
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
    const res = await getSocialServices({
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
      err?.response?.data?.message || 'Error al cargar trámites de Servicio Social.'
  } finally {
    isLoading.value = false
  }
}

async function openDetailModal(row: SocialServiceListItem): Promise<void> {
  errorMessage.value = ''
  try {
    const [detail, docs] = await Promise.all([
      getSocialServiceById(row.id),
      getStudentDocuments(row.studentId, { tramiteType: 'social_service', tramiteId: row.id }).catch(
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
    getSocialServiceById(id),
    getStudentDocuments(studentId, { tramiteType: 'social_service', tramiteId: id }).catch(
      () => [],
    ),
  ])
  selectedDetail.value = detail
  studentDocs.value = docs
  await loadData()
}

async function handleApproveApplication(isAccepted: boolean): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  errorMessage.value = ''
  try {
    await reviewSocialServiceApplication(selectedDetail.value.id, {
      isAccepted,
      rejectionReason: isAccepted ? null : reviewObservations.value,
      officeObservations: reviewObservations.value,
    })
    successMessage.value = isAccepted
      ? 'Solicitud aprobada y Carta de Presentación emitida con folio automático.'
      : 'Solicitud rechazada con observaciones.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo dictaminar la solicitud.'
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

async function handlePrint(docKey: string): Promise<void> {
  if (!selectedDetail.value) return
  try {
    const data = await getSocialServicePrintData(selectedDetail.value.id, docKey)
    openPrintableDocument(data)
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Plantilla aún no disponible.'
  }
}

async function handleAdvanceAcceptance(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await registerSocialServiceAcceptance(
      selectedDetail.value.id,
      new Date().toISOString().slice(0, 10),
    )
    successMessage.value = 'Carta de Aceptación registrada en el trámite.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Verifica que el documento CA esté aprobado.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleActivate(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await activateSocialService(selectedDetail.value.id)
    successMessage.value = 'Servicio Social activado formalmente.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Verifica que la Carta Compromiso (CC) esté aprobada.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleComplete(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await completeSocialService(selectedDetail.value.id, new Date().toISOString().slice(0, 10))
    successMessage.value = 'Documentación final validada. Trámite listo para liberar.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'Verifica que R1, R2, R3 y CT estén aprobados.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleRelease(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await releaseSocialService(selectedDetail.value.id, {
      finalGradeLevel: 'excellent',
      finalScore: 100,
      originalTerminationLetterReceived: true,
    })
    successMessage.value = 'Constancia de Terminación (SSC) emitida exitosamente.'
    await refreshCurrentDetail()
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'Verifica que todos los documentos obligatorios estén aprobados.'
  } finally {
    isActionRunning.value = false
  }
}

async function handleSendSchoolAct(): Promise<void> {
  if (!selectedDetail.value) return
  isActionRunning.value = true
  try {
    await sendSocialServiceSchoolAct(selectedDetail.value.id)
    successMessage.value = 'Acta turnada a Servicios Escolares con folio DGTV.'
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
        Gestión de Servicio Social (Vinculación)
      </h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Dictamen de solicitudes Anexo XVIII, emisión de Cartas de Presentación, validación de
        expediente documental y liberación
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
                placeholder="Ej. I22050114 o Mariana..."
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
              <option value="requested">Solicitud Recibida (Por Dictaminar)</option>
              <option value="approved">Carta Presentación Emitida</option>
              <option value="accepted">Carta Aceptación Validada</option>
              <option value="active">Activo (Subiendo Reportes)</option>
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

      <UiTable label="Trámites de Servicio Social" :columns="columns" :rows="items">
        <template #cell-modality="{ row }">
          <UiBadge :tone="row.modality === 'internal' ? 'info' : 'purple'">
            {{ row.modality === 'internal' ? 'Local (TecNM)' : 'Externo' }}
          </UiBadge>
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
                  background: '#0284c7',
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
            Gestionar Expediente
          </UiButton>
        </template>
      </UiTable>
    </UiCard>

    <!-- ================================================================== -->
    <!-- MODAL DE GESTIÓN INTEGRAL DEL TRÁMITE DE SERVICIO SOCIAL           -->
    <!-- ================================================================== -->
    <UiDialog
      v-model="isDetailOpen"
      size="xl"
      :title="
        selectedDetail
          ? `Expediente SS · ${selectedDetail.student.controlNumber} — ${selectedDetail.student.fullName}`
          : 'Detalle de Servicio Social'
      "
    >
      <div v-if="selectedDetail" style="display: flex; flex-direction: column; gap: 1rem;">
        <div
          style="display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 0.75rem; background: #f8fafc; padding: 0.85rem; border-radius: 8px;"
        >
          <div>
            <small style="color: #6b7280; display: block;">Estatus Actual</small>
            <strong>{{ formatStatus(selectedDetail.status) }}</strong>
          </div>
          <div>
            <small style="color: #6b7280; display: block;">Dependencia</small>
            <strong>{{ selectedDetail.company.name }}</strong>
          </div>
          <div>
            <small style="color: #6b7280; display: block;">Folio Carta Presentación</small>
            <strong>{{ selectedDetail.presentationLetterFolio || 'Pendiente' }}</strong>
          </div>
          <div>
            <small style="color: #6b7280; display: block;">Folio Constancia Terminación</small>
            <strong style="color: #16a34a;">
              {{ selectedDetail.releaseCertificateFolio || 'Pendiente' }}
            </strong>
          </div>
        </div>

        <!-- Botones de Impresión de Plantillas -->
        <div style="display: flex; gap: 0.5rem; flex-wrap: wrap;">
          <UiButton variant="secondary" size="sm" @click="handlePrint('application')">
            Ver Anexo XVIII
          </UiButton>
          <UiButton
            v-if="selectedDetail.presentationLetterFolio"
            variant="secondary"
            size="sm"
            @click="handlePrint('presentation_letter')"
          >
            Imprimir Carta Presentación (CP)
          </UiButton>
          <UiButton
            v-if="selectedDetail.modality === 'internal'"
            variant="secondary"
            size="sm"
            @click="handlePrint('acceptance_letter')"
          >
            Imprimir Carta Aceptación Local (CA)
          </UiButton>
          <UiButton
            v-if="selectedDetail.status === 'released'"
            variant="secondary"
            size="sm"
            @click="handlePrint('release_certificate')"
          >
            Imprimir Constancia Liberación (SSC)
          </UiButton>
        </div>

        <!-- Paso 1: Si está en 'requested', permitir Dictaminar y Emitir CP -->
        <div
          v-if="selectedDetail.status === 'requested'"
          style="padding: 0.9rem; border: 1px solid #bae6fd; background: #f0f9ff; border-radius: 8px;"
        >
          <strong>Dictamen de Apertura (Anexo XVIII)</strong>
          <p style="margin: 0.25rem 0 0.6rem; font-size: 0.85rem;">
            Al aprobar la solicitud, el sistema genera automáticamente la Carta de Presentación con
            su folio consecutivo (`SS_CP...`).
          </p>
          <UiField label="Observaciones de Vinculación">
            <input v-model="reviewObservations" type="text" />
          </UiField>
          <div style="display: flex; gap: 0.5rem; margin-top: 0.6rem;">
            <UiButton
              variant="primary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleApproveApplication(true)"
            >
              Aprobar Solicitud y Generar Carta de Presentación
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="isActionRunning"
              @click="handleApproveApplication(false)"
            >
              Rechazar Solicitud
            </UiButton>
          </div>
        </div>

        <!-- Lista de Documentos Subidos por el Alumno para Validar -->
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

        <!-- Botones de Avance de Etapa y Emisión de Constancia de Liberación -->
        <div
          style="display: flex; flex-wrap: wrap; gap: 0.5rem; padding-top: 0.75rem; border-top: 1px solid #e5e7eb;"
        >
          <UiButton
            v-if="selectedDetail.status === 'approved'"
            variant="primary"
            size="sm"
            :disabled="isActionRunning"
            @click="handleAdvanceAcceptance"
          >
            Confirmar Recepción de Carta Aceptación (CA)
          </UiButton>

          <UiButton
            v-if="selectedDetail.status === 'accepted'"
            variant="primary"
            size="sm"
            :disabled="isActionRunning"
            @click="handleActivate"
          >
            Activar Servicio Social (Con CC aprobada)
          </UiButton>

          <UiButton
            v-if="selectedDetail.status === 'active'"
            variant="primary"
            size="sm"
            :disabled="isActionRunning"
            @click="handleComplete"
          >
            Validar Cierre Documental (R1..R3 y CT)
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
            Emitir Constancia de Liberación (Folio SSC)
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
