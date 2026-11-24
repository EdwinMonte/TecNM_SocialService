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
    <slot name="mobile-bar" />
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
  --sidebar-width: 264px;
  width: 100%;
  max-width: none;
  margin: 0;
  flex: 1;
  min-height: 0;
  grid-template-columns: var(--sidebar-width) minmax(0, 1fr);
}

.layout--no-sidebar {
  grid-template-columns: minmax(0, 1fr);
}

.ui-main {
  width: 100%;
  padding: 14px 22px;
  min-height: 0;
  display: flex;
  flex-direction: column;
}

.ui-footer {
  padding: 6px 22px;
  font-size: 11px;
  border-top: 1px solid var(--color-border);
  flex-shrink: 0;
}

@media (min-width: 901px) {
  .app-shell {
    height: 100dvh;
    max-height: 100dvh;
    overflow: hidden;
  }

  .layout {
    overflow: hidden;
  }

  .ui-main {
    height: 100%;
    overflow-y: auto;
    overscroll-behavior-y: contain;
  }
}

@media (max-width: 1100px) {
  .layout {
    --sidebar-width: 252px;
  }
  .ui-main {
    padding: 12px 16px;
  }
}

@media (max-width: 900px) {
  .app-shell {
    width: 100%;
    max-width: 100vw;
    overflow-x: clip;
  }
  .layout {
    width: 100%;
    max-width: 100%;
    min-width: 0;
    grid-template-columns: minmax(0, 1fr);
  }
  .ui-main {
    width: 100%;
    max-width: 100%;
    min-width: 0;
    overflow-x: hidden;
    box-sizing: border-box;
    padding: 10px 12px 18px;
  }
  .ui-footer {
    width: 100%;
    box-sizing: border-box;
    padding: 10px 14px;
    font-size: 11px;
    flex-direction: column;
    align-items: center;
    text-align: center;
    gap: 4px;
  }
}

.layout--content-only {
  grid-template-columns: minmax(0, 1fr);
}
</style>
