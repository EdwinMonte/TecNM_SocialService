<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { canReadProcess, hasRole } from '../../../shared/composables/useSession'
import { useRouter } from 'vue-router'
import { getRoleDisplayLabel } from '../../../app/layouts/mainLayout.config'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard } from '../../../shared/components'
import { getReviewQueue } from '../../expedients/api/expedientsApi'
import type { ReviewQueueItem } from '../../expedients/types/expedients'
import { getInternships, getMyInternship } from '../../internships/api/internshipsApi'
import type { InternshipDetail, InternshipListItem } from '../../internships/types/internships'
import { getDashboardMetrics } from '../../reports/api/reportsApi'
import type { DashboardMetrics } from '../../reports/types/reports'
import { getMySocialService, getSocialServices } from '../../social-service/api/socialServiceApi'
import type { SocialServiceDetail, SocialServiceListItem } from '../../social-service/types/socialService'
import { getMyEligibility, getMyStudentProfile } from '../../students/api/studentsApi'
import type { StudentDetail, StudentEligibility } from '../../students/types/students'
import { me } from '../api/authApi'
import type { UserResponse } from '../types/auth'

const router = useRouter()
const user = ref<UserResponse | null>(null)
const isLoading = ref(true)
const errorMessage = ref('')

// Datos para vista de Estudiante
const studentProfile = ref<StudentDetail | null>(null)
const studentEligibility = ref<StudentEligibility | null>(null)
const mySocialService = ref<SocialServiceDetail | null>(null)
const myInternship = ref<InternshipDetail | null>(null)

// Datos para vista de Personal / Vinculación
const dashboard = ref<DashboardMetrics | null>(null)
const pendingDocs = ref<ReviewQueueItem[]>([])
const recentSS = ref<SocialServiceListItem[]>([])
const recentPP = ref<InternshipListItem[]>([])

const isStudentOnly = computed(() => {
  const roles = user.value?.roles ?? []
  return (
    roles.includes('student') &&
    !roles.some((r) =>
      ['admin', 'social_service_officer', 'internship_coordinator', 'department_head'].includes(r),
    )
  )
})

const roleTitle = computed(() => getRoleDisplayLabel(user.value?.roles ?? []))

const totalSSCount = computed(() => {
  if (!dashboard.value?.socialService) return 0
  return Object.values(dashboard.value.socialService).reduce((acc, n) => acc + Number(n || 0), 0)
})

const totalPPCount = computed(() => {
  if (!dashboard.value?.internships) return 0
  return Object.values(dashboard.value.internships).reduce((acc, n) => acc + Number(n || 0), 0)
})

const statusLabels: Record<string, string> = {
  requested: 'Solicitud en Revisión',
  approved: 'Carta Presentación Emitida',
  accepted: 'Carta Aceptación Validada',
  active: 'En Curso (Activo)',
  completed_pending_review: 'Documentos Completos · Por Liberar',
  released: 'Liberado (100%)',
  cancelled: 'Cancelado',
  non_compliant: 'Producto No Conforme (PNC)',
}

function formatStatus(status?: string | null): string {
  if (!status) return 'Sin iniciar'
  return statusLabels[status] || status
}

function getStatusTone(status?: string | null): 'success' | 'warning' | 'danger' | 'info' | 'purple' {
  if (status === 'released') return 'success'
  if (status === 'active' || status === 'approved' || status === 'accepted') return 'info'
  if (status === 'requested' || status === 'completed_pending_review') return 'warning'
  if (status === 'non_compliant' || status === 'cancelled') return 'danger'
  return 'info'
}

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    user.value = await me()
    if (isStudentOnly.value) {
      const [profile, elig, ss, pp] = await Promise.all([
        getMyStudentProfile().catch(() => null),
        getMyEligibility().catch(() => null),
        getMySocialService().catch(() => null),
        getMyInternship().catch(() => null),
      ])
      studentProfile.value = profile
      studentEligibility.value = elig
      mySocialService.value = ss
      myInternship.value = pp
    } else {
      const [dash, docs, ssRes, ppRes] = await Promise.all([
        getDashboardMetrics().catch(() => null),
        hasRole('admin', 'social_service_officer', 'internship_coordinator') ? getReviewQueue('pending', { pageSize: 10 }).catch(() => []) : Promise.resolve([]),
        canReadProcess('social_service') ? getSocialServices({ pageSize: 6 }).catch(() => ({ items: [] })) : Promise.resolve({ items: [] }),
        canReadProcess('internship') ? getInternships({ pageSize: 6 }).catch(() => ({ items: [] })) : Promise.resolve({ items: [] }),
      ])
      dashboard.value = dash
      pendingDocs.value = docs
      recentSS.value = ssRes.items ?? []
      recentPP.value = ppRes.items ?? []
    }
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'No se pudo cargar la información del panel.'
  } finally {
    isLoading.value = false
  }
}

onMounted(() => {
  void loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <!-- Encabezado Institucional con Rol Activo -->
    <div class="dashboard-header">
      <div>
        <div class="role-pill-row">
          <UiBadge tone="info">{{ roleTitle }}</UiBadge>
          <span v-if="dashboard?.period" class="period-tag">
            Periodo Activo: <strong>{{ dashboard.period.code }}</strong>
          </span>
        </div>
        <h1 class="page-title">
          {{ isStudentOnly ? `Bienvenido(a), ${user?.name || 'Estudiante'}` : 'Panel Operativo de Gestión Tecnológica y Vinculación' }}
        </h1>
        <p class="page-subtitle">
          {{
            isStudentOnly
              ? 'Consulta tu elegibilidad, genera tus formatos oficiales prellenados y sube tus documentos firmados y sellados.'
              : 'Seguimiento en tiempo real de Servicio Social, Prácticas Profesionales, Revisión Documental y Estadísticos.'
          }}
        </p>
      </div>
      <div v-if="!isStudentOnly" class="header-actions">
        <UiButton variant="secondary" @click="router.push('/students')">Padrón de Alumnos</UiButton>
        <UiButton variant="primary" @click="router.push('/reports/tracking')">
          Ver Sábana Seguimiento (Excel)
        </UiButton>
      </div>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>

    <div v-if="isLoading" class="loading-box">Cargando información en tiempo real...</div>

    <!-- ==================================================================== -->
    <!-- VISTA 1: PORTAL DEL ESTUDIANTE (Rol 'student')                       -->
    <!-- ==================================================================== -->
    <div v-else-if="isStudentOnly" class="student-portal-grid">
      <UiCard v-if="studentProfile" title="Mi Expediente Académico e IMSS">
        <div class="student-summary-grid">
          <div>
            <span class="meta-label">No. de Control</span>
            <strong class="meta-value">{{ studentProfile.controlNumber }}</strong>
          </div>
          <div>
            <span class="meta-label">Carrera</span>
            <strong class="meta-value">{{ studentProfile.careerName }}</strong>
          </div>
          <div>
            <span class="meta-label">Semestre / Turno</span>
            <strong class="meta-value">
              {{ studentProfile.currentSemester }}° Semestre ·
              {{ studentProfile.shift === 'morning' ? 'Matutino' : studentProfile.shift === 'night' ? 'Nocturno' : 'Vespertino' }}
            </strong>
          </div>
          <div>
            <span class="meta-label">Avance Reticular</span>
            <strong class="meta-value" style="color: #0284c7;">
              {{ studentProfile.curriculumProgressPercentage }}% ({{ studentProfile.completedCredits }} créditos)
            </strong>
          </div>
          <div>
            <span class="meta-label">Vigencia de Derechos IMSS</span>
            <div style="display: flex; align-items: center; gap: 0.5rem; margin-top: 0.25rem;">
              <UiBadge :tone="studentProfile.hasImssFile || studentProfile.hasValidImss ? 'success' : 'warning'">
                {{ studentProfile.hasImssFile ? 'Constancia PDF Cargada' : studentProfile.hasValidImss ? 'IMSS Vigente' : 'Falta Subir PDF IMSS' }}
              </UiBadge>
            </div>
          </div>
          <div style="display: flex; align-items: center; justify-content: flex-end;">
            <UiButton variant="secondary" @click="router.push('/students/me')">
              Actualizar Perfil / Subir IMSS
            </UiButton>
          </div>
        </div>
      </UiCard>

      <div class="tramites-cards-row">
        <!-- Tarjeta Servicio Social -->
        <UiCard title="1. Mi Servicio Social (480 Horas)">
          <div v-if="mySocialService" class="tramite-card-body">
            <div class="tramite-status-line">
              <UiBadge :tone="getStatusTone(mySocialService.status)">
                {{ formatStatus(mySocialService.status) }}
              </UiBadge>
              <UiBadge :tone="mySocialService.modality === 'internal' ? 'info' : 'purple'">
                {{ mySocialService.modality === 'internal' ? 'Local en el TecNM' : 'Dependencia Externa' }}
              </UiBadge>
            </div>

            <div class="info-pairs">
              <div>
                <span class="meta-label">Dependencia / Departamento:</span>
                <strong>{{ mySocialService.company.name }}</strong>
              </div>
              <div>
                <span class="meta-label">Programa:</span>
                <strong>{{ mySocialService.program.name }}</strong>
              </div>
              <div>
                <span class="meta-label">Folio Carta Presentación (CP):</span>
                <strong>{{ mySocialService.presentationLetterFolio || 'En espera de dictamen' }}</strong>
              </div>
              <div v-if="mySocialService.releaseCertificateFolio">
                <span class="meta-label">Constancia de Terminación:</span>
                <strong style="color: #16a34a;">{{ mySocialService.releaseCertificateFolio }}</strong>
              </div>
            </div>

            <div class="progress-section">
              <div class="progress-label-row">
                <span>Progreso Documental (Documentos Validados)</span>
                <strong>{{ mySocialService.documentProgressPercentage }}%</strong>
              </div>
              <div class="progress-track">
                <div
                  class="progress-fill"
                  :style="{ width: `${mySocialService.documentProgressPercentage}%` }"
                ></div>
              </div>
            </div>

            <div class="card-footer-actions">
              <UiButton variant="primary" @click="router.push('/social-service/my')">
                Gestionar Formatos y Subir Documentos SS
              </UiButton>
            </div>
          </div>

          <div v-else class="tramite-card-body">
            <p class="empty-text">
              Aún no has iniciado tu trámite de Servicio Social. Aquí puedes llenar tus datos del
              <strong>Anexo XVIII</strong> adjuntando tu Constancia de Vigencia del IMSS para que el
              sistema genere tu formato automáticamente y lo envíe a Vinculación.
            </p>
            <div v-if="studentEligibility" style="margin-bottom: 1rem;">
              <UiBadge :tone="studentEligibility.socialService.eligible ? 'success' : 'warning'">
                {{
                  studentEligibility.socialService.eligible
                    ? 'Cumples requisitos para solicitar apertura'
                    : 'Revisa requisitos pendientes'
                }}
              </UiBadge>
            </div>
            <UiButton variant="primary" @click="router.push('/social-service/my')">
              Solicitar Apertura de Servicio Social
            </UiButton>
          </div>
        </UiCard>

        <!-- Tarjeta Prácticas Profesionales -->
        <UiCard title="2. Mis Prácticas Profesionales (Productividad Laboral)">
          <div v-if="myInternship" class="tramite-card-body">
            <div class="tramite-status-line">
              <UiBadge :tone="getStatusTone(myInternship.status)">
                {{ formatStatus(myInternship.status) }}
              </UiBadge>
            </div>

            <div class="info-pairs">
              <div>
                <span class="meta-label">Empresa Receptora:</span>
                <strong>{{ myInternship.company?.name || 'Por registrar con Oficio de Aceptación (CA)' }}</strong>
              </div>
              <div>
                <span class="meta-label">Folio Carta Presentación (CP):</span>
                <strong>{{ myInternship.presentationLetterFolio || 'En espera de emisión' }}</strong>
              </div>
              <div v-if="myInternship.releaseCertificateFolio">
                <span class="meta-label">Constancia de Liberación (CC):</span>
                <strong style="color: #16a34a;">{{ myInternship.releaseCertificateFolio }}</strong>
              </div>
            </div>

            <div class="progress-section">
              <div class="progress-label-row">
                <span>Progreso Documental (CP, CA, EV, CT, CC)</span>
                <strong>{{ myInternship.documentProgressPercentage }}%</strong>
              </div>
              <div class="progress-track">
                <div
                  class="progress-fill green"
                  :style="{ width: `${myInternship.documentProgressPercentage}%` }"
                ></div>
              </div>
            </div>

            <div class="card-footer-actions">
              <UiButton variant="primary" @click="router.push('/internships/my')">
                Gestionar Carta Presentación y Documentos PP
              </UiButton>
            </div>
          </div>

          <div v-else class="tramite-card-body">
            <p class="empty-text">
              Solicita tu <strong>Carta de Presentación</strong> llenando el formulario automatizado
              y adjuntando tu Constancia de Vigencia de Derechos del IMSS. Posteriormente la empresa
              enviará su oficio de aceptación membretado a
              <code>arturo.martinez@monclova.tecnm.mx</code>.
            </p>
            <UiButton variant="primary" @click="router.push('/internships/my')">
              Solicitar Carta de Presentación de Prácticas
            </UiButton>
          </div>
        </UiCard>
      </div>
    </div>

    <!-- ==================================================================== -->
    <!-- VISTA 2: PANEL DE PERSONAL Y DIRECTIVOS (Staff / Admin / Jefatura)   -->
    <!-- ==================================================================== -->
    <div v-else class="staff-portal-grid">
      <div class="kpi-row">
        <UiCard v-if="canReadProcess('social_service')">
          <div class="kpi-label">TRÁMITES SERVICIO SOCIAL</div>
          <div class="kpi-number blue">{{ totalSSCount }}</div>
          <small class="kpi-sub">
            Liberados: <strong>{{ dashboard?.socialService?.released || 0 }}</strong> ·
            En curso: <strong>{{ dashboard?.socialService?.active || 0 }}</strong>
          </small>
        </UiCard>

        <UiCard v-if="canReadProcess('internship')">
          <div class="kpi-label">TRÁMITES PRÁCTICAS PROF.</div>
          <div class="kpi-number green">{{ totalPPCount }}</div>
          <small class="kpi-sub">
            Liberados: <strong>{{ dashboard?.internships?.released || 0 }}</strong> ·
            En curso: <strong>{{ dashboard?.internships?.active || 0 }}</strong>
          </small>
        </UiCard>

        <UiCard>
          <div class="kpi-label">DOCUMENTOS POR REVISAR</div>
          <div class="kpi-number amber">{{ dashboard?.pendingDocumentReviews ?? pendingDocs.length }}</div>
          <small class="kpi-sub">PDFs subidos por alumnos pendientes de validación</small>
        </UiCard>

        <UiCard>
          <div class="kpi-label">PRODUCTO NO CONFORME (PNC)</div>
          <div class="kpi-number purple">{{ dashboard?.openNonConformances ?? 0 }}</div>
          <small class="kpi-sub">
            Convenios por vencer: <strong>{{ dashboard?.agreementsExpiringSoon ?? 0 }}</strong>
          </small>
        </UiCard>
      </div>

      <div class="staff-panels-grid">
        <!-- Cola de Documentos Pendientes de Revisión -->
        <UiCard title="Documentos Recientes Pendientes de Validación">
          <div v-if="pendingDocs.length === 0" class="empty-box">
            No hay documentos pendientes de dictamen en la cola de revisión.
          </div>
          <div v-else class="list-stack">
            <div v-for="doc in pendingDocs" :key="doc.id" class="list-item-row">
              <div>
                <div class="item-title">
                  <strong>{{ doc.studentControlNumber }}</strong> · {{ doc.studentFullName }}
                  <UiBadge tone="info">{{ doc.careerCode }}</UiBadge>
                </div>
                <div class="item-sub">
                  <UiBadge :tone="doc.tramiteType === 'social_service' ? 'info' : 'purple'">
                    {{ doc.tramiteType === 'social_service' ? 'Servicio Social' : 'Prácticas' }}
                  </UiBadge>
                  <span>Clave: <strong>{{ doc.documentCode }}</strong> ({{ doc.originalFileName }})</span>
                </div>
              </div>
              <UiButton variant="primary" size="sm" @click="router.push('/expedients/review')">
                Revisar PDF
              </UiButton>
            </div>
          </div>
        </UiCard>

        <!-- Resumen de Trámites Activos y Solicitudes -->
        <UiCard title="Trámites en Seguimiento (Servicio Social y Prácticas)">
          <div class="list-stack">
            <div v-for="ss in recentSS.slice(0, 4)" :key="`ss-${ss.id}`" class="list-item-row">
              <div>
                <div class="item-title">
                  <UiBadge tone="info">SS</UiBadge>
                  <strong>{{ ss.studentControlNumber }}</strong> · {{ ss.studentFullName }}
                </div>
                <div class="item-sub">
                  <span>{{ ss.companyName }}</span> ·
                  <strong>Avance: {{ ss.documentProgressPercentage }}%</strong>
                </div>
              </div>
              <div style="display: flex; align-items: center; gap: 0.5rem;">
                <UiBadge :tone="getStatusTone(ss.status)">{{ formatStatus(ss.status) }}</UiBadge>
                <UiButton variant="secondary" size="sm" @click="router.push('/social-service')">
                  Abrir
                </UiButton>
              </div>
            </div>

            <div v-for="pp in recentPP.slice(0, 4)" :key="`pp-${pp.id}`" class="list-item-row">
              <div>
                <div class="item-title">
                  <UiBadge tone="purple">PP</UiBadge>
                  <strong>{{ pp.studentControlNumber }}</strong> · {{ pp.studentFullName }}
                </div>
                <div class="item-sub">
                  <span>{{ pp.companyName || 'Carta Presentación solicitada' }}</span> ·
                  <strong>Avance: {{ pp.documentProgressPercentage }}%</strong>
                </div>
              </div>
              <div style="display: flex; align-items: center; gap: 0.5rem;">
                <UiBadge :tone="getStatusTone(pp.status)">{{ formatStatus(pp.status) }}</UiBadge>
                <UiButton variant="secondary" size="sm" @click="router.push('/internships')">
                  Abrir
                </UiButton>
              </div>
            </div>
          </div>
        </UiCard>
      </div>
    </div>
  </UiBasePage>
</template>

<style scoped>
.dashboard-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-wrap: wrap;
  gap: 1rem;
  margin-bottom: 1.5rem;
}

.role-pill-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  margin-bottom: 0.4rem;
}

.period-tag {
  font-size: 0.85rem;
  color: var(--color-text-muted, #6b7280);
}

.page-title {
  font-size: 1.5rem;
  font-weight: 700;
  margin: 0;
}

.page-subtitle {
  color: var(--color-text-muted, #6b7280);
  margin: 0.25rem 0 0;
  font-size: 0.95rem;
}

.header-actions {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.loading-box,
.empty-box {
  padding: 2rem;
  text-align: center;
  color: var(--color-text-muted, #6b7280);
}

.student-portal-grid,
.staff-portal-grid {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.student-summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  align-items: center;
}

.meta-label {
  display: block;
  font-size: 0.8rem;
  color: var(--color-text-muted, #6b7280);
  margin-bottom: 0.15rem;
}

.meta-value {
  font-size: 1rem;
  font-weight: 600;
}

.tramites-cards-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
  gap: 1.5rem;
}

.tramite-card-body {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.tramite-status-line {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.info-pairs {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  font-size: 0.92rem;
}

.progress-section {
  background: var(--color-surface-subtle, #f8fafc);
  padding: 0.85rem;
  border-radius: 8px;
  border: 1px solid var(--color-border, #e5e7eb);
}

.progress-label-row {
  display: flex;
  justify-content: space-between;
  font-size: 0.85rem;
  margin-bottom: 0.4rem;
}

.progress-track {
  height: 10px;
  background: #e5e7eb;
  border-radius: 999px;
  overflow: hidden;
}

.progress-fill {
  height: 100%;
  background: #0284c7;
  transition: width 0.3s ease;
}

.progress-fill.green {
  background: #16a34a;
}

.card-footer-actions {
  display: flex;
  justify-content: flex-end;
  padding-top: 0.5rem;
}

.empty-text {
  font-size: 0.92rem;
  color: var(--color-text-secondary, #4b5563);
  line-height: 1.5;
  margin: 0;
}

.kpi-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1rem;
}

.kpi-label {
  font-size: 0.8rem;
  font-weight: 700;
  color: var(--color-text-muted, #6b7280);
}

.kpi-number {
  font-size: 2rem;
  font-weight: 800;
  margin: 0.35rem 0;
}

.kpi-number.blue {
  color: #0284c7;
}
.kpi-number.green {
  color: #16a34a;
}
.kpi-number.amber {
  color: #d97706;
}
.kpi-number.purple {
  color: #7c3aed;
}

.kpi-sub {
  font-size: 0.82rem;
  color: var(--color-text-muted, #6b7280);
}

.staff-panels-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(420px, 1fr));
  gap: 1.5rem;
}

.list-stack {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.list-item-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  padding: 0.75rem;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
}

.item-title {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.92rem;
  margin-bottom: 0.25rem;
}

.item-sub {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  font-size: 0.82rem;
  color: var(--color-text-muted, #6b7280);
}
</style>
