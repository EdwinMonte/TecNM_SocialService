<script setup lang="ts">
import { problemMessage } from '../../shared/api/problemDetails'
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../shared/composables/useFormValidation'
import { validateFields, optionalEmail, dateOrder } from '../../shared/utils/validationRules'
import UiFormErrors from '../../shared/components/UiFormErrors.vue'
import { onBeforeUnmount, ref, reactive } from 'vue'
import { http } from '../../shared/api/http'
import { UiAlert, UiBadge, UiButton, UiDialog, UiField } from '../../shared/components'
import type { Page } from './types'

interface Supervisor {
  id: number
  fullName: string
  position: string
  department: string | null
  email: string | null
  phoneNumber: string | null
  isSignatory: boolean
  isActive: boolean
}

interface Agreement {
  id: number
  agreementNumber: string
  agreementType: string
  description: string | null
  signedDate: string
  expirationDate: string
  isActive: boolean
  hasDocument: boolean
  validity: string
}

const props = defineProps<{ companyId: number; canManage: boolean }>()

const supervisors = ref<Supervisor[]>([])
const agreements = ref<Agreement[]>([])
const page = ref(1)
const pages = ref(0)
const error = ref('')
const formError = ref('')
const loading = ref(false)
const busy = ref(false)

const kind = ref<'supervisor' | 'agreement'>('supervisor')
const editingId = ref<number | null>(null)
const open = ref(false)

const supervisorForm = reactive({
  fullName: '',
  position: '',
  department: '',
  email: '',
  phoneNumber: '',
  isSignatory: true,
})

const agreementForm = reactive({
  agreementNumber: '',
  agreementType: 'general_framework',
  description: '',
  signedDate: '',
  expirationDate: '',
})

const deleting = ref<{ id: number; kind: 'supervisor' | 'agreement' } | null>(null)

const agreementTypes = [
  { value: 'general_framework', label: 'Convenio general' },
  { value: 'social_service', label: 'Servicio Social' },
  { value: 'internship', label: 'Prácticas Profesionales' },
  { value: 'dual_education', label: 'Educación dual' },
  { value: 'collaboration', label: 'Colaboración' },
]

const validityLabels: Record<string, string> = {
  valid: 'Vigente',
  expiring: 'Próximo a vencer',
  expired: 'Vencido',
  inactive: 'Inactivo',
}

const validityTones: Record<string, 'success' | 'warning' | 'danger' | 'info'> = {
  valid: 'success',
  expiring: 'warning',
  expired: 'danger',
  inactive: 'info',
}

const controller = new AbortController()
onBeforeUnmount(() => controller.abort())

function path(entity: 'supervisor' | 'agreement', id?: number): string {
  return (
    (entity === 'supervisor' ? `/companies/${props.companyId}/supervisors` : '/agreements') +
    (id ? `/${id}` : '')
  )
}

async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    const [s, a] = await Promise.all([
      http.get<Supervisor[]>(path('supervisor'), { signal: controller.signal }),
      http.get<Page<Agreement>>(path('agreement'), {
        params: { companyId: props.companyId, page: page.value, pageSize: 10 },
        signal: controller.signal,
      }),
    ])
    supervisors.value = s.data
    agreements.value = a.data.items
    pages.value = a.data.totalPages
  } catch (e: any) {
    if (e.code !== 'ERR_CANCELED') {
      error.value = problemMessage(e, 'No se pudieron cargar supervisores y convenios.')
    }
  } finally {
    loading.value = false
  }
}
void load()

function editSupervisor(item?: Supervisor): void {
  kind.value = 'supervisor'
  editingId.value = item?.id ?? null
  Object.assign(
    supervisorForm,
    item ?? {
      fullName: '',
      position: '',
      department: '',
      email: '',
      phoneNumber: '',
      isSignatory: true,
    },
  )
  formError.value = ''
  open.value = true
}

function editAgreement(item?: Agreement): void {
  kind.value = 'agreement'
  editingId.value = item?.id ?? null
  Object.assign(
    agreementForm,
    item ?? {
      agreementNumber: '',
      agreementType: 'general_framework',
      description: '',
      signedDate: '',
      expirationDate: '',
    },
  )
  formError.value = ''
  open.value = true
}

async function save(): Promise<void> {
  if (!(kind.value === 'supervisor' ? supervisorValidation : agreementValidation).validate()) {
    return
  }
  if (busy.value) return
  busy.value = true
  formError.value = ''
  try {
    const payload =
      kind.value === 'supervisor'
        ? supervisorForm
        : { ...agreementForm, companyId: props.companyId }
    if (editingId.value) {
      await http.put(path(kind.value, editingId.value), payload)
    } else {
      await http.post(path(kind.value), payload)
    }
    open.value = false
    await load()
  } catch (e: any) {
    ;(kind.value === 'supervisor' ? supervisorValidation : agreementValidation).apply(
      e,
      'No se pudo guardar el formulario.',
    )
    formError.value =
      Object.values(e.response?.data?.errors ?? {})
        .flat()
        .join(' ') || problemMessage(e, 'No se pudo guardar el registro.')
  } finally {
    busy.value = false
  }
}

async function mutate(
  entity: 'supervisor' | 'agreement',
  id: number,
  active?: boolean,
): Promise<void> {
  if (busy.value) return
  busy.value = true
  error.value = ''
  try {
    if (active === undefined) {
      await http.delete(path(entity, id))
    } else {
      await http.patch(`${path(entity, id)}/status`, { isActive: active })
    }
    deleting.value = null
    await load()
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo modificar el registro.')
    deleting.value = null
  } finally {
    busy.value = false
  }
}

async function upload(id: number, event: Event): Promise<void> {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  if (!file || busy.value) return
  busy.value = true
  error.value = ''
  try {
    if (file.size > 10 * 1024 * 1024 || !file.name.toLowerCase().endsWith('.pdf')) {
      throw new Error('Adjunta un PDF de hasta 10 MiB.')
    }
    const body = new FormData()
    body.append('file', file)
    await http.put(`${path('agreement', id)}/document`, body)
    await load()
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo incorporar el convenio.')
  } finally {
    busy.value = false
    input.value = ''
  }
}

async function download(item: Agreement): Promise<void> {
  error.value = ''
  try {
    const { data } = await http.get<Blob>(`${path('agreement', item.id)}/document`, {
      responseType: 'blob',
    })
    const url = URL.createObjectURL(data)
    const link = document.createElement('a')
    link.href = url
    link.download = `Convenio_${item.id}.pdf`
    link.click()
    setTimeout(() => URL.revokeObjectURL(url), 1000)
  } catch (e: any) {
    error.value = problemMessage(e, 'No se pudo descargar el convenio.')
  }
}

const supervisorValidation = useFormValidation(() =>
  validateFields(supervisorForm, {
    fullName: { required: true, max: 180 },
    position: { required: true, max: 150 },
    department: { max: 150 },
    email: optionalEmail,
    phoneNumber: { max: 30 },
  }),
)

const agreementValidation = useFormValidation(() =>
  dateOrder(
    validateFields(agreementForm, {
      agreementNumber: { required: true, max: 64 },
    }),
    agreementForm,
    'signedDate',
    'expirationDate',
  ),
)

watchValidation(
  () => [open.value, kind.value, editingId.value],
  () => {
    supervisorValidation.reset()
    agreementValidation.reset()
  },
)
</script>

<template>
  <UiAlert v-if="error" tone="danger" class="compact-alert">
    <span>{{ error }}</span>
    <UiButton variant="secondary" size="sm" @click="load">Reintentar</UiButton>
  </UiAlert>

  <p v-if="loading" role="status" class="loading-text">Cargando registros…</p>

  <div v-else class="relations-grid">
    <!-- Columna 1: Supervisores -->
    <section class="relation-panel">
      <div class="panel-head">
        <h3 class="panel-title">Supervisores ({{ supervisors.length }})</h3>
        <UiButton v-if="canManage" variant="primary" size="sm" @click="editSupervisor()">
          + Registrar
        </UiButton>
      </div>

      <div class="panel-list">
        <article v-for="item in supervisors" :key="item.id" class="relation-item">
          <div class="relation-top">
            <div class="relation-info">
              <strong class="item-title">{{ item.fullName }}</strong>
              <p class="item-meta">
                {{ item.position }} · {{ item.email || 'Sin correo' }}
              </p>
            </div>
            <UiBadge :tone="item.isActive ? 'success' : 'warning'">
              {{ item.isActive ? 'Activo' : 'Inactivo' }}
            </UiBadge>
          </div>
          <div v-if="canManage" class="actions-row">
            <UiButton variant="secondary" size="sm" @click="editSupervisor(item)">
              Editar
            </UiButton>
            <UiButton
              variant="secondary"
              size="sm"
              :disabled="busy"
              @click="mutate('supervisor', item.id, !item.isActive)"
            >
              {{ item.isActive ? 'Desactivar' : 'Activar' }}
            </UiButton>
            <UiButton
              variant="danger"
              size="sm"
              @click="deleting = { id: item.id, kind: 'supervisor' }"
            >
              Eliminar
            </UiButton>
          </div>
        </article>
        <p v-if="!supervisors.length" class="empty-msg">No hay supervisores registrados.</p>
      </div>
    </section>

    <!-- Columna 2: Convenios -->
    <section class="relation-panel">
      <div class="panel-head">
        <h3 class="panel-title">Convenios ({{ agreements.length }})</h3>
        <UiButton v-if="canManage" variant="primary" size="sm" @click="editAgreement()">
          + Registrar
        </UiButton>
      </div>

      <div class="panel-list">
        <article v-for="item in agreements" :key="item.id" class="relation-item">
          <div class="relation-top">
            <div class="relation-info">
              <strong class="item-title mono">{{ item.agreementNumber }}</strong>
              <p class="item-meta">{{ item.signedDate }} a {{ item.expirationDate }}</p>
            </div>
            <UiBadge :tone="validityTones[item.validity] ?? 'info'">
              {{ validityLabels[item.validity] || item.validity }}
            </UiBadge>
          </div>
          <div class="actions-row">
            <UiButton
              v-if="item.hasDocument"
              variant="secondary"
              size="sm"
              @click="download(item)"
            >
              Descargar PDF
            </UiButton>
            <template v-if="canManage">
              <UiButton variant="secondary" size="sm" @click="editAgreement(item)">
                Editar
              </UiButton>
              <UiButton
                variant="secondary"
                size="sm"
                :disabled="busy"
                @click="mutate('agreement', item.id, !item.isActive)"
              >
                {{ item.isActive ? 'Desactivar' : 'Activar' }}
              </UiButton>
              <UiButton
                variant="danger"
                size="sm"
                @click="deleting = { id: item.id, kind: 'agreement' }"
              >
                Eliminar
              </UiButton>
              <label :for="`agreement-file-${item.id}`" class="upload-pill">
                Incorporar PDF
                <input
                  :id="`agreement-file-${item.id}`"
                  type="file"
                  accept="application/pdf"
                  class="hidden-file-input"
                  :disabled="busy"
                  @change="upload(item.id, $event)"
                />
              </label>
            </template>
          </div>
        </article>
        <p v-if="!agreements.length" class="empty-msg">No hay convenios registrados.</p>
      </div>

      <nav v-if="pages > 1" class="mini-pagination" aria-label="Páginas de convenios">
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="page <= 1 || loading"
          @click="page--; load()"
        >
          Anterior
        </UiButton>
        <span class="page-indicator">{{ page }} / {{ Math.max(pages, 1) }}</span>
        <UiButton
          variant="secondary"
          size="sm"
          :disabled="page >= pages || loading"
          @click="page++; load()"
        >
          Siguiente
        </UiButton>
      </nav>
    </section>
  </div>

  <!-- Submodal: Alta / Edición de Supervisor o Convenio -->
  <UiDialog
    v-model="open"
    :title="kind === 'supervisor' ? 'Datos del supervisor' : 'Datos del convenio'"
    size="lg"
  >
    <form novalidate class="submodal-form" @submit.prevent="save">
      <UiAlert
        v-if="formError && !(supervisorValidation.hasErrors.value || agreementValidation.hasErrors.value)"
        tone="danger"
      >
        {{ formError }}
      </UiAlert>

      <template v-if="kind === 'supervisor'">
        <UiFormErrors :validation="supervisorValidation" />
        <UiField
          v-model="supervisorForm.fullName"
          label="Nombre completo"
          required
          maxlength="180"
          v-bind="supervisorValidation.field('fullName', 'Nombre completo')"
        />
        <div class="dates-grid">
          <UiField
            v-model="supervisorForm.position"
            label="Puesto"
            required
            maxlength="150"
            v-bind="supervisorValidation.field('position', 'Puesto')"
          />
          <UiField
            v-model="supervisorForm.department"
            label="Departamento"
            maxlength="150"
            v-bind="supervisorValidation.field('department', 'Departamento')"
          />
        </div>
        <div class="dates-grid">
          <UiField
            v-model="supervisorForm.email"
            label="Correo electrónico"
            type="email"
            v-bind="supervisorValidation.field('email', 'Correo electrónico')"
          />
          <UiField
            v-model="supervisorForm.phoneNumber"
            label="Teléfono"
            type="tel"
            v-bind="supervisorValidation.field('phoneNumber', 'Teléfono')"
          />
        </div>
        <label class="check-option">
          <input v-model="supervisorForm.isSignatory" type="checkbox" />
          <span>Puede firmar documentos</span>
        </label>
      </template>

      <template v-else>
        <UiFormErrors :validation="agreementValidation" />
        <div class="dates-grid">
          <UiField
            v-model="agreementForm.agreementNumber"
            label="Número de convenio"
            required
            maxlength="64"
            v-bind="agreementValidation.field('agreementNumber', 'Número de convenio')"
          />
          <UiField
            v-model="agreementForm.agreementType"
            label="Tipo de convenio"
            as="select"
            :options="agreementTypes"
            v-bind="agreementValidation.field('agreementType', 'Tipo de convenio')"
          />
        </div>
        <UiField
          v-model="agreementForm.description"
          label="Descripción"
          as="textarea"
          rows="2"
          v-bind="agreementValidation.field('description', 'Descripción')"
        />
        <div class="dates-grid">
          <UiField
            v-model="agreementForm.signedDate"
            label="Fecha de firma"
            type="date"
            required
            v-bind="agreementValidation.field('signedDate', 'Fecha de firma')"
          />
          <UiField
            v-model="agreementForm.expirationDate"
            label="Vencimiento"
            type="date"
            :min="agreementForm.signedDate"
            required
            v-bind="agreementValidation.field('expirationDate', 'Vencimiento')"
          />
        </div>
      </template>

      <div class="submodal-actions">
        <UiButton variant="secondary" size="sm" @click="open = false">Cancelar</UiButton>
        <UiButton type="submit" size="sm" :loading="busy">Guardar</UiButton>
      </div>
    </form>
  </UiDialog>

  <!-- Submodal: Confirmar Eliminación -->
  <UiDialog
    :model-value="!!deleting"
    @update:model-value="!$event && (deleting = null)"
    title="Eliminar registro"
    size="sm"
  >
    <p class="delete-msg">
      ¿Eliminar este registro? Los vínculos existentes pueden impedir su eliminación.
    </p>
    <template #footer>
      <UiButton variant="secondary" size="sm" @click="deleting = null">Cancelar</UiButton>
      <UiButton
        variant="danger"
        size="sm"
        :loading="busy"
        @click="deleting && mutate(deleting.kind, deleting.id)"
      >
        Confirmar eliminación
      </UiButton>
    </template>
  </UiDialog>
</template>

<style scoped>
.compact-alert {
  margin-bottom: 0.75rem;
}

.loading-text {
  margin: 0.5rem 0;
  font-size: 0.85rem;
  color: var(--color-text-secondary);
}

.relations-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.85rem;
  align-items: start;
}

.relation-panel {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
  padding: 0.8rem 0.9rem;
  border-radius: 10px;
  background: var(--surface-toolbar);
  border: 1px solid var(--color-border);
  min-width: 0;
}

.panel-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  padding-bottom: 0.45rem;
  border-bottom: 1px solid var(--color-border-light);
}

.panel-title {
  margin: 0;
  font-size: 0.92rem;
  font-weight: 700;
  color: var(--color-text-primary);
}

.panel-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.relation-item {
  display: flex;
  flex-direction: column;
  gap: 0.45rem;
  padding: 0.6rem 0.75rem;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
}

.relation-top {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.5rem;
}

.relation-info {
  min-width: 0;
  flex: 1;
}

.item-title {
  display: block;
  font-size: 0.84rem;
  font-weight: 700;
  color: var(--color-text-primary);
  line-height: 1.25;
}

.item-title.mono {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
}

.item-meta {
  margin: 0.15rem 0 0;
  font-size: 0.76rem;
  color: var(--color-text-secondary);
  overflow-wrap: anywhere;
}

.actions-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 0.35rem;
}

.upload-pill {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 28px;
  padding: 0 0.65rem;
  border-radius: 6px;
  border: 1px dashed var(--color-border-medium);
  background: var(--color-bg-light);
  color: var(--color-text-primary);
  font-size: 0.74rem;
  font-weight: 600;
  cursor: pointer;
  transition: background-color 140ms ease, border-color 140ms ease;
}

.upload-pill:hover {
  background: var(--secondary-hover);
  border-color: var(--color-accent);
}

.hidden-file-input {
  display: none;
}

.empty-msg {
  margin: 0.35rem 0;
  font-size: 0.8rem;
  color: var(--color-text-secondary);
}

.mini-pagination {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.4rem;
  border-top: 1px solid var(--color-border-light);
}

.page-indicator {
  font-size: 0.76rem;
  color: var(--color-text-secondary);
}

.submodal-form {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
}

.submodal-form :deep(.field) {
  margin-bottom: 0;
  gap: 4px;
  font-size: 13px;
}

.submodal-form :deep(input:not([type='checkbox']):not([type='radio'])),
.submodal-form :deep(select),
.submodal-form :deep(textarea) {
  min-height: 36px;
  padding: 6px 10px;
  font-size: 13px;
}

.dates-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 0.75rem;
  align-items: start;
}

.check-option {
  display: inline-flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.84rem;
  color: var(--color-text-primary);
  cursor: pointer;
}

.submodal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
  padding-top: 0.5rem;
  border-top: 1px solid var(--color-border-light);
}

.delete-msg {
  margin: 0;
  font-size: 0.88rem;
  color: var(--color-text-primary);
}

@media (max-width: 780px) {
  .relations-grid,
  .dates-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 640px) {
  .actions-row :deep(.btn),
  .upload-pill {
    flex: 1 1 auto;
    min-height: 34px;
    justify-content: center;
  }

  .submodal-form :deep(input:not([type='checkbox']):not([type='radio'])),
  .submodal-form :deep(select),
  .submodal-form :deep(textarea) {
    min-height: 40px;
    font-size: 16px;
  }

  .submodal-actions :deep(.btn) {
    flex: 1;
    min-height: 38px;
  }
}
</style>
