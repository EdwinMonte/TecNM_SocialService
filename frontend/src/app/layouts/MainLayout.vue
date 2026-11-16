<script setup lang="ts">
import { computed } from 'vue'
import { RouterView } from 'vue-router'
import { LogoutButton } from '../../modules/auth'
import { sessionUser } from '../../shared/composables/useSession'
import { UiBadge, UiHeader, UiShell, UiSidebar, UiToggle } from '../../shared/components'
import { useTheme } from '../../shared/composables/useTheme'
import {
  getRoleDisplayLabel,
  getSidebarNavigationForRoles,
  headerNavigation,
  mainLayoutConfig,
} from './mainLayout.config'

const { dark } = useTheme()
const currentUser = sessionUser

const dynamicSidebarItems = computed(() => {
  const roles = currentUser.value?.roles ?? []
  if (roles.length === 0) {
    return [{ label: 'Panel Principal', to: '/', icon: 'dashboard' as const }]
  }
  return getSidebarNavigationForRoles(roles)
})

const roleDisplay = computed(() => {
  return getRoleDisplayLabel(currentUser.value?.roles ?? [])
})


</script>

<template>
  <UiShell :show-header="mainLayoutConfig.showHeader" :show-sidebar="mainLayoutConfig.showSidebar">
    <template #header>
      <UiHeader v-bind="mainLayoutConfig.header" :items="headerNavigation">
        <UiToggle v-model="dark" type="switch" label="Modo oscuro" class="theme" />
        <LogoutButton v-if="!mainLayoutConfig.showSidebar" />
      </UiHeader>
    </template>
    <template #sidebar>
      <UiSidebar
        v-bind="mainLayoutConfig.sidebar"
        :items="dynamicSidebarItems"
      >
        <!-- Tarjeta de Usuario Activo -->
        <div v-if="currentUser" class="user-card-box">
          <div class="user-name">{{ currentUser.name }}</div>
          <div class="user-meta">
            <span class="user-username">@{{ currentUser.username }}</span>
            <UiBadge tone="info">{{ roleDisplay }}</UiBadge>
          </div>
        </div>

        <!-- Controles Inferiores: Cerrar Sesión y Cambio Rápido de Tema (Luna / Sol) -->
        <div class="sidebar-footer-controls">
          <LogoutButton class="logout-action-btn" />
          <button
            type="button"
            class="theme-icon-btn"
            :class="{ 'is-dark': dark }"
            :aria-label="dark ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'"
            :title="dark ? 'Modo claro' : 'Modo oscuro'"
            :aria-pressed="dark"
            @click="dark = !dark"
          >
            <svg
              v-if="!dark"
              viewBox="0 0 24 24"
              aria-hidden="true"
              class="theme-icon-svg"
            >
              <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
            </svg>
            <svg
              v-else
              viewBox="0 0 24 24"
              aria-hidden="true"
              class="theme-icon-svg"
            >
              <circle cx="12" cy="12" r="4" />
              <path d="M12 2v2" />
              <path d="M12 20v2" />
              <path d="m4.93 4.93 1.41 1.41" />
              <path d="m17.66 17.66 1.41 1.41" />
              <path d="M2 12h2" />
              <path d="M20 12h2" />
              <path d="m4.93 19.07 1.41-1.41" />
              <path d="m17.66 6.34 1.41-1.41" />
            </svg>
          </button>
        </div>
      </UiSidebar>
    </template>
    <RouterView />
    <template #footer>
      <span><strong>Tecnológico Nacional de México</strong> · Campus Monclova · Clave 05DIT0001V</span>
      <span>Sistema Institucional SSPP · SGC ISO 9001:2015</span>
      <LogoutButton v-if="!mainLayoutConfig.showHeader && !mainLayoutConfig.showSidebar" />
    </template>
  </UiShell>
</template>

<style scoped>
.user-card-box {
  padding: 7px 10px;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.04);
}

.user-name {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 2px;
  line-height: 1.2;
}

.user-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 4px;
  font-size: 11px;
  color: var(--color-text-muted);
}

.user-username {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-weight: 600;
  color: var(--color-accent);
}

.user-card-box {
  padding: 8px 10px;
  gap: 3px;
}

.sidebar-footer-controls {
  display: flex;
  align-items: center;
  gap: 6px;
}

.sidebar-footer-controls :deep(.btn) {
  flex: 1;
  min-width: 0;
  justify-content: center;
  min-height: 32px;
  height: 32px;
  padding: 0 10px;
  border-radius: 7px;
  font-size: 11.5px;
  font-weight: 600;
}

.theme-icon-btn {
  width: 34px;
  height: 32px;
  flex-shrink: 0;
  display: inline-grid;
  place-items: center;
  border-radius: 7px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: background-color 160ms ease, color 160ms ease, border-color 160ms ease, transform 140ms ease;
}

.theme-icon-btn:hover {
  background: var(--color-gray-100);
  border-color: var(--color-primary);
  color: var(--color-primary);
}

.theme-icon-btn.is-dark {
  color: var(--color-accent);
  border-color: color-mix(in srgb, var(--color-accent) 42%, var(--color-border));
}

.theme-icon-btn.is-dark:hover {
  background: var(--color-accent-light);
  border-color: var(--color-accent);
}

.theme-icon-btn:active {
  transform: scale(0.95);
}

.theme-icon-svg {
  width: 16px;
  height: 16px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.9;
  stroke-linecap: round;
  stroke-linejoin: round;
}
</style>
