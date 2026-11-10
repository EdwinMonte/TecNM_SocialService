<script setup lang="ts">
import { problemMessage } from '../../shared/api/problemDetails'
import { watch as watchValidation } from 'vue'
import { useFormValidation } from '../../shared/composables/useFormValidation'
import { validateFields, optionalEmail, dateOrder } from '../../shared/utils/validationRules'
import UiFormErrors from '../../shared/components/UiFormErrors.vue'
import { onBeforeUnmount, ref, reactive } from 'vue'
import { http } from '../../shared/api/http'
import { UiAlert, UiBadge, UiButton, UiCard, UiDialog, UiField } from '../../shared/components'
import type { Page } from './types'
interface Supervisor { id: number; fullName: string; position: string; department: string | null; email: string | null; phoneNumber: string | null; isSignatory: boolean; isActive: boolean }
interface Agreement { id: number; agreementNumber: string; agreementType: string; description: string | null; signedDate: string; expirationDate: string; isActive: boolean; hasDocument: boolean; validity: string }
const props = defineProps<{ companyId: number; canManage: boolean }>()
const supervisors = ref<Supervisor[]>([]), agreements = ref<Agreement[]>([]), page = ref(1), pages = ref(0), error = ref(''), formError = ref(''), loading = ref(false), busy = ref(false)
const kind = ref<'supervisor' | 'agreement'>('supervisor'), editingId = ref<number | null>(null), open = ref(false)
const supervisorForm = reactive({ fullName: '', position: '', department: '', email: '', phoneNumber: '', isSignatory: true })
const agreementForm = reactive({ agreementNumber: '', agreementType: 'general_framework', description: '', signedDate: '', expirationDate: '' })
const deleting = ref<{ id: number; kind: 'supervisor' | 'agreement' } | null>(null)
const agreementTypes = [{value:'general_framework',label:'Convenio general'}, {value:'social_service',label:'Servicio Social'}, {value:'internship',label:'Prácticas Profesionales'}, {value:'dual_education',label:'Educación dual'}, {value:'collaboration',label:'Colaboración'}]
const validityLabels: Record<string,string> = { valid: 'Vigente', expiring: 'Próximo a vencer', expired: 'Vencido', inactive: 'Inactivo' }
const controller = new AbortController()
onBeforeUnmount(() => controller.abort())
function path(entity: 'supervisor' | 'agreement', id?: number) { return (entity === 'supervisor' ? `/companies/${props.companyId}/supervisors` : '/agreements') + (id ? `/${id}` : '') }
async function load() {
  loading.value = true; error.value = ''
  try {
    const [s, a] = await Promise.all([http.get<Supervisor[]>(path('supervisor'), { signal: controller.signal }), http.get<Page<Agreement>>(path('agreement'), { params: {companyId: props.companyId, page: page.value, pageSize: 20}, signal: controller.signal })])
    supervisors.value = s.data; agreements.value = a.data.items; pages.value = a.data.totalPages
  } catch(e: any) { if (e.code !== 'ERR_CANCELED') error.value = problemMessage(e, 'No se pudieron cargar supervisores y convenios.') }
  finally { loading.value = false }
}
void load()
function editSupervisor(item?: Supervisor) { kind.value = 'supervisor'; editingId.value = item?.id ?? null; Object.assign(supervisorForm, item ?? {fullName:'',position:'',department:'',email:'',phoneNumber:'',isSignatory:true}); formError.value=''; open.value=true }
function editAgreement(item?: Agreement) { kind.value = 'agreement'; editingId.value = item?.id ?? null; Object.assign(agreementForm, item ?? {agreementNumber:'',agreementType:'general_framework',description:'',signedDate:'',expirationDate:''}); formError.value=''; open.value=true }
async function save() {
  if (!(kind.value === 'supervisor' ? supervisorValidation : agreementValidation).validate()) return

  if (busy.value) return
  busy.value=true; formError.value=''
  try {
    const payload = kind.value === 'supervisor' ? supervisorForm : {...agreementForm,companyId:props.companyId}
    if (editingId.value) await http.put(path(kind.value,editingId.value),payload); else await http.post(path(kind.value),payload)
    open.value=false; await load()
  } catch(e:any) {
    (kind.value === 'supervisor' ? supervisorValidation : agreementValidation).apply(e, 'No se pudo guardar el formulario.');
    formError.value=Object.values(e.response?.data?.errors ?? {}).flat().join(' ') || problemMessage(e, 'No se pudo guardar el registro.') }
  finally {busy.value=false}
}
async function mutate(entity:'supervisor'|'agreement',id:number,active?:boolean) {
  if (busy.value) return
  busy.value=true;error.value=''
  try { if(active === undefined) await http.delete(path(entity,id)); else await http.patch(`${path(entity,id)}/status`,{isActive:active}); deleting.value=null; await load() }
  catch(e:any){error.value=problemMessage(e, 'No se pudo modificar el registro.');deleting.value=null}
  finally {busy.value=false}
}
async function upload(id:number,event:Event) {
  const input=event.target as HTMLInputElement, file=input.files?.[0]; if(!file || busy.value)return
  busy.value=true;error.value=''
  try {if(file.size>10*1024*1024 || !file.name.toLowerCase().endsWith('.pdf'))throw new Error('Adjunta un PDF de hasta 10 MiB.');const body=new FormData();body.append('file',file);await http.put(`${path('agreement',id)}/document`,body);await load()}
  catch(e:any){error.value=problemMessage(e, 'No se pudo incorporar el convenio.')}
  finally{busy.value=false;input.value=''}
}
async function download(item:Agreement) {
  error.value=''
  try {const {data}=await http.get<Blob>(`${path('agreement',item.id)}/document`,{responseType:'blob'});const url=URL.createObjectURL(data);const link=document.createElement('a');link.href=url;link.download=`Convenio_${item.id}.pdf`;link.click();setTimeout(()=>URL.revokeObjectURL(url),1000)}
  catch(e:any){error.value=problemMessage(e, 'No se pudo descargar el convenio.')}
}
const supervisorValidation = useFormValidation(() => validateFields(supervisorForm,{fullName:{required:true,max:180},position:{required:true,max:150},department:{max:150},email:optionalEmail,phoneNumber:{max:30}}))
const agreementValidation = useFormValidation(() => dateOrder(validateFields(agreementForm,{agreementNumber:{required:true,max:64}}),agreementForm,'signedDate','expirationDate'))
watchValidation(() => [open.value,kind.value,editingId.value], () => { supervisorValidation.reset(); agreementValidation.reset() })
</script>
<template>
  <UiAlert v-if="error" tone="danger">{{ error }} <UiButton @click="load">Reintentar</UiButton></UiAlert>
  <p v-if="loading" role="status">Cargando registros…</p>
  <div v-else class="relations">
    <UiCard title="Supervisores"><UiButton v-if="canManage" @click="editSupervisor()">Registrar supervisor</UiButton>
      <article v-for="item in supervisors" :key="item.id"><h3>{{ item.fullName }}</h3><p>{{ item.position }} · {{ item.email || 'Sin correo' }}</p><UiBadge>{{ item.isActive ? 'Activo' : 'Inactivo' }}</UiBadge><div v-if="canManage" class="actions"><UiButton variant="secondary" @click="editSupervisor(item)">Editar</UiButton><UiButton variant="secondary" :disabled="busy" @click="mutate('supervisor',item.id,!item.isActive)">{{ item.isActive ? 'Desactivar' : 'Activar' }}</UiButton><UiButton variant="danger" @click="deleting={id:item.id,kind:'supervisor'}">Eliminar</UiButton></div></article>
      <p v-if="!supervisors.length">No hay supervisores registrados.</p>
    </UiCard>
    <UiCard title="Convenios"><UiButton v-if="canManage" @click="editAgreement()">Registrar convenio</UiButton>
      <article v-for="item in agreements" :key="item.id"><h3>{{ item.agreementNumber }}</h3><p>{{ item.signedDate }} a {{ item.expirationDate }}</p><UiBadge>{{ validityLabels[item.validity] }}</UiBadge><div class="actions"><UiButton v-if="item.hasDocument" variant="secondary" @click="download(item)">Descargar PDF</UiButton><template v-if="canManage"><UiButton variant="secondary" @click="editAgreement(item)">Editar</UiButton><UiButton variant="secondary" :disabled="busy" @click="mutate('agreement',item.id,!item.isActive)">{{ item.isActive ? 'Desactivar' : 'Activar' }}</UiButton><UiButton variant="danger" @click="deleting={id:item.id,kind:'agreement'}">Eliminar</UiButton><label :for="`agreement-file-${item.id}`">Incorporar PDF <input :id="`agreement-file-${item.id}`" type="file" accept="application/pdf" :disabled="busy" @change="upload(item.id,$event)" /></label></template></div></article>
      <p v-if="!agreements.length">No hay convenios registrados.</p><nav class="actions" aria-label="Páginas de convenios"><UiButton variant="secondary" :disabled="page<=1 || loading" @click="page--;load()">Anterior</UiButton><span>{{ page }} / {{ Math.max(pages,1) }}</span><UiButton variant="secondary" :disabled="page>=pages || loading" @click="page++;load()">Siguiente</UiButton></nav>
    </UiCard>
  </div>
  <UiDialog v-model="open" :title="kind==='supervisor' ? 'Datos del supervisor' : 'Datos del convenio'">
    <form novalidate @submit.prevent="save"><UiAlert v-if="formError && !(supervisorValidation.hasErrors.value || agreementValidation.hasErrors.value)" tone="danger">{{ formError }}</UiAlert>
      <template v-if="kind==='supervisor'"><UiFormErrors :validation="supervisorValidation" />
      <UiField v-model="supervisorForm.fullName" label="Nombre completo" required maxlength="180"  v-bind="supervisorValidation.field('fullName', 'Nombre completo')" /><UiField v-model="supervisorForm.position" label="Puesto" required maxlength="150"  v-bind="supervisorValidation.field('position', 'Puesto')" /><UiField v-model="supervisorForm.department" label="Departamento" maxlength="150"  v-bind="supervisorValidation.field('department', 'Departamento')" /><UiField v-model="supervisorForm.email" label="Correo electrónico" type="email"  v-bind="supervisorValidation.field('email', 'Correo electrónico')" /><UiField v-model="supervisorForm.phoneNumber" label="Teléfono" type="tel"  v-bind="supervisorValidation.field('phoneNumber', 'Teléfono')" /><label><input v-model="supervisorForm.isSignatory" type="checkbox" /> Puede firmar documentos</label></template>
      <template v-else><UiFormErrors :validation="agreementValidation" />
      <UiField v-model="agreementForm.agreementNumber" label="Número de convenio" required maxlength="64"  v-bind="agreementValidation.field('agreementNumber', 'Número de convenio')" /><UiField v-model="agreementForm.agreementType" label="Tipo de convenio" as="select" :options="agreementTypes"  v-bind="agreementValidation.field('agreementType', 'Tipo de convenio')" /><UiField v-model="agreementForm.description" label="Descripción" as="textarea"  v-bind="agreementValidation.field('description', 'Descripción')" /><UiField v-model="agreementForm.signedDate" label="Fecha de firma" type="date" required  v-bind="agreementValidation.field('signedDate', 'Fecha de firma')" /><UiField v-model="agreementForm.expirationDate" label="Vencimiento" type="date" :min="agreementForm.signedDate" required  v-bind="agreementValidation.field('expirationDate', 'Vencimiento')" /></template>
      <UiButton type="submit" :loading="busy">Guardar</UiButton>
    </form>
  </UiDialog>
  <UiDialog :model-value="!!deleting" @update:model-value="!$event && (deleting=null)" title="Eliminar registro"><p>¿Eliminar este registro? Los vínculos existentes pueden impedir su eliminación.</p><UiButton variant="danger" :loading="busy" @click="deleting && mutate(deleting.kind,deleting.id)">Confirmar eliminación</UiButton></UiDialog>
</template>
<style scoped>
.relations,form{display:grid;gap:1.25rem}.actions{display:flex;gap:.75rem;align-items:center;flex-wrap:wrap;margin:.75rem 0}article{padding:1rem 0;border-bottom:1px solid var(--color-border)}label{display:flex;gap:.75rem;align-items:center;min-height:44px}h3{margin-bottom:.25rem}
</style>
