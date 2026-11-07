<script setup lang="ts">
import { computed, onBeforeUnmount, reactive, ref, watch } from 'vue'
import { UiAlert, UiBadge, UiBasePage, UiButton, UiCard, UiDialog, UiField, UiTable } from '../../shared/components'
import { hasRole } from '../../shared/composables/useSession'
import { deleteCompany, getCompanies, saveCompany, setCompanyStatus } from './api'
import { companyTypeLabels, type Company, type CompanyRequest, type CompanyType } from './types'
import CompanyRelations from './CompanyRelations.vue'

const canManage = computed(() => hasRole('admin', 'social_service_officer', 'internship_coordinator'))
const rows = ref<(Company & { actions: string })[]>([]), search = ref(''), type = ref(''), status = ref(''), page = ref(1), pages = ref(0)
const loading = ref(false), busy = ref(false), error = ref(''), formError = ref(''), open = ref(false), editingId = ref<number | null>(null)
const deleting = ref<Company | null>(null), selected = ref<Company | null>(null)
const blank = (): CompanyRequest => ({ name: '', rfc: null, commercialName: null, companyType: '', sector: 'private', companySize: 'not_applicable', isTecnmInternal: false, address: null, city: 'Monclova', state: 'Coahuila', postalCode: null, phoneNumber: null, email: null })
const form = reactive(blank())
const columns = [{ key: 'name', label: 'Empresa o dependencia' }, { key: 'companyType', label: 'Tipo' }, { key: 'city', label: 'Ciudad' }, { key: 'isActive', label: 'Estado' }, { key: 'actions', label: 'Acciones' }] as const
const typeOptions = Object.entries(companyTypeLabels).map(([value, label]) => ({ value, label }))
let controller: AbortController | undefined, timer: ReturnType<typeof setTimeout> | undefined, sequence = 0
async function load() {
  controller?.abort(); controller = new AbortController(); const request = ++sequence
  loading.value = true; error.value = ''
  try {
    const result = await getCompanies({ page: page.value, search: search.value || undefined, companyType: type.value || undefined, isActive: status.value ? status.value === 'true' : undefined }, controller.signal)
    if (request !== sequence) return
    rows.value = result.items.map(item => ({ ...item, actions: '' })); pages.value = result.totalPages
  } catch (e: any) { if (request === sequence && e.code !== 'ERR_CANCELED') { rows.value = []; error.value = e.response?.data?.detail || 'No se pudo cargar el catálogo.' } }
  finally { if (request === sequence) loading.value = false }
}
watch([search, type, status], () => { clearTimeout(timer); page.value = 1; timer = setTimeout(load, 250) })
watch(page, load)
void load()
onBeforeUnmount(() => { sequence++; clearTimeout(timer); controller?.abort() })
function edit(company?: Company) { editingId.value = company?.id ?? null; Object.assign(form, company ?? blank()); formError.value = ''; open.value = true }
async function save() {
  if (busy.value) return
  busy.value = true; formError.value = ''
  try {
    const privateType = ['micro', 'small', 'medium', 'large'].includes(form.companyType)
    const payload: CompanyRequest = { ...form, sector: form.sector === 'educational' ? 'educational' : privateType ? 'private' : form.companyType, companySize: privateType ? form.companyType : form.companySize }
    await saveCompany(editingId.value, payload); open.value = false; await load()
  } catch (e: any) { formError.value = Object.values(e.response?.data?.errors ?? {}).flat().join(' ') || e.response?.data?.detail || 'No se pudo guardar la empresa.' }
  finally { busy.value = false }
}
async function change(company: Company, remove = false) {
  if (busy.value) return
  busy.value = true; error.value = ''
  try { if (remove) await deleteCompany(company.id); else await setCompanyStatus(company.id, !company.isActive); deleting.value = null; await load() }
  catch (e: any) { error.value = e.response?.data?.detail || 'No se pudo actualizar la empresa.'; deleting.value = null }
  finally { busy.value = false }
}
function typeLabel(value: string) { return companyTypeLabels[value as CompanyType] }
</script>
<template>
  <UiBasePage :fluid="true">
    <header class="toolbar"><div><h1>Empresas y dependencias</h1><p>Directorio compartido de instituciones receptoras.</p></div><UiButton v-if="canManage" @click="edit()">Registrar empresa</UiButton></header>
    <UiCard>
      <div class="filters">
        <UiField v-model="search" label="Buscar por nombre o RFC" type="search" />
        <UiField v-model="type" label="Tipo de empresa" as="select" :options="[{ value: '', label: 'Todos los tipos' }, ...typeOptions]" />
        <UiField v-model="status" label="Estado" as="select" :options="[{ value: '', label: 'Todos' }, { value: 'true', label: 'Activas' }, { value: 'false', label: 'Inactivas' }]" />
      </div>
      <UiAlert v-if="error" tone="danger">{{ error }} <UiButton @click="load">Reintentar</UiButton></UiAlert>
      <p v-if="loading" role="status">Cargando empresas…</p>
      <template v-else>
        <UiTable label="Catálogo de empresas" :columns="[...columns]" :rows="rows">
          <template #cell-companyType="{ value }">{{ typeLabel(String(value)) }}</template>
          <template #cell-isActive="{ row }"><UiBadge>{{ row.isActive ? 'Activa' : 'Inactiva' }}</UiBadge></template>
          <template #cell-actions="{ row }"><div class="actions">
            <UiButton variant="secondary" @click="selected = row">Supervisores y convenios</UiButton>
            <template v-if="canManage"><UiButton variant="secondary" @click="edit(row)">Editar</UiButton><UiButton variant="secondary" :disabled="busy" @click="change(row)">{{ row.isActive ? 'Desactivar' : 'Activar' }}</UiButton><UiButton variant="danger" @click="deleting = row">Eliminar</UiButton></template>
          </div></template>
        </UiTable>
        <p v-if="!rows.length">No hay empresas con estos filtros.</p>
        <nav class="actions" aria-label="Páginas de empresas"><UiButton variant="secondary" :disabled="page <= 1" @click="page--">Anterior</UiButton><span>Página {{ page }} de {{ Math.max(pages, 1) }}</span><UiButton variant="secondary" :disabled="page >= pages" @click="page++">Siguiente</UiButton></nav>
      </template>
    </UiCard>
    <UiDialog v-model="open" :title="editingId ? 'Editar empresa' : 'Registrar empresa'" size="lg">
      <form class="company-form" @submit.prevent="save">
        <UiAlert v-if="formError" tone="danger">{{ formError }}</UiAlert>
        <UiField v-model="form.name" label="Nombre o razón social" required maxlength="200" />
        <UiField :model-value="form.rfc ?? ''" @update:model-value="form.rfc = $event" label="RFC" maxlength="13" />
        <UiField :model-value="form.commercialName ?? ''" @update:model-value="form.commercialName = $event" label="Nombre comercial" maxlength="200" />
        <UiField v-model="form.companyType" label="Tipo de empresa" as="select" required :options="[{ value: '', label: 'Selecciona un tipo' }, ...typeOptions]" />
        <label><input type="checkbox" :checked="form.sector === 'educational'" @change="form.sector = ($event.target as HTMLInputElement).checked ? 'educational' : 'private'" /> Institución educativa (reporte 911)</label>
        <label><input v-model="form.isTecnmInternal" type="checkbox" /> Dependencia interna TecNM</label>
        <UiField :model-value="form.address ?? ''" @update:model-value="form.address = $event" label="Domicilio" />
        <div class="filters"><UiField v-model="form.city" label="Ciudad" required maxlength="100" /><UiField v-model="form.state" label="Estado" required maxlength="100" /></div>
        <UiField :model-value="form.postalCode ?? ''" @update:model-value="form.postalCode = $event" label="Código postal" pattern="[0-9]{5}" />
        <UiField :model-value="form.phoneNumber ?? ''" @update:model-value="form.phoneNumber = $event" label="Teléfono" type="tel" maxlength="30" />
        <UiField :model-value="form.email ?? ''" @update:model-value="form.email = $event" label="Correo electrónico" type="email" maxlength="255" />
        <UiButton type="submit" :loading="busy">Guardar empresa</UiButton>
      </form>
    </UiDialog>
    <UiDialog :model-value="!!deleting" @update:model-value="!$event && (deleting = null)" title="Eliminar empresa"><p>¿Eliminar {{ deleting?.name }} del catálogo? Si tiene vínculos, el sistema impedirá eliminarla.</p><UiButton variant="danger" :loading="busy" @click="deleting && change(deleting, true)">Confirmar eliminación</UiButton></UiDialog>
    <UiDialog :model-value="!!selected" @update:model-value="!$event && (selected = null)" :title="selected?.name ?? 'Empresa'" size="xl"><CompanyRelations v-if="selected" :key="selected.id" :company-id="selected.id" :can-manage="canManage" /></UiDialog>
  </UiBasePage>
</template>
<style scoped>
.toolbar,.actions{display:flex;gap:.75rem;align-items:center;flex-wrap:wrap}.toolbar{justify-content:space-between;margin-bottom:1.5rem}.filters{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,14rem),1fr));gap:1rem;margin-bottom:1rem}.company-form{display:grid;gap:1rem}.actions{margin:.5rem 0}h1{margin:0}p{color:var(--color-text-secondary)}label{display:flex;gap:.75rem;align-items:center;min-height:44px}
</style>
