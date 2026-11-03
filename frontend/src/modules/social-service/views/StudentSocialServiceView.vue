<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
} from '../../../shared/components'
import { openPrintableDocument } from '../../../shared/utils/printDocument'
import {
  getCompanyOptions,
  getPeriodOptions,
  getProgramOptions,
  getSupervisorOptions,
} from '../../catalogs/api/careersApi'
import type { OptionItem } from '../../catalogs/types/careers'
import { getDocumentBlob, getMyDocuments, uploadMyDocument } from '../../expedients/api/expedientsApi'
import type { ExpedientDocument } from '../../expedients/types/expedients'
import { getMyStudentProfile, uploadMyImssFile } from '../../students/api/studentsApi'
import type { StudentDetail } from '../../students/types/students'
import {
  getMySocialService,
  getSocialServicePrintData,
  requestSocialService,
  submitBimonthlyReport,
  upsertActivityAssessment,
  upsertCommitment,
  upsertSocialServiceEvaluation,
} from '../api/socialServiceApi'
import type { CreateSocialServicePayload, SocialServiceDetail } from '../types/socialService'

const service = ref<SocialServiceDetail | null>(null)
const studentProfile = ref<StudentDetail | null>(null)
const uploadedDocs = ref<ExpedientDocument[]>([])

const isLoading = ref(true)
const isSubmitting = ref(false)
const errorMessage = ref('')
const successMessage = ref('')

// Catálogos para el Asistente de Apertura Anexo XVIII
const periodOptions = ref<OptionItem[]>([])
const programOptions = ref<OptionItem[]>([])
const companyOptions = ref<OptionItem[]>([])
const supervisorOptions = ref<OptionItem[]>([])
const imssFileForApplication = ref<File | null>(null)

const openForm = reactive<CreateSocialServicePayload>({
  periodId: 0,
  programId: 0,
  companyId: 0,
  supervisorId: 0,
  modality: 'internal',
  startDate: '2026-02-16',
  endDate: '2026-08-16',
  targetCommunity: 'Comunidad Estudiantil y Regional',
  application: {
    studentAddress: '',
    studentPhone: '',
    periodType: 'february_july',
    dependencyName: '',
    dependencyHeadName: '',
    dependencyHeadPosition: '',
    activitiesOutline: '',
  },
})

// Modal Carta Compromiso (Anexo XX)
const isCommitmentModalOpen = ref(false)
const commitmentForm = reactive({
  studentAddress: '',
  studentPhone: '',
  dependencyAddress: 'Av. Tecnológico No. 4801, Monclova, Coahuila',
  commitmentDate: new Date().toISOString().slice(0, 10),
  city: 'Monclova',
})

// Modal Reporte Bimestral (Anexo XXII)
const isReportModalOpen = ref(false)
const activeReportNumber = ref<number>(1)
const reportForm = reactive({
  periodStartDate: '2026-02-16',
  periodEndDate: '2026-04-16',
  activitiesSummary: '',
  reportedHours: 160,
})

// Modal Evaluaciones Cualitativas (Anexos XXIII, XXIV, XXV)
const isEvalModalOpen = ref(false)
const evalForm = reactive({
  bimonthlyPeriod: '1' as '1' | '2' | '3',
  defaultGrade: 'excellent',
  observations: 'Cumplió satisfactoriamente con las actividades asignadas en el bimestre.',
})

// Subida de Documentos PDF Firmados y Sellados
const uploadingCode = ref<string | null>(null)

const requiredDocumentSteps = computed(() => {
  const isInternal = service.value?.modality === 'internal'
  return [
    {
      code: 'SOL',
      title: '1. Solicitud de Servicio Social (Anexo XVIII)',
      hint: 'Generada al solicitar apertura. Imprímela, fírmala y súbela en PDF.',
      printKey: 'application',
    },
    {
      code: 'CP',
      title: '2. Carta de Presentación Emitida (CP)',
      hint: 'Se genera automáticamente cuando Vinculación aprueba tu apertura. Descárgala y entrégala en la dependencia.',
      printKey: 'presentation_letter',
    },
    {
      code: 'CA',
      title: isInternal
        ? '3. Carta de Aceptación Local TecNM (CA — Generación Automática)'
        : '3. Carta de Aceptación de la Dependencia (CA)',
      hint: isInternal
        ? 'Al ser servicio local en el TecNM, descárgala prellenada aquí, recaba firma/sello del jefe de depto. y súbela.'
        : 'Emitida por la dependencia receptora. Súbela firmada y sellada.',
      printKey: isInternal ? 'acceptance_letter' : null,
    },
    {
      code: 'CC',
      title: '4. Carta Compromiso de Servicio Social (Anexo XX)',
      hint: 'Llena tus datos, imprímela, fírmala y sube el PDF escaneado.',
      printKey: 'commitment',
      openModal: 'commitment',
    },
    {
      code: 'R1',
      title: '5. 1er Reporte Bimestral (Anexo XXII)',
      hint: 'Llena tus actividades del 1er bimestre para imprimir el formato lleno, recaba firma y sello y súbelo.',
      printKey: 'report_1',
      openModal: 'report_1',
    },
    {
      code: 'EV1',
      title: '6. Evaluación Cualitativa y Autoevaluación 1er Bimestre (Anexos XXIII y XXIV)',
      hint: 'Genera el formato prellenado, pide firma y sello y sube el PDF escaneado.',
      printKey: 'evaluation_1',
      openModal: 'eval_1',
    },
    {
      code: 'R2',
      title: '7. 2do Reporte Bimestral (Anexo XXII)',
      hint: 'Llena tus actividades del 2do bimestre, imprímelo para firma/sello y sube el PDF.',
      printKey: 'report_2',
      openModal: 'report_2',
    },
    {
      code: 'EV2',
      title: '8. Evaluación Cualitativa y Autoevaluación 2do Bimestre (Anexos XXIII y XXIV)',
      hint: 'Genera el formato prellenado, pide firma y sello y sube el PDF escaneado.',
      printKey: 'evaluation_2',
      openModal: 'eval_2',
    },
    {
      code: 'R3',
      title: '9. 3er Reporte Bimestral (Anexo XXII)',
      hint: 'Llena tus actividades del 3er bimestre, imprímelo para firma/sello y sube el PDF.',
      printKey: 'report_3',
      openModal: 'report_3',
    },
    {
      code: 'EV3',
      title: '10. Evaluación Final y de Actividades (Anexos XXIII, XXIV y XXV)',
      hint: 'Genera los formatos finales prellenados, recaba firma y sello y sube el PDF.',
      printKey: 'evaluation_3',
      openModal: 'eval_3',
    },
    {
      code: 'CT',
      title: '11. Carta de Terminación de la Dependencia (Anexo XIX / CT)',
      hint: 'Sube tu Carta de Terminación firmada y sellada por la dependencia receptora.',
      printKey: null,
    },
  ]
})

function getDocByCode(code: string): ExpedientDocument | undefined {
  return uploadedDocs.value.find((d) => d.documentCode.toUpperCase() === code.toUpperCase())
}

async function loadCatalogsForApplication(): Promise<void> {
  const [periods, programs] = await Promise.all([
    getPeriodOptions().catch(() => []),
    getProgramOptions().catch(() => []),
  ])
  periodOptions.value = periods
  programOptions.value = programs
  if (periods.length > 0) openForm.periodId = periods[0].id
  if (programs.length > 0) openForm.programId = programs[0].id
  await handleModalityChange()
}

async function handleModalityChange(): Promise<void> {
  const isInternal = openForm.modality === 'internal'
  const companies = await getCompanyOptions(isInternal).catch(() => [])
  companyOptions.value = companies
  if (companies.length > 0) {
    openForm.companyId = companies[0].id
    openForm.application.dependencyName = companies[0].label
    await handleCompanyChange()
  } else {
    openForm.companyId = 0
    supervisorOptions.value = []
  }
}

async function handleCompanyChange(): Promise<void> {
  if (!openForm.companyId) return
  const comp = companyOptions.value.find((c) => c.id === openForm.companyId)
  if (comp) openForm.application.dependencyName = comp.label

  const sups = await getSupervisorOptions(openForm.companyId).catch(() => [])
  supervisorOptions.value = sups
  if (sups.length > 0) {
    openForm.supervisorId = sups[0].id
    handleSupervisorChange()
  }
}

function handleSupervisorChange(): void {
  const sup = supervisorOptions.value.find((s) => s.id === openForm.supervisorId)
  if (sup) {
    const parts = sup.label.split('—').map((p) => p.trim())
    openForm.application.dependencyHeadName = parts[0] || sup.label
    openForm.application.dependencyHeadPosition = parts[1] || 'Responsable de Área'
  }
}

async function loadData(): Promise<void> {
  isLoading.value = true
  errorMessage.value = ''
  try {
    const [profile, ss, docs] = await Promise.all([
      getMyStudentProfile().catch(() => null),
      getMySocialService().catch(() => null),
      getMyDocuments({ tramiteType: 'social_service' }).catch(() => []),
    ])
    studentProfile.value = profile
    service.value = ss
    uploadedDocs.value = docs

    if (profile) {
      openForm.application.studentPhone = profile.phoneNumber || ''
    }

    if (!ss) {
      await loadCatalogsForApplication()
    } else {
      commitmentForm.studentAddress =
        ss.commitment?.studentAddress || ss.application?.studentAddress || ''
      commitmentForm.studentPhone =
        ss.commitment?.studentPhone || ss.application?.studentPhone || profile?.phoneNumber || ''
      commitmentForm.commitmentDate = ss.commitment?.commitmentDate || ss.startDate
    }
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Error al cargar tu Servicio Social.'
  } finally {
    isLoading.value = false
  }
}

function handleImssFileSelect(event: Event): void {
  const input = event.target as HTMLInputElement
  imssFileForApplication.value = input.files?.[0] ?? null
}

async function handleSubmitApplication(): Promise<void> {
  isSubmitting.value = true
  errorMessage.value = ''
  successMessage.value = ''
  try {
    if (imssFileForApplication.value) {
      await uploadMyImssFile(imssFileForApplication.value)
    }
    const created = await requestSocialService(openForm)
    service.value = created
    successMessage.value =
      'Solicitud de Servicio Social (Anexo XVIII) generada y enviada a Vinculación exitosamente.'
    await handlePrintFormat('application')
    await loadData()
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'Verifica todos los campos de la solicitud y tu vigencia IMSS.'
  } finally {
    isSubmitting.value = false
  }
}

async function handlePrintFormat(documentKey: string): Promise<void> {
  if (!service.value) return
  errorMessage.value = ''
  try {
    const printData = await getSocialServicePrintData(service.value.id, documentKey)
    openPrintableDocument(printData)
  } catch (err: any) {
    errorMessage.value =
      err?.response?.data?.message || 'El formato aún no está disponible para impresión.'
  }
}

function handleOpenStepModal(modalKey: string): void {
  if (!service.value) return
  if (modalKey === 'commitment') {
    isCommitmentModalOpen.value = true
  } else if (modalKey.startsWith('report_')) {
    const num = Number(modalKey.split('_')[1] || 1)
    activeReportNumber.value = num
    const existing = service.value.reports?.find((r) => r.reportNumber === num)
    reportForm.periodStartDate = existing?.periodStartDate || service.value.startDate
    reportForm.periodEndDate = existing?.periodEndDate || service.value.endDate
    reportForm.activitiesSummary =
      existing?.activitiesSummary || service.value.application?.activitiesOutline || ''
    reportForm.reportedHours = existing?.reportedHours || 160
    isReportModalOpen.value = true
  } else if (modalKey.startsWith('eval_')) {
    const num = (modalKey.split('_')[1] || '1') as '1' | '2' | '3'
    evalForm.bimonthlyPeriod = num
    isEvalModalOpen.value = true
  }
}

async function handleSaveAndPrintCommitment(): Promise<void> {
  if (!service.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    service.value = await upsertCommitment(service.value.id, commitmentForm)
    isCommitmentModalOpen.value = false
    successMessage.value = 'Datos de Carta Compromiso (Anexo XX) guardados.'
    await handlePrintFormat('commitment')
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Error al guardar la Carta Compromiso.'
  } finally {
    isSubmitting.value = false
  }
}

async function handleSaveAndPrintReport(): Promise<void> {
  if (!service.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    await submitBimonthlyReport(service.value.id, activeReportNumber.value, {
      periodStartDate: reportForm.periodStartDate,
      periodEndDate: reportForm.periodEndDate,
      activitiesSummary: reportForm.activitiesSummary,
      reportedHours: reportForm.reportedHours,
      evaluatorSupervisorId: service.value.supervisor?.id ?? null,
    })
    isReportModalOpen.value = false
    successMessage.value = `Reporte Bimestral #${activeReportNumber.value} (Anexo XXII) guardado y listo para imprimir.`
    await handlePrintFormat(`report_${activeReportNumber.value}`)
    await loadData()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Error al guardar el Reporte Bimestral.'
  } finally {
    isSubmitting.value = false
  }
}

async function handleSaveAndPrintEvaluations(): Promise<void> {
  if (!service.value) return
  isSubmitting.value = true
  errorMessage.value = ''
  try {
    const g = evalForm.defaultGrade
    const criteria7 = [g, g, g, g, g, g, g]
    await upsertSocialServiceEvaluation(
      service.value.id,
      'external_supervisor',
      evalForm.bimonthlyPeriod,
      {
        criteria: criteria7,
        evaluatorSupervisorId: service.value.supervisor?.id ?? null,
        observations: evalForm.observations,
      },
    )
    await upsertSocialServiceEvaluation(service.value.id, 'student_self', evalForm.bimonthlyPeriod, {
      criteria: criteria7,
      observations: evalForm.observations,
    })
    if (evalForm.bimonthlyPeriod === '3') {
      await upsertActivityAssessment(service.value.id, {
        questions: [g, g, g, g, g, g, g, g],
        observations: evalForm.observations,
      })
    }
    isEvalModalOpen.value = false
    successMessage.value = `Formatos de Evaluación del Bimestre ${evalForm.bimonthlyPeriod} guardados.`
    await handlePrintFormat(`evaluation_${evalForm.bimonthlyPeriod}`)
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || 'Error al guardar las evaluaciones.'
  } finally {
    isSubmitting.value = false
  }
}

async function handleUploadStepPdf(code: string, event: Event): Promise<void> {
  if (!service.value) return
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file) return

  uploadingCode.value = code
  errorMessage.value = ''
  successMessage.value = ''
  try {
    await uploadMyDocument({
      file,
      tramiteType: 'social_service',
      tramiteId: service.value.id,
      documentCode: code,
    })
    successMessage.value = `Documento ${code} (${file.name}) subido exitosamente y enviado a revisión de Vinculación.`
    await loadData()
  } catch (err: any) {
    errorMessage.value = err?.response?.data?.message || `Error al subir el documento ${code}.`
  } finally {
    uploadingCode.value = null
    input.value = ''
  }
}

async function handleViewUploadedPdf(docId: number): Promise<void> {
  try {
    const blob = await getDocumentBlob(docId)
    const url = window.URL.createObjectURL(blob)
    window.open(url, '_blank')
  } catch {
    errorMessage.value = 'El archivo PDF aún no está disponible en almacenamiento.'
  }
}

onMounted(() => {
  void loadData()
})
</script>

<template>
  <UiBasePage :fluid="true">
    <div style="margin-bottom: 1.5rem;">
      <h1 style="font-size: 1.5rem; font-weight: 700; margin: 0;">Mi Servicio Social</h1>
      <p style="color: var(--color-text-muted, #6c757d); margin: 0.25rem 0 0;">
        Generación automática de formatos oficiales (Anexos XVIII–XXV), Carta de Aceptación Local y
        seguimiento por documentos firmados y sellados
      </p>
    </div>

    <UiAlert v-if="errorMessage" tone="danger" style="margin-bottom: 1rem;">
      {{ errorMessage }}
    </UiAlert>
    <UiAlert v-if="successMessage" tone="success" style="margin-bottom: 1rem;">
      {{ successMessage }}
    </UiAlert>

    <div v-if="isLoading" style="padding: 2rem; text-align: center;">Cargando trámite...</div>

    <!-- ==================================================================== -->
    <!-- CASO 1: ALUMNO SIN TRÁMITE -> ASISTENTE DE APERTURA (ANEXO XVIII)    -->
    <!-- ==================================================================== -->
    <div v-else-if="!service" style="display: flex; flex-direction: column; gap: 1.5rem;">
      <UiCard title="Solicitud Automatizada de Apertura de Servicio Social (Anexo XVIII + Vigencia IMSS)">
        <p style="margin: 0 0 1rem; color: #4b5563; font-size: 0.92rem;">
          Llena tus datos y adjunta tu <strong>Constancia de Vigencia de Derechos del IMSS</strong>.
          El sistema generará automáticamente tu formato oficial <strong>Anexo XVIII</strong> y
          enviará tu solicitud al departamento de Vinculación para emitir tu Carta de Presentación.
        </p>

        <div class="form-grid">
          <UiField label="Modalidad del Servicio Social *">
            <select v-model="openForm.modality" @change="handleModalityChange">
              <option value="internal">
                Local en el TecNM (Genera Carta de Aceptación automática)
              </option>
              <option value="external">Dependencia Externa (Gobierno / Asociación Civil)</option>
            </select>
          </UiField>

          <UiField label="Periodo Escolar *">
            <select v-model.number="openForm.periodId">
              <option v-for="p in periodOptions" :key="p.id" :value="p.id">{{ p.label }}</option>
            </select>
          </UiField>

          <UiField label="Programa de Servicio Social *">
            <select v-model.number="openForm.programId">
              <option v-for="prog in programOptions" :key="prog.id" :value="prog.id">
                {{ prog.label }}
              </option>
            </select>
          </UiField>

          <UiField label="Dependencia / Departamento Receptor *">
            <select v-model.number="openForm.companyId" @change="handleCompanyChange">
              <option v-for="c in companyOptions" :key="c.id" :value="c.id">{{ c.label }}</option>
            </select>
          </UiField>

          <UiField label="Titular / Responsable del Área *">
            <select v-model.number="openForm.supervisorId" @change="handleSupervisorChange">
              <option v-for="s in supervisorOptions" :key="s.id" :value="s.id">{{ s.label }}</option>
            </select>
          </UiField>

          <UiField label="Teléfono del Estudiante *">
            <input
              v-model="openForm.application.studentPhone"
              type="text"
              placeholder="Ej. 8661458920"
            />
          </UiField>

          <UiField label="Domicilio Completo del Estudiante *">
            <input
              v-model="openForm.application.studentAddress"
              type="text"
              placeholder="Calle, Número, Colonia y Ciudad"
            />
          </UiField>

          <UiField label="Fecha de Inicio *">
            <input v-model="openForm.startDate" type="date" />
          </UiField>

          <UiField label="Fecha de Término (Mín. 6 meses) *">
            <input v-model="openForm.endDate" type="date" />
          </UiField>
        </div>

        <div style="margin-top: 1rem;">
          <UiField label="Actividades a Desarrollar en el Programa (Anexo XVIII) *">
            <textarea
              v-model="openForm.application.activitiesOutline"
              rows="3"
              style="width: 100%; padding: 0.5rem; border: 1px solid #cbd5e1; border-radius: 6px;"
              placeholder="Describe brevemente las actividades que realizarás durante tu servicio social..."
            ></textarea>
          </UiField>
        </div>

        <div class="imss-upload-banner">
          <div>
            <strong>Constancia de Vigencia de Derechos del IMSS (PDF)</strong>
            <div style="font-size: 0.85rem; color: #4b5563;">
              Estado actual:
              <UiBadge :tone="studentProfile?.hasValidImss ? 'success' : 'warning'">
                {{
                  studentProfile?.hasValidImss
                    ? 'Ya cuentas con Vigencia IMSS registrada'
                    : 'Sube tu archivo PDF aquí'
                }}
              </UiBadge>
            </div>
          </div>
          <input type="file" accept="application/pdf" @change="handleImssFileSelect" />
        </div>

        <div style="margin-top: 1.25rem; display: flex; justify-content: flex-end;">
          <UiButton variant="primary" :disabled="isSubmitting" @click="handleSubmitApplication">
            {{
              isSubmitting
                ? 'Generando Solicitud...'
                : 'Enviar Solicitud a Vinculación y Generar Anexo XVIII'
            }}
          </UiButton>
        </div>
      </UiCard>
    </div>

    <!-- ==================================================================== -->
    <!-- CASO 2: ALUMNO CON TRÁMITE EN CURSO O LIBERADO                       -->
    <!-- ==================================================================== -->
    <div v-else style="display: flex; flex-direction: column; gap: 1.5rem;">
      <!-- Resumen y Botones Rápidos de Plantillas -->
      <UiCard title="Resumen de Mi Trámite y Descarga de Cartas Oficiales">
        <div class="summary-grid">
          <div>
            <span class="label">Dependencia Receptora</span>
            <strong>{{ service.company.name }}</strong>
          </div>
          <div>
            <span class="label">Modalidad</span>
            <UiBadge :tone="service.modality === 'internal' ? 'info' : 'purple'">
              {{
                service.modality === 'internal'
                  ? 'Local en el TecNM (Interno)'
                  : 'Dependencia Externa'
              }}
            </UiBadge>
          </div>
          <div>
            <span class="label">Folio Carta de Presentación (CP)</span>
            <strong>{{ service.presentationLetterFolio || 'Pendiente de dictamen' }}</strong>
          </div>
          <div>
            <span class="label">Avance por Documentos Validados</span>
            <strong style="font-size: 1.15rem; color: #0284c7;">
              {{ service.documentProgressPercentage }}%
            </strong>
          </div>
        </div>

        <div class="templates-toolbar">
          <UiButton variant="secondary" size="sm" @click="handlePrintFormat('application')">
            Imprimir Solicitud Anexo XVIII
          </UiButton>

          <UiButton
            v-if="service.presentationLetterFolio"
            variant="primary"
            size="sm"
            @click="handlePrintFormat('presentation_letter')"
          >
            Descargar Carta de Presentación ({{ service.presentationLetterFolio }})
          </UiButton>

          <UiButton
            v-if="service.modality === 'internal'"
            variant="primary"
            size="sm"
            @click="handlePrintFormat('acceptance_letter')"
          >
            Generar Carta de Aceptación Local TecNM (Plantilla Automática)
          </UiButton>

          <UiButton
            v-if="service.status === 'released'"
            variant="primary"
            size="sm"
            @click="handlePrintFormat('release_certificate')"
          >
            Descargar Constancia de Liberación ({{ service.releaseCertificateFolio }})
          </UiButton>
        </div>
      </UiCard>

      <!-- Paso a Paso de Documentos y Prellenado de Formatos -->
      <UiCard
        title="Flujo de Formatos Prellenados y Subida de Documentos Firmados y Sellados"
      >
        <p style="margin: 0 0 1rem; font-size: 0.9rem; color: #4b5563;">
          El avance se calcula automáticamente conforme subes tus documentos firmados y sellados y
          Vinculación los valida. Usa los botones <strong>"Llenar e Imprimir Formato"</strong> para
          que tus reportes y evaluaciones salgan ya con todos tus datos y solo recabes firma y sello.
        </p>

        <div class="steps-list">
          <div v-for="step in requiredDocumentSteps" :key="step.code" class="step-card">
            <div class="step-info">
              <div class="step-header">
                <strong>{{ step.title }}</strong>
                <UiBadge
                  v-if="getDocByCode(step.code)"
                  :tone="
                    getDocByCode(step.code)?.status === 'approved'
                      ? 'success'
                      : getDocByCode(step.code)?.status === 'rejected'
                        ? 'danger'
                        : 'warning'
                  "
                >
                  {{
                    getDocByCode(step.code)?.status === 'approved'
                      ? 'Validado (SI)'
                      : getDocByCode(step.code)?.status === 'rejected'
                        ? 'Observado / Rechazado'
                        : 'En Revisión por Vinculación'
                  }}
                </UiBadge>
                <UiBadge v-else tone="info">Pendiente de Subir</UiBadge>
              </div>
              <p class="step-hint">{{ step.hint }}</p>
              <div
                v-if="getDocByCode(step.code)?.rejectionReason"
                style="color: #dc2626; font-size: 0.84rem; margin-top: 0.25rem;"
              >
                <strong>Motivo de observación:</strong>
                {{ getDocByCode(step.code)?.rejectionReason }}
              </div>
            </div>

            <div class="step-actions">
              <!-- Botón para llenar formulario e imprimir formato prellenado -->
              <UiButton
                v-if="step.openModal"
                variant="secondary"
                size="sm"
                @click="handleOpenStepModal(step.openModal)"
              >
                Llenar e Imprimir Formato
              </UiButton>
              <UiButton
                v-else-if="step.printKey"
                variant="secondary"
                size="sm"
                @click="handlePrintFormat(step.printKey)"
              >
                Imprimir Plantilla Llena
              </UiButton>

              <!-- Botón para ver PDF ya subido -->
              <UiButton
                v-if="getDocByCode(step.code)"
                variant="secondary"
                size="sm"
                @click="handleViewUploadedPdf(getDocByCode(step.code)!.id)"
              >
                Ver PDF
              </UiButton>

              <!-- Subir archivo PDF firmado y sellado -->
              <label class="upload-label">
                <span>
                  {{
                    uploadingCode === step.code
                      ? 'Subiendo...'
                      : getDocByCode(step.code)
                        ? 'Reemplazar PDF'
                        : 'Subir PDF Firmado'
                  }}
                </span>
                <input
                  type="file"
                  accept="application/pdf"
                  style="display: none;"
                  :disabled="uploadingCode === step.code"
                  @change="handleUploadStepPdf(step.code, $event)"
                />
              </label>
            </div>
          </div>
        </div>
      </UiCard>
    </div>

    <!-- ==================================================================== -->
    <!-- MODAL 1: PRELLENAR CARTA COMPROMISO (ANEXO XX)                       -->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isCommitmentModalOpen"
      size="lg"
      title="Llenar e Imprimir Carta Compromiso (Anexo XX)"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <UiField label="Domicilio del Estudiante *">
          <input v-model="commitmentForm.studentAddress" type="text" />
        </UiField>
        <UiField label="Teléfono del Estudiante *">
          <input v-model="commitmentForm.studentPhone" type="text" />
        </UiField>
        <UiField label="Domicilio de la Dependencia *">
          <input v-model="commitmentForm.dependencyAddress" type="text" />
        </UiField>
        <UiField label="Fecha de Compromiso *">
          <input v-model="commitmentForm.commitmentDate" type="date" />
        </UiField>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem;">
          <UiButton variant="secondary" @click="isCommitmentModalOpen = false">Cancelar</UiButton>
          <UiButton
            variant="primary"
            :disabled="isSubmitting"
            @click="handleSaveAndPrintCommitment"
          >
            Guardar e Imprimir Anexo XX
          </UiButton>
        </div>
      </template>
    </UiDialog>

    <!-- ==================================================================== -->
    <!-- MODAL 2: PRELLENAR REPORTE BIMESTRAL (ANEXO XXII)                    -->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isReportModalOpen"
      size="lg"
      :title="`Llenar e Imprimir Reporte Bimestral #${activeReportNumber} (Anexo XXII)`"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 0.75rem;">
          <UiField label="Fecha Inicio del Bimestre *">
            <input v-model="reportForm.periodStartDate" type="date" />
          </UiField>
          <UiField label="Fecha Fin del Bimestre *">
            <input v-model="reportForm.periodEndDate" type="date" />
          </UiField>
        </div>
        <UiField label="Horas Reportadas en el Bimestre (Informativo para impresión) *">
          <input v-model.number="reportForm.reportedHours" type="number" min="1" max="480" />
        </UiField>
        <UiField label="Resumen de Actividades Realizadas *">
          <textarea
            v-model="reportForm.activitiesSummary"
            rows="4"
            style="width: 100%; padding: 0.5rem; border: 1px solid #cbd5e1; border-radius: 6px;"
          ></textarea>
        </UiField>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem;">
          <UiButton variant="secondary" @click="isReportModalOpen = false">Cancelar</UiButton>
          <UiButton variant="primary" :disabled="isSubmitting" @click="handleSaveAndPrintReport">
            Guardar e Imprimir Formato Lleno
          </UiButton>
        </div>
      </template>
    </UiDialog>

    <!-- ==================================================================== -->
    <!-- MODAL 3: PRELLENAR EVALUACIONES CUALITATIVAS (ANEXOS XXIII, XXIV, XXV)-->
    <!-- ==================================================================== -->
    <UiDialog
      v-model="isEvalModalOpen"
      size="lg"
      :title="`Generar Formatos de Evaluación Cualitativa · Bimestre ${evalForm.bimonthlyPeriod}`"
    >
      <div style="display: flex; flex-direction: column; gap: 0.85rem;">
        <p style="margin: 0; font-size: 0.88rem; color: #4b5563;">
          Se generarán los formatos oficiales prellenados (Anexo XXIII Evaluación de la Dependencia,
          Anexo XXIV Autoevaluación del Prestador y Anexo XXV al cierre) listos para imprimir y
          recabar firma y sello.
        </p>
        <UiField label="Nivel Cualitativo Predeterminado para Impresión">
          <select v-model="evalForm.defaultGrade">
            <option value="excellent">Excelente</option>
            <option value="notable">Notable</option>
            <option value="good">Bueno</option>
            <option value="sufficient">Suficiente</option>
          </select>
        </UiField>
        <UiField label="Observaciones">
          <input v-model="evalForm.observations" type="text" />
        </UiField>
      </div>
      <template #footer>
        <div style="display: flex; justify-content: flex-end; gap: 0.5rem; flex-wrap: wrap;">
          <UiButton variant="secondary" @click="isEvalModalOpen = false">Cancelar</UiButton>
          <UiButton
            variant="secondary"
            @click="handlePrintFormat(`self_evaluation_${evalForm.bimonthlyPeriod}`)"
          >
            Imprimir Autoevaluación (Anexo XXIV)
          </UiButton>
          <UiButton
            v-if="evalForm.bimonthlyPeriod === '3'"
            variant="secondary"
            @click="handlePrintFormat('activity_assessment')"
          >
            Imprimir Eval. Actividades (Anexo XXV)
          </UiButton>
          <UiButton
            variant="primary"
            :disabled="isSubmitting"
            @click="handleSaveAndPrintEvaluations"
          >
            Guardar e Imprimir Evaluación (Anexo XXIII)
          </UiButton>
        </div>
      </template>
    </UiDialog>
  </UiBasePage>
</template>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
  gap: 1rem;
}

.imss-upload-banner {
  margin-top: 1.25rem;
  padding: 1rem;
  border-radius: 8px;
  background: #f8fafc;
  border: 1px dashed #0284c7;
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 1rem;
  align-items: center;
}

.label {
  display: block;
  font-size: 0.8rem;
  color: #6b7280;
  margin-bottom: 0.2rem;
}

.templates-toolbar {
  margin-top: 1.25rem;
  padding-top: 1rem;
  border-top: 1px solid #e5e7eb;
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
}

.steps-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.step-card {
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-wrap: wrap;
  gap: 1rem;
  padding: 0.9rem 1rem;
  border: 1px solid var(--color-border, #e5e7eb);
  border-radius: 8px;
}

.step-info {
  flex: 1;
  min-width: 260px;
}

.step-header {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.step-hint {
  margin: 0.25rem 0 0;
  font-size: 0.84rem;
  color: #6b7280;
}

.step-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.upload-label {
  display: inline-flex;
  align-items: center;
  padding: 0.4rem 0.8rem;
  background: #0284c7;
  color: #fff;
  border-radius: 6px;
  font-size: 0.82rem;
  font-weight: 600;
  cursor: pointer;
}

.upload-label:hover {
  background: #0369a1;
}
</style>
