<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { validateFields } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import { isAxiosError } from 'axios'
import { computed, onMounted, reactive, ref, watch } from 'vue'
import {
  UiAlert,
  UiBasePage,
  UiButton,
  UiCard,
  UiDialog,
  UiField,
  UiToast,
} from '../../../shared/components'
import {
  createCareer,
  deleteCareer,
  getCareers,
  updateCareer,
  updateCareerStatus,
} from '../api/careersApi'
import type { Career } from '../types/careers'

// Catálogo semilla local para cuando el backend no está encendido en desarrollo
const fallbackCareers = ref<Career[]>([
  {
    id: 1,
    code: 'IIND',
    name: 'Ingeniería Industrial',
    totalCredits: 260,
    isActive: true,
    createdAt: '2026-08-15T08:00:00Z',
    updatedAt: '2026-10-12T10:30:00Z',
  },
  {
    id: 2,
    code: 'IINF',
    name: 'Ingeniería Informática',
    totalCredits: 260,
    isActive: true,
    createdAt: '2026-08-15T08:00:00Z',
    updatedAt: '2026-10-14T09:15:00Z',
  },
  {
    id: 3,
    code: 'IGEM',
    name: 'Ingeniería en Gestión Empresarial',
    totalCredits: 260,
    isActive: true,
    createdAt: '2026-08-15T08:00:00Z',
    updatedAt: '2026-10-11T14:20:00Z',
  },
  {
    id: 4,
    code: 'IEME',
    name: 'Ingeniería Electromecánica',
    totalCredits: 260,
    isActive: true,
    createdAt: '2026-08-15T08:00:00Z',
    updatedAt: '2026-10-09T11:00:00Z',
  },
  {
    id: 5,
    code: 'IELC',
    name: 'Ingeniería Electrónica',
    totalCredits: 260,
    isActive: true,
    createdAt: '2026-08-15T08:00:00Z',
    updatedAt: '2026-10-05T16:45:00Z',
  },
  {
    id: 6,
    code: 'IENR',
    name: 'Ingeniería en Energías Renovables',
    totalCredits: 260,
    isActive: true,
    createdAt: '2026-08-15T08:00:00Z',
    updatedAt: '2026-10-02T12:10:00Z',
  },
])

const careers = ref<Career[]>([])
const isOfflinePreview = ref(false)
const isLoading = ref(true)
const barsReady = ref(false)
const toast = ref<InstanceType<typeof UiToast> | null>(null)

// Filtros y paginación
const searchQuery = ref('')
const statusFilter = ref<'all' | 'active' | 'inactive'>('all')
const currentPage = ref(1)
const totalPages = ref(1)
const totalItems = ref(0)
const pageSize = 10

// Contadores KPI animados
const animatedStats = reactive({
  total: 0,
  active: 0,
  baseCredits: 0,
  minSocialCredits: 0,
})

function runKpiAnimation(totalVal: number, activeVal: number): void {
  const prefersReduced =
    typeof window !== 'undefined' &&
    typeof window.matchMedia === 'function' &&
    window.matchMedia('(prefers-reduced-motion: reduce)').matches

  if (prefersReduced) {
    animatedStats.total = totalVal
    animatedStats.active = activeVal
    animatedStats.baseCredits = 260
    animatedStats.minSocialCredits = 182
    barsReady.value = true
    return
  }

  const duration = 680
  const start = performance.now()

  function tick(now: number): void {
    const elapsed = now - start
    const progress = Math.min(elapsed / duration, 1)
    const ease = 1 - Math.pow(1 - progress, 4)

    animatedStats.total = Math.round(totalVal * ease)
    animatedStats.active = Math.round(activeVal * ease)
    animatedStats.baseCredits = Math.round(260 * ease)
    animatedStats.minSocialCredits = Math.round(182 * ease)

    if (progress < 1) {
      requestAnimationFrame(tick)
    }
  }

  requestAnimationFrame(tick)
  setTimeout(() => {
    barsReady.value = true
  }, 80)
}

function applyLocalFilters(): void {
  const query = searchQuery.value.trim().toLowerCase()
  const filtered = fallbackCareers.value.filter((item) => {
    const matchesSearch =
      !query ||
      item.code.toLowerCase().includes(query) ||
      item.name.toLowerCase().includes(query)
    const matchesStatus =
      statusFilter.value === 'all' ||
      (statusFilter.value === 'active' && item.isActive) ||
      (statusFilter.value === 'inactive' && !item.isActive)
    return matchesSearch && matchesStatus
  })

  careers.value = filtered
  totalItems.value = filtered.length
  totalPages.value = Math.max(1, Math.ceil(filtered.length / pageSize))
  const activeCount = fallbackCareers.value.filter((c) => c.isActive).length
  runKpiAnimation(fallbackCareers.value.length, activeCount)
}

async function loadCareers(): Promise<void> {
  isLoading.value = true

  try {
    const isActiveParam =
      statusFilter.value === 'active'
        ? true
        : statusFilter.value === 'inactive'
          ? false
          : null

    const data = await getCareers({
      page: currentPage.value,
      pageSize,
      search: searchQuery.value.trim() || undefined,
      isActive: isActiveParam,
      sort: 'code',
    })

    isOfflinePreview.value = false
    careers.value = data.items
    totalPages.value = data.totalPages
    totalItems.value = data.totalItems
    const activeCount = data.items.filter((c) => c.isActive).length
    runKpiAnimation(data.totalItems, activeCount)
  } catch {
    isOfflinePreview.value = true
    applyLocalFilters()
  } finally {
    isLoading.value = false
  }
}

let searchDebounce: ReturnType<typeof setTimeout> | null = null
watch(searchQuery, () => {
  if (searchDebounce) clearTimeout(searchDebounce)
  searchDebounce = setTimeout(() => {
    currentPage.value = 1
    void loadCareers()
  }, 240)
})

watch(statusFilter, () => {
  currentPage.value = 1
  void loadCareers()
})

watch(currentPage, () => {
  void loadCareers()
})

// Cálculo de créditos mínimos (70% Servicio Social, 80% Prácticas)
function socialServiceCredits(total: number): number {
  return Math.round(total * 0.7)
}

function internshipCredits(total: number): number {
  return Math.round(total * 0.8)
}

// Estado del Modal de Creación / Edición
const dialogOpen = ref(false)
const isEditing = ref(false)
const editingId = ref<number | null>(null)
const formCode = ref('')
const formName = ref('')
const formCredits = ref('260')
const formError = ref('')
const isSaving = ref(false)

const dialogTitle = computed(() =>
  isEditing.value ? 'Editar Programa Educativo' : 'Registrar Nueva Carrera',
)

function openCreateModal(): void {
  isEditing.value = false
  editingId.value = null
  formCode.value = ''
  formName.value = ''
  formCredits.value = '260'
  formError.value = ''
  dialogOpen.value = true
}

function openEditModal(career: Career): void {
  isEditing.value = true
  editingId.value = career.id
  formCode.value = career.code
  formName.value = career.name
  formCredits.value = career.totalCredits.toString()
  formError.value = ''
  dialogOpen.value = true
}

async function handleSave(): Promise<void> {
  if (!careerValidation.validate()) return

  formError.value = ''

  const code = formCode.value.trim().toUpperCase()
  const name = formName.value.trim()
  const credits = parseInt(formCredits.value, 10)

  if (!code) {
    formError.value = 'La clave oficial es obligatoria.'
    return
  }
  if (!name) {
    formError.value = 'El nombre oficial del programa es obligatorio.'
    return
  }
  if (isNaN(credits) || credits <= 0) {
    formError.value = 'Los créditos deben ser un número entero mayor a 0.'
    return
  }

  isSaving.value = true

  try {
    if (isOfflinePreview.value) {
      if (isEditing.value && editingId.value !== null) {
        const target = fallbackCareers.value.find((c) => c.id === editingId.value)
        if (target) {
          target.code = code
          target.name = name
          target.totalCredits = credits
          target.updatedAt = new Date().toISOString()
        }
        toast.value?.show(`Carrera "${code}" actualizada en catálogo.`)
      } else {
        const duplicate = fallbackCareers.value.some(
          (c) => c.code.toUpperCase() === code,
        )
        if (duplicate) {
          formError.value = 'La clave ingresada ya está registrada en otra carrera.'
          isSaving.value = false
          return
        }
        fallbackCareers.value.push({
          id: Date.now(),
          code,
          name,
          totalCredits: credits,
          isActive: true,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
        })
        toast.value?.show(`Carrera "${code}" registrada exitosamente.`)
      }
      dialogOpen.value = false
      applyLocalFilters()
      return
    }

    if (isEditing.value && editingId.value !== null) {
      await updateCareer(editingId.value, { code, name, totalCredits: credits })
      toast.value?.show(`Carrera "${code}" actualizada correctamente.`)
    } else {
      await createCareer({ code, name, totalCredits: credits })
      toast.value?.show(`Carrera "${code}" registrada exitosamente.`)
    }

    dialogOpen.value = false
    await loadCareers()
  } catch (error) {
    careerValidation.apply(error, 'No se pudo guardar la carrera.')
    formError.value = problemMessage(error, 'No se pudo guardar la carrera.')
  } finally {
    isSaving.value = false
  }
}

// Toggle de estado Activa / Inactiva
const togglingId = ref<number | null>(null)

async function handleToggleStatus(career: Career): Promise<void> {
  togglingId.value = career.id
  const nextStatus = !career.isActive

  try {
    if (!isOfflinePreview.value) {
      await updateCareerStatus(career.id, nextStatus)
    }
    career.isActive = nextStatus
    if (isOfflinePreview.value) {
      applyLocalFilters()
    }
    toast.value?.show(
      `Carrera "${career.code}" ${nextStatus ? 'habilitada' : 'desactivada'} correctamente.`,
    )
  } catch {
    toast.value?.show('No fue posible actualizar el estado de la carrera.')
  } finally {
    togglingId.value = null
  }
}

// Confirmación de Eliminación
const deleteDialogOpen = ref(false)
const careerToDelete = ref<Career | null>(null)
const deleteError = ref('')
const isDeleting = ref(false)

function openDeleteModal(career: Career): void {
  careerToDelete.value = career
  deleteError.value = ''
  deleteDialogOpen.value = true
}

async function handleConfirmDelete(): Promise<void> {
  if (!careerToDelete.value) return

  isDeleting.value = true
  deleteError.value = ''

  try {
    if (!isOfflinePreview.value) {
      await deleteCareer(careerToDelete.value.id)
    } else {
      fallbackCareers.value = fallbackCareers.value.filter(
        (c) => c.id !== careerToDelete.value?.id,
      )
    }
    toast.value?.show(`Carrera "${careerToDelete.value.code}" dada de baja.`)
    deleteDialogOpen.value = false
    await loadCareers()
  } catch (error) {
    if (isAxiosError(error) && error.response?.status === 409) {
      deleteError.value =
        'No se puede eliminar la carrera porque tiene expedientes o alumnos asociados.'
    } else {
      deleteError.value = 'Ocurrió un error al eliminar la carrera.'
    }
  } finally {
    isDeleting.value = false
  }
}

onMounted(loadCareers)
const careerValidation = useFormValidation(() => validateFields({code:formCode.value.trim().toUpperCase(),name:formName.value,totalCredits:formCredits.value},{code:{required:true,max:16,pattern:/^[A-Z0-9_-]+$/},name:{required:true,max:150},totalCredits:{required:true,integer:true,min:1}}))
watchValidation(dialogOpen, () => careerValidation.reset())
</script>

<template>
  <UiBasePage fluid>
    <!-- Encabezado Ejecutivo Compacto -->
    <template #header>
      <header class="ctrl-header reveal-step reveal-1">
        <div class="ctrl-header__left">
          <div class="ctrl-eyebrow">
            <span class="ctrl-badge">CATÁLOGO OFICIAL DE PROGRAMAS EDUCATIVOS</span>
            <span class="ctrl-period">· Retículas TecNM AD26</span>
            <span class="ctrl-status-pill">
              <span class="ctrl-live-dot" aria-hidden="true" />
              Plantel <code>05DIT0001V</code>
            </span>
          </div>
          <h1 class="ctrl-title">Catálogo de Carreras Académicas</h1>
        </div>

        <div class="ctrl-header__actions">
          <UiButton variant="secondary" class="ctrl-btn" @click="loadCareers">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="ctrl-btn__icon">
              <path d="M21 12a9 9 0 0 0-9-9 9.75 9.75 0 0 0-6.74 2.74L3 8" />
              <path d="M3 3v5h5" />
              <path d="M3 12a9 9 0 0 0 9 9 9.75 9.75 0 0 0 6.74-2.74L21 16" />
              <path d="M16 16h5v5" />
            </svg>
            Sincronizar
          </UiButton>

          <UiButton variant="primary" class="ctrl-btn" @click="openCreateModal">
            <svg viewBox="0 0 24 24" aria-hidden="true" class="ctrl-btn__icon">
              <line x1="12" y1="5" x2="12" y2="19" />
              <line x1="5" y1="12" x2="19" y2="12" />
            </svg>
            Nueva Carrera
          </UiButton>
        </div>
      </header>
    </template>

    <!-- 1. Fila de 4 Recuadros KPI Compactos -->
    <section class="compact-kpis reveal-step reveal-2" aria-label="Indicadores del catálogo">
      <!-- KPI 1: Total de Programas -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': statusFilter === 'all' }"
        @click="statusFilter = 'all'"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--navy" aria-hidden="true" />
            Programas Educativos
          </span>
          <span class="kpi-meta kpi-meta--sky">Oferta Vigente</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedStats.total }}</strong>
          <span class="kpi-tile__unit">carreras registradas</span>
        </div>

        <div class="kpi-tile__foot">
          <span><strong>Nivel</strong> Licenciatura</span>
          <span>Modalidad Escolarizada</span>
        </div>
      </article>

      <!-- KPI 2: Carreras Activas -->
      <article
        class="kpi-tile"
        :class="{ 'kpi-tile--selected': statusFilter === 'active' }"
        @click="statusFilter = statusFilter === 'active' ? 'all' : 'active'"
      >
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--emerald" aria-hidden="true" />
            Carreras Habilitadas
          </span>
          <span class="kpi-meta kpi-meta--emerald">• En Operación</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedStats.active }}</strong>
          <span class="kpi-tile__unit">programas activos</span>
        </div>

        <div class="kpi-tile__foot">
          <span>Vinculación abierta</span>
          <span class="kpi-highlight--emerald">100% acreditadas</span>
        </div>
      </article>

      <!-- KPI 3: Créditos Oficiales -->
      <article class="kpi-tile kpi-tile--gold">
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--gold" aria-hidden="true" />
            Estándar Reticular
          </span>
          <span class="kpi-tag-gold">DGEST / TecNM</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value kpi-tile__value--gold">
            {{ animatedStats.baseCredits }}
          </strong>
          <span class="kpi-tile__unit">créditos totales</span>
        </div>

        <div class="kpi-tile__foot">
          <span>10 Créd. Servicio Social</span>
          <span class="kpi-highlight--gold">Planes homologados</span>
        </div>
      </article>

      <!-- KPI 4: Umbral Servicio Social -->
      <article class="kpi-tile">
        <div class="kpi-tile__top">
          <span class="kpi-tile__label">
            <span class="kpi-dot kpi-dot--sky" aria-hidden="true" />
            Requisito Apertura SS
          </span>
          <span class="kpi-meta kpi-meta--sky">70% Avance</span>
        </div>

        <div class="kpi-tile__main">
          <strong class="kpi-tile__value">{{ animatedStats.minSocialCredits }}</strong>
          <span class="kpi-tile__unit">créditos mínimos</span>
        </div>

        <div class="kpi-tile__foot">
          <span><strong>208 créd.</strong> para Prácticas</span>
          <span>Validación Kárdex</span>
        </div>
      </article>
    </section>

    <!-- 2. Área Principal en 2 Columnas: Tabla del Catálogo + Panel Normativo Lateral -->
    <div class="workspace-grid reveal-step reveal-3">
      <!-- Columna Izquierda: Tabla Interactiva de Carreras -->
      <UiCard class="tray-card">
        <!-- Barra Superior de Búsqueda y Filtros -->
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
              placeholder="Buscar carrera por clave (ej. IIND, IINF) o nombre..."
              aria-label="Buscar por clave o nombre de carrera"
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
            <div class="status-tabs" role="tablist" aria-label="Filtrar carreras por estado">
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'all' }"
                @click="statusFilter = 'all'"
              >
                Todas
              </button>
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'active' }"
                @click="statusFilter = 'active'"
              >
                Activas
              </button>
              <button
                type="button"
                class="status-tab"
                :class="{ 'status-tab--active': statusFilter === 'inactive' }"
                @click="statusFilter = 'inactive'"
              >
                Inactivas
              </button>
            </div>

            <span class="tray-count">
              Total: <strong>{{ totalItems }}</strong>
            </span>
          </div>
        </div>

        <!-- Tabla con Transición de Filas -->
        <div class="tray-table-wrap" role="region" aria-label="Catálogo de Carreras">
          <table class="tray-table">
            <thead>
              <tr>
                <th scope="col">CLAVE OFICIAL</th>
                <th scope="col">PROGRAMA EDUCATIVO &amp; REQUISITOS</th>
                <th scope="col">RETÍCULA / CRÉDITOS</th>
                <th scope="col">ESTADO</th>
                <th scope="col" class="th-right">ACCIONES</th>
              </tr>
            </thead>
            <TransitionGroup name="tray-row" tag="tbody">
              <tr v-for="row in careers" :key="row.id" class="tray-tr">
                <td>
                  <span class="code-pill">
                    <span
                      class="code-pill__dot"
                      :class="row.isActive ? 'code-pill__dot--active' : 'code-pill__dot--inactive'"
                      aria-hidden="true"
                    />
                    {{ row.code }}
                  </span>
                </td>

                <td>
                  <div class="cell-program">
                    <strong>{{ row.name }}</strong>
                    <span>
                      Req. Servicio Social: {{ socialServiceCredits(row.totalCredits) }} créd. (70%) · Prácticas: {{ internshipCredits(row.totalCredits) }} créd. (80%)
                    </span>
                  </div>
                </td>

                <td>
                  <div class="cell-credits">
                    <span class="credits-number">{{ row.totalCredits }} créditos</span>
                    <div class="credits-track">
                      <span
                        class="credits-fill"
                        :style="{
                          width: barsReady
                            ? `${Math.min(100, Math.round((row.totalCredits / 260) * 100))}%`
                            : '0%',
                        }"
                      />
                    </div>
                  </div>
                </td>

                <td>
                  <span
                    class="status-box"
                    :class="row.isActive ? 'status-box--success' : 'status-box--danger'"
                  >
                    <span class="status-box__dot" aria-hidden="true" />
                    {{ row.isActive ? 'Activa' : 'Inactiva' }}
                  </span>
                </td>

                <td class="td-right">
                  <div class="row-actions">
                    <button
                      type="button"
                      class="row-btn row-btn--outline"
                      @click="openEditModal(row)"
                    >
                      Editar
                    </button>
                    <button
                      type="button"
                      class="row-btn row-btn--outline"
                      :disabled="togglingId === row.id"
                      @click="handleToggleStatus(row)"
                    >
                      {{ row.isActive ? 'Desactivar' : 'Activar' }}
                    </button>
                    <button
                      type="button"
                      class="row-btn row-btn--danger"
                      @click="openDeleteModal(row)"
                    >
                      Baja
                    </button>
                  </div>
                </td>
              </tr>
            </TransitionGroup>
          </table>

          <div v-if="!isLoading && careers.length === 0" class="empty-catalog">
            <p>No se encontraron carreras con los criterios seleccionados.</p>
            <UiButton
              variant="secondary"
              @click="
                searchQuery = '';
                statusFilter = 'all'
              "
            >
              Restablecer filtros
            </UiButton>
          </div>
        </div>

        <!-- Pie de Tabla y Paginación -->
        <footer class="tray-footer">
          <span>
            Catálogo oficial TecNM Campus Monclova ·
            {{ isOfflinePreview ? 'Vista operativa local activa' : 'Sincronizado con PostgreSQL' }}
          </span>

          <div v-if="totalPages > 1" class="pagination-inline">
            <span>Página {{ currentPage }} de {{ totalPages }}</span>
            <button
              type="button"
              class="row-btn row-btn--outline"
              :disabled="currentPage <= 1"
              @click="currentPage--"
            >
              ← Anterior
            </button>
            <button
              type="button"
              class="row-btn row-btn--outline"
              :disabled="currentPage >= totalPages"
              @click="currentPage++"
            >
              Siguiente →
            </button>
          </div>
          <span v-else class="footer-iso-tag">SGC ISO 9001:2015</span>
        </footer>
      </UiCard>

      <!-- Columna Derecha: Reglas de Créditos y Resumen Reticular -->
      <aside class="right-stack">
        <!-- Tarjeta Superior: Criterios Normativos de Créditos -->
        <UiCard class="side-card">
          <div class="side-card__head">
            <div class="side-card__title-group">
              <svg viewBox="0 0 24 24" aria-hidden="true" class="side-card__icon">
                <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" />
                <path d="m9 12 2 2 4-4" />
              </svg>
              <h2 class="side-card__title">CRITERIOS DE ELEGIBILIDAD</h2>
            </div>
            <span class="official-tag">Lineamiento</span>
          </div>

          <div class="rules-stack">
            <article class="rule-card rule-card--navy">
              <div class="rule-card__top">
                <span class="rule-badge rule-badge--navy">SERVICIO SOCIAL</span>
                <strong class="rule-pct">≥ 70% Créditos</strong>
              </div>
              <h3 class="rule-title">182 de 260 créditos aprobados</h3>
              <p class="rule-desc">
                Requisito obligatorio en kárdex para autorizar solicitud Anexo XVIII y Carta de Presentación (480 horas).
              </p>
            </article>

            <article class="rule-card rule-card--gold">
              <div class="rule-card__top">
                <span class="rule-badge rule-badge--gold">PRÁCTICAS PROF.</span>
                <strong class="rule-pct rule-pct--gold">≥ 80% Créditos</strong>
              </div>
              <h3 class="rule-title">208 de 260 créditos aprobados</h3>
              <p class="rule-desc">
                Requerido para vinculación en empresas con convenio activo y asignación de asesor interno.
              </p>
            </article>
          </div>
        </UiCard>

        <!-- Tarjeta Inferior: Distribución Rápida por Clave -->
        <UiCard class="side-card">
          <div class="side-card__head">
            <h2 class="side-card__title">CLAVES OFICIALES VIGENTES</h2>
            <span class="side-card__meta">Filtro rápido</span>
          </div>

          <div class="quick-codes">
            <button
              v-for="item in careers"
              :key="item.id"
              type="button"
              class="code-chip-btn"
              :class="{ 'code-chip-btn--selected': searchQuery.toUpperCase() === item.code }"
              @click="
                searchQuery =
                  searchQuery.toUpperCase() === item.code ? '' : item.code
              "
            >
              <span class="code-chip-btn__sigla">{{ item.code }}</span>
              <span class="code-chip-btn__name">{{ item.name }}</span>
              <span class="code-chip-btn__cr">{{ item.totalCredits }}c</span>
            </button>
          </div>
        </UiCard>
      </aside>
    </div>

    <!-- Modal de Creación / Edición -->
    <UiDialog v-model="dialogOpen" :title="dialogTitle">
      <form novalidate class="dialog-form" @submit.prevent="handleSave">
        <UiAlert v-if="formError && !(careerValidation.hasErrors.value)" tone="danger" title="Revisa los datos ingresados" class="mb-3">
          {{ formError }}
        </UiAlert>

        <UiFormErrors :validation="careerValidation" />
        <UiField
          v-model="formCode"
          label="Clave Oficial (Siglas)"
          hint="Entre 1 y 16 caracteres alfanuméricos en mayúsculas. Ej. IIND, IINF, IGEM"
          placeholder="Ej. IIND"
          required
         v-bind="careerValidation.field('code', 'Clave Oficial (Siglas)')" />

        <UiField
          v-model="formName"
          label="Nombre Oficial de la Carrera"
          hint="Nombre completo conforme al plan de estudios autorizado por el TecNM"
          placeholder="Ej. Ingeniería Industrial"
          required
         v-bind="careerValidation.field('name', 'Nombre Oficial de la Carrera')" />

        <UiField
          v-model="formCredits"
          label="Créditos Totales de Retícula"
          type="number"
          min="1"
          max="500"
          hint="Estándar TecNM: 260 créditos totales (182 para Servicio Social)"
          required
         v-bind="careerValidation.field('totalCredits', 'Créditos Totales de Retícula')" />
      </form>

      <template #actions>
        <UiButton variant="secondary" :disabled="isSaving" @click="dialogOpen = false">
          Cancelar
        </UiButton>
        <UiButton variant="primary" :loading="isSaving" @click="handleSave">
          {{ isEditing ? 'Guardar Cambios' : 'Registrar Carrera' }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- Modal de Confirmación de Baja -->
    <UiDialog v-model="deleteDialogOpen" title="Confirmar Baja de Carrera">
      <UiAlert v-if="deleteError" tone="danger" title="Operación restringida" class="mb-3">
        {{ deleteError }}
      </UiAlert>

      <p v-if="careerToDelete" class="delete-confirm-copy">
        ¿Confirmas dar de baja del catálogo institucional la carrera
        <strong>{{ careerToDelete.name }} ({{ careerToDelete.code }})</strong>?
        Los expedientes históricos asociados conservarán su trazabilidad.
      </p>

      <template #actions>
        <UiButton variant="secondary" :disabled="isDeleting" @click="deleteDialogOpen = false">
          Cancelar
        </UiButton>
        <UiButton variant="danger" :loading="isDeleting" @click="handleConfirmDelete">
          Confirmar Baja
        </UiButton>
      </template>
    </UiDialog>

    <UiToast ref="toast" />
  </UiBasePage>
</template>

<style scoped>
/* ==========================================================================
   Animaciones Escalonadas y Transición de Tabla
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
  gap: 12px;
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

.tray-count {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11.5px;
  color: var(--color-text-secondary);
}

.tray-count strong {
  color: var(--color-text-primary);
}

/* Tabla del Catálogo */
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
  padding: 10px 18px;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.tray-tr:last-child td {
  border-bottom: 0;
}

.code-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 12px;
  font-weight: 700;
  color: var(--color-primary);
  background: var(--color-primary-subtle);
  border: 1px solid color-mix(in srgb, var(--color-primary) 28%, transparent);
  padding: 3px 9px;
  border-radius: 6px;
}

[data-theme="dark"] .code-pill {
  color: var(--color-white);
}

.code-pill__dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
}

.code-pill__dot--active {
  background: var(--color-accent);
}
.code-pill__dot--inactive {
  background: var(--color-danger);
}

.cell-program {
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.cell-program strong {
  font-size: 13px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.cell-program span {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  color: var(--color-text-secondary);
}

.cell-credits {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 115px;
}

.credits-number {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11.5px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.credits-track {
  width: 96px;
  height: 4px;
  background: var(--color-gray-100);
  border-radius: 99px;
  overflow: hidden;
}

.credits-fill {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, var(--color-primary), var(--color-accent));
  border-radius: 99px;
  transition: width 680ms cubic-bezier(0.16, 1, 0.3, 1);
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

.row-btn--outline {
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  border: 1px solid var(--color-border-medium);
}

.row-btn--outline:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.row-btn--danger {
  background: transparent;
  color: var(--destructive-text);
  border: 1px solid color-mix(in srgb, var(--color-danger) 35%, transparent);
}

.row-btn--danger:hover {
  background: var(--color-danger-bg);
}

.empty-catalog {
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

.pagination-inline {
  display: flex;
  align-items: center;
  gap: 8px;
}

.footer-iso-tag {
  color: var(--color-accent);
  font-weight: 700;
}

/* ==========================================================================
   Columna Derecha: Criterios Normativos + Filtro Rápido por Clave
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

.rules-stack {
  display: grid;
  gap: 9px;
}

.rule-card {
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--color-bg-light);
  border: 1px solid var(--color-border);
  border-left-width: 3px;
}

.rule-card--navy {
  border-left-color: var(--color-primary);
}

.rule-card--gold {
  border-left-color: var(--color-accent);
}

.rule-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 4px;
}

.rule-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 9.5px;
  font-weight: 700;
  letter-spacing: 0.05em;
}

.rule-badge--navy {
  color: var(--color-info);
}

.rule-badge--gold {
  color: var(--color-accent);
}

.rule-pct {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-text-primary);
}

.rule-pct--gold {
  color: var(--color-accent);
}

.rule-title {
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.rule-desc {
  font-size: 11px;
  line-height: 1.38;
  color: var(--color-text-secondary);
  margin-top: 2px;
}

/* Chips Rápidos de Claves */
.quick-codes {
  display: grid;
  gap: 6px;
}

.code-chip-btn {
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

.code-chip-btn:hover {
  background: var(--color-primary-subtle);
  border-color: var(--color-primary);
  transform: translateX(2px);
}

.code-chip-btn--selected {
  background: var(--color-primary-subtle);
  border-color: var(--color-accent);
}

.code-chip-btn__sigla {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 11px;
  font-weight: 700;
  color: var(--color-accent);
  min-width: 38px;
}

.code-chip-btn__name {
  font-size: 11.5px;
  font-weight: 500;
  color: var(--color-text-primary);
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.code-chip-btn__cr {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  color: var(--color-text-muted);
}

.dialog-form {
  display: grid;
  gap: 12px;
  margin-top: 12px;
}

.delete-confirm-copy {
  font-size: 13.5px;
  line-height: 1.5;
}

.mb-3 {
  margin-bottom: 12px;
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
}
</style>
