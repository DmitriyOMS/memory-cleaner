param(
    [switch]$SkipAppBuild
)

$ErrorActionPreference = "Stop"
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$appExe = Join-Path $projectRoot "MemoryCleaner.exe"

if (-not $SkipAppBuild) {
    $publishDir = Join-Path $projectRoot "native\artifacts\installer"
    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }

    $project = Join-Path $projectRoot "native\MemoryCleaner.csproj"
    & dotnet publish $project `
        --configuration Release `
        --runtime win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        --output $publishDir
    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось собрать Memory Cleaner."
    }

    Move-Item (Join-Path $publishDir "MemoryCleaner.exe") $appExe -Force
    Remove-Item $publishDir -Recurse -Force
    $artifactsDir = Split-Path $publishDir -Parent
    if ((Test-Path $artifactsDir) -and -not (Get-ChildItem $artifactsDir -Force | Select-Object -First 1)) {
        Remove-Item $artifactsDir -Force
    }
}

if (-not (Test-Path $appExe -PathType Leaf)) {
    throw "Файл MemoryCleaner.exe не найден в корне проекта."
}
if (-not (Test-Path (Join-Path $projectRoot "MemoryCleaner-UserGuide.pdf") -PathType Leaf)) {
    throw "Файл MemoryCleaner-UserGuide.pdf не найден в корне проекта."
}

$compilerCandidates = @(
    $env:ISCC,
    (Join-Path $PSScriptRoot ".tools\inno\ISCC.exe"),
    "ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 7\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 7\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
)

$compiler = $null
foreach ($candidate in $compilerCandidates) {
    if (-not $candidate) {
        continue
    }
    if ($candidate -eq "ISCC.exe") {
        $command = Get-Command $candidate -ErrorAction SilentlyContinue
        if ($command) {
            $compiler = $command.Source
            break
        }
    } elseif (Test-Path $candidate -PathType Leaf) {
        $compiler = $candidate
        break
    }
}

if (-not $compiler) {
    throw "Inno Setup не найден. Установите Inno Setup 7 или задайте путь к ISCC.exe в переменной ISCC."
}

$script = Join-Path $PSScriptRoot "MemoryCleaner.iss"
& $compiler $script
if ($LASTEXITCODE -ne 0) {
    throw "Не удалось собрать установщик."
}

$setup = Join-Path $PSScriptRoot "output\MemoryCleaner-Setup-2.0.0.exe"
if (-not (Test-Path $setup -PathType Leaf)) {
    throw "Установщик не создан."
}

Write-Output "Установщик создан: $setup"
