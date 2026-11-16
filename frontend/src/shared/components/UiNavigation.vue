<script setup lang="ts">
import UiNavLink from './UiNavLink.vue'
import type { NavigationItem } from './navigation'

withDefaults(
  defineProps<{
    items: NavigationItem[]
    variant?: 'header' | 'sidebar'
    label: string
  }>(),
  { variant: 'sidebar' },
)

function closeGroup(event: KeyboardEvent) {
  const target = event.target as HTMLElement
  const details = target.closest('details')
  if (!details) return
  details.open = false
  details.querySelector('summary')?.focus()
  event.preventDefault()
}
</script>

<template>
  <nav
    :class="variant === 'header' ? 'topnav' : 'sidenav'"
    :aria-label="label"
    @keydown.esc="closeGroup"
  >
    <template v-for="item in items" :key="item.label">
      <details
        v-if="'children' in item"
        class="navigation-group"
        :open="variant === 'sidebar' ? item.open : undefined"
      >
        <summary>
          <span class="summary-text">{{ item.label }}</span>
          <svg
            v-if="variant === 'sidebar'"
            viewBox="0 0 24 24"
            aria-hidden="true"
            class="summary-chevron"
          >
            <polyline points="9 18 15 12 9 6" />
          </svg>
        </summary>
        <div class="navigation-children">
          <UiNavLink v-for="child in item.children" :key="child.label" :item="child" />
        </div>
      </details>
      <UiNavLink v-else :item="item" />
    </template>
  </nav>
</template>

<style scoped>
nav a {
  display: block;
}

.sidenav {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 2px;
  min-width: 0;
}

.navigation-group {
  margin: 2px 0 0;
  padding: 3px 0 0;
  border-top: 1px solid var(--color-border-light);
  position: relative;
  min-width: 0;
}

.navigation-group summary {
  list-style: none;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
  padding: 3px 8px;
  min-height: 24px;
  font-size: 10px;
  font-weight: 700;
  letter-spacing: 0.07em;
  text-transform: uppercase;
  color: var(--color-text-muted);
  border-radius: 6px;
  cursor: pointer;
}

.navigation-group summary:focus:not(:focus-visible) {
  outline: none;
}

.navigation-group summary:focus-visible {
  outline: 2px solid var(--color-border-focus);
  outline-offset: -2px;
}

.navigation-group summary::-webkit-details-marker {
  display: none;
}

.navigation-group summary:hover {
  color: var(--color-text-primary);
  background: var(--color-gray-100);
}

.navigation-group[open] > summary {
  color: var(--color-accent);
}

.summary-chevron {
  width: 13px;
  height: 13px;
  fill: none;
  stroke: currentColor;
  stroke-width: 2.2;
  stroke-linecap: round;
  stroke-linejoin: round;
  transition: transform 200ms cubic-bezier(0.22, 1, 0.36, 1);
}

.navigation-group[open] > summary .summary-chevron {
  transform: rotate(90deg);
}

.navigation-children {
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  gap: 1px;
  padding: 1px 0 2px 7px;
  border-left: 1px solid var(--color-border);
  margin-left: 8px;
  min-width: 0;
}

.topnav {
  display: flex;
}

.topnav .navigation-children {
  position: absolute;
  top: 100%;
  left: 0;
  min-width: 200px;
  max-width: calc(100vw - 32px);
  padding: 8px;
  z-index: 3;
  color: var(--color-text-primary);
  background: var(--floating-bg);
  border: 1px solid var(--floating-border);
  border-radius: 8px;
  box-shadow: var(--floating-shadow);
}

.topnav :deep(a.active) {
  border-color: var(--color-accent);
}

@media (max-width: 1100px) {
  .topnav {
    width: 100%;
    order: 3;
    margin-left: 0;
  }
  .topnav .navigation-children {
    position: static;
    min-width: 0;
  }
}

@media (max-width: 640px) {
  .sidenav {
    display: grid;
  }
}
</style>
