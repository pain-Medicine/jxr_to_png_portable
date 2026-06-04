$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptDir
$cppExe = Join-Path $repoRoot "jxr_to_png_\jxr_to_png.exe"
$distDir = Join-Path $repoRoot "dist"

if (-not (Test-Path $cppExe)) {
    Write-Error "Could not find the C++ engine executable at: $cppExe"
    Exit 1
}

Push-Location $scriptDir
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -o "$distDir"
Pop-Location

if ($LASTEXITCODE -eq 0) {
    Write-Host "Success"
} else {
    Write-Error "Failed"
}
