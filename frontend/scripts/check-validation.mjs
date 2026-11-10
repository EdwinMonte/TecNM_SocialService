import assert from 'node:assert/strict'
import fs from 'node:fs'
import { createRequire } from 'node:module'
import { pathToFileURL } from 'node:url'
const require = createRequire(new URL('../package.json', import.meta.url))
const ts = require('typescript')
const vue = require('vue')
const { renderToString } = createRequire(require.resolve('vue'))('@vue/server-renderer')
function moduleUrl(path, replacements = {}) {
  let code = ts.transpileModule(fs.readFileSync(new URL(path, import.meta.url), 'utf8'), { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.ES2022 } }).outputText
  for (const [from, to] of Object.entries(replacements)) code = code.replaceAll(`'${from}'`, JSON.stringify(to))
  return `data:text/javascript;base64,${Buffer.from(code).toString('base64')}`
}
const problemUrl = moduleUrl('../src/shared/api/problemDetails.ts')
const { problemFields, problemMessage } = await import(problemUrl)
const response = (errors, detail = 'Uno o más campos presentan errores de validación') => ({ response: { data: { errors, detail } } })
assert.deepEqual({ ...problemFields(response({ FirstName: ['Obligatorio'], firstName: ['Máximo 100'], '$.Application.StudentPhone': ['Máximo 20'] })) }, { firstName: ['Obligatorio', 'Máximo 100'], 'application.studentPhone': ['Máximo 20'] })
assert.equal(problemMessage(response({ curp: ['Formato de 18 caracteres'] }), 'Error'), 'Formato de 18 caracteres')
assert.equal(problemMessage(new Error('red'), 'No hay conexión'), 'No hay conexión')
assert.equal(problemMessage(response(null, 'Matrícula duplicada'), 'Error'), 'Matrícula duplicada')
assert.deepEqual(problemFields(response(['dato inesperado'])), {})
assert.equal(problemFields(response(JSON.parse('{"__proto__":["Error"]}'))).__proto__[0], 'Error')
const { validateFields, optionalEmail, dateOrder } = await import(moduleUrl('../src/shared/utils/validationRules.ts'))
assert.deepEqual(validateFields({ email: '', phone: '' }, { email: optionalEmail, phone: { max: 20 } }), {})
assert.deepEqual(validateFields({ email: 'a@' }, { email: optionalEmail }), {}) // Preserve the current API rule.
assert.equal(Object.keys(validateFields({ name: '', semester: 16, email: 'incorrecto' }, { name: { required: true }, semester: { integer: true, min: 1, maxNumber: 15 }, email: optionalEmail })).length, 3)
assert.ok(dateOrder({}, { start: '2026-12-02', end: '2026-12-01' }, 'start', 'end').end)
const { useFormValidation } = await import(moduleUrl('../src/shared/composables/useFormValidation.ts', { vue: pathToFileURL(require.resolve('vue')).href, '../api/problemDetails': problemUrl }))
let first, second, value = ''
globalThis.document = { getElementById: () => ({ focus() {} }) }
await renderToString(vue.createSSRApp({ setup() {
  first = useFormValidation(() => value ? {} : { name: ['Obligatorio'] })
  second = useFormValidation()
  first.field('name', 'Nombre')
  return () => vue.h('div')
} }))
assert.equal(first.validate(), false)
assert.equal(second.entries.value.length, 0)
value = 'María'
first.field('name', 'Nombre').onInput()
assert.equal(first.entries.value.length, 0)
first.apply(response({ Name: ['Duplicado'], hidden: ['Dato no editable'] }), 'Error')
first.field('name', 'Nombre').onBlur()
assert.equal(first.errors.name[0], 'Duplicado') // Blur alone must not erase server validation.
first.field('name', 'Nombre').onInput()
assert.equal(first.errors.name, undefined)
assert.equal(first.entries.value[0].key, 'hidden')
first.reset()
assert.equal(first.entries.value.length, 0)
first.apply(new Error('red'), 'No hay conexión')
assert.equal(first.general.value, 'No hay conexión')
console.log('Validation normalization, unchanged rules, independent form state, correction and reset checks passed.')
