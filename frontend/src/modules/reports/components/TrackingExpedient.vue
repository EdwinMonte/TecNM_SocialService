<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { UiAlert, UiBadge, UiButton } from '../../../shared/components'
import { problemMessage } from '../../../shared/api/problemDetails'
import { getStudentById, type StudentDetail } from '../../students'
import { getSocialServiceById, type SocialServiceDetail } from '../../social-service'
import { getInternshipById, type InternshipDetail } from '../../internships'
import { getDocumentBlob, getStudentDocuments, type ExpedientDocument } from '../../expedients'
import { socialDocumentLabel, socialDocumentOrder, socialDocumentPeriod } from '../../../shared/utils/socialServiceDocuments'
import DocumentViewerModal from '../../expedients/components/DocumentViewerModal.vue'

const props = defineProps<{ tramiteId: number; tramiteType: 'social_service' | 'internship' }>()
const student = ref<StudentDetail | null>(null)
const detail = ref<SocialServiceDetail | InternshipDetail | null>(null)
const documents = ref<ExpedientDocument[]>([])
const loading = ref(true)
const error = ref('')
const pdfError = ref('')
const pdfLoading = ref<number | null>(null)
const pdfOpen = ref(false)
const pdfBlob = ref<Blob | null>(null)
const pdfTitle = ref('')
let active = true

const statusLabels: Record<string, string> = {
  requested: 'Solicitado', approved: 'Aprobado', accepted: 'Aceptado', active: 'En curso',
  completed_pending_review: 'Concluido, pendiente de revisión', released: 'Liberado',
  cancelled: 'Cancelado', non_compliant: 'Producto no conforme',
  pending: 'Pendiente de revisión', rejected: 'Rechazado', superseded: 'Sustituido', missing: 'Sin documento',
}

function date(value?: string | null): string {
  return value ? new Date(value.length === 10 ? `${value}T00:00:00` : value).toLocaleDateString('es-MX') : 'Sin registro'
}

const sections = computed(() => {
  const s = student.value, d = detail.value
  if (!s || !d) return []
  return [
    { title: 'Datos personales y académicos', fields: [
      ['Nombre completo', [s.firstName, s.firstSurname, s.secondSurname].filter(Boolean).join(' ')],
      ['No. de control', s.controlNumber], ['CURP', s.curp], ['Carrera', s.careerName],
      ['Sexo', s.gender === 'M' ? 'Masculino' : s.gender === 'F' ? 'Femenino' : null],
      ['Semestre actual', s.currentSemester], ['Turno', { morning: 'Matutino', afternoon: 'Vespertino', night: 'Nocturno' }[s.shift]],
      ['Créditos completados', s.completedCredits], ['Avance académico', `${s.curriculumProgressPercentage}%`],
      ['Inscrito', s.isEnrolled ? 'Sí' : 'No'], ['Discapacidad', s.hasDisability ? s.disabilityDescription || 'Sí' : 'No'],
    ] },
    { title: 'Contacto', fields: [
      ['Correo institucional', s.institutionalEmail], ['Correo personal', s.personalEmail], ['Teléfono', s.phoneNumber],
    ] },
    { title: 'Vigencia IMSS', fields: [
      ['Estado documental', statusLabels[s.imssStatus] || s.imssStatus], ['Vigencia aprobada', s.hasValidImss ? 'Sí' : 'No'],
      ['Fecha del documento', date(s.imssDocumentDate)], ['Validó', s.imssValidatedByName],
      ['Fecha de validación', date(s.imssValidatedAt)], ['Observaciones', s.imssRejectionReason],
    ] },
    { title: props.tramiteType === 'social_service' ? 'Servicio Social' : 'Prácticas Profesionales', fields: [
      ['Periodo', d.period.code], ['Estado', statusLabels[d.status] || d.status], ['Semestre de inscripción', d.semesterEnrolled],
      ['Dependencia / Empresa', d.company?.name], ['Responsable', d.supervisor?.fullName], ['Cargo', d.supervisor?.position],
      ['Convenio', d.agreement?.agreementNumber], ['Inicio', date(d.startDate)], ['Término', date(d.endDate)],
      ['Horas completadas / requeridas', `${'accumulatedHours' in d ? d.accumulatedHours : d.completedHours} / ${d.requiredHours}`],
      ['Avance documental', `${d.documentProgressPercentage}%`], ['Folio de presentación', d.presentationLetterFolio],
      ['Fecha de presentación', date(d.presentationLetterDate)], ['Fecha de aceptación', date(d.acceptanceLetterDate)],
      ['Fecha de terminación', date(d.terminationLetterDate)], ['Carta de terminación original', d.originalTerminationLetterReceived ? 'Recibida' : 'Pendiente'],
      ['Constancia de liberación', d.releaseCertificateFolio], ['Fecha de liberación', date(d.releasedAt)],
      ['Acta Escolares', d.schoolServicesActFolio], ['Envío a Escolares', d.schoolServicesActSent ? date(d.schoolServicesActSentAt) : 'Pendiente'],
      ...('program' in d ? [
        ['Programa', d.program?.name], ['Modalidad', d.modality === 'internal' ? 'Interna' : 'Externa'],
        ['Comunidad', d.targetCommunity], ['Beneficiarios', d.beneficiariesCount],
        ['Actividades de inclusión', d.hasInclusionActivities ? 'Sí' : 'No'], ['Calificación', d.finalGradeLevel],
      ] : [['Horario', d.scheduleDescription], ['Créditos otorgados', d.creditsAwarded]]),
      ['Observaciones', d.observations], ['Motivo de cancelación', d.cancellationReason],
    ] },
  ]
})

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  student.value = null
  detail.value = null
  documents.value = []
  try {
    const process = props.tramiteType === 'social_service'
      ? await getSocialServiceById(props.tramiteId)
      : await getInternshipById(props.tramiteId)
    if (!active) return
    const [profile, processDocs, generalDocs] = await Promise.all([
      getStudentById(process.student.id),
      getStudentDocuments(process.student.id, { tramiteType: props.tramiteType, tramiteId: props.tramiteId }),
      getStudentDocuments(process.student.id, { tramiteType: 'general' }),
    ])
    if (!active) return
    student.value = profile
    detail.value = process
    documents.value = [...(props.tramiteType === 'social_service' ? [...processDocs].sort((a,b) => socialDocumentOrder(a.documentCode) - socialDocumentOrder(b.documentCode)) : processDocs), ...generalDocs]
  } catch (err) {
    if (active) error.value = problemMessage(err, 'No se pudo cargar el expediente. Vuelve a intentar.')
  } finally {
    if (active) loading.value = false
  }
}

async function viewDocument(document: ExpedientDocument): Promise<void> {
  pdfError.value = ''
  pdfLoading.value = document.id
  try {
    const blob = await getDocumentBlob(document.id)
    if (!active) return
    pdfBlob.value = blob
    pdfTitle.value = `${document.tramiteType === 'social_service' ? socialDocumentLabel(document.documentCode) : document.documentCode} · ${document.originalFileName}`
    pdfOpen.value = true
  } catch (err) {
    if (active) pdfError.value = problemMessage(err, 'No se pudo abrir el PDF. Vuelve a intentar.')
  } finally {
    if (active) pdfLoading.value = null
  }
}

onMounted(load)
onBeforeUnmount(() => { active = false })
</script>

<template>
  <p v-if="loading" role="status">Cargando expediente…</p>
  <UiAlert v-else-if="error" tone="danger">
    <p>{{ error }}</p>
    <UiButton variant="secondary" @click="load">Reintentar</UiButton>
  </UiAlert>
  <div v-else-if="student && detail" class="expedient-content">
    <section v-for="section in sections" :key="section.title">
      <h3>{{ section.title }}</h3>
      <dl class="expedient-fields">
        <div v-for="[label, value] in section.fields" :key="String(label)">
          <dt>{{ label }}</dt>
          <dd>{{ value === null || value === undefined || value === '' ? 'Sin registro' : value }}</dd>
        </div>
      </dl>
    </section>
    <section>
      <h3>Documentación</h3>
      <UiAlert v-if="pdfError" tone="danger">{{ pdfError }}</UiAlert>
      <p v-if="!documents.length" role="status">No hay documentos registrados en este expediente.</p>
      <ul v-else class="expedient-documents">
        <li v-for="document in documents" :key="document.id">
          <div>
            <strong>{{ document.tramiteType === 'social_service' ? socialDocumentLabel(document.documentCode) : detail.documentRequirements?.find(item => item.code === document.documentCode)?.title || document.documentCode }}</strong>
            <p v-if="document.tramiteType === 'social_service'">{{ socialDocumentPeriod(document.documentCode) }}</p>
            <p>{{ document.originalFileName }}</p>
            <UiBadge :tone="document.status === 'approved' ? 'success' : document.status === 'rejected' ? 'danger' : 'info'">
              {{ statusLabels[document.status] || document.status }}
            </UiBadge>
            <p>Subido: {{ date(document.createdAt) }}<span v-if="document.reviewedAt"> · Revisado: {{ date(document.reviewedAt) }}</span></p>
            <p v-if="document.reviewedByName">Revisó: {{ document.reviewedByName }}</p>
            <p v-if="document.rejectionReason">{{ document.rejectionReason }}</p>
          </div>
          <UiButton variant="secondary" :disabled="pdfLoading !== null" :aria-label="`Ver PDF: ${document.originalFileName}`" @click="viewDocument(document)">
            {{ pdfLoading === document.id ? 'Cargando PDF…' : 'Ver PDF' }}
          </UiButton>
        </li>
      </ul>
    </section>
  </div>
  <DocumentViewerModal v-model="pdfOpen" :title="pdfTitle" :blob="pdfBlob" />
</template>

<style scoped>
.expedient-content { display: grid; gap: 1.5rem; }
h3 { margin: 0 0 1rem; font-size: 1rem; }
.expedient-fields { display: grid; grid-template-columns: repeat(auto-fit, minmax(min(100%, 220px), 1fr)); gap: 1rem; margin: 0; }
dt { color: var(--color-text-muted); font-size: 0.85rem; }
dd { margin: 0.25rem 0 0; overflow-wrap: anywhere; }
.expedient-documents { list-style: none; margin: 0; padding: 0; }
.expedient-documents li { display: flex; align-items: center; justify-content: space-between; flex-wrap: wrap; gap: 1rem; padding: 1rem 0; border-bottom: 1px solid var(--color-border); }
.expedient-documents li > div { min-width: 0; flex: 1 1 220px; overflow-wrap: anywhere; }
.expedient-documents p { margin: 0.4rem 0; }
</style>
