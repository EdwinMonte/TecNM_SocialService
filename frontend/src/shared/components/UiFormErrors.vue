<script setup lang="ts">
import UiAlert from './UiAlert.vue'
import type { useFormValidation } from '../composables/useFormValidation'
defineProps<{ validation: ReturnType<typeof useFormValidation> }>()
function focus(id: string) { document.getElementById(id)?.focus() }
</script>
<template>
  <UiAlert v-if="validation.entries.value.length || validation.general.value" class="form-errors" tone="danger" role="alert">
    <p v-if="validation.general.value">{{ validation.general.value }}</p>
    <template v-if="validation.entries.value.length">
      <p>Corrige los campos señalados:</p>
      <ul>
        <li v-for="entry in validation.entries.value" :key="entry.key">
          <a v-if="entry.id" :href="`#${entry.id}`" @click.prevent="focus(entry.id)">{{ entry.label }}</a>
          <strong v-else>{{ entry.label }}</strong>: {{ entry.messages.join(' ') }}
        </li>
      </ul>
    </template>
  </UiAlert>
</template>
<style scoped>
p { margin: 0; } ul { margin: .5rem 0 0; padding-left: 1.25rem; } a { color: inherit; text-decoration: underline; }
.form-errors { grid-column: 1 / -1; }
.form-errors strong { display: inline; }
</style>
