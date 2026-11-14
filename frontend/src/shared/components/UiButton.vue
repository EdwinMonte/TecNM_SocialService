<script setup lang="ts">
withDefaults(
  defineProps<{
    variant?: 'primary' | 'accent' | 'secondary' | 'danger'
    size?: 'sm' | 'md'
    loading?: boolean
    disabled?: boolean
    type?: 'button' | 'submit' | 'reset'
  }>(),
  {
    variant: 'primary',
    size: 'md',
    loading: false,
    disabled: false,
    type: 'button',
  },
)
</script>

<template>
  <button
    class="btn"
    :class="[variant, size === 'sm' ? 'btn--sm' : '']"
    :type="type"
    :disabled="disabled || loading"
    :aria-busy="loading || undefined"
  >
    <slot />
  </button>
</template>
