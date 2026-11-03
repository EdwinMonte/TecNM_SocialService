<script setup lang="ts">
import { computed } from 'vue'

const props = withDefaults(
  defineProps<{ showHeader?: boolean; showSidebar?: boolean }>(),
  { showHeader: true, showSidebar: true },
)

const layoutClass = computed(() => ({
  'layout--no-header': !props.showHeader,
  'layout--no-sidebar': !props.showSidebar,
  'layout--content-only': !props.showHeader && !props.showSidebar,
}))
</script>

<template>
  <div class="app-shell">
    <header v-if="showHeader && $slots.header" class="ui-header-slot">
      <slot name="header" />
    </header>
    <div class="layout" :class="layoutClass">
      <slot v-if="showSidebar" name="sidebar" />
      <main class="ui-main"><slot /></main>
    </div>
    <footer v-if="$slots.footer" class="ui-footer"><slot name="footer" /></footer>
  </div>
</template>

<style scoped>
.app-shell {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}

.layout {
  --sidebar-width: 272px;
  width: 100%;
  max-width: none;
  margin: 0;
  flex: 1;
  grid-template-columns: var(--sidebar-width) minmax(0, 1fr);
}

.layout--no-sidebar {
  grid-template-columns: minmax(0, 1fr);
}

.ui-main {
  width: 100%;
  padding: 18px 26px;
}

.ui-footer {
  padding: 10px 26px;
  font-size: 11.5px;
  border-top: 1px solid var(--color-border);
}

@media (max-width: 1100px) {
  .layout {
    --sidebar-width: 248px;
  }
  .ui-main {
    padding: 16px 20px;
  }
}

.layout--content-only {
  grid-template-columns: minmax(0, 1fr);
}
</style>
