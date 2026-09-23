param([string]$InnoCompiler)
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($InnoCompiler)) {
    $command = Get-Command 'ISCC.exe' -ErrorAction SilentlyContinue
    $candidates = @(
        $(if ($command) { $command.Source }),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$PSScriptRoot\work\build-tools\inno\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) }
    $InnoCompiler = $candidates | Select-Object -First 1
}
if (-not $InnoCompiler) {
    throw 'Inno Setup 6을 찾지 못했습니다. 설치하거나 -InnoCompiler 경로를 지정해 주세요.'
}

$publishDir = Join-Path $PSScriptRoot 'work\publish'
New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
$probeDir = Join-Path $PSScriptRoot 'work\probe'
& dotnet publish (Join-Path $PSScriptRoot 'src\BossMonitor.SetupProbe\BossMonitor.SetupProbe.csproj') -c Release -r win-x64 --self-contained true -o $probeDir -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false
if ($LASTEXITCODE -ne 0) { throw '설치 경로 탐색 도구 빌드 실패' }
foreach ($projectName in @('BossMonitor','BossMonitor.SetupHelper','BossMonitor.Cleanup')) {
    & dotnet publish (Join-Path $PSScriptRoot "src\$projectName\$projectName.csproj") -c Release -r win-x64 --self-contained true -o $publishDir -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "빌드 실패: $projectName" }
}
& $InnoCompiler (Join-Path $PSScriptRoot 'installer\BossMonitor.iss')
if ($LASTEXITCODE -ne 0) { throw '설치 프로그램 생성 실패' }
