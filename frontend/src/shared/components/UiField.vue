<script setup lang="ts">
import { computed, ref, useAttrs, useId, useSlots } from 'vue'

defineOptions({ inheritAttrs: false })

const props = withDefaults(
  defineProps<{
    id?: string
    label: string
    error?: string | string[]
    hint?: string
    as?: 'input' | 'select' | 'textarea'
    modelValue?: string | number
    options?: { value: string | number; label: string }[]
  }>(),
  {
    as: 'input',
    modelValue: '',
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: any]
  blur: [event: FocusEvent]
  input: [event: Event]
  change: [event: Event]
}>()

const slots = useSlots()
const attrs = useAttrs()
const generatedId = useId()
const messages = computed(() => (Array.isArray(props.error) ? props.error : [props.error]).filter((value): value is string => !!value))
const controlId = computed(() => props.id || generatedId)
const hintId = computed(() => `${controlId.value}-hint`)
const errorId = computed(() => `${controlId.value}-error`)
const describedBy = computed(() => {
  const ids = typeof attrs['aria-describedby'] === 'string' ? attrs['aria-describedby'].split(' ') : []

  if (props.hint) ids.push(hintId.value)
  if (messages.value.length) ids.push(errorId.value)

  return ids.length ? ids.join(' ') : undefined
})

const control = ref<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement | null>(null)
const root = ref<HTMLElement | null>(null)
const controlAttrs = computed(() => ({ id: controlId.value, 'aria-invalid': messages.value.length ? true : undefined, 'aria-describedby': describedBy.value }))

function updateValue(event: Event): void {
  emit('update:modelValue', (event.target as HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement).value)
}

function focus(): void {
  ;(control.value || root.value?.querySelector<HTMLElement>('input,select,textarea'))?.focus()
}

defineExpose({ focus })
</script>

<template>
  <div ref="root" class="field" @focusout="emit('blur', $event)" @input="emit('input', $event)" @change="emit('change', $event)">
    <label :for="controlId">{{ label }}</label>
    <slot :control-attrs="controlAttrs">
      <component
        :is="as"
        ref="control"
        v-bind="{ ...attrs, ...controlAttrs }"
        :value="modelValue"
        @input="updateValue"
      >
        <template v-if="as === 'select'">
          <option v-for="option in options" :key="option.value" :value="option.value">
            {{ option.label }}
          </option>
          <slot name="options" />
        </template>
      </component>
    </slot>
    <span v-if="hint" :id="hintId" class="hint">{{ hint }}</span>
    <ul v-if="messages.length" :id="errorId" class="error field-errors"><li v-for="message in messages" :key="message">{{ message }}</li></ul>
  </div>
</template>
<style scoped>
.field-errors { list-style: none; margin: 0; padding: 0; }
.field :deep([aria-invalid='true']) { border-color: var(--color-danger, #b42318); }
</style>
