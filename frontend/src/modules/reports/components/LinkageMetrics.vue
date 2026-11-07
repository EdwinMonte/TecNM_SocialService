<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { http } from '../../../shared/api/http'
import { UiAlert, UiButton, UiCard, UiTable } from '../../../shared/components'
import { companyTypeLabels, type CompanyType, type Page } from '../../companies/types'
interface Counts { assignedStudents: number; activeStudents: number; projects: number }
interface TypeCounts extends Counts { companyType: CompanyType }
interface CompanyCounts extends TypeCounts { companyId: number; companyName: string }
interface LinkageResponse { tramiteType: string; totals: Counts; byCompanyType: TypeCounts[]; companies: Page<CompanyCounts> }
const props = defineProps<{ tramiteType: 'social_service' | 'internship'; periodId?: number }>()
const data = ref<LinkageResponse | null>(null), page = ref(1), loading = ref(false), error = ref('')
let sequence = 0, controller: AbortController | undefined
const rows = computed(() => data.value?.companies.items.map(row => ({ ...row, id: row.companyId })) ?? [])
const columns = [{ key:'companyName',label:'Empresa' }, {key:'companyType',label:'Tipo'}, {key:'projects',label:'Proyectos'}, {key:'assignedStudents',label:'Asignados'}, {key:'activeStudents',label:'Activos'}] as const
async function load() {
  controller?.abort();controller=new AbortController();const request=++sequence
  loading.value=true;error.value='';data.value=null
  try {const response=await http.get<LinkageResponse>('/reports/linkage',{params:{tramiteType:props.tramiteType,periodId:props.periodId,page:page.value,pageSize:20},signal:controller.signal});if(request===sequence)data.value=response.data}
  catch(e:any){if(request===sequence && e.code!=='ERR_CANCELED')error.value=e.response?.data?.detail || 'No se pudieron cargar las métricas por empresa.'}
  finally{if(request===sequence)loading.value=false}
}
watch(()=>[props.tramiteType,props.periodId],()=>{page.value=1;void load()},{immediate:true})
onBeforeUnmount(()=>{sequence++;controller?.abort()})
function share(count:number){return data.value?.totals.assignedStudents ? count/data.value.totals.assignedStudents*100 : 0}
</script>
<template>
  <UiCard title="Asignaciones y proyectos por empresa">
    <p>Trámites asignados vigentes. Cada trámite cuenta como un proyecto.</p>
    <UiAlert v-if="error" tone="danger">{{ error }} <UiButton @click="load">Reintentar</UiButton></UiAlert>
    <p v-if="loading" role="status">Calculando métricas…</p>
    <template v-if="data">
      <p><strong>{{ data.totals.assignedStudents }}</strong> alumnos asignados · <strong>{{ data.totals.activeStudents }}</strong> activos · <strong>{{ data.totals.projects }}</strong> proyectos</p>
      <ul class="distribution" aria-label="Distribución por tipo de empresa">
        <li v-for="item in data.byCompanyType" :key="item.companyType"><span>{{ companyTypeLabels[item.companyType] }}</span><span class="track" aria-hidden="true"><span :style="{width:`${share(item.assignedStudents)}%`}" /></span><strong>{{ item.assignedStudents }} ({{ share(item.assignedStudents).toFixed(1) }} %)</strong></li>
      </ul>
      <UiTable label="Proyectos y alumnos por empresa" :columns="[...columns]" :rows="rows"><template #cell-companyType="{row}">{{ companyTypeLabels[row.companyType] }}</template></UiTable>
      <p v-if="!rows.length">No hay asignaciones vigentes en este periodo.</p>
      <nav class="pagination" aria-label="Páginas de métricas"><UiButton variant="secondary" :disabled="page<=1" @click="page--;load()">Anterior</UiButton><span>{{ page }} / {{ Math.max(data.companies.totalPages,1) }}</span><UiButton variant="secondary" :disabled="page>=data.companies.totalPages" @click="page++;load()">Siguiente</UiButton></nav>
    </template>
  </UiCard>
</template>
<style scoped>
.distribution{list-style:none;padding:0;display:grid;gap:1rem;margin:1.5rem 0}.distribution li{display:grid;grid-template-columns:minmax(10rem,1fr) minmax(4rem,2fr) auto;gap:.75rem;align-items:center}.track{height:.75rem;background:var(--color-border);border-radius:.25rem;overflow:hidden}.track>span{display:block;height:100%;background:var(--color-primary)}.pagination{display:flex;gap:1rem;align-items:center;margin-top:1rem}@media(max-width:600px){.distribution li{grid-template-columns:1fr auto}.track{grid-column:1/-1;grid-row:auto}.distribution strong{font-size:.875rem}}
</style>
