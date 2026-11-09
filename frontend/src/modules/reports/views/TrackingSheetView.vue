<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { defaultProcess, canReadProcess } from '../../../shared/composables/useSession'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiDialog, UiField } from '../../../shared/components'
import { getCareerOptions, getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { getTrackingData } from '../api/reportsApi'
import type { TrackingItem } from '../types/reports'
import { socialServiceDocuments, socialDocumentLabel } from '../../../shared/utils/socialServiceDocuments'
import TrackingExpedient from '../components/TrackingExpedient.vue'

const items = ref<TrackingItem[]>([])
const periodOptions = ref<OptionItem[]>([])
const careerOptions = ref<OptionItem[]>([])

const isLoading = ref(true)
const errorMessage = ref('')
let dataRequest = 0

const tramiteType = ref<'social_service' | 'internship'>(defaultProcess.value)
const selectedPeriodId = ref<number | undefined>(undefined)
const selectedCareerId = ref<number | undefined>(undefined)
const searchQuery = ref('')
const isExpedientOpen = ref(false)
const selectedRow = ref<TrackingItem | null>(null)
const selectedProcess = ref<'social_service' | 'internship'>(tramiteType.value)

function openExpedient(row: TrackingItem): void {
  selectedRow.value = row
  selectedProcess.value = tramiteType.value
  isExpedientOpen.value = true
}

const ssDocKeys = socialServiceDocuments.filter(item => item.trackingKey).map(item => ({ key: item.trackingKey!, label: socialDocumentLabel(item.codes[0]!) }))

const ppDocKeys = [
  { key: 'cp', label: 'CP' },
  { key: 'ca', label: 'CA' },
  { key: 'ev', label: 'EV' },
  { key: 'ct', label: 'CT' },
  { key: 'cc', label: 'CC' },
] as const

const activeDocKeys = computed(() =>
  tramiteType.value === 'social_service' ? ssDocKeys : ppDocKeys,
)

async function loadCatalogs(): Promise<void> {
  const [periods, careers] = await Promise.all([
    getPeriodOptions().catch(() => []),
    getCareerOptions().catch(() => []),
  ])
  periodOptions.value = periods
  careerOptions.value = careers
}

function handleSearch(): void {
  loadData()
}

function handleClearSearch(): void {
  searchQuery.value = ''
  loadData()
}

async function loadData(): Promise<void> {
  const request = ++dataRequest
  isLoading.value = true
  errorMessage.value = ''
  try {
    const res = await getTrackingData(
      selectedPeriodId.value,
      tramiteType.value,
      selectedCareerId.value,
      searchQuery.value.trim() || undefined,
    )
    if (request !== dataRequest) return
    items.value = (res.items || []).map((i) => ({ ...i, id: i.tramiteId }))
  } catch (err: any) {
    if (request !== dataRequest) return
    errorMessage.value =
      err?.response?.data?.message || 'Error al cargar la matriz de seguimiento en vivo.'
  } finally {
    if (request === dataRequest) isLoading.value = false
  }
}

function handleExportCsv(): void {
  if (items.value.length === 0) return
  const docCols = activeDocKeys.value.map((d) => d.label)
  const headers = [
    'NO_CONTROL',
    'ALUMNO',
    'CARRERA',
    'SEMESTRE',
    'DEPENDENCIA_EMPRESA',
    'FOLIO_CP',
    ...docCols,
    'AVANCE_%',
    'FOLIO_CONSTANCIA',
    'ACTA_ESCOLARES',
  ]

  const rows = items.value.map((r) => {
    const docs = (r.documents || {}) as Record<string, boolean | null | undefined>
    const docVals = activeDocKeys.value.map((d) => (docs[d.key] === null ? 'SIN DICTAMEN INDIVIDUAL' : docs[d.key] ? 'SI' : 'NO'))
    return [
      r.controlNumber,
      `"${(r.studentFullName || '').replace(/"/g, '""')}"`,
      r.careerCode,
      r.semesterEnrolled,
      `"${(r.companyName || '').replace(/"/g, '""')}"`,
      r.presentationLetterFolio || '',
      ...docVals,
      `${r.progressPercentage}%`,
      r.releaseCertificateFolio || '',
      r.schoolServicesActFolio || '',
    ].join(',')
  })

  const csvContent = '\uFEFF' + [headers.join(','), ...rows].join('\n')
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' })
  const url = window.URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = `SEGUIMIENTO_VINCULACION_${tramiteType.value.toUpperCase()}.csv`
  a.click()
  window.URL.revokeObjectURL(url)
}

onMounted(async () => {
  await Promise.all([loadCatalogs(), loadData()])
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div class="header-row">
      <div>
        <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">
          Sábana de Seguimiento de Vinculación (`SEGUIMIENTO VINCULACIÓN1.xlsx`)
        </h1>
        <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
          Matriz en vivo que refleja automáticamente cada documento subido y validado (`SI`) de 0%
          a 100%
        </p>
      </div>
      <UiButton variant="secondary" @click="handleExportCsv">
        Exportar Sábana a Excel (.CSV)
      </UiButton>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>

    <UiCard>
      <!-- Pestañas Servicio Social vs Prácticas + Filtros -->
      <div class="toolbar-row">
        <div style="display: flex; gap: 0.5rem;">
          <UiButton
            v-if="canReadProcess('social_service')"
            :variant="tramiteType === 'social_service' ? 'primary' : 'secondary'"
            @click="
              tramiteType = 'social_service';
              loadData()
            "
          >
            Hoja: Servicio Social (10 Hitos)
          </UiButton>
          <UiButton
            v-if="canReadProcess('internship')"
            :variant="tramiteType === 'internship' ? 'primary' : 'secondary'"
            @click="
              tramiteType = 'internship';
              loadData()
            "
          >
            Hoja: Prácticas Profesionales (5 Hitos)
          </UiButton>
        </div>

        <div class="filters-row">
          <UiField label="Periodo">
            <select v-model.number="selectedPeriodId" @change="loadData">
              <option :value="undefined">Periodo Activo</option>
              <option :value="0">Todos los Periodos</option>
              <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
            </select>
          </UiField>

          <UiField label="Carrera">
            <select v-model.number="selectedCareerId" @change="loadData">
              <option :value="undefined">Todas las Carreras</option>
              <option v-for="c in careerOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </UiField>

          <UiField label="Buscar Matrícula / Alumno">
            <div style="display: flex; gap: 0.5rem; align-items: center;">
              <input
                v-model="searchQuery"
                type="text"
                placeholder="Ej. I23050618"
                @keyup.enter="handleSearch"
              />
              <UiButton variant="primary" size="sm" @click="handleSearch">
                Buscar
              </UiButton>
              <UiButton
                v-if="searchQuery"
                variant="secondary"
                size="sm"
                @click="handleClearSearch"
              >
                Limpiar
              </UiButton>
            </div>
          </UiField>
        </div>
      </div>

      <!-- Tabla tipo Excel con Checklist Documental SI / PEND -->
      <div style="overflow-x: auto;">
        <table class="excel-table">
          <thead>
            <tr v-if="tramiteType === 'social_service'">
              <th colspan="9" />
              <th colspan="3" scope="colgroup">4. {{ socialServiceDocuments.find(item => item.number === '4')?.title }}</th>
              <th colspan="7" />
            </tr>
            <tr>
              <th>No. Control</th>
              <th>Nombre del Alumno</th>
              <th>Carrera</th>
              <th>Dependencia / Empresa</th>
              <th>Folio C.P.</th>
              <th v-for="col in activeDocKeys" :key="col.key" style="text-align: center;">
                {{ col.label }}
              </th>
              <th>Avance %</th>
              <th>Const. Term.</th>
              <th>Acta Escolares</th>
            </tr>
          </thead>
          <tbody>
            <tr v-if="isLoading">
              <td :colspan="8 + activeDocKeys.length" style="text-align: center; padding: 1.5rem;">
                Cargando sábana de seguimiento...
              </td>
            </tr>
            <tr v-else-if="items.length === 0">
              <td :colspan="8 + activeDocKeys.length" style="text-align: center; padding: 1.5rem;">
                No se encontraron trámites con los filtros seleccionados.
              </td>
            </tr>
            <tr v-for="row in items" :key="row.tramiteId">
              <td><strong>{{ row.controlNumber }}</strong></td>
              <td>
                <div class="student-cell">
                  <span>{{ row.studentFullName }}</span>
                  <UiButton
                    class="expedient-button"
                    variant="secondary"
                    title="Ver expediente"
                    :aria-label="`Ver expediente de ${row.studentFullName} (${row.controlNumber})`"
                    aria-haspopup="dialog"
                    :disabled="isLoading || !!errorMessage"
                    @click="openExpedient(row)"
                  >
                    <svg aria-hidden="true" viewBox="0 0 24 24" width="16" height="16" fill="none" stroke="currentColor" stroke-width="2">
                      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12Z" />
                      <circle cx="12" cy="12" r="3" />
                    </svg>
                  </UiButton>
                </div>
              </td>
              <td><UiBadge tone="info">{{ row.careerCode }}</UiBadge></td>
              <td>{{ row.companyName || 'Pendiente de CA' }}</td>
              <td><small>{{ row.presentationLetterFolio || '—' }}</small></td>
              <td
                v-for="col in activeDocKeys"
                :key="col.key"
                style="text-align: center;"
              >
                <span
                  :class="[
                    'doc-cell-pill',
                    (row.documents as any)?.[col.key] ? 'si' : 'no',
                  ]"
                >
                  {{ (row.documents as any)?.[col.key] ? 'SI' : '—' }}
                </span>
              </td>
              <td style="min-width: 115px;">
                <div style="display: flex; align-items: center; gap: 0.4rem;">
                  <div
                    style="flex: 1; height: 8px; background: #e5e7eb; border-radius: 4px; overflow: hidden;"
                  >
                    <div
                      :style="{
                        width: `${row.progressPercentage}%`,
                        height: '100%',
                        background: row.progressPercentage === 100 ? '#16a34a' : '#0284c7',
                      }"
                    ></div>
                  </div>
                  <strong style="font-size: 0.8rem;">{{ row.progressPercentage }}%</strong>
                </div>
              </td>
              <td>
                <strong style="color: #16a34a; font-size: 0.82rem;">
                  {{ row.releaseCertificateFolio || '—' }}
                </strong>
              </td>
              <td>
                <small>{{ row.schoolServicesActFolio || '—' }}</small>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </UiCard>
    <UiDialog v-model="isExpedientOpen" :title="`Expediente · ${selectedRow?.studentFullName || ''}`" size="xl">
      <TrackingExpedient v-if="selectedRow" :tramite-id="selectedRow.tramiteId" :tramite-type="selectedProcess" />
      <template #footer>
        <UiButton variant="secondary" @click="isExpedientOpen = false">Cerrar expediente</UiButton>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
.student-cell {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.expedient-button {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
  width: 24px;
  min-height: 24px;
  padding: 0;
}

.header-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.toolbar-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1.25rem;
}

.filters-row {
  display: flex;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.excel-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.84rem;
}

.excel-table th,
.excel-table td {
  border: 1px solid var(--color-border, #e5e7eb);
  padding: 0.55rem 0.6rem;
  text-align: left;
  white-space: nowrap;
}

.excel-table th {
  background: var(--color-surface-subtle, #f8fafc);
  font-weight: 700;
  color: var(--color-text-secondary, #374151);
}

.doc-cell-pill {
  display: inline-block;
  padding: 2px 7px;
  border-radius: 4px;
  font-size: 0.75rem;
  font-weight: 700;
}

.doc-cell-pill.si {
  background: #dcfce7;
  color: #15803d;
}

.doc-cell-pill.no {
  background: #f3f4f6;
  color: #9ca3af;
}
</style>
