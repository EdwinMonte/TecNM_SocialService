<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { defaultProcess } from '../../../shared/composables/useSession'
import LinkageMetrics from '../components/LinkageMetrics.vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiField } from '../../../shared/components'
import { getPeriodOptions } from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { get911Report, getSemesterStatistics } from '../api/reportsApi'

const stats = ref<any>(null)
const rep911 = ref<any>(null)
const periodOptions = ref<OptionItem[]>([])
const selectedPeriodId = ref<number | undefined>(undefined)
const route = useRoute(), router = useRouter()
const tramiteType = computed<'social_service' | 'internship'>(() => route.query.tramiteType === 'internship' || route.query.tramiteType === 'social_service' ? route.query.tramiteType : defaultProcess.value)
function selectProcess(value: 'social_service' | 'internship') { void router.replace({ query: { ...route.query, tramiteType: value } }) }
let sequence = 0, controller: AbortController | undefined
watch(tramiteType, () => { void loadData() })
onBeforeUnmount(() => { sequence++; controller?.abort() })
const isLoading = ref(true)
const errorMessage = ref('')

const sectorLabels: Record<string, string> = {
  educational: 'Educativo (Propia Institución TecNM)',
  public: 'Sector Público',
  private: 'Sector Privado',
  social: 'Sector Social',
}

async function loadData(): Promise<void> {
  controller?.abort(); controller = new AbortController(); const request = ++sequence
  isLoading.value = true
  errorMessage.value = ''; stats.value = null; rep911.value = null
  try {
    const [sData, rData] = await Promise.all([
      getSemesterStatistics(selectedPeriodId.value, tramiteType.value, undefined, controller.signal),
      get911Report(selectedPeriodId.value, tramiteType.value, undefined, controller.signal),
    ])
    if (request !== sequence) return
    stats.value = sData
    rep911.value = rData
  } catch (err: any) {
    if (request !== sequence || err.code === 'ERR_CANCELED') return
    errorMessage.value =
      problemMessage(err, 'Error al cargar estadísticas institucionales.')
  } finally {
    if (request === sequence) isLoading.value = false
  }
}

onMounted(async () => {
  periodOptions.value = await getPeriodOptions().catch(() => [])
  await loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">
        Estadístico Semestral y Formato Oficial SEP/TecNM 911.10
      </h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Replica en tiempo real las hojas `ESTADÍSTICO`, `PNC` y `911.10` de `SEGUIMIENTO VINCULACIÓN1.xlsx`
      </p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>

    <UiCard style="margin-bottom: 1.5rem;">
      <div style="display: flex; justify-content: space-between; align-items: flex-end; flex-wrap: wrap; gap: 1rem;">
        <div style="display: flex; gap: 0.5rem;">
          <UiButton
            :variant="tramiteType === 'social_service' ? 'primary' : 'secondary'"
            @click="selectProcess('social_service')"
          >
            Estadístico Servicio Social
          </UiButton>
          <UiButton
            :variant="tramiteType === 'internship' ? 'primary' : 'secondary'"
            @click="selectProcess('internship')"
          >
            Estadístico Prácticas Profesionales
          </UiButton>
        </div>

        <UiField label="Periodo Escolar" v-slot="{ controlAttrs }">
          <select v-bind="controlAttrs" v-model="selectedPeriodId" @change="loadData">
            <option :value="undefined">Periodo Activo</option>
            <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
          </select>
        </UiField>
      </div>
    </UiCard>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">
      Calculando indicadores en tiempo real...
    </div>

    <div v-else-if="stats && rep911" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <LinkageMetrics :tramite-type="tramiteType" :period-id="selectedPeriodId" />
      <!-- Tabla 1: Hoja ESTADÍSTICO por Carrera y Género -->
      <UiCard
        :title="`Hoja ESTADÍSTICO (${tramiteType === 'social_service' ? 'Servicio Social' : 'Prácticas Profesionales'})`"
      >
        <div style="overflow-x: auto;">
          <table class="stats-table">
            <thead>
              <tr>
                <th>Carrera</th>
                <th style="text-align: center;">En Condiciones (C.P. = SI)</th>
                <th style="text-align: center;">Solicitantes H</th>
                <th style="text-align: center;">Solicitantes M</th>
                <th style="text-align: center;">Total Solicitantes (C.A. = SI)</th>
                <th style="text-align: center;">Liberados H</th>
                <th style="text-align: center;">Liberados M</th>
                <th style="text-align: center;">Total Liberados</th>
                <th style="text-align: center;">PNC (Solic. - Lib.)</th>
                <th style="text-align: center;">% Liberación</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in stats?.byCareer || []" :key="row.careerCode">
                <td>
                  <strong>{{ row.careerCode }}</strong> — {{ row.careerName }}
                </td>
                <td style="text-align: center;">{{ row.eligibleWithPresentationLetter }}</td>
                <td style="text-align: center;">{{ row.applicantsMale }}</td>
                <td style="text-align: center;">{{ row.applicantsFemale }}</td>
                <td style="text-align: center; font-weight: 700;">{{ row.applicantsTotal }}</td>
                <td style="text-align: center;">{{ row.releasedMale }}</td>
                <td style="text-align: center;">{{ row.releasedFemale }}</td>
                <td style="text-align: center; font-weight: 700; color: #16a34a;">
                  {{ row.releasedTotal }}
                </td>
                <td style="text-align: center; color: #d97706; font-weight: 600;">
                  {{ row.pncTotal }}
                </td>
                <td style="text-align: center;">
                  <UiBadge :tone="row.releaseRatePercentage >= 80 ? 'success' : 'info'">
                    {{ row.releaseRatePercentage }}%
                  </UiBadge>
                </td>
              </tr>
              <tr v-if="stats?.totals" class="totals-row">
                <td><strong>TOTAL INSTITUCIONAL</strong></td>
                <td style="text-align: center;">
                  {{ stats.totals.eligibleWithPresentationLetter }}
                </td>
                <td style="text-align: center;">{{ stats.totals.applicantsMale }}</td>
                <td style="text-align: center;">{{ stats.totals.applicantsFemale }}</td>
                <td style="text-align: center;">{{ stats.totals.applicantsTotal }}</td>
                <td style="text-align: center;">{{ stats.totals.releasedMale }}</td>
                <td style="text-align: center;">{{ stats.totals.releasedFemale }}</td>
                <td style="text-align: center; color: #16a34a;">
                  {{ stats.totals.releasedTotal }}
                </td>
                <td style="text-align: center; color: #d97706;">{{ stats.totals.pncTotal }}</td>
                <td style="text-align: center;">{{ stats.totals.releaseRatePercentage }}%</td>
              </tr>
            </tbody>
          </table>
        </div>
      </UiCard>

      <!-- Tabla 2: Formato Oficial SEP / TecNM 911.10 -->
      <div style="display: grid; grid-template-columns: repeat(auto-fit, minmax(340px, 1fr)); gap: 1.5rem;">
        <UiCard title="Formato 911.10 — Distribución por Sector">
          <table class="stats-table">
            <thead>
              <tr>
                <th>Sector</th>
                <th style="text-align: center;">Hombres</th>
                <th style="text-align: center;">Mujeres</th>
                <th style="text-align: center;">Total</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="sec in rep911?.byCompanySector || []" :key="sec.sector">
                <td>{{ sectorLabels[sec.sector] || sec.sector }}</td>
                <td style="text-align: center;">{{ sec.male }}</td>
                <td style="text-align: center;">{{ sec.female }}</td>
                <td style="text-align: center; font-weight: 700;">{{ sec.total }}</td>
              </tr>
            </tbody>
          </table>
        </UiCard>

        <UiCard title="Indicadores de Impacto e Inclusión (911.10)">
          <div style="display: flex; flex-direction: column; gap: 0.85rem;">
            <div style="display: flex; justify-content: space-between; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.5rem;">
              <span>Estudiantes con Discapacidad Atendidos:</span>
              <strong>{{ rep911?.withDisability?.total || 0 }}</strong>
            </div>
            <div
              v-if="tramiteType === 'social_service'"
              style="display: flex; justify-content: space-between; border-bottom: 1px solid #e5e7eb; padding-bottom: 0.5rem;"
            >
              <span>Trámites con Actividades de Inclusión:</span>
              <strong>{{ rep911?.withInclusionActivities || 0 }}</strong>
            </div>
            <div
              v-if="tramiteType === 'social_service'"
              style="display: flex; justify-content: space-between;"
            >
              <span>Total de Ciudadanos Beneficiados:</span>
              <strong style="color: #0284c7; font-size: 1.1rem;">
                {{ rep911?.totalBeneficiaries || 0 }} personas
              </strong>
            </div>
          </div>
        </UiCard>
      </div>
    </div>
  </UiBasePage>
</template>

<style scoped>
.stats-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.86rem;
}

.stats-table th,
.stats-table td {
  border: 1px solid var(--color-border, #e5e7eb);
  padding: 0.6rem 0.75rem;
  text-align: left;
}

.stats-table th {
  background: var(--color-surface-subtle, #f8fafc);
  font-weight: 700;
}

.totals-row td {
  background: #f1f5f9;
  font-weight: 700;
}
</style>
