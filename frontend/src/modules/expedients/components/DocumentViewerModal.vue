<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { UiDialog, UiButton, UiAlert } from '../../../shared/components'
const open = defineModel<boolean>({ default: false })
const props = defineProps<{ title: string; blob: Blob | null; confirmUpload?: boolean; busy?: boolean; error?: string }>()
const emit = defineEmits<{ confirm: [] }>()
const url = ref('')
function release() { if (url.value) URL.revokeObjectURL(url.value); url.value = '' }
watch([open, () => props.blob], () => { release(); if (open.value && props.blob) url.value = URL.createObjectURL(props.blob) })
onBeforeUnmount(release)
</script>
<template>
  <UiDialog v-model="open" :title="title" size="xl">
    <UiAlert v-if="error">{{ error }}</UiAlert>
    <iframe v-if="url" :src="url" :title="title" style="width:100%;height:70vh;border:0" />
    <UiAlert v-else>No se pudo abrir el PDF.</UiAlert>
    <template #footer>
      <UiButton v-if="confirmUpload" :disabled="busy || !url" @click="emit('confirm')">{{ busy ? 'Enviando…' : 'Confirmar y enviar a revisión' }}</UiButton>
      <a v-if="url && !confirmUpload" :href="url" :download="`${title}.pdf`" style="margin-right:auto">Descargar PDF</a>
      <UiButton :disabled="busy" @click="open = false">{{ confirmUpload ? 'Cancelar' : 'Cerrar' }}</UiButton>
    </template>
  </UiDialog>
</template>
