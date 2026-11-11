param([Parameter(Mandatory=$true)][string]$PythonPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$output = Join-Path $projectRoot 'tmp/individual-act/original.pdf'
New-Item -ItemType Directory -Force (Split-Path $output -Parent) | Out-Null
$word = New-Object -ComObject Word.Application
$word.Visible = $false
$word.DisplayAlerts = 0
try {
    $doc = $word.Documents.Open((Join-Path $projectRoot 'docs/sources/servicio-social/formatos/ACTA INDIVIDUAL.docx'), $false, $true)
    try { $doc.ExportAsFixedFormat($output, 17) }
    finally { $doc.Close(0) }
}
finally { $word.Quit() }
& $PythonPath (Join-Path $PSScriptRoot 'prepare_individual_act.py') $output
if ($LASTEXITCODE -ne 0) { throw 'Falló la preparación del Acta Individual.' }
