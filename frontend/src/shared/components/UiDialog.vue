<script setup lang="ts">
import { computed, nextTick, onMounted, ref, useAttrs, useId, watch } from 'vue'

defineOptions({ inheritAttrs: false })

const props = withDefaults(
  defineProps<{
    title: string
    size?: 'sm' | 'md' | 'lg' | 'xl'
  }>(),
  {
    size: 'md',
  },
)

const model = defineModel<boolean>({ default: false })
const attrs = useAttrs()
const dialog = ref<HTMLDialogElement | null>(null)
const titleId = useId()
let previouslyFocused: HTMLElement | null = null

async function openDialog(): Promise<void> {
  await nextTick()
  if (!dialog.value || dialog.value.open) return

  previouslyFocused = document.activeElement instanceof HTMLElement ? document.activeElement : null
  dialog.value.showModal()
}

function restoreFocus(): void {
  const target = previouslyFocused
  previouslyFocused = null
  target?.focus()
}

function closeDialog(): void {
  if (!dialog.value?.open) {
    restoreFocus()
    return
  }

  dialog.value.close()
}

function syncModelFromDialog(): void {
  model.value = false
  restoreFocus()
}

function syncDialog(): void {
  if (model.value) {
    void openDialog()
  } else {
    closeDialog()
  }
}

function handleBackdropClick(event: MouseEvent): void {
  if (event.target === dialog.value) {
    model.value = false
  }
}

const sizeClass = computed(() => `size-${props.size}`)

watch(model, syncDialog)
onMounted(syncDialog)
</script>

<template>
  <dialog
    ref="dialog"
    v-bind="attrs"
    :class="['ui-dialog-root', sizeClass]"
    :aria-labelledby="titleId"
    @cancel="model = false"
    @close="syncModelFromDialog"
    @click="handleBackdropClick"
  >
    <div class="dialog-inner" @click.stop>
      <div class="dialog-header">
        <h2 :id="titleId" class="dialog-title">{{ title }}</h2>
        <button
          type="button"
          class="dialog-close-btn"
          aria-label="Cerrar modal"
          title="Cerrar (Esc)"
          @click="model = false"
        >
          <svg viewBox="0 0 24 24" width="18" height="18" stroke="currentColor" stroke-width="2.2" fill="none">
            <line x1="18" y1="6" x2="6" y2="18" />
            <line x1="6" y1="6" x2="18" y2="18" />
          </svg>
        </button>
      </div>

      <div v-if="model" class="dialog-body">
        <slot />
      </div>

      <div v-if="$slots.footer || $slots.actions" class="dialog-footer">
        <slot name="footer" />
        <slot name="actions" />
      </div>
    </div>
  </dialog>
</template>

<style scoped>
.ui-dialog-root {
  border: 1px solid var(--dialog-border, #e2e8f0);
  background: var(--dialog-bg, #ffffff);
  color: var(--color-text-primary, #0f172a);
  border-radius: 14px;
  box-shadow: 0 25px 50px -12px rgba(15, 23, 42, 0.25);
  padding: 0;
  overflow: hidden;
  position: fixed;
  inset: 0;
  margin: auto;
}

.ui-dialog-root::backdrop {
  background: rgba(15, 23, 42, 0.65);
  backdrop-filter: blur(3px);
}

.size-sm {
  width: min(440px, calc(100% - 32px));
  max-width: 440px;
}

.size-md {
  width: min(600px, calc(100% - 32px));
  max-width: 600px;
}

.size-lg {
  width: min(800px, calc(100% - 32px));
  max-width: 800px;
}

.size-xl {
  width: min(980px, calc(100% - 32px));
  max-width: 980px;
}

.dialog-inner {
  display: flex;
  flex-direction: column;
  max-height: 88vh;
  padding: 1.5rem;
}

.dialog-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding-bottom: 1rem;
  border-bottom: 1px solid var(--color-border-light, #e2e8f0);
  flex-shrink: 0;
}

.dialog-title {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 700;
  color: var(--color-text-primary, #0f172a);
  line-height: 1.3;
}

.dialog-close-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 34px;
  height: 34px;
  border-radius: 8px;
  border: 1px solid transparent;
  background: transparent;
  color: #64748b;
  cursor: pointer;
  transition: all 0.15s ease;
  flex-shrink: 0;
}

.dialog-close-btn:hover {
  background: rgba(15, 23, 42, 0.08);
  color: #0f172a;
}

.dialog-body {
  overflow-y: auto;
  padding: 1.25rem 0.25rem;
  flex: 1 1 auto;
}

.dialog-footer {
  padding-top: 1rem;
  border-top: 1px solid var(--color-border-light, #e2e8f0);
  display: flex;
  justify-content: flex-end;
  gap: 0.75rem;
  align-items: center;
  flex-shrink: 0;
}
</style>
