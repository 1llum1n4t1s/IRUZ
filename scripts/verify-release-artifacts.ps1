param(
    [Parameter(Mandatory)][string]$ArtifactsDir,
    [Parameter(Mandatory)][string]$WorkDir,
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$CertificateSubject
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$manifest = Get-Content (Join-Path $ArtifactsDir 'releases.win.json') -Raw | ConvertFrom-Json
$assets = @($manifest.Assets)
if (-not $assets.Count -or -not (Test-Path (Join-Path $ArtifactsDir 'IRUZ-win-Setup.exe'))) { throw '配布物が不足しています' }
foreach ($asset in $assets) {
    if ($asset.PackageId -ne 'IRUZ' -or $asset.Version -ne $Version -or [IO.Path]::GetFileName($asset.FileName) -ne $asset.FileName) { throw 'manifest の識別情報が一致しません' }
    $package = Join-Path $ArtifactsDir $asset.FileName
    $file = Get-Item -LiteralPath $package
    if ($file.Length -ne $asset.Size -or (Get-FileHash $package -Algorithm SHA256).Hash -ne $asset.SHA256 -or (Get-FileHash $package -Algorithm SHA1).Hash -ne $asset.SHA1) { throw "manifest と実物が一致しません: $package" }
    $zip = [IO.Compression.ZipFile]::OpenRead($package)
    try {
        $nuspecs = @($zip.Entries | Where-Object { $_.FullName -like '*.nuspec' })
        $executables = @($zip.Entries | Where-Object { $_.Name -eq 'IRUZ.exe' })
        if ($nuspecs.Count -ne 1 -or $executables.Count -ne 1) { throw 'package の構成が不正です' }
        $reader = [IO.StreamReader]::new($nuspecs[0].Open())
        try { $spec = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
        if ($spec.package.metadata.id -ne 'IRUZ' -or $spec.package.metadata.version -ne $Version) { throw 'nuspec の版が一致しません' }
        $payload = Join-Path $WorkDir ('verify-' + $file.BaseName + '.exe')
        [IO.Compression.ZipFileExtensions]::ExtractToFile($executables[0], $payload, $false)
        $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($payload)
        if ($info.ProductVersion.Split('+')[0] -ne $Version) { throw '実行ファイルの版が一致しません' }
        $sig = Get-AuthenticodeSignature -LiteralPath $payload
        if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike "CN=$CertificateSubject*") { throw '実行ファイルの署名が無効です' }
        $bytes = [IO.File]::ReadAllBytes($payload)
        $pe = [BitConverter]::ToInt32($bytes, 0x3c)
        if ([BitConverter]::ToUInt16($bytes, $pe + 4) -ne 0x8664) { throw '実行ファイルが x64 ではありません' }
    } finally { $zip.Dispose() }
    Write-Host "公開前検証成功: $($asset.FileName) / $Version / x64 / 署名 / SHA256"
}
foreach ($exe in Get-ChildItem -LiteralPath $ArtifactsDir -Filter '*.exe') {
    $sig = Get-AuthenticodeSignature -LiteralPath $exe.FullName
    if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notlike "CN=$CertificateSubject*") { throw "配布 exe の署名が無効です: $($exe.Name)" }
}
