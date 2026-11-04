<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { UiDialog, UiButton, UiAlert } from '../../../shared/components'
const open = defineModel<boolean>({ default: false })
const props = defineProps<{ title: string; blob: Blob | null }>()
const url = ref('')
function release() { if (url.value) URL.revokeObjectURL(url.value); url.value = '' }
watch([open, () => props.blob], () => { release(); if (open.value && props.blob) url.value = URL.createObjectURL(props.blob) })
onBeforeUnmount(release)
</script>
<template>
  <UiDialog v-model="open" :title="title" size="xl">
    <iframe v-if="url" :src="url" :title="title" style="width:100%;height:70vh;border:0" />
    <UiAlert v-else>No se pudo abrir el PDF.</UiAlert>
    <template #footer>
      <a v-if="url" :href="url" :download="`${title}.pdf`" style="margin-right:auto">Descargar PDF</a>
      <UiButton @click="open = false">Cerrar</UiButton>
    </template>
  </UiDialog>
</template>
