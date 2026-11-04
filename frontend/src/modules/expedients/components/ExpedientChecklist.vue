<script setup lang="ts">
import { ref } from 'vue'
import { UiCard, UiBadge, UiButton, UiAlert } from '../../../shared/components'
import { http } from '../../../shared/api/http'
import { getDocumentBlob, uploadMyDocument, uploadStudentDocument } from '../api/expedientsApi'
import type { ExpedientDocument, DocumentRequirement } from '../types/expedients'
import DocumentViewerModal from './DocumentViewerModal.vue'
const props = defineProps<{ requirements: DocumentRequirement[]; documents: ExpedientDocument[]; tramiteType: 'social_service' | 'internship'; tramiteId: number; studentId?: number; canEdit?: boolean }>()
const emit = defineEmits<{ changed: []; edit: [key: string] }>()
const error = ref(''), busy = ref(''), title = ref(''), blob = ref<Blob | null>(null), open = ref(false)
function current(code: string) { return props.documents.find(d => d.documentCode === code && d.status !== 'superseded') }
async function view(item: DocumentRequirement, uploaded = false) {
  error.value = ''; busy.value = item.code
  try {
    const doc = current(item.code)
    blob.value = uploaded && doc ? await getDocumentBlob(doc.id) : (await http.get(
      item.templateKey ? `/document-templates/${item.templateKey}/file` : `/${props.tramiteType === 'internship' ? 'internships' : 'social-services'}/${props.tramiteId}/pdf/${item.pdfKey}`, { responseType: 'blob' })).data
    title.value = item.title; open.value = true
  } catch { error.value = 'El PDF no está disponible. Revisa los datos guardados y vuelve a intentar.' }
  finally { busy.value = '' }
}
async function upload(item: DocumentRequirement, event: Event) {
  const input = event.target as HTMLInputElement, file = input.files?.[0]
  if (!file) return
  error.value = ''; busy.value = item.code
  try {
    if (!file.name.toLowerCase().endsWith('.pdf') || file.size > 5 * 1024 * 1024) throw new Error('Adjunta un PDF de hasta 5 MiB.')
    if (props.studentId) {
      const data = new FormData(); data.append('file', file); data.append('tramiteType', props.tramiteType); data.append('tramiteId', String(props.tramiteId)); data.append('documentCode', item.code)
      await uploadStudentDocument(props.studentId, data)
    } else await uploadMyDocument({ file, tramiteType: props.tramiteType, tramiteId: props.tramiteId, documentCode: item.code })
    emit('changed')
  } catch (e: any) { error.value = e.response?.data?.detail || e.message || 'No se pudo cargar el PDF.' }
  finally { busy.value = ''; input.value = '' }
}
</script>
<template>
  <UiCard title="Expediente documental">
    <UiAlert v-if="error">{{ error }}</UiAlert>
    <article v-for="item in requirements" :key="item.code" style="padding:1rem 0;border-bottom:1px solid var(--color-border)">
      <strong>{{ item.title }}</strong> <UiBadge>{{ current(item.code)?.status === 'approved' ? 'Aprobado' : current(item.code)?.status === 'rejected' ? 'Rechazado' : current(item.code) ? 'En revisión' : 'Pendiente' }}</UiBadge>
      <p v-if="current(item.code)?.rejectionReason">{{ current(item.code)?.rejectionReason }}</p>
      <p v-if="item.blockedReason">{{ item.blockedReason }}</p>
      <div style="display:flex;gap:.75rem;flex-wrap:wrap;margin-top:.75rem">
        <UiButton v-if="canEdit !== false && current(item.code)?.status !== 'approved' && (item.pdfKey === 'commitment' || item.pdfKey?.startsWith('report_') || item.pdfKey === 'evaluation')" variant="secondary" @click="emit('edit', item.pdfKey!)">Editar datos del formato</UiButton>
        <UiButton v-if="item.pdfKey || item.templateKey" :disabled="!!busy" @click="view(item)">{{ item.templateKey ? 'Descargar plantilla' : 'Descargar prellenado' }}</UiButton>
        <UiButton v-if="current(item.code)" variant="secondary" @click="view(item, true)">Consultar PDF</UiButton>
        <label v-if="item.canUpload && current(item.code)?.status !== 'approved'">Subir PDF firmado <input type="file" accept="application/pdf" :disabled="!!busy" @change="upload(item, $event)" /></label>
      </div>
    </article>
    <DocumentViewerModal v-model="open" :title="title" :blob="blob" />
  </UiCard>
</template>
