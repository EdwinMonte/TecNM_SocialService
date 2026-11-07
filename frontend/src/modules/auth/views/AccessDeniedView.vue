<script setup lang="ts">
import { UiBasePage, UiAlert } from '../../../shared/components'
</script>
<template>
  <UiBasePage :fluid="true">
    <h1>Acceso no disponible</h1>
    <UiAlert>No tienes acceso a este apartado o no fue posible verificar tu sesión.</UiAlert>
    <RouterLink to="/">Volver al inicio</RouterLink>
  </UiBasePage>
</template>
