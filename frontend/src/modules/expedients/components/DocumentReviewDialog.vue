<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { UiAlert, UiButton, UiDialog, UiField } from '../../../shared/components'
import { problemMessage } from '../../../shared/api/problemDetails'
import { getDocumentBlob, reviewDocument } from '../api/expedientsApi'
import type { ExpedientDocument } from '../types/expedients'

const open = defineModel<boolean>({ default: false })
const props = defineProps<{ document: ExpedientDocument | null; studentName?: string }>()
const emit = defineEmits<{ reviewed: [document: ExpedientDocument] }>()
const imssVerified = ref(false)
const reason = ref(''), error = ref(''), dateError = ref(''), reasonError = ref('')
const url = ref(''), loading = ref(false), submitting = ref(false)
let sequence = 0
function release() { if (url.value) URL.revokeObjectURL(url.value); url.value = '' }
watch([open, () => props.document?.id], async () => {
  const request = ++sequence
  release()
  imssVerified.value = false; reason.value = ''; error.value = ''; dateError.value = ''; reasonError.value = ''
  if (!open.value || !props.document) return
  const document = props.document
  reason.value = document.rejectionReason || ''
  loading.value = true
  try {
    const blob = await getDocumentBlob(document.id)
    if (request !== sequence) return
    url.value = URL.createObjectURL(blob)
  } catch (failure) {
    if (request !== sequence) return
    error.value = problemMessage(failure, 'No fue posible abrir el PDF. Cierra y vuelve a intentar.')
  }
  loading.value = false
}, { immediate: true })
onBeforeUnmount(() => { sequence++; release() })

async function submit(status: 'approved' | 'rejected') {
  const document = props.document
  if (!document || document.status !== 'pending' || submitting.value || loading.value) return
  error.value = ''; dateError.value = ''; reasonError.value = ''
  if (status === 'approved' && document.documentCode === 'IMSS' && !imssVerified.value) dateError.value = 'Confirma que el PDF corresponde al alumno y muestra estado VIGENTE.'
  if (status === 'rejected' && !reason.value.trim()) reasonError.value = 'Indica el motivo de rechazo.'
  if (dateError.value || reasonError.value) return
  submitting.value = true
  try {
    const reviewed = await reviewDocument(document.id, { status,
      rejectionReason: status === 'rejected' ? reason.value.trim() : undefined })
    emit('reviewed', reviewed)
    open.value = false
  } catch (failure: any) {
    error.value = problemMessage(failure, 'No fue posible registrar el dictamen.')
    dateError.value = failure?.response?.data?.errors?.documentDate?.join(' ') || ''
    reasonError.value = failure?.response?.data?.errors?.rejectionReason?.join(' ') || ''
  } finally { submitting.value = false }
}
</script>

<template>
  <UiDialog v-model="open" :title="`Revisar ${document?.documentCode || 'documento'}${studentName ? ` · ${studentName}` : ''}`" size="xl">
    <div class="review-content" :aria-busy="loading || submitting">
      <UiAlert v-if="error" tone="danger" role="alert">{{ error }}</UiAlert>
      <p v-if="loading" role="status">Cargando documento…</p>
      <iframe v-if="url" :src="url" title="PDF que se está revisando" class="review-pdf" />
      <a v-if="url" :href="url" :download="document?.originalFileName">Descargar PDF</a>
      <p v-if="document?.status !== 'pending'">Dictamen: {{ document?.status === 'approved' ? 'Aprobado' : document?.status === 'rejected' ? 'Rechazado' : 'Sustituido' }}. {{ document?.rejectionReason }}</p>
      <template v-if="document?.documentCode === 'IMSS'">
        <template v-if="document.status === 'pending'">
          <p>Verifica los datos del alumno y que la constancia muestre estado VIGENTE.</p>
          <label><input v-model="imssVerified" type="checkbox" :disabled="loading || submitting" /> Confirmo que el PDF corresponde al alumno y muestra estado VIGENTE.</label>
          <UiAlert v-if="dateError" tone="danger" role="alert">{{ dateError }}</UiAlert>
        </template>
      </template>
      <UiField v-if="document?.status === 'pending'" v-model="reason" as="textarea" label="Motivo de rechazo (obligatorio para rechazar)" :error="reasonError" maxlength="1000" :disabled="submitting" />
    </div>
    <template #footer>
      <div class="review-actions">
      <UiButton variant="secondary" :disabled="submitting" @click="open = false">Cerrar</UiButton>
      <template v-if="document?.status === 'pending'">
        <UiButton variant="danger" :disabled="loading || submitting || !url" @click="submit('rejected')">Rechazar documento</UiButton>
        <UiButton :disabled="loading || submitting || !url" :loading="submitting" @click="submit('approved')">{{ 'Aprobar documento' }}</UiButton>
      </template>
      </div>
    </template>
  </UiDialog>
</template>

<style scoped>
.review-content { display: grid; gap: 1rem; }
.review-pdf { width: 100%; height: min(48vh, 32rem); border: 1px solid var(--color-border); }
.review-actions { display: flex; flex-wrap: wrap; justify-content: flex-end; gap: .75rem; width: 100%; }
@media (max-width: 600px) { .review-actions { flex-direction: column; align-items: stretch; } }
</style>
