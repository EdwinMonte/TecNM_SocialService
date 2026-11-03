<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterView } from 'vue-router'
import { LogoutButton } from '../../modules/auth'
import { me } from '../../modules/auth/api/authApi'
import type { UserResponse } from '../../modules/auth/types/auth'
import { UiBadge, UiHeader, UiShell, UiSidebar, UiToggle } from '../../shared/components'
import { useTheme } from '../../shared/composables/useTheme'
import {
  getRoleDisplayLabel,
  getSidebarNavigationForRoles,
  headerNavigation,
  mainLayoutConfig,
} from './mainLayout.config'

const { dark } = useTheme()
const currentUser = ref<UserResponse | null>(null)

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

onMounted(async () => {
  try {
    currentUser.value = await me()
  } catch {
    currentUser.value = null
  }
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

        <!-- Controles Inferiores: Tema y Cerrar Sesión -->
        <div class="sidebar-footer-controls">
          <div class="theme-switch-wrap">
            <UiToggle v-model="dark" type="switch" label="Modo oscuro" class="theme-toggle" />
          </div>
          <LogoutButton class="logout-action-btn" />
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
  padding: 10px 12px;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.user-name {
  font-size: 13px;
  font-weight: 700;
  color: var(--color-text-primary);
  margin-bottom: 4px;
  line-height: 1.25;
}

.user-meta {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 6px;
  font-size: 11.5px;
  color: var(--color-text-muted);
}

.user-username {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-weight: 600;
  color: var(--color-accent);
}

.sidebar-footer-controls {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.theme-switch-wrap {
  padding: 4px 10px;
  border-radius: 8px;
  background: var(--color-surface-white);
  border: 1px solid var(--color-border);
}

.theme-switch-wrap :deep(.theme-toggle) {
  width: 100%;
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 32px;
  font-size: 12px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.logout-action-btn :deep(button) {
  width: 100%;
  justify-content: center;
  height: 34px;
  font-size: 12px;
}
</style>
