const GRADE_LABELS: Record<string, string> = {
  excellent: 'EXCELENTE',
  notable: 'NOTABLE',
  good: 'BUENO',
  sufficient: 'SUFICIENTE',
  insufficient: 'INSUFICIENTE',
}

function escapeHtml(value: unknown): string {
  if (value === null || value === undefined) return ''
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
}

export function openPrintableDocument(data: Record<string, any>): void {
  const title = data.anexo || 'Formato Oficial TecNM — Servicio Social y Prácticas Profesionales'
  const docType = String(data.documentType || '')
  if (docType !== 'release_certificate' && !(docType === 'presentation_letter' && 'creditsAwarded' in data)) {
    throw new Error('Este formato requiere descarga desde su plantilla PDF oficial.')
  }
  const authorities = data.authorities || {}
  const outreachHead = authorities.outreach_division_head || {
    title: 'M.I.',
    fullName: 'Rocío Mendoza Riojas',
    position: 'Jefa de División de Gestión Tecnológica y Vinculación',
  }
  const ssHead = authorities.social_service_head || {
    title: 'M.A.F.H.',
    fullName: 'Miguel Alejandro Sanson Olguin',
    position: 'Encargado de la Oficina de Servicio Social',
  }

  let bodyHtml = ''

  if (docType === 'presentation_letter') {
    const isPP = String(data.folio || data.presentationLetterFolio || '').includes('PR')
    const folioText = data.folio || data.presentationLetterFolio || 'PENDIENTE'
    bodyHtml = `
      <div style="text-align:right; margin-bottom:18px;">
        <strong>ASUNTO:</strong> Carta de Presentación de ${isPP ? 'Prácticas Profesionales' : 'Servicio Social'}<br/>
        <strong>OFICIO No.:</strong> ${escapeHtml(folioText)}<br/>
        <strong>FECHA:</strong> Monclova, Coahuila a ${escapeHtml(data.issueDate || data.presentationLetterDate || new Date().toISOString().slice(0, 10))}
      </div>
      <div style="margin-bottom:18px;">
        <strong>${escapeHtml(data.supervisorFullName || 'A QUIEN CORRESPONDA')}</strong><br/>
        <strong>${escapeHtml(data.supervisorPosition || 'PRESENTE')}</strong><br/>
        <strong>${escapeHtml(data.companyName || 'DEPENDENCIA / EMPRESA RECEPTORA')}</strong>
      </div>
      <p style="text-align:justify; line-height:1.6;">
        Por medio de la presente me permito presentar a usted al(la) C. <strong>${escapeHtml(data.studentFullName)}</strong>,
        con número de control <strong>${escapeHtml(data.studentControlNumber)}</strong>, alumno(a) regular de
        <strong>${escapeHtml(data.studentSemester)}°</strong> semestre de la carrera de
        <strong>${escapeHtml(data.careerName)}</strong>, quien desea realizar su
        <strong>${isPP ? 'PRÁCTICA PROFESIONAL (PRODUCTIVIDAD LABORAL)' : 'SERVICIO SOCIAL'}</strong> en esa importante institución a su digno cargo,
        cubriendo un total de <strong>${escapeHtml(data.requiredHours || 480)} horas</strong> durante el periodo
        comprendido del <strong>${escapeHtml(data.startDate || '____')}</strong> al <strong>${escapeHtml(data.endDate || '____')}</strong>.
      </p>
      <p style="text-align:justify; line-height:1.6;">
        Asimismo, hacemos constar que el(la) estudiante cuenta con afiliación médica vigente ante el Instituto Mexicano del Seguro Social (IMSS).
        Agradeciendo de antemano las atenciones brindadas al portador de la presente, quedo de usted.
      </p>
      <div class="signatures-grid" style="margin-top:65px;">
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(outreachHead.title)} ${escapeHtml(outreachHead.fullName)}</strong><br/>
          <span>${escapeHtml(outreachHead.position)}</span>
        </div>
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>SELLO DE LA INSTITUCIÓN</strong><br/>
          <span>Instituto Tecnológico Superior de Monclova</span>
        </div>
      </div>
    `
  } else {
    bodyHtml = `
      <table class="info-table">
        <tr><th>Estudiante:</th><td>${escapeHtml(data.studentFullName)}</td><th>No. Control:</th><td>${escapeHtml(data.studentControlNumber)}</td></tr>
        <tr><th>Carrera:</th><td>${escapeHtml(data.careerName)}</td><th>Semestre:</th><td>${escapeHtml(data.studentSemester)}°</td></tr>
        <tr><th>Teléfono:</th><td>${escapeHtml(data.studentPhone)}</td><th>Domicilio:</th><td>${escapeHtml(data.studentAddress)}</td></tr>
        <tr><th>Dependencia / Empresa:</th><td>${escapeHtml(data.dependencyName || data.companyName)}</td><th>Titular / Supervisor:</th><td>${escapeHtml(data.dependencyHeadName || data.supervisorFullName)} (${escapeHtml(data.dependencyHeadPosition || data.supervisorPosition)})</td></tr>
        <tr><th>Programa / Modalidad:</th><td>${escapeHtml(data.programName || 'Prácticas Profesionales')} (${escapeHtml(data.modality === 'internal' ? 'Interno TecNM' : 'Externo')})</td><th>Periodo de Realización:</th><td>${escapeHtml(data.startDate)} al ${escapeHtml(data.endDate)}</td></tr>
        ${data.folio ? `<tr><th>Folio Oficial:</th><td colspan="3"><strong>${escapeHtml(data.folio)}</strong></td></tr>` : ''}
      </table>
      ${
        data.activitiesOutline
          ? `<h4 style="margin:16px 0 6px;">ACTIVIDADES A DESARROLLAR:</h4>
             <div style="border:1px solid #1e293b; padding:12px; border-radius:4px;">${escapeHtml(data.activitiesOutline)}</div>`
          : ''
      }
      <div class="signatures-grid" style="margin-top:60px;">
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.studentFullName)}</strong><br/>
          <span>Firma del Estudiante</span>
        </div>
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(outreachHead.title)} ${escapeHtml(outreachHead.fullName)}</strong><br/>
          <span>${escapeHtml(outreachHead.position)}</span>
        </div>
      </div>
    `
  }

  const win = window.open('', '_blank', 'width=920,height=800')
  if (!win) {
    alert('Por favor habilita las ventanas emergentes para visualizar e imprimir el formato oficial.')
    return
  }

  win.document.write(`<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8" />
  <title>${escapeHtml(title)}</title>
  <style>
    body { font-family: Arial, Helvetica, sans-serif; color: #111827; margin: 28px 36px; font-size: 13px; }
    .header-banner { display: flex; justify-content: space-between; align-items: center; border-bottom: 2px solid #1b396a; padding-bottom: 10px; margin-bottom: 18px; }
    .inst-title { font-size: 15px; font-weight: 700; color: #1b396a; }
    .inst-sub { font-size: 12px; color: #374151; }
    h2.doc-title { text-align: center; font-size: 15px; text-transform: uppercase; background: #f1f5f9; padding: 8px; border: 1px solid #cbd5e1; margin: 0 0 18px; }
    table.info-table { width: 100%; border-collapse: collapse; margin-bottom: 12px; }
    table.info-table th, table.info-table td { border: 1px solid #475569; padding: 7px 9px; text-align: left; font-size: 12.5px; }
    table.info-table th { background: #f8fafc; width: 22%; font-weight: 700; }
    .signatures-grid { display: flex; justify-content: space-around; gap: 24px; text-align: center; page-break-inside: avoid; }
    .sig-box { flex: 1; font-size: 12px; }
    .sig-line { border-top: 1px solid #111827; margin: 0 auto 6px; width: 85%; }
    .print-bar { display: flex; justify-content: space-between; align-items: center; background: #1b396a; color: #fff; padding: 10px 16px; border-radius: 6px; margin-bottom: 20px; }
    .print-btn { background: #ffffff; color: #1b396a; border: none; padding: 7px 14px; font-weight: 700; border-radius: 4px; cursor: pointer; }
    @media print { .print-bar { display: none; } body { margin: 12px; } }
  </style>
</head>
<body>
  <div class="print-bar">
    <span><strong>Vista Previa Oficial TecNM</strong> · Lista para imprimir o Guardar como PDF para recabar firmas y sellos</span>
    <button class="print-btn" onclick="window.print()">Imprimir / Guardar PDF</button>
  </div>
  <div class="header-banner">
    <div>
      <div class="inst-title">TECNOLÓGICO NACIONAL DE MÉXICO</div>
      <div class="inst-sub">INSTITUTO TECNOLÓGICO SUPERIOR DE MONCLOVA · DIVISIÓN DE GESTIÓN TECNOLÓGICA Y VINCULACIÓN</div>
    </div>
    <div style="text-align:right; font-size:11px; color:#4b5563;">
      SGC ISO 9001:2015<br/>Periodo: ${escapeHtml(data.periodCode || '2026')}
    </div>
  </div>
  <h2 class="doc-title">${escapeHtml(title)}</h2>
  ${bodyHtml}
</body>
</html>`)
  win.document.close()
}
