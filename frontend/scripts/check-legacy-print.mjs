import assert from 'node:assert/strict'
import fs from 'node:fs'
import vm from 'node:vm'
import ts from 'typescript'

const source = fs.readFileSync(new URL('../src/shared/utils/printDocument.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText
let html = ''
const exports = {}
vm.runInNewContext(compiled, {
  exports,
  window: { open: () => ({ document: { write: value => { html = value }, close() {} }, focus() {} }) },
})
for (const documentType of ['application', 'commitment', 'acceptance_letter', 'report_1', 'evaluation', 'activity_assessment']) {
  assert.throws(() => exports.openPrintableDocument({ documentType }), /plantilla PDF oficial/)
}
assert.throws(() => exports.openPrintableDocument({ documentType: 'presentation_letter', folio: 'SS_CP0001' }), /plantilla PDF oficial/)
exports.openPrintableDocument({ documentType: 'presentation_letter', creditsAwarded: 2, folio: 'EJ26/PR0001', studentFullName: '<Alumno>' })
assert.match(html, /PRÁCTICA PROFESIONAL/)
assert.match(html, /&lt;Alumno&gt;/)
exports.openPrintableDocument({ documentType: 'release_certificate', studentFullName: 'Alumno' })
assert.match(html, /Alumno/)
console.log('Legacy print compatibility and official PDF restrictions passed.')
