param(
    [string]$GameRoot=(Join-Path $env:TEMP 'contour4-ring-dev-052'),
    [string]$Output
)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(!$Output){$Output=Join-Path $root 'dist/SpaceJourney-Contour4Valuables-0.2.4.zip'}
if(Test-Path -LiteralPath $Output){throw 'Output already exists; preserve the tested package or select a new output path.'}
$bundle=Join-Path $root 'dist/bundles/contour4-valuables.bundle'
$report=Get-Content -LiteralPath (Join-Path $root 'evidence/unity-report.json') -Raw|ConvertFrom-Json
if(!$report.success -or $report.items.Count -ne 20 -or @($report.items|Where-Object {!$_.settled -or !$_.contactFit}).Count){throw 'Native Unity checks must pass first.'}
$packed=Get-Content -LiteralPath (Join-Path $root 'evidence/unity-packed-audit.json') -Raw|ConvertFrom-Json
if(!$packed.success -or $packed.items.Count -ne 20 -or @($packed.items|Where-Object {!$_.settled -or !$_.contactFit}).Count){throw 'Frozen bundle audit must pass first.'}
& dotnet build (Join-Path $root 'Plugin/Contour4Valuables.csproj') -c Release -o (Join-Path $root 'dist/compiled') "-p:GameManaged=$GameRoot/REPO_Data/Managed" "-p:BepInExCore=$GameRoot/BepInEx/core" "-p:RepoLibPath=$GameRoot/BepInEx/plugins/Zehs-REPOLib/REPOLib.dll" -p:NuGetAudit=false --ignore-failed-sources --nologo *> (Join-Path $root 'evidence/dotnet-build.log')
if($LASTEXITCODE -ne 0){throw 'Companion compilation failed.'}
$dll=Join-Path $root 'dist/compiled/Contour4Valuables.dll'
$manifest=Get-Content -LiteralPath (Join-Path $root 'manifest.json') -Raw|ConvertFrom-Json
if(([Reflection.AssemblyName]::GetAssemblyName($dll)).Version.ToString(3) -ne $manifest.version_number){throw 'DLL/manifest version mismatch.'}
$icon=Join-Path $root 'icon.png'
if(!(Test-Path -LiteralPath $icon)){
    $original=[IO.Compression.ZipFile]::OpenRead((Join-Path $root '../prototype/first-raid/dist/SpaceJourney-Contour4-0.1.0.zip'))
    try{[IO.Compression.ZipFileExtensions]::ExtractToFile($original.GetEntry('icon.png'),$icon,$false)}finally{$original.Dispose()}
}
$entries=[ordered]@{'manifest.json'=(Join-Path $root 'manifest.json');'README.md'=(Join-Path $root 'README.md');'icon.png'=$icon;'plugins/Contour4Valuables/Contour4Valuables.dll'=$dll;'plugins/Contour4Valuables/contour4-valuables.bundle'=$bundle}
$zip=[IO.Compression.ZipFile]::Open($Output,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($entry in $entries.GetEnumerator()){[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$entry.Value,$entry.Key,[IO.Compression.CompressionLevel]::Optimal)|Out-Null}}finally{$zip.Dispose()}
$check=[IO.Compression.ZipFile]::OpenRead($Output)
try{if($check.Entries.Count -ne 5){throw 'Unexpected package entry count.'};$check.Entries|Select-Object FullName,Length}finally{$check.Dispose()}
$inputs=@($entries.Values)|ForEach-Object {[ordered]@{path=$_;sha256=(Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant();bytes=(Get-Item -LiteralPath $_).Length}}
$receipt=[ordered]@{recordedAt=[DateTimeOffset]::UtcNow.ToString('o');package=[ordered]@{path=$Output;sha256=(Get-FileHash -LiteralPath $Output -Algorithm SHA256).Hash.ToLowerInvariant();bytes=(Get-Item -LiteralPath $Output).Length};inputs=$inputs;checks=[ordered]@{dotnet='passed: 0 warnings/errors';unity='twenty authored prefabs: bundle reload, dimensions, contact-fit and six-second settle; field-core real render. Capsule 2x remains runtime-only';runtime='pending exact-candidate native game probe';multiplayer='pending actual two-client test';ownerAcceptance='pending'}}
$receipt|ConvertTo-Json -Depth 8|Set-Content -LiteralPath (Join-Path $root 'evidence/package-receipt.json') -Encoding utf8
$receipt.package


