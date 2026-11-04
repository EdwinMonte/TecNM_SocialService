<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import {
  UiAlert,
  UiBadge,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiToast,
} from '../../../shared/components'
import { getDocumentBlob, getReviewQueuePage, reviewDocument } from '../api/expedientsApi'
import type { ExpedientDocumentStatus, ReviewQueueItem } from '../types/expedients'

// Catálogo descriptivo de códigos normativos de documentos
const documentCatalog: Record<string, { title: string; anexo: string }> = {
  SOL: { title: 'Solicitud de Apertura', anexo: 'Anexo XVIII' },
  CP: { title: 'Carta de Presentación', anexo: 'Oficio Vinculación' },
  CA: { title: 'Carta de Aceptación Oficial', anexo: 'Membrete Empresa' },
  R1: { title: '1er Reporte Bimestral', anexo: 'Anexo XXII · Bim. 1' },
  R2: { title: '2do Reporte Bimestral', anexo: 'Anexo XXII · Bim. 2' },
  R3: { title: '3er Reporte Bimestral', anexo: 'Anexo XXII · Bim. 3' },
  EV: { title: 'Evaluación Final de Desempeño', anexo: 'Anexo XVII' },
  IMSS: { title: 'Vigencia de Derechos IMSS', anexo: 'Requisito de admisión' },
  CC: { title: 'Carta Compromiso', anexo: 'Anexo XX' },
  CT: { title: 'Carta de Terminación y Liberación', anexo: 'Constancia 480 h' },
}

const queue = ref<ReviewQueueItem[]>([])
const loadError = ref(false)
const isLoading = ref(true)
const toast = ref<InstanceType<typeof UiToast> | null>(null)

// Filtros reactivos
const searchQuery = ref('')
const modalityFilter = ref<'all' | 'social_service' | 'internship' | 'general'>('all')
const statusFilter = ref<'all' | ExpedientDocumentStatus>('all')
const docCodeFilter = ref<string | null>(null)
const page = ref(1), totalPages = ref(0), totalItems = ref(0)
const periodId = ref<number | undefined>()
let requestSequence = 0
let filterTimer: ReturnType<typeof setTimeout> | undefined
watch([searchQuery, modalityFilter, statusFilter, docCodeFilter, periodId], () => {
  clearTimeout(filterTimer)
  page.value = 1
  filterTimer = setTimeout(() => { void loadQueue() }, 250)
})
onBeforeUnmount(() => { clearTimeout(filterTimer); requestSequence++ })

// Contadores KPI animados
const animatedCounts = reactive({
  pending: 0,
  socialService: 0,
  internship: 0,
  approved: 0,
})

function updateAnimatedKpis(): void {
  const source = queue.value
  const targets = {
    pending: source.filter((d) => d.status === 'pending').length,
    socialService: source.filter((d) => d.tramiteType === 'social_service').length,
    internship: source.filter((d) => d.tramiteType === 'internship').length,
    approved: source.filter((d) => d.status === 'approved').length,
  }

  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (prefersReduced) {
    Object.assign(animatedCounts, targets)
    return
  }

  const duration = 640
  const start = performance.now()

  function tick(now: number): void {
    const elapsed = now - start
    const progress = Math.min(elapsed / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)

    animatedCounts.pending = Math.round(targets.pending * ease)
    animatedCounts.socialService = Math.round(targets.socialService * ease)
    animatedCounts.internship = Math.round(targets.internship * ease)
    animatedCounts.approved = Math.round(targets.approved * ease)

    if (progress < 1) {
      requestAnimationFrame(tick)
    }
  }

  requestAnimationFrame(tick)
}

async function loadQueue(): Promise<void> {
  const sequence = ++requestSequence
  isLoading.value = true
  try {
    const data = await getReviewQueuePage({ page: page.value, pageSize: 20, status: statusFilter.value,
      tramiteType: modalityFilter.value === 'all' ? undefined : modalityFilter.value,
      documentCode: docCodeFilter.value || undefined, search: searchQuery.value || undefined, periodId: periodId.value })
    if (sequence !== requestSequence) return
    totalPages.value = data.totalPages
    totalItems.value = data.totalItems
    loadError.value = false
    queue.value = data.items
  } catch {
    if (sequence !== requestSequence) return
    totalItems.value = 0; totalPages.value = 0
    loadError.value = true
    queue.value = []
    toast.value?.show('No fue posible cargar la bandeja. Actualiza para intentar nuevamente.')
  } finally {
    if (sequence === requestSequence) isLoading.value = false
    updateAnimatedKpis()
  }
}

const filteredQueue = computed(() => queue.value)

function formatFileSize(bytes: number): string {
  const kb = Math.round(bytes / 1024)
  return kb >= 1024 ? `${(kb / 1024).toFixed(1)} MB` : `${kb} KB`
}

function getDocMeta(code: string): { title: string; anexo: string } {
  return (
    documentCatalog[code] ?? {
      title: `Documento Institucional (${code})`,
      anexo: 'Formato SGC',
    }
  )
}

// Visor / Previsualización de PDF en Modal
const isPdfViewerOpen = ref(false)
const pdfPreviewItem = ref<ReviewQueueItem | null>(null)
const pdfBlobUrl = ref<string | null>(null)

function releasePdfUrl(): void {
  if (pdfBlobUrl.value) window.URL.revokeObjectURL(pdfBlobUrl.value)
  pdfBlobUrl.value = null
}
watch(isPdfViewerOpen, open => { if (!open) releasePdfUrl() })
onBeforeUnmount(releasePdfUrl)

async function handleViewPdf(doc: ReviewQueueItem): Promise<void> {
  pdfPreviewItem.value = doc
  if (pdfBlobUrl.value) {
    window.URL.revokeObjectURL(pdfBlobUrl.value)
    pdfBlobUrl.value = null
  }

  try {
    const blob = await getDocumentBlob(doc.id)
    pdfBlobUrl.value = window.URL.createObjectURL(blob)
  } catch {
    pdfBlobUrl.value = null
  }

  isPdfViewerOpen.value = true
}

function proceedFromViewerToReview(): void {
  if (!pdfPreviewItem.value) return
  const target = pdfPreviewItem.value
  isPdfViewerOpen.value = false
  openReview(target)
}

// Modal de Dictamen Técnico
const selectedItem = ref<ReviewQueueItem | null>(null)
const isDialogOpen = ref(false)
const rejectionReason = ref('')
const imssReviewDate = ref('')
const reviewError = ref('')
const isSubmitting = ref(false)

const quickObservations = [
  'Falta sello oficial de la dependencia receptora.',
  'Rúbrica del asesor externo incompleta o ausente.',
  'Periodo de fechas no coincide con el trámite.',
  'Escaneo con baja resolución; texto no legible.',
]

function openReview(item: ReviewQueueItem): void {
  imssReviewDate.value = item.documentDate || ''
  selectedItem.value = item
  rejectionReason.value = item.rejectionReason ?? ''
  reviewError.value = ''
  isDialogOpen.value = true
}

function openNextPending(): void {
  const nextDoc = queue.value.find((d) => d.status === 'pending')
  if (nextDoc) {
    openReview(nextDoc)
  } else {
    toast.value?.show('No hay documentos pendientes de dictamen en la cola.')
  }
}

async function submitReview(status: 'approved' | 'rejected'): Promise<void> {
  if (!selectedItem.value) return
  reviewError.value = ''

  if (status === 'rejected' && !rejectionReason.value.trim()) {
    reviewError.value =
      'Para observar o rechazar un documento debes indicar el motivo técnico de corrección.'
    return
  }

  if (status === 'approved' && selectedItem.value.documentCode === 'IMSS' && !imssReviewDate.value) {
    reviewError.value = 'Confirma la fecha de expedición del PDF IMSS.'
    return
  }
  isSubmitting.value = true
  try {
    await reviewDocument(selectedItem.value.id, {
      status,
      rejectionReason: status === 'rejected' ? rejectionReason.value.trim() : undefined,
      documentDate: selectedItem.value.documentCode === 'IMSS' && status === 'approved' ? imssReviewDate.value : undefined,
    })
    await loadQueue()

    const actionWord = status === 'approved' ? 'aprobado' : 'observado para corrección'
    toast.value?.show(
      `Documento ${selectedItem.value.documentCode} de ${selectedItem.value.studentFullName} ${actionWord}.`,
    )
    isDialogOpen.value = false
  } catch {
    reviewError.value = 'No fue posible registrar el dictamen del documento.'
  } finally {
    isSubmitting.value = false
  }
}

// Códigos rápidos en panel lateral
const documentCodeChips = computed(() => ['SOL','CA','CC','R1','R2','R3','EV','EV_ACT','CT','IMSS'].map(code => ({
  code, label: documentCatalog[code]?.title || code, count: queue.value.filter(d => d.documentCode === code).length,
})))

onMounted(() => {
  void loadQueue()
})
</script>

<template>
  <UiBasePage fluid>
    <!-- Encabezado Ejecutivo Compacto -->
    <template #header>
      <header class="ctrl-header reveal-step reveal-1">
        <div class="ctrl-header__left">
          <div class="ctrl-eyebrow">
            <span class="ctrl-badge">BANDEJA UNIFICADA DE VALIDACIÓN DOCUMENTAL</span>
            <span class="ctrl-period">· Control SGC ISO 9001:2015</span>
            <span class="ctrl-status-pill">
              <span class="ctrl-live-dot" aria-hidden="true" />
              Plantel <code>05DIT0001V</code>
            </span>
          </div>
          <h1 class="ctrl-title">Revisión y Dictamen de Expedientes</h1>
        </div>

        <div class="ctrl-header__actions">
          <UiButton variant="secondary" class="ctrl-btn" @click="loadQueue">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="ctrl-btn__icon">
              <path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
              <path d="M3 3v5h5" />
              <path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16" />
              <path d="M16 16h5v5" />
            </svg>
            Actualizar Cola
          </UiButton>

          <UiButton variant="primary" class="ctrl-btn" @click="openNextPending">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="ctrl-btn__icon">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <polyline points="14 2 14 8 20 8" />
              <path d="m9 15 2 2 4-4" />
            </svg>
            Dictaminar Siguiente
          </UiButton>
        </div>
      </header>
    </template>

    <!-- 1. Fila de 4 Recuadros KPI Compactos -->
    <section class="compact-kpis reveal-step reveal-2" aria-label="Indicadores de revisión documental">
      <!-- KPI 1: Por Dictaminar -->
      <article
        class="kpi-tile kpi-tile--gold"
        :class="{ 'kpi-tile--selected': statusFilter === 'pending' }"
        @click="statusFilter = statusFilter === 'pending' ? 'all' : 'pending'"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--gold" aria-hidden="true" />
            En Espera de Dictamen
          </span>
          <span class="kpi-tag-gold">Prioritario</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value kpi-tile__value--gold">
            {{ animatedCounts.pending }}
          </strong>
          <span class="kpi-tile__unit">documentos por firmar</span>
        </div>

        <div class="kpi-tile__foot">
          <span>Tiempo meta: <strong>&lt; 24 h</strong></span>
          <span class="kpi-highlight--gold">Revisión SGC</span>
        </div>
      </article>

      <!-- KPI 2: Servicio Social -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': modalityFilter === 'social_service' }"
        @click="
          modalityFilter = modalityFilter === 'social_service' ? 'all' : 'social_service'
        "
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--navy" aria-hidden="true" />
            Servicio Social
          </span>
          <span class="kpi-meta kpi-meta--sky">Anexos XVIII–XXV</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedCounts.socialService }}</strong>
          <span class="kpi-tile__unit">entregas registradas</span>
        </div>

        <div class="kpi-tile__foot">
          <span>Reportes y Cartas 480h</span>
          <span>Filtro rápido</span>
        </div>
      </article>

      <!-- KPI 3: Prácticas Profesionales -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': modalityFilter === 'internship' }"
        @click="modalityFilter = modalityFilter === 'internship' ? 'all' : 'internship'"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--sky" aria-hidden="true" />
            Prácticas Prof.
          </span>
          <span class="kpi-meta kpi-meta--sky">Sector Industrial</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedCounts.internship }}</strong>
          <span class="kpi-tile__unit">entregas vinculadas</span>
        </div>

        <div class="kpi-tile__foot">
          <span>Planes y Evaluaciones</span>
          <span>Empresas convenio</span>
        </div>
      </article>

      <!-- KPI 4: Aprobados / Sellados -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': statusFilter === 'approved' }"
        @click="statusFilter = statusFilter === 'approved' ? 'all' : 'approved'"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--emerald" aria-hidden="true" />
            Expedientes Validados
          </span>
          <span class="kpi-meta kpi-meta--emerald">• Dictamen aprobado</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedCounts.approved }}</strong>
          <span class="kpi-tile__unit">aprobados en bandeja</span>
        </div>

        <div class="kpi-tile__foot">
          <span>Trazabilidad completa</span>
          <span class="kpi-highlight--emerald">ISO 9001:2015</span>
        </div>
      </article>
    </section>

    <!-- 2. Área Principal en 2 Columnas -->
    <div class="workspace-grid reveal-step reveal-3">
      <!-- Columna Izquierda: Tabla Unificada de Revisión -->
      <UiCard class="tray-card">
        <div class="tray-toolbar">
          <div class="search-box">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="search-box__icon">
              <circle cx="11" cy="11" r="8" />
              <line x1="21" y1="21" x2="16.65" y2="16.65" />
            </svg>
            <input
              v-model="searchQuery"
              type="search"
              class="search-box__input"
              placeholder="Buscar por no. control, alumno, carrera o código (CA, R1, CT)..."
              aria-label="Buscar documento en cola de revisión"
            />
            <button
              v-if="searchQuery"
              type="button"
              class="search-box__clear"
              aria-label="Limpiar búsqueda"
              @click="searchQuery = ''"
            >
              ✕
            </button>
          </div>

          <div class="toolbar-right">
            <div class="status-tabs" role="tablist" aria-label="Filtrar por estatus">
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'all' }"
                @click="statusFilter = 'all'"
              >
                Todos
              </button>
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'pending' }"
                @click="statusFilter = 'pending'"
              >
                Pendientes
              </button>
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'approved' }"
                @click="statusFilter = 'approved'"
              >
                Aprobados
              </button>
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'rejected' }"
                @click="statusFilter = 'rejected'"
              >
                Observados
              </button>
            </div>

            <button
              v-if="modalityFilter !== 'all' || docCodeFilter"
              type="button"
              class="clear-chip"
              @click="
                modalityFilter = 'all';
                docCodeFilter = null
              "
            >
              Limpiar filtro ✕
            </button>
          </div>
        </div>

        <div class="tray-table-wrap" role="region" aria-label="Cola de Documentos">
          <table class="tray-table">
            <thead>
              <tr>
                <th scope="col">ESTUDIANTE / CARRERA</th>
                <th scope="col">DOCUMENTO &amp; MODALIDAD</th>
                <th scope="col">ARCHIVO PDF</th>
                <th scope="col">ESTATUS SGC</th>
                <th scope="col" class="th-right">ACCIÓN</th>
              </tr>
            </thead>
            <TransitionGroup name="tray-row" tag="tbody">
              <tr v-for="row in filteredQueue" :key="row.id" class="tray-tr">
                <td>
                  <div class="cell-student">
                    <strong>{{ row.studentFullName }}</strong>
                    <span>{{ row.studentControlNumber }} · {{ row.careerCode }}</span>
                  </div>
                </td>

                <td>
                  <div class="cell-doc">
                    <div class="doc-head-line">
                      <span class="doc-code-badge">{{ row.documentCode }}</span>
                      <strong>{{ getDocMeta(row.documentCode).title }}</strong>
                    </div>
                    <span
                      :class="
                        row.tramiteType === 'social_service'
                          ? 'mod-sub--navy'
                          : 'mod-sub--gold'
                      "
                    >
                      {{
                        row.tramiteType === 'social_service'
                          ? 'Servicio Social (480h)'
                          : row.tramiteType === 'general' ? 'General / IMSS' : 'Prácticas Profesionales'
                      }}
                      · {{ getDocMeta(row.documentCode).anexo }}
                    </span>
                  </div>
                </td>

                <td>
                  <div class="cell-file">
                    <span class="file-name">{{ row.originalFileName }}</span>
                    <small class="file-meta">PDF · {{ formatFileSize(row.fileSizeBytes) }}</small>
                  </div>
                </td>

                <td>
                  <span
                    class="status-box"
                    :class="
                      row.status === 'approved'
                        ? 'status-box--success'
                        : row.status === 'rejected'
                          ? 'status-box--danger'
                          : 'status-box--warning'
                    "
                  >
                    <span class="status-box__dot" aria-hidden="true" />
                    {{
                      row.status === 'approved'
                        ? 'Aprobado SGC'
                        : row.status === 'rejected'
                          ? 'Observado'
                          : 'Por Dictaminar'
                    }}
                  </span>
                </td>

                <td class="td-right">
                  <div class="row-actions">
                    <button
                      type="button"
                      class="row-btn row-btn--outline"
                      @click="handleViewPdf(row)"
                    >
                      Ver PDF
                    </button>
                    <button
                      type="button"
                      class="row-btn"
                      :class="
                        row.status === 'pending'
                          ? 'row-btn--primary'
                          : 'row-btn--outline'
                      "
                      @click="openReview(row)"
                    >
                      Dictaminar
                    </button>
                  </div>
                </td>
              </tr>
            </TransitionGroup>
          </table>

          <div v-if="!isLoading && filteredQueue.length === 0" class="empty-queue">
            <p>No hay documentos que coincidan con los filtros seleccionados.</p>
            <UiButton
              variant="secondary"
              @click="
                searchQuery = '';
                modalityFilter = 'all';
                statusFilter = 'all';
                docCodeFilter = null
              "
            >
              Restablecer bandeja
            </UiButton>
          </div>
        </div>

        <footer class="tray-footer">
          <span>
            Mostrando <strong>{{ filteredQueue.length }}</strong> de {{ queue.length }} documentos en cola ·
            {{ loadError ? 'Error al cargar. Actualiza la bandeja para reintentar.' : isLoading ? 'Cargando repositorio documental…' : 'Conectado al repositorio documental' }}
          </span>
          <span class="footer-iso-tag">Verificación SGC TecNM</span>
        </footer>
      </UiCard>

      <!-- Columna Derecha: Filtro por Código de Anexo + Criterios de Validación -->
      <aside class="right-stack">
        <!-- Tarjeta Superior: Filtro por Código de Formato -->
        <UiCard class="side-card">
          <div class="side-card__head">
            <h2 class="side-card__title">FORMATOS OFICIALES</h2>
            <span class="side-card__meta">Filtrar por clave</span>
          </div>

          <div class="code-filter-list">
            <button
              v-for="item in documentCodeChips"
              :key="item.code"
              type="button"
              class="doc-chip-btn"
              :class="{ 'doc-chip-btn--active': docCodeFilter === item.code }"
              @click="docCodeFilter = docCodeFilter === item.code ? null : item.code"
            >
              <span class="doc-chip-btn__code">{{ item.code }}</span>
              <span class="doc-chip-btn__label">{{ item.label }}</span>
              <span class="doc-chip-btn__count">{{ item.count }}</span>
            </button>
          </div>
        </UiCard>

        <!-- Tarjeta Inferior: Reglas de Dictamen SGC -->
        <UiCard class="side-card">
          <div class="side-card__head">
            <div class="side-card__title-group">
              <svg viewBox="0 0 24 24" aria-hidden="true" class="side-card__icon">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                <path d="m9 12 2 2 4-4" />
              </svg>
              <h2 class="side-card__title">CRITERIOS DE DICTAMEN</h2>
            </div>
            <span class="official-tag">SGC</span>
          </div>

          <div class="criteria-list">
            <article class="criteria-box criteria-box--gold">
              <div class="criteria-box__top">
                <span class="criteria-tag criteria-tag--gold">SELLO Y FIRMA</span>
                <span class="criteria-scope">Obligatorio</span>
              </div>
              <h3 class="criteria-title">Rúbrica autógrafa y sello húmedo</h3>
              <p class="criteria-desc">
                Verificar que la Carta de Aceptación y Reportes Bimestrales cuenten con sello legible de la Unidad Receptora.
              </p>
            </article>

            <article class="criteria-box criteria-box--blue">
              <div class="criteria-box__top">
                <span class="criteria-tag criteria-tag--blue">INTEGRIDAD PDF</span>
                <span class="criteria-scope">≤ 5.0 MB</span>
              </div>
              <h3 class="criteria-title">Formato completo sin recortes</h3>
              <p class="criteria-desc">
                Confirmar que el número de control, periodo y horas acumuladas coincidan con el expediente.
              </p>
            </article>
          </div>
        </UiCard>
      </aside>
    </div>

    <!-- Modal 1: Previsualizador Institucional de PDF -->
    <UiDialog
      v-model="isPdfViewerOpen"
      :title="
        pdfPreviewItem
          ? `Inspección PDF · ${pdfPreviewItem.originalFileName}`
          : 'Visor de Documento'
      "
    >
      <div v-if="pdfPreviewItem" class="pdf-preview-sheet">
        <div class="pdf-preview-header">
          <span class="doc-code-badge">{{ pdfPreviewItem.documentCode }}</span>
          <span class="pdf-preview-student">
            {{ pdfPreviewItem.studentFullName }} ({{ pdfPreviewItem.studentControlNumber }})
          </span>
          <UiBadge
            :tone="
              pdfPreviewItem.status === 'approved'
                ? 'success'
                : pdfPreviewItem.status === 'rejected'
                  ? 'danger'
                  : 'warning'
            "
          >
            {{
              pdfPreviewItem.status === 'approved'
                ? 'Aprobado'
                : pdfPreviewItem.status === 'rejected'
                  ? 'Observado'
                  : 'En Revisión'
            }}
          </UiBadge>
        </div>

        <iframe
          v-if="pdfBlobUrl"
          :src="pdfBlobUrl"
          class="pdf-iframe"
          title="Vista previa del documento PDF"
        />

        <!-- Hoja de Inspección Técnica cuando no hay servidor de blobs activo -->
        <UiAlert v-else>No fue posible abrir el PDF. Cierra y vuelve a intentar.</UiAlert>
      </div>

      <template #actions>
        <UiButton variant="secondary" @click="isPdfViewerOpen = false">
          Cerrar Visor
        </UiButton>
        <UiButton variant="primary" @click="proceedFromViewerToReview">
          Ir a Dictaminar →
        </UiButton>
      </template>
    </UiDialog>

    <!-- Modal 2: Dictamen Técnico de Documento -->
    <UiDialog
      v-model="isDialogOpen"
      :title="
        selectedItem
          ? `Dictaminar ${selectedItem.documentCode} · ${selectedItem.studentFullName}`
          : 'Dictaminar Documento'
      "
    >
      <div v-if="selectedItem" class="review-modal-body">
        <UiAlert v-if="reviewError" tone="danger" title="Atención requerida">
          {{ reviewError }}
        </UiAlert>

        <div class="review-summary-box">
          <div class="review-summary-box__row">
            <span class="doc-code-badge">{{ selectedItem.documentCode }}</span>
            <strong>{{ getDocMeta(selectedItem.documentCode).title }}</strong>
          </div>
          <p class="review-summary-box__meta">
            Presentado por <strong>{{ selectedItem.studentFullName }}</strong> ({{
              selectedItem.studentControlNumber
            }}
            · {{ selectedItem.careerCode }}) · Archivo:
            <code>{{ selectedItem.originalFileName }}</code>
          </p>
        </div>

        <div class="preset-observations">
          <span class="preset-label">Motivos frecuentes de observación (clic para insertar):</span>
          <div class="preset-chips">
            <button
              v-for="obs in quickObservations"
              :key="obs"
              type="button"
              class="preset-chip"
              @click="rejectionReason = obs"
            >
              {{ obs }}
            </button>
          </div>
        </div>

        <UiField v-if="selectedItem?.documentCode === 'IMSS'" v-model="imssReviewDate" type="date" label="Fecha de expedición confirmada en el PDF" />
        <UiField
          v-model="rejectionReason"
          as="textarea"
          rows="3"
          label="Observaciones técnicas / Motivo de rechazo (obligatorio si se observa)"
          placeholder="Especifica qué sello, firma o dato debe corregir el estudiante antes de volver a subir su PDF..."
        />
      </div>

      <template #actions>
        <UiButton variant="secondary" :disabled="isSubmitting" @click="isDialogOpen = false">
          Cancelar
        </UiButton>
        <UiButton
          variant="danger"
          :disabled="isSubmitting"
          @click="submitReview('rejected')"
        >
          Observar / Rechazar
        </UiButton>
        <UiButton
          variant="primary"
          :loading="isSubmitting"
          @click="submitReview('approved')"
        >
          Aprobar documento
        </UiButton>
      </template>
    </UiDialog>

    <UiToast ref="toast" />
  <div style="display:flex;gap:1rem;align-items:center;flex-wrap:wrap;margin-top:1rem">
      <UiButton variant="secondary" :disabled="page <= 1 || isLoading" @click="page--; loadQueue()">Anterior</UiButton>
      <span>Página {{ page }} de {{ Math.max(1, totalPages) }} · {{ totalItems }} documentos. Contadores de esta página.</span>
      <UiButton variant="secondary" :disabled="page >= totalPages || isLoading" @click="page++; loadQueue()">Siguiente</UiButton>
      <label for="review-tramite-type">Tipo</label>
      <select id="review-tramite-type" v-model="modalityFilter"><option value="all">Todos</option><option value="general">General / IMSS</option><option value="social_service">Servicio Social</option><option value="internship">Prácticas Profesionales</option></select>
      <label>Id de periodo <input v-model.number="periodId" type="number" min="1" /></label>
      <UiButton variant="secondary" :disabled="isLoading" @click="loadQueue">Actualizar bandeja</UiButton>
    </div>
  </UiBasePage>
</template>

<style scoped>
/* ==========================================================================
   Animaciones Escalonadas y Transición de Filas
   ========================================================================== */
@keyframes slideInPanel {
  from {
    opacity: 0;
    transform: translateY(8px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}

.reveal-step {
  animation: slideInPanel 380ms cubic-bezier(0.22, 1, 0.36, 1) both;
}

.reveal-1 {
  animation-delay: 15ms;
}
.reveal-2 {
  animation-delay: 55ms;
}
.reveal-3 {
  animation-delay: 95ms;
}

.tray-row-enter-active,
.tray-row-leave-active {
  transition:
    opacity 200ms ease,
    transform 200ms ease;
}

.tray-row-enter-from,
.tray-row-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

/* ==========================================================================
   Encabezado Compacto Institucional
   ========================================================================== */
.ctrl-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 14px;
  flex-wrap: wrap;
}

.ctrl-eyebrow {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 6px;
  flex-wrap: wrap;
}

.ctrl-badge {
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 45%, transparent);
  padding: 2px 8px;
  border-radius: 4px;
}

.ctrl-period {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.ctrl-status-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 11px;
  color: var(--color-text-secondary);
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  padding: 2px 8px;
  border-radius: 99px;
}

.ctrl-status-pill code {
  color: var(--color-accent);
  font-weight: 700;
}

.ctrl-live-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-success);
}

.ctrl-title {
  font-size: clamp(20px, 2vw, 25px);
  font-weight: 700;
  letter-spacing: -0.02em;
  color: var(--color-text-primary);
  line-height: 1.15;
}

.ctrl-header__actions {
  display: flex;
  align-items: center;
  gap: 10px;
}

.ctrl-btn {
  min-height: 36px !important;
  padding: 6px 14px !important;
  font-size: 12.5px !important;
  display: inline-flex;
  align-items: center;
  gap: 7px;
}

.ctrl-btn__icon {
  width: 14px;
  height: 14px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.9;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* ==========================================================================
   1. Fila de 4 KPIs Compactos
   ========================================================================== */
.compact-kpis {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 14px;
  margin-bottom: 14px;
}

.kpi-tile {
  padding: 13px 16px;
  border-radius: 10px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  box-shadow: var(--card-shadow);
  cursor: pointer;
  transition:
    transform 180ms cubic-bezier(0.22, 1, 0.36, 1),
    border-color 180ms ease,
    box-shadow 180ms ease;
}

.kpi-tile:hover {
  transform: translateY(-2px);
  border-color: var(--color-primary);
  box-shadow: var(--floating-shadow);
}

.kpi-tile--selected {
  border-color: var(--color-primary);
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-primary) 22%, transparent);
}

.kpi-tile--gold:hover {
  border-color: var(--color-accent);
}

.kpi-tile__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}

.kpi-tile__label {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.kpi-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
}

.kpi-dot--navy {
  background: var(--color-primary);
}
.kpi-dot--sky {
  background: var(--color-info);
}
.kpi-dot--gold {
  background: var(--color-accent);
}
.kpi-dot--emerald {
  background: var(--color-success);
}

.kpi-meta {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  font-weight: 600;
}

.kpi-meta--emerald {
  color: var(--color-success);
}
.kpi-meta--sky {
  color: var(--color-info);
}

.kpi-tag-gold {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 1px 6px;
  border-radius: 4px;
}

.kpi-tile__main {
  display: flex;
  align-items: baseline;
  gap: 7px;
  margin: 8px 0;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--color-border-light);
}

.kpi-tile__value {
  font-size: 27px;
  font-weight: 700;
  letter-spacing: -0.03em;
  line-height: 1;
  color: var(--color-text-primary);
  font-variant-numeric: tabular-nums;
}

.kpi-tile__value--gold {
  color: var(--color-accent);
}

.kpi-tile__unit {
  font-size: 12px;
  color: var(--color-text-secondary);
}

.kpi-tile__foot {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.kpi-tile__foot strong {
  color: var(--color-text-primary);
}

.kpi-highlight--gold {
  color: var(--color-accent);
  font-weight: 600;
}

.kpi-highlight--emerald {
  color: var(--color-success);
  font-weight: 600;
}

/* ==========================================================================
   2. Área Principal en 2 Columnas
   ========================================================================== */
.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.95fr) minmax(0, 1fr);
  gap: 14px;
  align-items: stretch;
}

.tray-card {
  padding: 0;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  overflow: hidden;
}

.tray-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 12px 18px;
  border-bottom: 1px solid var(--color-border);
  flex-wrap: wrap;
}

.search-box {
  position: relative;
  display: flex;
  align-items: center;
  flex: 1;
  min-width: 240px;
}

.search-box__icon {
  position: absolute;
  left: 11px;
  width: 15px;
  height: 15px;
  stroke: var(--color-text-muted);
  fill: none;
  stroke-width: 2;
  pointer-events: none;
}

.search-box__input {
  width: 100%;
  min-height: 36px !important;
  padding: 6px 32px 6px 34px !important;
  font-size: 12.5px !important;
  border-radius: 8px !important;
  background: var(--color-bg-light) !important;
  border: 1px solid var(--color-border-medium) !important;
}

.search-box__clear {
  position: absolute;
  right: 8px;
  background: transparent;
  border: 0;
  color: var(--color-text-muted);
  font-size: 12px;
  padding: 4px;
}

.toolbar-right {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.status-tabs {
  display: inline-flex;
  padding: 3px;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
}

.status-tab {
  border: 0;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 11.5px;
  font-weight: 600;
  padding: 4px 10px;
  border-radius: 6px;
  transition:
    background 150ms ease,
    color 150ms ease;
}

.status-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
}

.clear-chip {
  font-size: 11px;
  font-weight: 600;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 3px 8px;
  border-radius: 5px;
}

/* Tabla de Documentos */
.tray-table-wrap {
  margin: 0;
  overflow-x: auto;
  flex: 1;
}

.tray-table {
  width: 100%;
  border-collapse: collapse;
  white-space: normal;
}

.tray-table th {
  padding: 9px 18px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.06em;
  color: var(--color-text-muted);
  background: var(--color-bg-light);
  border-bottom: 1px solid var(--color-border);
}

.th-right,
.td-right {
  text-align: right;
}

.tray-tr {
  transition: background 150ms ease;
}

.tray-tr:hover {
  background: var(--color-primary-subtle);
}

.tray-tr td {
  padding: 11px 18px;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.tray-tr:last-child td {
  border-bottom: 0;
}

.cell-student,
.cell-doc,
.cell-file {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.cell-student strong {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.cell-student span {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.doc-head-line {
  display: flex;
  align-items: center;
  gap: 7px;
}

.doc-head-line strong {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.doc-code-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 45%, transparent);
  padding: 1px 6px;
  border-radius: 4px;
}

.mod-sub--navy {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-info);
}

.mod-sub--gold {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-text-secondary);
}

.file-name {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11.5px;
  color: var(--color-text-primary);
  max-width: 220px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.file-meta {
  font-size: 10.5px;
  color: var(--color-text-muted);
}

.status-box {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 3px 9px;
  border-radius: 6px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  font-weight: 600;
}

.status-box__dot {
  width: 5px;
  height: 5px;
  border-radius: 50%;
  background: currentColor;
}

.status-box--warning {
  background: var(--color-warning-badge);
  color: var(--color-warning-text);
  border: 1px solid var(--color-warning-border);
}

.status-box--success {
  background: var(--color-success-badge);
  color: var(--color-success-text);
  border: 1px solid var(--color-success-border);
}

.status-box--danger {
  background: var(--color-danger-badge);
  color: var(--color-danger-text);
  border: 1px solid var(--color-danger-border);
}

.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: flex-end;
  gap: 6px;
}

.row-btn {
  min-height: 28px;
  padding: 3px 10px;
  border-radius: 6px;
  font-size: 11.5px;
  font-weight: 600;
  transition:
    transform 140ms ease,
    background 160ms ease,
    border-color 160ms ease;
}

.row-btn:active {
  transform: scale(0.97);
}

.row-btn--primary {
  background: var(--color-primary);
  color: var(--color-white);
  border: 1px solid transparent;
}

.row-btn--primary:hover {
  background: var(--color-primary-hover);
}

.row-btn--outline {
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border-medium);
}

.row-btn--outline:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.empty-queue {
  padding: 28px;
  text-align: center;
  display: grid;
  justify-items: center;
  gap: 10px;
}

.tray-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 18px;
  background: var(--color-bg-light);
  border-top: 1px solid var(--color-border);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.tray-footer strong {
  color: var(--color-text-primary);
}

.footer-iso-tag {
  color: var(--color-accent);
  font-weight: 700;
}

/* ==========================================================================
   Columna Derecha: Formatos Oficiales + Criterios de Dictamen
   ========================================================================== */
.right-stack {
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.side-card {
  padding: 14px 16px;
}

.side-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  margin-bottom: 10px;
}

.side-card__title-group {
  display: flex;
  align-items: center;
  gap: 7px;
}

.side-card__icon {
  width: 14px;
  height: 14px;
  stroke: var(--color-accent);
  fill: none;
  stroke-width: 1.9;
}

.side-card__title {
  font-size: 11px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--color-text-primary);
}

.side-card__meta {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-text-secondary);
}

.official-tag {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  font-weight: 700;
  color: var(--color-accent);
  background: var(--color-accent-light);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
  padding: 1px 6px;
  border-radius: 4px;
}

.code-filter-list {
  display: grid;
  gap: 6px;
}

.doc-chip-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  width: 100%;
  padding: 6px 10px;
  border-radius: 7px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  text-align: left;
  transition:
    background 150ms ease,
    border-color 150ms ease,
    transform 150ms ease;
}

.doc-chip-btn:hover {
  background: var(--color-primary-subtle);
  border-color: var(--color-primary);
  transform: translateX(2px);
}

.doc-chip-btn--active {
  background: var(--color-primary-subtle);
  border-color: var(--color-accent);
}

.doc-chip-btn__code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  font-weight: 700;
  color: var(--color-accent);
  min-width: 32px;
}

.doc-chip-btn__label {
  font-size: 11.5px;
  font-weight: 500;
  color: var(--color-text-primary);
  flex: 1;
}

.doc-chip-btn__count {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-text-muted);
}

.criteria-list {
  display: grid;
  gap: 9px;
}

.criteria-box {
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  border-left-width: 3px;
}

.criteria-box--gold {
  border-left-color: var(--color-accent);
}

.criteria-box--blue {
  border-left-color: var(--color-info);
}

.criteria-box__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 3px;
}

.criteria-tag {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 9.5px;
  font-weight: 700;
  letter-spacing: 0.05em;
}

.criteria-tag--gold {
  color: var(--color-accent);
}

.criteria-tag--blue {
  color: var(--color-info);
}

.criteria-scope {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10px;
  color: var(--color-text-muted);
}

.criteria-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.criteria-desc {
  font-size: 11px;
  line-height: 1.38;
  color: var(--color-text-secondary);
  margin-top: 2px;
}

/* ==========================================================================
   Modales de Previsualización PDF y Dictamen Técnico
   ========================================================================== */
.pdf-preview-sheet,
.review-modal-body {
  display: grid;
  gap: 12px;
  margin: 12px 0 18px;
}

.pdf-preview-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  flex-wrap: wrap;
}

.pdf-preview-student {
  font-size: 12.5px;
  font-weight: 600;
  color: var(--color-text-primary);
  flex: 1;
}

.pdf-iframe {
  width: 100%;
  height: 360px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
}

.pdf-mock-canvas {
  padding: 14px;
  border-radius: 10px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
}

.pdf-mock-sheet {
  padding: 16px;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border-medium);
  display: grid;
  gap: 12px;
}

.pdf-mock-sheet__top {
  display: flex;
  justify-content: space-between;
  gap: 8px;
  font-size: 10px;
  font-weight: 700;
  color: var(--color-accent);
  border-bottom: 1px solid var(--color-border-light);
  padding-bottom: 8px;
}

.pdf-mock-sheet__title {
  font-size: 15px;
  color: var(--color-text-primary);
}

.pdf-mock-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
  margin: 0;
}

.pdf-mock-grid dt {
  font-size: 10px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-muted);
}

.pdf-mock-grid dd {
  margin: 2px 0 0;
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.pdf-mock-stamp {
  padding: 8px 12px;
  border-radius: 6px;
  background: var(--color-success-badge);
  color: var(--color-success-text);
  border: 1px solid var(--color-success-border);
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  font-weight: 600;
}

.review-summary-box {
  padding: 12px;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  display: grid;
  gap: 6px;
}

.review-summary-box__row {
  display: flex;
  align-items: center;
  gap: 8px;
}

.review-summary-box__meta {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin: 0;
}

.review-summary-box__meta code {
  color: var(--color-primary);
  font-weight: 600;
}

.preset-observations {
  display: grid;
  gap: 6px;
}

.preset-label {
  font-size: 11.5px;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.preset-chips {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.preset-chip {
  font-size: 11px;
  padding: 4px 9px;
  border-radius: 6px;
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border-medium);
  transition:
    background 140ms ease,
    border-color 140ms ease;
}

.preset-chip:hover {
  background: var(--color-accent-light);
  border-color: var(--color-accent);
}

@media (max-width: 1150px) {
  .compact-kpis {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .workspace-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .compact-kpis {
    grid-template-columns: 1fr;
  }
  .ctrl-header__actions {
    width: 100%;
    flex-direction: column;
    align-items: stretch;
  }
  .pdf-mock-grid {
    grid-template-columns: 1fr;
  }
}
</style>
