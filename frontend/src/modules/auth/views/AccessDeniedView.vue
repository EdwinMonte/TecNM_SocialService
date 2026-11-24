<script setup lang="ts">
import { UiBasePage, UiAlert } from '../../../shared/components'
</script>
<template>
  <UiBasePage :fluid="true" class="access-denied-page">
    <h1 class="access-denied-title">Acceso no disponible</h1>
    <UiAlert>No tienes acceso a este apartado o no fue posible verificar tu sesión.</UiAlert>
    <RouterLink to="/" class="btn secondary access-denied-link">Volver al inicio</RouterLink>
  </UiBasePage>
</template>

<style scoped>
.access-denied-title {
  margin: 0 0 0.75rem;
  font-size: 1.35rem;
}
.access-denied-link {
  margin-top: 0.5rem;
}
@media (max-width: 640px) {
  .access-denied-title {
    font-size: 1.15rem;
  }
  .access-denied-link {
    width: 100%;
  }
}
</style>
