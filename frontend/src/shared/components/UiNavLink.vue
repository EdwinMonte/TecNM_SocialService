<script setup lang="ts">
import { computed } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import type { NavigationLink } from './navigation'

const props = defineProps<{ item: NavigationLink }>()
const route = useRoute()
const router = useRouter()
const active = computed(() => {
  const target = router.resolve(props.item.to)
  return route.path === target.path && route.hash === target.hash
})
</script>

<template>
  <RouterLink v-slot="{ href, navigate }" :to="item.to" custom>
    <a
      :href="href"
      :class="{ active }"
      :aria-current="active ? 'location' : undefined"
      :title="item.label"
      @click="navigate"
    >
      <span v-if="item.icon" class="nav-icon" aria-hidden="true">
        <svg v-if="item.icon === 'dashboard'" viewBox="0 0 24 24">
          <rect x="3" y="3" width="7" height="7" rx="1.5" />
          <rect x="14" y="3" width="7" height="7" rx="1.5" />
          <rect x="14" y="14" width="7" height="7" rx="1.5" />
          <rect x="3" y="14" width="7" height="7" rx="1.5" />
        </svg>
        <svg v-else-if="item.icon === 'social'" viewBox="0 0 24 24">
          <path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" />
          <circle cx="9" cy="7" r="4" />
          <path d="M22 21v-2a4 4 0 0 0-3-3.87" />
          <path d="M16 3.13a4 4 0 0 1 0 7.75" />
        </svg>
        <svg v-else-if="item.icon === 'internship'" viewBox="0 0 24 24">
          <rect x="2" y="7" width="20" height="14" rx="2" ry="2" />
          <path d="M16 21V5a2 2 0 0 0-2-2h-4a2 2 0 0 0-2 2v16" />
        </svg>
        <svg v-else-if="item.icon === 'review'" viewBox="0 0 24 24">
          <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
          <polyline points="14 2 14 8 20 8" />
          <path d="m9 15 2 2 4-4" />
        </svg>
        <svg v-else-if="item.icon === 'students'" viewBox="0 0 24 24">
          <path d="M22 10v6M2 10l10-5 10 5-10 5z" />
          <path d="M6 12v5c3 3 9 3 12 0v-5" />
        </svg>
        <svg v-else-if="item.icon === 'careers'" viewBox="0 0 24 24">
          <path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20" />
          <path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z" />
        </svg>
        <svg v-else-if="item.icon === 'companies'" viewBox="0 0 24 24">
          <path d="M3 21h18" />
          <path d="M5 21V7l8-4v18" />
          <path d="M19 21V11l-6-4" />
          <path d="M9 9v.01" />
          <path d="M9 12v.01" />
          <path d="M9 15v.01" />
          <path d="M9 18v.01" />
        </svg>
        <svg v-else-if="item.icon === 'profile'" viewBox="0 0 24 24">
          <circle cx="12" cy="8" r="4" />
          <path d="M4 20a8 8 0 0 1 16 0" />
        </svg>
        <svg v-else-if="item.icon === 'tracking'" viewBox="0 0 24 24">
          <rect x="3" y="4" width="18" height="16" rx="2" />
          <path d="M3 10h18" />
          <path d="M9 4v16" />
        </svg>
        <svg v-else-if="item.icon === 'stats'" viewBox="0 0 24 24">
          <path d="M4 20V10" />
          <path d="M10 20V4" />
          <path d="M16 20v-7" />
          <path d="M22 20H2" />
        </svg>
        <svg v-else-if="item.icon === 'pnc'" viewBox="0 0 24 24">
          <path d="m10.29 3.86-8.48 14.7A2 2 0 0 0 3.53 21h16.94a2 2 0 0 0 1.72-2.44l-8.48-14.7a2 2 0 0 0-3.42 0Z" />
          <path d="M12 9v4" />
          <path d="M12 17h.01" />
        </svg>
        <svg v-else viewBox="0 0 24 24">
          <circle cx="12" cy="12" r="3" />
          <path d="M3 12h6m6 0h6" />
        </svg>
      </span>
      <span class="nav-label">{{ item.label }}</span>
      <span
        v-if="item.badge !== undefined"
        class="nav-badge"
        :class="`nav-badge--${item.badgeTone || 'slate'}`"
      >
        {{ item.badge }}
      </span>
    </a>
  </RouterLink>
</template>

<style scoped>
a {
  display: flex !important;
  align-items: center;
  gap: 8px;
  padding: 4px 8px;
  min-height: 28px !important;
  min-width: 0;
  border-left: 3px solid transparent;
  border-radius: 6px;
  font-size: 12px;
  font-weight: 500;
  color: var(--color-text-secondary);
  transition: background-color 160ms ease, color 160ms ease, border-color 160ms ease;
}

a:hover {
  background: var(--color-primary-subtle);
  color: var(--color-text-primary);
}

a:focus:not(:focus-visible) {
  outline: none;
}

a:focus-visible {
  outline: 2px solid var(--color-border-focus);
  outline-offset: -2px;
}

a.active {
  background: var(--color-primary-subtle);
  color: var(--nav-active-text);
  border-left-color: var(--color-accent);
  font-weight: 600;
  box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--color-accent) 28%, transparent);
}

.nav-icon {
  width: 15px;
  height: 15px;
  display: grid;
  place-items: center;
  flex-shrink: 0;
  color: var(--color-text-muted);
  transition: color 180ms ease;
}

a:hover .nav-icon,
a.active .nav-icon {
  color: var(--color-accent);
}

.nav-icon svg {
  width: 15px;
  height: 15px;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.85;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.nav-label {
  flex: 1;
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  line-height: 1.25;
}

.nav-badge {
  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  font-size: 10.5px;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 5px;
  line-height: 1.35;
  flex-shrink: 0;
}

.nav-badge--emerald {
  background: var(--color-success-badge);
  color: var(--color-success-text);
  border: 1px solid var(--color-success-border);
}

.nav-badge--navy {
  background: var(--color-info-badge);
  color: var(--color-info-text);
  border: 1px solid var(--color-info-border);
}

.nav-badge--gold {
  background: var(--color-accent-light);
  color: var(--color-accent);
  border: 1px solid color-mix(in srgb, var(--color-accent) 45%, transparent);
}

.nav-badge--slate {
  background: var(--color-gray-100);
  color: var(--color-text-secondary);
  border: 1px solid var(--color-border);
}
</style>
