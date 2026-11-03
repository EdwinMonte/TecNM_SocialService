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
  } else if (docType === 'acceptance_letter') {
    bodyHtml = `
      <div style="text-align:right; margin-bottom:18px;">
        <strong>ASUNTO:</strong> Carta de Aceptación de Servicio Social (Modalidad Interna TecNM)<br/>
        <strong>FECHA:</strong> Monclova, Coahuila a ${escapeHtml(data.issueDate || new Date().toISOString().slice(0, 10))}
      </div>
      <div style="margin-bottom:18px;">
        <strong>${escapeHtml(outreachHead.title)} ${escapeHtml(outreachHead.fullName)}</strong><br/>
        <strong>${escapeHtml(outreachHead.position)}</strong><br/>
        <strong>INSTITUTO TECNOLÓGICO SUPERIOR DE MONCLOVA</strong><br/>
        <strong>PRESENTE.-</strong>
      </div>
      <p style="text-align:justify; line-height:1.6;">
        En atención a la Carta de Presentación con folio <strong>${escapeHtml(data.presentationLetterFolio || 'S/F')}</strong>,
        le informo que el(la) alumno(a) <strong>${escapeHtml(data.studentFullName)}</strong>, con número de control
        <strong>${escapeHtml(data.studentControlNumber)}</strong> de la carrera de <strong>${escapeHtml(data.careerName)}</strong>,
        ha sido <strong>ACEPTADO(A)</strong> para realizar su <strong>SERVICIO SOCIAL</strong> en el área interna de
        <strong>${escapeHtml(data.supervisorDepartment || data.companyName)}</strong> dentro del programa
        <strong>${escapeHtml(data.programName)}</strong>.
      </p>
      <p style="text-align:justify; line-height:1.6;">
        El(la) prestador(a) cubrirá un total de <strong>480 horas</strong> en el periodo del
        <strong>${escapeHtml(data.startDate)}</strong> al <strong>${escapeHtml(data.endDate)}</strong>, realizando las siguientes actividades:
        <em>${escapeHtml(data.activitiesOutline)}</em>.
      </p>
      <div class="signatures-grid" style="margin-top:65px;">
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.supervisorFullName)}</strong><br/>
          <span>${escapeHtml(data.supervisorPosition)}</span>
        </div>
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>SELLO DEL DEPARTAMENTO INTERNO</strong><br/>
          <span>TecNM Campus Monclova</span>
        </div>
      </div>
    `
  } else if (docType === 'commitment') {
    bodyHtml = `
      <p style="text-align:justify; line-height:1.6;">
        Con el fin de dar cumplimiento a lo establecido en la Ley Reglamentaria del Artículo 5° Constitucional relativo al ejercicio de profesiones,
        el(la) suscrito(a) <strong>${escapeHtml(data.studentFullName)}</strong>, con número de control <strong>${escapeHtml(data.studentControlNumber)}</strong>,
        domicilio en <strong>${escapeHtml(data.studentAddress)}</strong> y teléfono <strong>${escapeHtml(data.studentPhone)}</strong>,
        estudiante de la carrera de <strong>${escapeHtml(data.careerName)}</strong>:
      </p>
      <p style="text-align:justify; line-height:1.6;">
        Me comprometo a realizar mi Servicio Social en la dependencia: <strong>${escapeHtml(data.companyName)}</strong>,
        con domicilio en <strong>${escapeHtml(data.dependencyAddress || data.companyAddress)}</strong>,
        cubriendo <strong>480 horas</strong> en el periodo del <strong>${escapeHtml(data.startDate)}</strong> al <strong>${escapeHtml(data.endDate)}</strong>,
        acatando los reglamentos institucionales y conduciéndome con ética, responsabilidad y respeto.
      </p>
      <p><strong>Lugar y Fecha:</strong> ${escapeHtml(data.city || 'Monclova')}, Coahuila a ${escapeHtml(data.commitmentDate || data.startDate)}</p>
      <div class="signatures-grid" style="margin-top:65px;">
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.studentFullName)}</strong><br/>
          <span>FIRMA DE CONFORMIDAD DEL PRESTADOR DE SERVICIO SOCIAL</span>
        </div>
      </div>
    `
  } else if (docType.startsWith('report_')) {
    bodyHtml = `
      <table class="info-table">
        <tr><th>Nombre del Prestador:</th><td>${escapeHtml(data.studentFullName)}</td><th>No. Control:</th><td>${escapeHtml(data.studentControlNumber)}</td></tr>
        <tr><th>Carrera:</th><td>${escapeHtml(data.careerName)}</td><th>Reporte Bimestral No.:</th><td><strong>${escapeHtml(data.reportNumber)}</strong></td></tr>
        <tr><th>Dependencia:</th><td>${escapeHtml(data.companyName)}</td><th>Programa:</th><td>${escapeHtml(data.programName)}</td></tr>
        <tr><th>Periodo Reportado:</th><td>Del ${escapeHtml(data.periodStartDate)} al ${escapeHtml(data.periodEndDate)}</td><th>Horas del Bimestre / Acumuladas:</th><td><strong>${escapeHtml(data.reportedHours)} hrs</strong> (Acumuladas: <strong>${escapeHtml(data.reportAccumulatedHours)} hrs</strong>)</td></tr>
      </table>
      <h4 style="margin:18px 0 8px;">RESUMEN DE ACTIVIDADES REALIZADAS EN EL BIMESTRE:</h4>
      <div style="border:1px solid #1e293b; padding:14px; min-height:110px; border-radius:4px;">
        ${escapeHtml(data.activitiesSummary)}
      </div>
      <div class="signatures-grid" style="margin-top:60px;">
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.studentFullName)}</strong><br/>
          <span>Firma del Estudiante</span>
        </div>
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.supervisorFullName)}</strong><br/>
          <span>${escapeHtml(data.supervisorPosition)} (Firma y Sello Dependencia)</span>
        </div>
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(ssHead.title)} ${escapeHtml(ssHead.fullName)}</strong><br/>
          <span>Vo. Bo. Oficina de Servicio Social</span>
        </div>
      </div>
    `
  } else if (Array.isArray(data.criteria) || Array.isArray(data.questions)) {
    const rows = (data.criteria || data.questions || []) as Array<{
      index: number
      label: string
      grade?: string
      score?: number
    }>
    const rowsHtml = rows
      .map(
        (r) => `
        <tr>
          <td style="text-align:center; width:40px;">${r.index}</td>
          <td>${escapeHtml(r.label)}</td>
          <td style="text-align:center; font-weight:700; width:140px;">
            ${r.score !== undefined ? `${r.score} / 4` : escapeHtml(GRADE_LABELS[String(r.grade)] || r.grade || 'EXCELENTE')}
          </td>
        </tr>`,
      )
      .join('')

    bodyHtml = `
      <table class="info-table">
        <tr><th>Nombre del Alumno:</th><td>${escapeHtml(data.studentFullName)}</td><th>No. Control:</th><td>${escapeHtml(data.studentControlNumber)}</td></tr>
        <tr><th>Carrera:</th><td>${escapeHtml(data.careerName)}</td><th>Periodo / Bimestre:</th><td>${escapeHtml(data.bimonthlyPeriod || data.periodCode || 'Final')}</td></tr>
        <tr><th>Dependencia / Empresa:</th><td>${escapeHtml(data.companyName)}</td><th>Responsable Evaluador:</th><td>${escapeHtml(data.supervisorFullName)}</td></tr>
      </table>
      <table class="info-table" style="margin-top:14px;">
        <thead>
          <tr><th>No.</th><th>Criterio de Evaluación Normativo</th><th>Nivel de Desempeño</th></tr>
        </thead>
        <tbody>${rowsHtml}</tbody>
      </table>
      ${
        data.numericalAverage !== undefined
          ? `<p style="margin-top:10px;"><strong>Promedio Numérico:</strong> ${escapeHtml(data.numericalAverage)} &nbsp;|&nbsp; <strong>Nivel Alcanzado:</strong> ${escapeHtml(GRADE_LABELS[String(data.performanceLevel)] || data.performanceLevel)}</p>`
          : ''
      }
      <p style="margin-top:10px;"><strong>Observaciones:</strong> ${escapeHtml(data.observations || 'Sin observaciones adicionales.')}</p>
      <div class="signatures-grid" style="margin-top:55px;">
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.supervisorFullName || data.studentFullName)}</strong><br/>
          <span>Nombre, Firma y Sello</span>
        </div>
        <div class="sig-box">
          <div class="sig-line"></div>
          <strong>${escapeHtml(data.studentFullName)}</strong><br/>
          <span>Firma del Estudiante</span>
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
