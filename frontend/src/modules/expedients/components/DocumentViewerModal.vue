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
  <UiDialog v-model="open" :title="title" size="full">
    <UiAlert v-if="error" tone="danger">{{ error }}</UiAlert>
    <iframe
      v-if="url"
      :src="url"
      :title="title"
      class="viewer-pdf-frame"
    />
    <UiAlert v-else tone="warning">No se pudo abrir el PDF.</UiAlert>
    <template #footer>
      <a
        v-if="url && !confirmUpload"
        :href="url"
        :download="`${title}.pdf`"
        class="btn secondary btn--sm viewer-download-btn"
      >
        Descargar PDF
      </a>
      <UiButton v-if="confirmUpload" size="sm" :disabled="busy || !url" @click="emit('confirm')">
        {{ busy ? 'Enviando…' : 'Confirmar y enviar a revisión' }}
      </UiButton>
      <UiButton variant="secondary" size="sm" :disabled="busy" @click="open = false">
        {{ confirmUpload ? 'Cancelar' : 'Cerrar' }}
      </UiButton>
    </template>
  </UiDialog>
</template>

<style scoped>
.viewer-pdf-frame {
  width: 100%;
  height: min(72dvh, 720px);
  border: 1px solid var(--color-border-medium);
  border-radius: 8px;
}

.viewer-download-btn {
  margin-right: auto;
}

@media (max-width: 640px) {
  .viewer-pdf-frame {
    height: min(58dvh, 460px);
  }
  .viewer-download-btn {
    margin-right: 0;
    flex: 1 1 100%;
  }
}
</style>
