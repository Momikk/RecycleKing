param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$previousDalamudHome = $env:DALAMUD_HOME

try {
    if ([string]::IsNullOrWhiteSpace($env:DALAMUD_HOME)) {
        $candidates = @(
            (Join-Path $env:APPDATA 'XIVLauncherCN\addon\Hooks\dev'),
            (Join-Path $env:APPDATA 'XIVLauncher\addon\Hooks\dev')
        )
        $env:DALAMUD_HOME = $candidates | Where-Object {
            Test-Path -LiteralPath (Join-Path $_ 'Dalamud.dll')
        } | Select-Object -First 1
    }

    if ([string]::IsNullOrWhiteSpace($env:DALAMUD_HOME)) {
        throw '未找到 Dalamud 开发程序集，请先运行 XIVLauncher，或指定 DALAMUD_HOME。'
    }

    $dalamudDll = Join-Path $env:DALAMUD_HOME 'Dalamud.dll'
    $apiLevel = [System.Reflection.AssemblyName]::GetAssemblyName($dalamudDll).Version.Major
    if ($apiLevel -ne 15) {
        throw "当前 Dalamud API 是 $apiLevel，此基准版本要求 API 15。"
    }

    & dotnet build (Join-Path $PSScriptRoot 'RecycleKing\RecycleKing.csproj') -c $Configuration
    if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE。" }

    [xml]$project = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'RecycleKing\RecycleKing.csproj') -Raw
    $version = $project.Project.PropertyGroup.Version
    $output = Join-Path $PSScriptRoot "RecycleKing\bin\$Configuration"
    $stage = Join-Path $PSScriptRoot "artifacts\$Configuration-$version"
    New-Item -ItemType Directory -Path $stage -Force | Out-Null
    foreach ($name in @('RecycleKing.dll', 'RecycleKing.KamiToolKit.dll', 'RecycleKing.json', 'licenses', 'Assets', 'images')) {
        Copy-Item -LiteralPath (Join-Path $output $name) -Destination $stage -Recurse -Force
    }
    foreach ($name in @('LICENSE', 'THIRD-PARTY-NOTICES.md', 'CHANGELOG.md', 'CHANGELOG.zh-CN.md', 'README.md', 'README.en.md')) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination $stage -Force
    }
    $manifest = Get-Content -LiteralPath (Join-Path $stage 'RecycleKing.json') -Raw | ConvertFrom-Json
    if ($manifest.AssemblyVersion -ne $version -or [string]::IsNullOrWhiteSpace($manifest.Changelog)) {
        throw '插件清单版本或更新说明不正确。'
    }
    $dist = Join-Path $PSScriptRoot 'dist'
    New-Item -ItemType Directory -Path $dist -Force | Out-Null
    $zip = Join-Path $dist "RecycleKing-$version.zip"
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip }
    [System.IO.Compression.ZipFile]::CreateFromDirectory($stage, $zip)
    Write-Output "已生成 $zip"
}
finally {
    $env:DALAMUD_HOME = $previousDalamudHome
}
