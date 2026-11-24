<script setup lang="ts">
import { problemMessage } from '../../../shared/api/problemDetails'
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch, watch as watchValidation } from 'vue'
import { useFormValidation } from '../../../shared/composables/useFormValidation'
import { validateFields } from '../../../shared/utils/validationRules'
import UiFormErrors from '../../../shared/components/UiFormErrors.vue'
import { isAxiosError } from 'axios'
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
const kpiPool = ref<Career[]>([])
const isOfflinePreview = ref(false)
const isLoading = ref(true)
const barsReady = ref(false)
const toast = ref<InstanceType<typeof UiToast> | null>(null)

// Filtros, autocompletado y paginación estandarizada (10 registros por página)
const searchQuery = ref('')
const statusFilter = ref<'all' | 'active' | 'inactive'>('all')
const currentPage = ref(1)
const totalPages = ref(1)
const totalItems = ref(0)
const pageSize = 10

const autocompleteSuggestions = ref<Career[]>([])
const isAutocompleteOpen = ref(false)
let searchDebounce: ReturnType<typeof setTimeout> | null = null

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

  const duration = 560
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
  }, 60)
}

function normalizeText(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .trim()
    .toLowerCase()
}

function filterCareersSmart(list: Career[], rawQuery: string): Career[] {
  const q = normalizeText(rawQuery)
  if (!q) return list

  const exactCodeMatches = list.filter((item) => normalizeText(item.code) === q)
  if (exactCodeMatches.length > 0) return exactCodeMatches

  const codePrefixMatches = list.filter((item) => normalizeText(item.code).startsWith(q))
  const nameMatches = list.filter(
    (item) =>
      !normalizeText(item.code).startsWith(q) &&
      normalizeText(item.name).includes(q),
  )
  return [...codePrefixMatches, ...nameMatches]
}

function applyLocalFilters(): void {
  const query = searchQuery.value.trim()
  const statusFiltered = fallbackCareers.value.filter(
    (item) =>
      statusFilter.value === 'all' ||
      (statusFilter.value === 'active' && item.isActive) ||
      (statusFilter.value === 'inactive' && !item.isActive),
  )
  const filtered = filterCareersSmart(statusFiltered, query)

  totalItems.value = filtered.length
  totalPages.value = Math.max(1, Math.ceil(filtered.length / pageSize))
  const start = (currentPage.value - 1) * pageSize
  careers.value = filtered.slice(start, start + pageSize)
  kpiPool.value = fallbackCareers.value
  const activeCount = fallbackCareers.value.filter((c) => c.isActive).length
  runKpiAnimation(fallbackCareers.value.length, activeCount)
}

async function loadKpiPool(): Promise<void> {
  try {
    const data = await getCareers({ page: 1, pageSize: 100, sort: 'code' })
    kpiPool.value = data.items || []
    const activeCount = kpiPool.value.filter((c) => c.isActive).length
    runKpiAnimation(data.totalItems, activeCount)
  } catch {
    // Fallback handled by loadCareers
  }
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
    const rawItems = data.items || []
    const refinedItems = filterCareersSmart(rawItems, searchQuery.value)
    careers.value = refinedItems
    const exactMatched =
      searchQuery.value.trim().length > 0 && refinedItems.length < rawItems.length
    totalItems.value = exactMatched ? refinedItems.length : data.totalItems
    totalPages.value = Math.max(
      1,
      exactMatched
        ? Math.ceil(refinedItems.length / pageSize)
        : data.totalPages || Math.ceil((data.totalItems || 0) / pageSize),
    )
    if (kpiPool.value.length === 0) {
      const activeCount = careers.value.filter((c) => c.isActive).length
      runKpiAnimation(data.totalItems, activeCount)
    } else {
      barsReady.value = true
    }
  } catch {
    isOfflinePreview.value = true
    applyLocalFilters()
  } finally {
    isLoading.value = false
  }
}

async function fetchSuggestions(query: string): Promise<void> {
  const q = query.trim()
  if (!q) {
    autocompleteSuggestions.value = []
    isAutocompleteOpen.value = false
    return
  }

  const pool = kpiPool.value.length > 0 ? kpiPool.value : careers.value
  autocompleteSuggestions.value = filterCareersSmart(pool, q).slice(0, 6)
  isAutocompleteOpen.value = autocompleteSuggestions.value.length > 0
}

function handleSearchInput(): void {
  if (searchDebounce) clearTimeout(searchDebounce)
  searchDebounce = setTimeout(() => {
    currentPage.value = 1
    void fetchSuggestions(searchQuery.value)
    void loadCareers()
  }, 280)
}

function selectSuggestion(item: Career): void {
  searchQuery.value = item.code
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadCareers()
}

function clearSearch(): void {
  if (searchDebounce) clearTimeout(searchDebounce)
  searchQuery.value = ''
  autocompleteSuggestions.value = []
  isAutocompleteOpen.value = false
  currentPage.value = 1
  void loadCareers()
}

function handleSearchFocus(): void {
  if (searchQuery.value.trim().length > 0 && autocompleteSuggestions.value.length > 0) {
    isAutocompleteOpen.value = true
  }
}

function handleSearchBlur(): void {
  setTimeout(() => {
    isAutocompleteOpen.value = false
  }, 180)
}

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
    await Promise.all([loadKpiPool(), loadCareers()])
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
    } else {
      await loadKpiPool()
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
    await Promise.all([loadKpiPool(), loadCareers()])
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

const quickCodeList = computed(() => (kpiPool.value.length > 0 ? kpiPool.value : careers.value))

const careerValidation = useFormValidation(() =>
  validateFields(
    {
      code: formCode.value.trim().toUpperCase(),
      name: formName.value,
      totalCredits: formCredits.value,
    },
    {
      code: { required: true, max: 16, pattern: /^[A-Z0-9_-]+$/ },
      name: { required: true, max: 150 },
      totalCredits: { required: true, integer: true, min: 1 },
    },
  ),
)
watchValidation(dialogOpen, () => careerValidation.reset())

onBeforeUnmount(() => {
  if (searchDebounce) clearTimeout(searchDebounce)
})

onMounted(async () => {
  await Promise.all([loadKpiPool(), loadCareers()])
})
</script>

<template>
  <UiBasePage :fluid="true" class="fullscreen-page">
    <!-- Encabezado Ejecutivo Compacto con 4 KPIs (solo borde dorado activo) -->
    <header class="exec-header">
      <div class="exec-header__left">
        <div class="exec-eyebrow">
          <span class="exec-badge">CATÁLOGO OFICIAL DE PROGRAMAS EDUCATIVOS</span>
          <span class="exec-period">· Retículas TecNM · Plantel 05DIT0001V</span>
        </div>
        <div class="title-row">
          <h1 class="exec-title">Catálogo de Carreras Académicas</h1>
          <div class="header-actions">
            <UiButton variant="secondary" size="sm" @click="loadCareers">
              Sincronizar
            </UiButton>
            <UiButton variant="primary" size="sm" @click="openCreateModal">
              + Nueva Carrera
            </UiButton>
          </div>
        </div>
      </div>

      <!-- 4 KPIs con superficie neutra y borde dorado únicamente cuando está activo -->
      <div class="exec-kpis" aria-label="Indicadores del catálogo">
        <button
          type="button"
          class="mini-kpi"
          :class="{ 'mini-kpi--active': statusFilter === 'all' }"
          @click="statusFilter = 'all'"
        >
          <span class="mini-kpi__label">Programas</span>
          <strong class="mini-kpi__value">{{ animatedStats.total }}</strong>
        </button>

        <button
          type="button"
          class="mini-kpi mini-kpi--emerald"
          :class="{ 'mini-kpi--active': statusFilter === 'active' }"
          @click="statusFilter = statusFilter === 'active' ? 'all' : 'active'"
        >
          <span class="mini-kpi__label">Habilitadas</span>
          <strong class="mini-kpi__value">{{ animatedStats.active }}</strong>
        </button>

        <div class="mini-kpi mini-kpi--gold mini-kpi--static">
          <span class="mini-kpi__label">Retícula Base</span>
          <strong class="mini-kpi__value">{{ animatedStats.baseCredits }}</strong>
        </div>

        <div class="mini-kpi mini-kpi--navy mini-kpi--static">
          <span class="mini-kpi__label">Mín. SS (70%)</span>
          <strong class="mini-kpi__value">{{ animatedStats.minSocialCredits }}</strong>
        </div>
      </div>
    </header>

    <!-- Área Principal en 2 Columnas a Pantalla Completa -->
    <div class="workspace-grid">
      <!-- Columna Izquierda: Tabla Interactiva de Carreras -->
      <UiCard class="tray-card">
        <!-- Barra Superior de Búsqueda Autocomplete y Filtros -->
        <div class="filters-bar">
          <div class="search-box">
            <label class="filter-label" for="careers-search-input">
              Buscar por Clave Oficial o Nombre de Carrera
            </label>
            <div class="autocomplete-container">
              <div class="input-wrapper">
                <svg
                  viewBox="0 0 24 24"
                  aria-hidden="true"
                  class="search-icon"
                  width="15"
                  height="15"
                  stroke="currentColor"
                  stroke-width="2"
                  fill="none"
                >
                  <circle cx="11" cy="11" r="8" />
                  <line x1="21" y1="21" x2="16.65" y2="16.65" />
                </svg>
                <input
                  id="careers-search-input"
                  v-model="searchQuery"
                  type="search"
                  class="modern-input"
                  placeholder="Ej. IIND, IINF o Ingeniería Industrial..."
                  aria-label="Buscar por clave o nombre de carrera"
                  autocomplete="off"
                  @input="handleSearchInput"
                  @focus="handleSearchFocus"
                  @blur="handleSearchBlur"
                  @keyup.enter="isAutocompleteOpen = false; currentPage = 1; loadCareers()"
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

              <!-- Lista desplegable de sugerencias (Autocomplete) -->
              <div
                v-if="isAutocompleteOpen && autocompleteSuggestions.length > 0"
                class="suggestions-dropdown"
                role="listbox"
              >
                <div
                  v-for="suggestion in autocompleteSuggestions"
                  :key="suggestion.id"
                  class="suggestion-item"
                  role="option"
                  @mousedown.prevent="selectSuggestion(suggestion)"
                >
                  <div class="suggestion-left">
                    <span class="suggestion-code">{{ suggestion.code }}</span>
                    <span class="suggestion-name">{{ suggestion.name }}</span>
                  </div>
                  <div class="suggestion-right">
                    <span class="suggestion-credits">{{ suggestion.totalCredits }} créd.</span>
                    <UiBadge :tone="suggestion.isActive ? 'success' : 'danger'">
                      {{ suggestion.isActive ? 'Activa' : 'Inactiva' }}
                    </UiBadge>
                  </div>
                </div>
              </div>
            </div>
          </div>

          <div class="toolbar-right">
            <span class="filter-label">Estatus</span>
            <div class="status-tabs-row">
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
            </div>
          </div>
        </div>

        <!-- Tabla con scroll interno y encabezados fijos -->
        <div class="tray-table-wrap" role="region" aria-label="Catálogo de Carreras">
          <table class="tray-table">
            <thead>
              <tr>
                <th scope="col">CLAVE OFICIAL</th>
                <th scope="col">PROGRAMA EDUCATIVO &amp; REQUISITOS</th>
                <th scope="col">RETÍCULA / CRÉDITOS</th>
                <th scope="col">ESTADO</th>
                <th scope="col" class="th-actions">ACCIONES</th>
              </tr>
            </thead>
            <TransitionGroup name="tray-row" tag="tbody">
              <tr v-if="isLoading" key="loading">
                <td colspan="5" class="empty-cell">Cargando catálogo de carreras…</td>
              </tr>
              <tr v-else-if="careers.length === 0" key="empty">
                <td colspan="5" class="empty-cell">
                  <div class="empty-catalog">
                    <p>No se encontraron carreras con los criterios seleccionados.</p>
                    <UiButton
                      variant="secondary"
                      size="sm"
                      @click="
                        searchQuery = '';
                        statusFilter = 'all'
                      "
                    >
                      Restablecer filtros
                    </UiButton>
                  </div>
                </td>
              </tr>
              <tr v-for="row in careers" :key="row.id" class="tray-tr">
                <td>
                  <span class="code-pill">{{ row.code }}</span>
                </td>

                <td>
                  <div class="cell-program">
                    <strong>{{ row.name }}</strong>
                    <span>
                      Req. SS: {{ socialServiceCredits(row.totalCredits) }} créd. (70%) · Prácticas:
                      {{ internshipCredits(row.totalCredits) }} créd. (80%)
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
                  <UiBadge :tone="row.isActive ? 'success' : 'danger'">
                    {{ row.isActive ? 'Activa' : 'Inactiva' }}
                  </UiBadge>
                </td>

                <td class="td-actions">
                  <div class="row-actions">
                    <UiButton variant="secondary" size="sm" @click="openEditModal(row)">
                      Editar
                    </UiButton>
                    <UiButton
                      variant="secondary"
                      size="sm"
                      :disabled="togglingId === row.id"
                      @click="handleToggleStatus(row)"
                    >
                      {{ row.isActive ? 'Desactivar' : 'Activar' }}
                    </UiButton>
                    <UiButton variant="danger" size="sm" @click="openDeleteModal(row)">
                      Baja
                    </UiButton>
                  </div>
                </td>
              </tr>
            </TransitionGroup>
          </table>
        </div>

        <!-- Barra de Paginación Estandarizada (10 registros por página) -->
        <footer class="pagination-bar">
          <span class="pagination-info">
            Mostrando <strong>{{ careers.length }}</strong> de
            <strong>{{ totalItems }}</strong> carreras · Página
            <strong>{{ currentPage }}</strong> de <strong>{{ totalPages }}</strong>
            <template v-if="isOfflinePreview"> · Vista operativa local</template>
          </span>

          <div class="pagination-controls">
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="currentPage <= 1 || isLoading"
              @click="currentPage--"
            >
              Anterior
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="currentPage >= totalPages || isLoading"
              @click="currentPage++"
            >
              Siguiente
            </UiButton>
          </div>
        </footer>
      </UiCard>

      <!-- Columna Derecha: Reglas de Créditos y Claves Oficiales -->
      <aside class="right-stack">
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
            <article class="rule-card">
              <div class="rule-card__top">
                <span class="rule-badge rule-badge--navy">SERVICIO SOCIAL</span>
                <strong class="rule-pct">≥ 70% Créditos</strong>
              </div>
              <h3 class="rule-title">182 de 260 créditos aprobados</h3>
              <p class="rule-desc">
                Requisito obligatorio en kárdex para autorizar solicitud Anexo XVIII y Carta de
                Presentación (480 horas).
              </p>
            </article>

            <article class="rule-card">
              <div class="rule-card__top">
                <span class="rule-badge rule-badge--gold">PRÁCTICAS PROF.</span>
                <strong class="rule-pct rule-pct--gold">≥ 80% Créditos</strong>
              </div>
              <h3 class="rule-title">208 de 260 créditos aprobados</h3>
              <p class="rule-desc">
                Requerido para vinculación en empresas con convenio activo y asignación de asesor
                interno.
              </p>
            </article>
          </div>
        </UiCard>

        <UiCard class="side-card side-card--codes">
          <div class="side-card__head">
            <h2 class="side-card__title">CLAVES OFICIALES VIGENTES</h2>
            <span class="side-card__meta">Filtro rápido</span>
          </div>

          <div class="quick-codes">
            <button
              v-for="item in quickCodeList"
              :key="item.id"
              type="button"
              class="code-chip-btn"
              :class="{ 'code-chip-btn--selected': searchQuery.toUpperCase() === item.code }"
              @click="
                searchQuery = searchQuery.toUpperCase() === item.code ? '' : item.code;
                currentPage = 1;
                loadCareers()
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
    <UiDialog v-model="dialogOpen" :title="dialogTitle" size="lg">
      <form novalidate class="dialog-form" @submit.prevent="handleSave">
        <UiAlert
          v-if="formError && !careerValidation.hasErrors.value"
          tone="danger"
          title="Revisa los datos ingresados"
        >
          {{ formError }}
        </UiAlert>

        <UiFormErrors :validation="careerValidation" />

        <div class="dialog-grid-2">
          <UiField
            v-model="formCode"
            label="Clave Oficial (Siglas)"
            hint="1 a 16 caracteres en mayúsculas. Ej. IIND, IINF, GE"
            placeholder="Ej. IIND"
            required
            v-bind="careerValidation.field('code', 'Clave Oficial (Siglas)')"
          />

          <UiField
            v-model="formCredits"
            label="Créditos Totales de Retícula"
            type="number"
            min="1"
            max="500"
            hint="Estándar TecNM: 260 créditos totales"
            required
            v-bind="careerValidation.field('totalCredits', 'Créditos Totales de Retícula')"
          />
        </div>

        <UiField
          v-model="formName"
          label="Nombre Oficial de la Carrera"
          hint="Nombre completo conforme al plan de estudios autorizado por el TecNM"
          placeholder="Ej. Ingeniería Industrial"
          required
          v-bind="careerValidation.field('name', 'Nombre Oficial de la Carrera')"
        />

        <div class="credits-preview-strip">
          <span>
            Mínimo Servicio Social (70%):
            <strong>{{ socialServiceCredits(Number(formCredits) || 0) }} créd.</strong>
          </span>
          <span>
            Mínimo Prácticas Prof. (80%):
            <strong>{{ internshipCredits(Number(formCredits) || 0) }} créd.</strong>
          </span>
        </div>
      </form>

      <template #actions>
        <UiButton variant="secondary" size="sm" :disabled="isSaving" @click="dialogOpen = false">
          Cancelar
        </UiButton>
        <UiButton variant="primary" size="sm" :loading="isSaving" @click="handleSave">
          {{ isEditing ? 'Guardar Cambios' : 'Registrar Carrera' }}
        </UiButton>
      </template>
    </UiDialog>

    <!-- Modal de Confirmación de Baja -->
    <UiDialog v-model="deleteDialogOpen" title="Confirmar Baja de Carrera" size="sm">
      <UiAlert v-if="deleteError" tone="danger" title="Operación restringida">
        {{ deleteError }}
      </UiAlert>

      <p v-if="careerToDelete" class="delete-confirm-copy">
        ¿Confirmas dar de baja del catálogo institucional la carrera
        <strong>{{ careerToDelete.name }} ({{ careerToDelete.code }})</strong>? Los expedientes
        históricos asociados conservarán su trazabilidad.
      </p>

      <template #actions>
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="isDeleting"
          @click="deleteDialogOpen = false"
        >
          Cancelar
        </UiButton>
        <UiButton
          variant="danger"
          size="sm"
          :loading="isDeleting"
          @click="handleConfirmDelete"
        >
          Confirmar Baja
        </UiButton>
      </template>
    </UiDialog>

    <UiToast ref="toast" />
  </UiBasePage>
</template>

<style scoped>
/* Estructura a pantalla completa (100dvh) sin scroll exterior */
.fullscreen-page {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  overflow: hidden;
  padding: 12px 22px 10px !important;
}

.exec-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.85rem;
  margin-bottom: 0.65rem;
  flex-wrap: wrap;
  flex-shrink: 0;
}

.exec-header__left {
  flex: 1;
  min-width: 280px;
}

.exec-eyebrow {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  margin-bottom: 0.15rem;
  flex-wrap: wrap;
}

.exec-badge {
  font-size: 0.65rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  padding: 0.14rem 0.5rem;
  border-radius: 5px;
  background: var(--color-accent-light);
  color: var(--color-accent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 40%, transparent);
}

.exec-period {
  font-size: 0.74rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.title-row {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.exec-title {
  font-size: 1.22rem;
  font-weight: 800;
  margin: 0;
  color: var(--color-text-primary);
  letter-spacing: -0.015em;
  line-height: 1.2;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.4rem;
}

/* 4 KPIs con superficie neutra y borde dorado únicamente al estar activo */
.exec-kpis {
  display: flex;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.mini-kpi {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  padding: 0.42rem 0.85rem;
  min-width: 108px;
  border-radius: 8px;
  border: 1px solid var(--color-border-medium);
  background: var(--color-surface-white);
  color: var(--color-text-primary);
  cursor: pointer;
  transition:
    background-color 180ms ease,
    border-color 180ms ease,
    box-shadow 180ms ease;
}

.mini-kpi--static {
  cursor: default;
}

.mini-kpi:not(.mini-kpi--static):hover {
  background: var(--secondary-hover);
  border-color: color-mix(in srgb, var(--color-accent) 70%, var(--color-border-medium));
}

.mini-kpi__label {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  color: var(--color-text-secondary);
}

.mini-kpi__value {
  font-size: 1.18rem;
  font-weight: 800;
  color: var(--color-text-primary);
  line-height: 1.15;
  font-variant-numeric: tabular-nums;
}

.mini-kpi--emerald .mini-kpi__value {
  color: var(--color-success);
}

.mini-kpi--gold .mini-kpi__value {
  color: var(--color-warning);
}

.mini-kpi--navy .mini-kpi__value {
  color: var(--color-info);
}

.mini-kpi--active {
  background: var(--kpi-active-bg);
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 1px var(--color-accent) !important;
}

/* Grid Principal de 2 Columnas a Pantalla Completa */
.workspace-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 295px;
  gap: 0.75rem;
  flex: 1;
  min-height: 0;
}

.tray-card {
  display: flex;
  flex-direction: column;
  flex: 1;
  min-height: 0;
  padding: 12px 16px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
}

/* Barra de filtros con superficie institucional contrastada */
.filters-bar {
  display: flex;
  gap: 0.7rem;
  flex-wrap: wrap;
  align-items: flex-end;
  margin-bottom: 0.6rem;
  padding: 0.55rem 0.75rem;
  border-radius: 8px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  flex-shrink: 0;
}

.search-box {
  flex: 1.5;
  min-width: 240px;
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.toolbar-right {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
}

.status-tabs-row {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.filter-label {
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

.input-wrapper {
  position: relative;
  display: flex;
  align-items: center;
  width: 100%;
}

.search-icon {
  position: absolute;
  left: 0.75rem;
  color: var(--color-text-secondary);
  pointer-events: none;
  z-index: 2;
}

input.modern-input {
  width: 100%;
  min-height: 34px;
  height: 34px;
  border-radius: 7px;
  border: 1px solid var(--input-border) !important;
  background: var(--input-bg) !important;
  padding: 0 2rem 0 2.35rem !important;
  font-size: 0.81rem;
  color: var(--color-text-primary) !important;
  outline: none;
  box-sizing: border-box;
  transition: border-color 160ms ease, background-color 160ms ease, box-shadow 160ms ease;
}

input.modern-input::-webkit-search-cancel-button,
input.modern-input::-webkit-search-decoration {
  -webkit-appearance: none;
  appearance: none;
  display: none;
}

input.modern-input:hover {
  border-color: var(--color-gray-400) !important;
}

input.modern-input:focus {
  border-color: var(--color-border-focus) !important;
  box-shadow: 0 0 0 2px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

.autocomplete-container {
  position: relative;
  width: 100%;
}

.clear-search-btn {
  position: absolute;
  right: 0.55rem;
  background: transparent;
  border: none;
  color: var(--color-primary);
  cursor: pointer;
  font-size: 0.85rem;
  font-weight: 800;
  padding: 0.2rem 0.3rem;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border-radius: 4px;
  transition: background-color 140ms ease, color 140ms ease;
}

.clear-search-btn:hover {
  color: var(--color-primary-hover);
  background: var(--color-primary-subtle);
}

.suggestions-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  left: 0;
  right: 0;
  background: var(--floating-bg);
  border: 1px solid var(--floating-border);
  border-radius: 8px;
  box-shadow: var(--floating-shadow);
  z-index: 999;
  max-height: 220px;
  overflow-y: auto;
  box-sizing: border-box;
}

.suggestion-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.45rem 0.75rem;
  cursor: pointer;
  border-bottom: 1px solid var(--color-border-light);
  transition: background-color 140ms ease;
}

.suggestion-item:last-child {
  border-bottom: none;
}

.suggestion-item:hover {
  background: var(--color-primary-subtle);
}

.suggestion-left {
  display: flex;
  flex-direction: column;
  gap: 0.08rem;
}

.suggestion-code {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-weight: 700;
  font-size: 0.78rem;
  color: var(--color-info-text);
}

.suggestion-name {
  font-size: 0.8rem;
  color: var(--color-text-primary);
}

.suggestion-right {
  display: flex;
  align-items: center;
  gap: 0.45rem;
}

.suggestion-credits {
  font-size: 0.72rem;
  font-weight: 600;
  color: var(--color-text-secondary);
}

/* Pestañas de Estatus */
.status-tabs {
  display: inline-flex;
  align-items: center;
  gap: 3px;
  padding: 3px;
  height: 34px;
  border-radius: 7px;
  background: var(--input-bg);
  border: 1px solid var(--input-border);
}

.status-tab {
  min-height: 26px !important;
  height: 26px;
  padding: 0 10px;
  border: 1px solid transparent;
  border-radius: 5px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 0.75rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 150ms ease, color 150ms ease, border-color 150ms ease;
}

.status-tab:hover {
  color: var(--color-text-primary);
  background: var(--secondary-hover);
}

.status-tab--active {
  background: var(--color-primary);
  color: var(--color-white);
  border-color: var(--color-accent);
}

/* Tabla con scroll interno y encabezados fijos */
.tray-table-wrap {
  flex: 1;
  min-height: 0;
  overflow: auto;
  margin: 0 -16px;
  border-top: 1px solid var(--color-border);
}

.tray-table {
  width: 100%;
  border-collapse: collapse;
  text-align: left;
  white-space: normal;
}

.tray-table th {
  padding: 7px 14px;
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

.tray-table td {
  padding: 7px 14px;
  font-size: 0.8rem;
  vertical-align: middle;
  border-bottom: 1px solid var(--color-border-light);
}

.tray-tr {
  transition: background-color 150ms ease;
}

.tray-tr:hover {
  background: var(--color-primary-subtle);
}

.code-pill {
  display: inline-block;
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.74rem;
  font-weight: 800;
  padding: 0.14rem 0.5rem;
  border-radius: 5px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.cell-program {
  display: flex;
  flex-direction: column;
  gap: 0.12rem;
}

.cell-program strong {
  font-size: 0.82rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.cell-program span {
  font-size: 0.73rem;
  color: var(--color-text-secondary);
}

.cell-credits {
  display: flex;
  flex-direction: column;
  gap: 0.22rem;
  min-width: 110px;
}

.credits-number {
  font-size: 0.76rem;
  font-weight: 600;
  color: var(--color-text-primary);
}

.credits-track {
  width: 100%;
  height: 6px;
  border-radius: 4px;
  background: var(--color-border);
  overflow: hidden;
}

.credits-fill {
  display: block;
  height: 100%;
  background: var(--color-info);
  border-radius: 4px;
  transition: width 360ms ease;
}

.th-actions,
.td-actions {
  width: 210px;
  text-align: center;
  white-space: nowrap;
}

.row-actions {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 0.35rem;
}

.empty-cell {
  padding: 2rem 1rem !important;
  text-align: center;
  color: var(--color-text-secondary);
}

.empty-catalog {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.6rem;
}

.pagination-bar {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 0.75rem;
  padding-top: 0.6rem;
  margin-top: auto;
  border-top: 1px solid var(--color-border);
  flex-wrap: wrap;
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

/* Columna Derecha Compacta */
.right-stack {
  display: flex;
  flex-direction: column;
  gap: 0.65rem;
  min-height: 0;
  overflow: hidden;
}

.side-card {
  padding: 10px 12px !important;
  border: 1px solid var(--color-border);
  background: var(--color-surface-white);
  display: flex;
  flex-direction: column;
  min-height: 0;
}

.side-card--codes {
  flex: 1;
}

.side-card__head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding-bottom: 0.45rem;
  margin-bottom: 0.45rem;
  border-bottom: 1px solid var(--color-border-light);
  flex-shrink: 0;
}

.side-card__title-group {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
}

.side-card__icon {
  width: 15px;
  height: 15px;
  fill: none;
  stroke: var(--color-accent);
  stroke-width: 2;
}

.side-card__title {
  margin: 0;
  font-size: 0.72rem;
  font-weight: 800;
  letter-spacing: 0.06em;
  color: var(--color-text-primary);
}

.side-card__meta,
.official-tag {
  font-size: 0.68rem;
  font-weight: 700;
  color: var(--color-text-secondary);
}

.rules-stack {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
}

.rule-card {
  padding: 0.5rem 0.65rem;
  border-radius: 7px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-light);
}

.rule-card__top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.4rem;
  margin-bottom: 0.2rem;
}

.rule-badge {
  font-size: 0.64rem;
  font-weight: 800;
  letter-spacing: 0.05em;
  padding: 0.08rem 0.38rem;
  border-radius: 4px;
}

.rule-badge--navy {
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.rule-badge--gold {
  background: var(--color-warning-badge);
  color: var(--color-warning-text);
  border: 1px solid var(--color-warning-border);
}

.rule-pct {
  font-size: 0.7rem;
  font-weight: 700;
  color: var(--color-info-text);
}

.rule-pct--gold {
  color: var(--color-warning-text);
}

.rule-title {
  margin: 0 0 0.15rem;
  font-size: 0.76rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.rule-desc {
  margin: 0;
  font-size: 0.71rem;
  line-height: 1.3;
  color: var(--color-text-secondary);
}

.quick-codes {
  display: flex;
  flex-direction: column;
  gap: 0.32rem;
  overflow-y: auto;
  min-height: 0;
  padding-right: 2px;
}

.code-chip-btn {
  display: flex;
  align-items: center;
  gap: 0.45rem;
  width: 100%;
  min-height: 30px !important;
  padding: 0.28rem 0.5rem;
  border-radius: 6px;
  border: 1px solid var(--color-border);
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  text-align: left;
  cursor: pointer;
  transition: background-color 150ms ease, border-color 150ms ease;
}

.code-chip-btn:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.code-chip-btn--selected {
  background: var(--kpi-active-bg);
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 1px var(--color-accent);
}

.code-chip-btn__sigla {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.68rem;
  font-weight: 800;
  padding: 0.08rem 0.35rem;
  border-radius: 4px;
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
  flex-shrink: 0;
}

.code-chip-btn__name {
  flex: 1;
  min-width: 0;
  font-size: 0.73rem;
  font-weight: 600;
  color: var(--color-text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.code-chip-btn__cr {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 0.7rem;
  font-weight: 700;
  color: var(--color-text-secondary);
  flex-shrink: 0;
}

/* Modales compactos */
.dialog-form {
  display: flex;
  flex-direction: column;
  gap: 0.7rem;
}

.dialog-grid-2 {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  align-items: start;
}

.dialog-form :deep(.field) {
  margin-bottom: 0;
  gap: 4px;
  font-size: 13px;
}

.dialog-form :deep(input) {
  min-height: 36px;
  padding: 6px 10px;
  font-size: 13px;
  outline: none;
}

.dialog-form :deep(input:focus) {
  outline: none;
  border-color: var(--color-border-focus);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--color-border-focus) 22%, transparent);
}

.credits-preview-strip {
  display: flex;
  flex-wrap: wrap;
  justify-content: space-between;
  gap: 0.5rem 1rem;
  padding: 0.55rem 0.75rem;
  border-radius: 7px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  font-size: 0.78rem;
  color: var(--color-text-secondary);
}

.credits-preview-strip strong {
  color: var(--color-text-primary);
}

.delete-confirm-copy {
  margin: 0;
  font-size: 0.88rem;
  line-height: 1.5;
  color: var(--color-text-primary);
}

.tray-row-enter-active,
.tray-row-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.tray-row-enter-from,
.tray-row-leave-to {
  opacity: 0;
  transform: translateY(4px);
}

@media (max-width: 1100px) {
  .workspace-grid {
    grid-template-columns: minmax(0, 1fr);
  }
  .right-stack {
    display: none;
  }
}

@media (max-width: 900px) {
  .fullscreen-page {
    height: auto !important;
    overflow: visible !important;
    padding: 4px 0 12px !important;
    min-width: 0;
    max-width: 100%;
  }

  .workspace-grid {
    min-width: 0;
    max-width: 100%;
  }

  .tray-card {
    min-width: 0;
    max-width: 100%;
    overflow: hidden;
    padding: 10px 12px !important;
  }

  .tray-table-wrap {
    margin: 0 -12px;
    max-width: calc(100% + 24px);
    overflow-x: auto;
    -webkit-overflow-scrolling: touch;
  }

  .tray-table {
    min-width: 600px;
  }
}

@media (max-width: 640px) {
  .exec-header {
    flex-direction: column;
    align-items: stretch;
    gap: 0.6rem;
  }

  .exec-header__left {
    min-width: 0;
  }

  .title-row {
    justify-content: space-between;
  }

  .exec-title {
    font-size: 1.05rem;
  }

  .exec-kpis {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    width: 100%;
    gap: 0.45rem;
  }

  .mini-kpi {
    min-width: 0;
    padding: 0.45rem 0.65rem;
  }

  .filters-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.6rem;
    padding: 0.6rem;
  }

  .search-box,
  .toolbar-right {
    min-width: 0;
    width: 100%;
    flex: 1 1 auto;
  }

  .status-tabs-row {
    flex-wrap: wrap;
    width: 100%;
  }

  .status-tabs {
    display: flex;
    flex: 1;
    width: 100%;
    height: auto;
    min-height: 36px;
  }

  .status-tab {
    flex: 1;
    min-height: 30px !important;
    height: 30px;
    text-align: center;
  }

  input.modern-input,
  .dialog-form :deep(input) {
    min-height: 40px;
    height: 40px;
    font-size: 16px;
  }

  .dialog-grid-2 {
    grid-template-columns: 1fr;
  }

  .pagination-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 0.5rem;
  }

  .pagination-info {
    text-align: center;
  }

  .pagination-controls {
    width: 100%;
  }

  .pagination-controls :deep(.btn) {
    flex: 1;
  }
}
</style>
