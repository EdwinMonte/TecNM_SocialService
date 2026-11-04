$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$sourceDoc = (Get-ChildItem -LiteralPath "$projectRoot/docs/sources/servicio-social/formatos" -Filter '*.docx' | Select-Object -First 1).FullName
$targetPdf = Join-Path $projectRoot 'backend/Resources/Documents/templates/ss-acceptance-example.pdf'
$wordApp = New-Object -ComObject Word.Application
$wordApp.Visible = $false
$wordApp.DisplayAlerts = 0
try {
    $wordDoc = $wordApp.Documents.Open($sourceDoc, $false, $true)
    try { $wordDoc.ExportAsFixedFormat($targetPdf, 17) }
    finally { $wordDoc.Close(0) }
}
finally {
    $wordApp.Quit(0)
    [System.Runtime.InteropServices.Marshal]::ReleaseComObject($wordApp) | Out-Null
}
Write-Output 'Re-register the PDF hash/version in manifest.json after visual review.'
