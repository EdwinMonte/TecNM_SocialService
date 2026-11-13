<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { problemMessage } from '../../../shared/api/problemDetails'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiFormErrors,
  UiTable,
} from '../../../shared/components'
import { hasRole } from '../../../shared/composables/useSession'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { validateFields } from '../../../shared/utils/validationRules'
import { getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { getInternships } from '../../internships/api/internshipsApi'
import { getSocialServices } from '../../social-service/api/socialServiceApi'
import {
  createNonConformance,
  getNonConformanceById,
  getNonConformances,
  updateNonConformanceStatus,
} from '../api/qualityApi'
import type { NonConformance, NonConformanceStatus } from '../types/quality'

interface NonConformanceRow extends NonConformance {
  actions?: string
}

interface TramiteOption {
  id: number
  label: string
}

const CATEGORY_LABELS: Record<string, string> = {
  desertion: 'Deserción',
  delayed_reports: 'Reportes extemporáneos',
  company_dismissal: 'Baja por empresa',
  insufficient_grade: 'Calificación insuficiente',
  invalid_documents: 'Documentación inválida',
  other: 'Otro',
}

const STATUS_META: Record<NonConformanceStatus, { label: string; tone?: 'danger' | 'warning' | 'success' | 'info' }> = {
  open: { label: 'Abierto', tone: 'danger' },
  under_review: { label: 'En revisión', tone: 'warning' },
  resolved: { label: 'Resuelto', tone: 'success' },
  cancelled: { label: 'Cancelado' },
}

const canWriteSocialService = computed(() => hasRole('admin', 'social_service_officer'))
const canWriteInternship = computed(() => hasRole('admin', 'internship_coordinator'))
const canCreateAny = computed(() => canWriteSocialService.value || canWriteInternship.value)

const items = ref<NonConformanceRow[]>([])
const periodOptions = ref<OptionItem[]>([])
const isLoading = ref(true)
const errorMessage = ref('')
const successMessage = ref('')

const page = ref(1)
const pageSize = 20
const totalItems = ref(0)
const totalPages = ref(1)

const searchQuery = ref('')
const selectedPeriodId = ref<string>('')
const selectedTramiteType = ref<string>('')
const selectedStatus = ref<string>('')
const selectedCategory = ref<string>('')

let debounceTimer: ReturnType<typeof setTimeout> | null = null
let loadRequestId = 0

const columns: { key: keyof NonConformanceRow & string; label: string }[] = [
  { key: 'incidentCode', label: 'Código PNC' },
  { key: 'periodCode', label: 'Periodo' },
  { key: 'tramiteType', label: 'Trámite' },
  { key: 'studentFullName', label: 'Alumno' },
  { key: 'category', label: 'Categoría' },
  { key: 'status', label: 'Estatus' },
  { key: 'createdAt', label: 'Registro' },
  { key: 'actions', label: 'Acciones' },
]

// Modal: Registrar PNC
const isCreateOpen = ref(false)
const isSubmittingCreate = ref(false)
const tramiteOptions = ref<TramiteOption[]>([])
const isLoadingTramites = ref(false)

const defaultCreateTramiteType = (): 'social_service' | 'internship' =>
  canWriteSocialService.value ? 'social_service' : 'internship'

const createForm = reactive({
  tramiteType: defaultCreateTramiteType() as 'social_service' | 'internship',
  tramiteId: 0,
  category: 'invalid_documents',
  description: '',
  actionPlan: '',
})

const createValidation = useFormValidation(() =>
  validateFields(createForm, {
    tramiteType: { required: true, choices: ['social_service', 'internship'] },
    tramiteId: { required: true, min: 1, integer: true, message: 'Selecciona un trámite válido.' },
    category: { required: true, choices: Object.keys(CATEGORY_LABELS) },
    description: { required: true, max: 4000 },
    actionPlan: { max: 4000 },
  }),
)

// Modal: Detalle y seguimiento
const isDetailOpen = ref(false)
const selectedPnc = ref<NonConformance | null>(null)
const isLoadingDetail = ref(false)
const isUpdatingStatus = ref(false)
const detailActionPlan = ref('')
const pendingTargetStatus = ref<'under_review' | 'resolved' | 'cancelled'>('under_review')

const statusValidation = useFormValidation(() => {
  const rules: Record<string, { required?: boolean; max?: number }> = {
    actionPlan: {
      required: pendingTargetStatus.value === 'resolved',
      max: 4000,
    },
  }
  return validateFields({ actionPlan: detailActionPlan.value }, rules)
})

const canManageSelected = computed(() => {
  const pnc = selectedPnc.value
  if (!pnc) return false
  if (pnc.status !== 'open' && pnc.status !== 'under_review') return false
  return pnc.tramiteType === 'social_service'
    ? canWriteSocialService.value
    : canWriteInternship.value
})

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—'
  const date = new Date(iso)
  if (Number.isNaN(date.getTime())) return iso
  return date.toLocaleDateString('es-MX', {
    year: 'numeric',
    month: 'short',
    day: '2-digit',
    timeZone: 'America/Monterrey',
  })
}

function tramiteLabel(type: string): string {
  return type === 'social_service' ? 'Servicio Social' : 'Prácticas Prof.'
}

function canManageRow(row: NonConformance): boolean {
  if (row.status !== 'open' && row.status !== 'under_review') return false
  return row.tramiteType === 'social_service'
    ? canWriteSocialService.value
    : canWriteInternship.value
}

async function loadData(): Promise<void> {
  const reqId = ++loadRequestId
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getNonConformances({
      page: page.value,
      pageSize,
      periodId: selectedPeriodId.value ? Number(selectedPeriodId.value) : undefined,
      tramiteType: selectedTramiteType.value || undefined,
      status: selectedStatus.value || undefined,
      category: selectedCategory.value || undefined,
      search: searchQuery.value.trim() || undefined,
    })
    if (reqId !== loadRequestId) return
    items.value = res.items
    totalItems.value = res.totalItems
    totalPages.value = Math.max(1, res.totalPages)
  } catch (err: any) {
    if (reqId !== loadRequestId) return
    errorMessage.value = problemMessage(err, 'No se pudieron cargar los incidentes de Producto No Conforme.')
  } finally {
    if (reqId === loadRequestId) {
      isLoading.value = false
    }
  }
}

async function loadTramiteOptions(type: 'social_service' | 'internship'): Promise<void> {
  isLoadingTramites.value = true
  tramiteOptions.value = []
  try {
    if (type === 'social_service') {
      const res = await getSocialServices({ page: 1, pageSize: 50 })
      tramiteOptions.value = res.items.map((item) => ({
        id: item.id,
        label: `#${item.id} · ${item.studentControlNumber} — ${item.studentFullName} (${item.periodCode})`,
      }))
    } else {
      const res = await getInternships({ page: 1, pageSize: 50 })
      tramiteOptions.value = res.items.map((item) => ({
        id: item.id,
        label: `#${item.id} · ${item.studentControlNumber} — ${item.studentFullName} (${item.periodCode})`,
      }))
    }
  } catch {
    tramiteOptions.value = []
  } finally {
    isLoadingTramites.value = false
  }
}

async function openCreateModal(): Promise<void> {
  successMessage.value = ''
  createForm.tramiteType = defaultCreateTramiteType()
  createForm.tramiteId = 0
  createForm.category = 'invalid_documents'
  createForm.description = ''
  createForm.actionPlan = ''
  createValidation.reset()
  isCreateOpen.value = true
  await loadTramiteOptions(createForm.tramiteType)
}

async function handleCreateSubmit(): Promise<void> {
  if (isSubmittingCreate.value || !createValidation.validate()) return
  isSubmittingCreate.value = true
  try {
    const created = await createNonConformance({
      tramiteType: createForm.tramiteType,
      tramiteId: createForm.tramiteId,
      category: createForm.category,
      description: createForm.description.trim(),
      actionPlan: createForm.actionPlan.trim() || null,
    })
    isCreateOpen.value = false
    successMessage.value = `Incidencia ${created.incidentCode} registrada correctamente.`
    page.value = 1
    await loadData()
  } catch (err: any) {
    createValidation.apply(err, 'No se pudo registrar la incidencia PNC.')
  } finally {
    isSubmittingCreate.value = false
  }
}

async function openDetailModal(row: NonConformance): Promise<void> {
  successMessage.value = ''
  selectedPnc.value = row
  detailActionPlan.value = row.actionPlan ?? ''
  statusValidation.reset()
  isDetailOpen.value = true
  isLoadingDetail.value = true
  try {
    const fresh = await getNonConformanceById(row.id)
    selectedPnc.value = fresh
    detailActionPlan.value = fresh.actionPlan ?? ''
  } catch (err: any) {
    errorMessage.value = problemMessage(err, 'No se pudo actualizar el detalle del PNC.')
  } finally {
    isLoadingDetail.value = false
  }
}

async function handleStatusTransition(targetStatus: 'under_review' | 'resolved' | 'cancelled'): Promise<void> {
  const pnc = selectedPnc.value
  if (!pnc || isUpdatingStatus.value) return
  pendingTargetStatus.value = targetStatus
  if (!statusValidation.validate()) return
  isUpdatingStatus.value = true
  try {
    const updated = await updateNonConformanceStatus(pnc.id, {
      status: targetStatus,
      actionPlan: detailActionPlan.value.trim() || null,
    })
    selectedPnc.value = updated
    detailActionPlan.value = updated.actionPlan ?? ''
    successMessage.value = `Incidencia ${updated.incidentCode} actualizada a ${STATUS_META[updated.status].label}.`
    if (updated.status === 'resolved' || updated.status === 'cancelled') {
      isDetailOpen.value = false
    }
    await loadData()
  } catch (err: any) {
    statusValidation.apply(err, 'No se pudo actualizar el estado del PNC.')
  } finally {
    isUpdatingStatus.value = false
  }
}

function handleSearchInput(): void {
  if (debounceTimer) clearTimeout(debounceTimer)
  debounceTimer = setTimeout(() => {
    page.value = 1
    loadData()
  }, 300)
}

function handleFilterChange(): void {
  page.value = 1
  loadData()
}

function resetFilters(): void {
  searchQuery.value = ''
  selectedPeriodId.value = ''
  selectedTramiteType.value = ''
  selectedStatus.value = ''
  selectedCategory.value = ''
  page.value = 1
  loadData()
}

function goToPage(nextPage: number): void {
  if (nextPage < 1 || nextPage > totalPages.value || nextPage === page.value) return
  page.value = nextPage
  loadData()
}

watch(
  () => createForm.tramiteType,
  async (nextType) => {
    if (!isCreateOpen.value) return
    createForm.tramiteId = 0
    await loadTramiteOptions(nextType)
  },
)

onBeforeUnmount(() => {
  if (debounceTimer) clearTimeout(debounceTimer)
  loadRequestId++
})

onMounted(async () => {
  try {
    periodOptions.value = await getPeriodOptions()
  } catch {
    periodOptions.value = []
  }
  await loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div class="pnc-header">
      <div>
        <h1 class="pnc-title">Producto No Conforme (PNC - ISO 9001)</h1>
        <p class="pnc-subtitle">Registro de desvíos operativos, planes de acción correctiva y seguimiento de calidad</p>
      </div>
      <UiButton v-if="canCreateAny" variant="primary" @click="openCreateModal">
        Registrar PNC
      </UiButton>
    </div>

    <UiAlert v-if="successMessage" tone="success" class="mb-4">{{ successMessage }}</UiAlert>
    <UiAlert v-if="errorMessage" tone="danger" class="mb-4">
      <div class="error-row">
        <span>{{ errorMessage }}</span>
        <UiButton variant="secondary" size="sm" @click="loadData">Reintentar</UiButton>
      </div>
    </UiAlert>

    <UiCard class="mb-4">
      <div class="filters-grid">
        <UiField label="Buscar" v-slot="{ controlAttrs }">
          <input
            v-bind="controlAttrs"
            v-model="searchQuery"
            type="search"
            class="modern-input"
            placeholder="Código PNC, matrícula o nombre"
            @input="handleSearchInput"
          />
        </UiField>

        <UiField label="Periodo" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="selectedPeriodId" class="modern-select" @change="handleFilterChange">
            <option value="">Todos</option>
            <option v-for="p in periodOptions" :key="p.id" :value="String(p.id)">{{ p.label }}</option>
          </select>
        </UiField>

        <UiField label="Trámite" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="selectedTramiteType" class="modern-select" @change="handleFilterChange">
            <option value="">Todos</option>
            <option value="social_service">Servicio Social</option>
            <option value="internship">Prácticas Profesionales</option>
          </select>
        </UiField>

        <UiField label="Estatus" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="selectedStatus" class="modern-select" @change="handleFilterChange">
            <option value="">Todos</option>
            <option value="open">Abierto</option>
            <option value="under_review">En revisión</option>
            <option value="resolved">Resuelto</option>
            <option value="cancelled">Cancelado</option>
          </select>
        </UiField>

        <UiField label="Categoría" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="selectedCategory" class="modern-select" @change="handleFilterChange">
            <option value="">Todas</option>
            <option v-for="(label, key) in CATEGORY_LABELS" :key="key" :value="key">{{ label }}</option>
          </select>
        </UiField>

        <div class="filter-reset">
          <UiButton variant="secondary" @click="resetFilters">Limpiar</UiButton>
        </div>
      </div>
    </UiCard>

    <UiCard>
      <div v-if="isLoading" class="state-box">Cargando incidencias de Producto No Conforme…</div>
      <div v-else-if="items.length === 0" class="state-box">
        No se encontraron incidencias PNC con los filtros actuales.
      </div>
      <template v-else>
        <UiTable label="Incidencias PNC" :columns="columns" :rows="items">
          <template #cell-incidentCode="{ value }">
            <span class="mono-code">{{ value }}</span>
          </template>
          <template #cell-tramiteType="{ row }">
            {{ tramiteLabel(row.tramiteType) }}
          </template>
          <template #cell-studentFullName="{ row }">
            <div class="student-cell">
              <span class="student-name">{{ row.studentFullName }}</span>
              <span class="student-control">{{ row.studentControlNumber }}</span>
            </div>
          </template>
          <template #cell-category="{ row }">
            {{ CATEGORY_LABELS[row.category] ?? row.category }}
          </template>
          <template #cell-status="{ row }">
            <UiBadge :tone="STATUS_META[row.status]?.tone">
              {{ STATUS_META[row.status]?.label ?? row.status }}
            </UiBadge>
          </template>
          <template #cell-createdAt="{ row }">
            {{ formatDate(row.createdAt) }}
          </template>
          <template #cell-actions="{ row }">
            <UiButton variant="secondary" size="sm" @click="openDetailModal(row)">
              {{ canManageRow(row) ? 'Gestionar' : 'Ver detalle' }}
            </UiButton>
          </template>
        </UiTable>

        <div class="pagination-bar">
          <span class="pagination-info">
            Mostrando {{ items.length }} de {{ totalItems }} incidencias · Página {{ page }} de {{ totalPages }}
          </span>
          <div class="pagination-actions">
            <UiButton variant="secondary" size="sm" :disabled="page <= 1" @click="goToPage(page - 1)">
              Anterior
            </UiButton>
            <UiButton variant="secondary" size="sm" :disabled="page >= totalPages" @click="goToPage(page + 1)">
              Siguiente
            </UiButton>
          </div>
        </div>
      </template>
    </UiCard>

    <UiDialog v-model="isCreateOpen" size="md" title="Registrar Producto No Conforme (PNC)">
      <div class="dialog-body">
        <UiFormErrors :validation="createValidation" />

        <UiField
          v-bind="createValidation.field('tramiteType', 'Tipo de trámite')"
          label="Proceso / Trámite *"
          v-slot="{ controlAttrs }"
        >
          <select
            v-bind="controlAttrs"
            v-model="createForm.tramiteType"
            class="modern-select"
            :disabled="!(canWriteSocialService && canWriteInternship)"
          >
            <option v-if="canWriteSocialService" value="social_service">Servicio Social</option>
            <option v-if="canWriteInternship" value="internship">Prácticas Profesionales</option>
          </select>
        </UiField>

        <UiField
          v-bind="createValidation.field('tramiteId', 'Trámite')"
          label="Expediente asociado *"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model.number="createForm.tramiteId" class="modern-select" :disabled="isLoadingTramites">
            <option :value="0">
              {{ isLoadingTramites ? 'Cargando trámites…' : 'Selecciona un trámite…' }}
            </option>
            <option v-for="t in tramiteOptions" :key="t.id" :value="t.id">{{ t.label }}</option>
          </select>
        </UiField>

        <UiField
          v-bind="createValidation.field('category', 'Categoría')"
          label="Categoría *"
          v-slot="{ controlAttrs }"
        >
          <select v-bind="controlAttrs" v-model="createForm.category" class="modern-select">
            <option v-for="(label, key) in CATEGORY_LABELS" :key="key" :value="key">{{ label }}</option>
          </select>
        </UiField>

        <UiField
          v-bind="createValidation.field('description', 'Descripción')"
          label="Descripción de la no conformidad *"
          v-slot="{ controlAttrs }"
        >
          <textarea
            v-bind="controlAttrs"
            v-model="createForm.description"
            rows="3"
            class="modern-textarea"
            placeholder="Describe el desvío detectado conforme a ISO 9001"
          />
        </UiField>

        <UiField
          v-bind="createValidation.field('actionPlan', 'Plan de acción')"
          label="Plan de acción correctiva (opcional)"
          v-slot="{ controlAttrs }"
        >
          <textarea
            v-bind="controlAttrs"
            v-model="createForm.actionPlan"
            rows="2"
            class="modern-textarea"
            placeholder="Acciones acordadas para corregir la incidencia"
          />
        </UiField>

        <div class="dialog-footer">
          <UiButton variant="secondary" @click="isCreateOpen = false">Cancelar</UiButton>
          <UiButton variant="primary" :loading="isSubmittingCreate" @click="handleCreateSubmit">
            Registrar PNC
          </UiButton>
        </div>
      </div>
    </UiDialog>

    <UiDialog
      v-model="isDetailOpen"
      size="md"
      :title="selectedPnc ? `Incidencia ${selectedPnc.incidentCode}` : 'Detalle PNC'"
    >
      <div v-if="selectedPnc" class="dialog-body">
        <div v-if="isLoadingDetail" class="detail-loading">Actualizando información…</div>

        <div class="detail-grid">
          <div>
            <span class="detail-label">Estatus</span>
            <div>
              <UiBadge :tone="STATUS_META[selectedPnc.status]?.tone">
                {{ STATUS_META[selectedPnc.status]?.label ?? selectedPnc.status }}
              </UiBadge>
            </div>
          </div>
          <div>
            <span class="detail-label">Proceso</span>
            <p class="detail-value">{{ tramiteLabel(selectedPnc.tramiteType) }} #{{ selectedPnc.tramiteId }}</p>
          </div>
          <div>
            <span class="detail-label">Alumno</span>
            <p class="detail-value">{{ selectedPnc.studentFullName }} ({{ selectedPnc.studentControlNumber }})</p>
          </div>
          <div>
            <span class="detail-label">Periodo</span>
            <p class="detail-value">{{ selectedPnc.periodCode }}</p>
          </div>
          <div>
            <span class="detail-label">Categoría</span>
            <p class="detail-value">{{ CATEGORY_LABELS[selectedPnc.category] ?? selectedPnc.category }}</p>
          </div>
          <div>
            <span class="detail-label">Fecha de registro</span>
            <p class="detail-value">{{ formatDate(selectedPnc.createdAt) }}</p>
          </div>
        </div>

        <div>
          <span class="detail-label">Descripción</span>
          <p class="detail-box">{{ selectedPnc.description }}</p>
        </div>

        <template v-if="canManageSelected">
          <UiFormErrors :validation="statusValidation" />
          <UiField
            v-bind="statusValidation.field('actionPlan', 'Plan de acción correctiva')"
            label="Plan de acción correctiva"
            hint="Obligatorio para resolver la incidencia."
            v-slot="{ controlAttrs }"
          >
            <textarea
              v-bind="controlAttrs"
              v-model="detailActionPlan"
              rows="3"
              class="modern-textarea"
              placeholder="Documenta la causa raíz y acciones correctivas realizadas"
            />
          </UiField>

          <div class="dialog-footer detail-actions">
            <UiButton variant="secondary" @click="isDetailOpen = false">Cerrar</UiButton>
            <UiButton
              v-if="selectedPnc.status === 'open'"
              variant="secondary"
              :disabled="isUpdatingStatus"
              @click="handleStatusTransition('under_review')"
            >
              Pasar a revisión
            </UiButton>
            <UiButton
              variant="danger"
              :disabled="isUpdatingStatus"
              @click="handleStatusTransition('cancelled')"
            >
              Cancelar PNC
            </UiButton>
            <UiButton
              variant="primary"
              :loading="isUpdatingStatus"
              @click="handleStatusTransition('resolved')"
            >
              Resolver PNC
            </UiButton>
          </div>
        </template>

        <template v-else>
          <div>
            <span class="detail-label">Plan de acción correctiva</span>
            <p class="detail-box">{{ selectedPnc.actionPlan || 'Sin plan de acción documentado.' }}</p>
          </div>
          <div v-if="selectedPnc.resolvedAt" class="detail-grid">
            <div>
              <span class="detail-label">Resuelto el</span>
              <p class="detail-value">{{ formatDate(selectedPnc.resolvedAt) }}</p>
            </div>
            <div>
              <span class="detail-label">Resuelto por</span>
              <p class="detail-value">{{ selectedPnc.resolvedByName || '—' }}</p>
            </div>
          </div>
          <div class="dialog-footer">
            <UiButton variant="secondary" @click="isDetailOpen = false">Cerrar</UiButton>
          </div>
        </template>
      </div>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
.pnc-header {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.pnc-title {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0;
}

.pnc-subtitle {
  color: var(--color-text-muted, #6c757d);
  margin: 0.25rem 0 0;
}

.mb-4 {
  margin-bottom: 1rem;
}

.error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.filters-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 0.85rem;
  align-items: end;
}

.filter-reset {
  display: flex;
  justify-content: flex-start;
}

.modern-input,
.modern-select,
.modern-textarea {
  width: 100%;
  padding: 0.55rem 0.75rem;
  border-radius: 8px;
  border: 1px solid var(--color-border, #d0d7de);
  background: var(--color-surface, #ffffff);
  color: var(--color-text, #1f2328);
  font: inherit;
}

.modern-textarea {
  resize: vertical;
}

.state-box {
  padding: 2rem 1rem;
  text-align: center;
  color: var(--color-text-muted, #6c757d);
}

.mono-code {
  font-family: var(--font-mono, monospace);
  font-weight: 600;
}

.student-cell {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
}

.student-name {
  font-weight: 500;
}

.student-control {
  font-size: 0.82rem;
  color: var(--color-text-muted, #6c757d);
}

.pagination-bar {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  margin-top: 1rem;
  padding-top: 0.85rem;
  border-top: 1px solid var(--color-border, #e5e7eb);
}

.pagination-info {
  font-size: 0.875rem;
  color: var(--color-text-muted, #6c757d);
}

.pagination-actions {
  display: flex;
  gap: 0.5rem;
}

.dialog-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.dialog-footer {
  display: flex;
  flex-wrap: wrap;
  justify-content: flex-end;
  gap: 0.5rem;
  margin-top: 0.5rem;
}

.detail-loading {
  font-size: 0.85rem;
  color: var(--color-text-muted, #6c757d);
}

.detail-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 0.75rem;
}

.detail-label {
  display: block;
  font-size: 0.8rem;
  font-weight: 600;
  color: var(--color-text-muted, #6c757d);
  margin-bottom: 0.25rem;
}

.detail-value {
  margin: 0;
  font-weight: 500;
}

.detail-box {
  margin: 0;
  padding: 0.75rem;
  border-radius: 8px;
  background: var(--color-surface-subtle, #f6f8fa);
  border: 1px solid var(--color-border, #e5e7eb);
  white-space: pre-wrap;
}
</style>
