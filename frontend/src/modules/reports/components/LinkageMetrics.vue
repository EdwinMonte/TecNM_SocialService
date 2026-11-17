<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { http } from '../../../shared/api/http'
import { UiAlert, UiBadge, UiButton } from '../../../shared/components'
import { companyTypeLabels, type CompanyType, type Page } from '../../companies/types'

interface Counts {
  assignedStudents: number
  activeStudents: number
  projects: number
}
interface TypeCounts extends Counts {
  companyType: CompanyType
}
interface CompanyCounts extends TypeCounts {
  companyId: number
  companyName: string
}
interface LinkageResponse {
  tramiteType: string
  totals: Counts
  byCompanyType: TypeCounts[]
  companies: Page<CompanyCounts>
}

const props = defineProps<{ tramiteType: 'social_service' | 'internship'; periodId?: number }>()

const data = ref<LinkageResponse | null>(null)
const page = ref(1)
const pageSize = 10
const loading = ref(false)
const error = ref('')
let sequence = 0
let controller: AbortController | undefined

const rows = computed(
  () => data.value?.companies.items.map((row) => ({ ...row, id: row.companyId })) ?? [],
)

async function load(): Promise<void> {
  controller?.abort()
  controller = new AbortController()
  const request = ++sequence
  loading.value = true
  error.value = ''
  data.value = null
  try {
    const response = await http.get<LinkageResponse>('/reports/linkage', {
      params: {
        tramiteType: props.tramiteType,
        periodId: props.periodId,
        page: page.value,
        pageSize,
      },
      signal: controller.signal,
    })
    if (request === sequence) data.value = response.data
  } catch (e: any) {
    if (request === sequence && e.code !== 'ERR_CANCELED') {
      error.value = problemMessage(e, 'No se pudieron cargar las métricas por empresa.')
    }
  } finally {
    if (request === sequence) loading.value = false
  }
}

watch(
  () => [props.tramiteType, props.periodId],
  () => {
    page.value = 1
    void load()
  },
  { immediate: true },
)

onBeforeUnmount(() => {
  sequence++
  controller?.abort()
})

function share(count: number): number {
  return data.value?.totals.assignedStudents
    ? (count / data.value.totals.assignedStudents) * 100
    : 0
}
</script>

<template>
  <div class="linkage-shell">
    <UiAlert v-if="error" tone="danger" class="compact-alert">
      <div class="error-row">
        <span>{{ error }}</span>
        <UiButton variant="secondary" size="sm" @click="load">Reintentar</UiButton>
      </div>
    </UiAlert>

    <div v-if="loading" class="loading-box" role="status">
      Calculando métricas de vinculación por empresa…
    </div>

    <div v-else-if="data" class="linkage-grid">
      <!-- Panel Izquierdo: Distribución por Tipo de Empresa -->
      <section class="dist-panel">
        <div class="panel-head">
          <h3 class="panel-title">Distribución por Tipo de Empresa</h3>
          <div class="summary-chips">
            <UiBadge tone="info">{{ data.totals.assignedStudents }} asignados</UiBadge>
            <UiBadge tone="success">{{ data.totals.activeStudents }} activos</UiBadge>
            <UiBadge tone="warning">{{ data.totals.projects }} proyectos</UiBadge>
          </div>
        </div>

        <ul class="distribution" aria-label="Distribución por tipo de empresa">
          <li v-for="item in data.byCompanyType" :key="item.companyType" class="dist-row">
            <div class="dist-meta">
              <span class="dist-label">{{ companyTypeLabels[item.companyType] }}</span>
              <strong class="dist-val">
                {{ item.assignedStudents }} ({{ share(item.assignedStudents).toFixed(1) }}%)
              </strong>
            </div>
            <span class="track" aria-hidden="true">
              <span class="track-fill" :style="{ width: `${share(item.assignedStudents)}%` }" />
            </span>
          </li>
        </ul>
      </section>

      <!-- Panel Derecho: Tabla de Empresas (10 por página) -->
      <section class="table-panel">
        <div class="linkage-table-wrap" role="region" aria-label="Proyectos y alumnos por empresa">
          <table class="linkage-table">
            <thead>
              <tr>
                <th scope="col">EMPRESA / DEPENDENCIA</th>
                <th scope="col">TIPO</th>
                <th scope="col" class="th-center">PROYECTOS</th>
                <th scope="col" class="th-center">ASIGNADOS</th>
                <th scope="col" class="th-center">ACTIVOS</th>
              </tr>
            </thead>
            <tbody>
              <tr v-if="!rows.length">
                <td colspan="5" class="empty-cell">
                  No hay asignaciones vigentes en este periodo.
                </td>
              </tr>
              <tr v-for="row in rows" :key="row.id" class="linkage-tr">
                <td class="company-name">{{ row.companyName }}</td>
                <td>
                  <span class="type-pill">{{ companyTypeLabels[row.companyType] }}</span>
                </td>
                <td class="td-center num">{{ row.projects }}</td>
                <td class="td-center num">{{ row.assignedStudents }}</td>
                <td class="td-center num active-num">{{ row.activeStudents }}</td>
              </tr>
            </tbody>
          </table>
        </div>

        <footer class="pagination-bar">
          <span class="pagination-info">
            Mostrando <strong>{{ rows.length }}</strong> de
            <strong>{{ data.companies.totalItems }}</strong> empresas · Página
            <strong>{{ page }}</strong> de
            <strong>{{ Math.max(data.companies.totalPages, 1) }}</strong>
          </span>
          <div class="pagination-controls">
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="page <= 1"
              @click="page--; load()"
            >
              Anterior
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="page >= data.companies.totalPages"
              @click="page++; load()"
            >
              Siguiente
            </UiButton>
          </div>
        </footer>
      </section>
    </div>
  </div>
</template>

<style scoped>
.linkage-shell {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
}

.compact-alert {
  margin-bottom: 0.55rem;
}

.error-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
}

.loading-box {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-secondary);
}

.linkage-grid {
  display: grid;
  grid-template-columns: 340px minmax(0, 1fr);
  gap: 0.85rem;
  flex: 1;
  min-height: 0;
}

.dist-panel,
.table-panel {
  display: flex;
  flex-direction: column;
  min-height: 0;
  padding: 0.75rem 0.9rem;
  border-radius: 8px;
  border: 1px solid var(--color-border);
  background: var(--surface-toolbar);
}

.table-panel {
  background: var(--color-surface-white);
  padding: 0;
  overflow: hidden;
}

.panel-head {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  padding-bottom: 0.55rem;
  margin-bottom: 0.55rem;
  border-bottom: 1px solid var(--color-border-light);
}

.panel-title {
  margin: 0;
  font-size: 0.84rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.summary-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.distribution {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  overflow-y: auto;
}

.dist-row {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.dist-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.78rem;
}

.dist-label {
  color: var(--color-text-secondary);
  font-weight: 600;
}

.dist-val {
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.track {
  height: 7px;
  background: var(--color-border);
  border-radius: 4px;
  overflow: hidden;
}

.track-fill {
  display: block;
  height: 100%;
  background: var(--color-info);
  border-radius: 4px;
}

.linkage-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0;
}

.linkage-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  font-size: 0.79rem;
}

.linkage-table th {
  padding: 7px 12px;
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  background: var(--surface-table-head);
  color: var(--color-text-secondary);
  border-bottom: 1px solid var(--color-border-medium);
  position: sticky;
  top: 0;
  z-index: 1;
}

.linkage-table td {
  padding: 7px 12px;
  border-bottom: 1px solid var(--color-border-light);
  vertical-align: middle;
}

.linkage-tr:hover {
  background: var(--color-primary-subtle);
}

.company-name {
  font-weight: 600;
  color: var(--color-text-primary);
}

.type-pill {
  display: inline-block;
  padding: 0.12rem 0.45rem;
  border-radius: 5px;
  font-size: 0.72rem;
  font-weight: 600;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
}

.th-center,
.td-center {
  text-align: center;
}

.num {
  font-variant-numeric: tabular-nums;
  font-weight: 600;
  color: var(--color-text-primary);
}

.active-num {
  color: var(--color-success-text);
}

.empty-cell {
  padding: 1.5rem !important;
  text-align: center;
  color: var(--color-text-secondary);
}

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding: 0.55rem 0.85rem;
  border-top: 1px solid var(--color-border);
  flex-shrink: 0;
}

.pagination-info {
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.pagination-controls {
  display: flex;
  gap: 0.4rem;
}

@media (max-width: 980px) {
  .linkage-grid {
    grid-template-columns: 1fr;
  }
}
</style>
