param([string]$Output)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$Output){$Output=Join-Path $root 'dist/Contour4Valuables-0.2.3-source.zip'}
if(Test-Path -LiteralPath $Output){throw 'Preserve existing source archive; select a new output.'}
$files=@()
foreach($folder in @('Plugin','tools','Native','Unity/Assets','Unity/Packages','Unity/ProjectSettings','evidence')){
    $files+=Get-ChildItem -LiteralPath (Join-Path $root $folder) -Recurse -File|Where-Object {$_.FullName -notmatch '\\(obj|bin)\\' -and $_.Name -notmatch '^source-(manifest|receipt)' }
}
foreach($name in @('README.md','implementation.md','manifest.json','icon.png')){$files+=Get-Item -LiteralPath (Join-Path $root $name)}
$manifest=@($files|Sort-Object FullName|ForEach-Object {[ordered]@{path=$_.FullName.Substring($root.Length+1).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
$zip=[IO.Compression.ZipFile]::Open($Output,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($row in $manifest){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,(Join-Path $root $row.path),$row.path,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$zip.Dispose()}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $root 'evidence/source-manifest-0.2.3.json')
$receipt=[ordered]@{path=$Output;bytes=(Get-Item -LiteralPath $Output).Length;sha256=(Get-FileHash -LiteralPath $Output -Algorithm SHA256).Hash.ToLowerInvariant();entries=$manifest.Count;runtimePackage=(Get-FileHash -LiteralPath (Join-Path $root 'dist/SpaceJourney-Contour4Valuables-0.2.3.zip') -Algorithm SHA256).Hash.ToLowerInvariant()}
$receipt|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $root 'evidence/source-receipt-0.2.3.json')
$receipt|ConvertTo-Json


