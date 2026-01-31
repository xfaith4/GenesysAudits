param(
  [Parameter()] [ValidateSet('Release','Debug')] [string] $Configuration = 'Release',
  [Parameter()] [ValidateSet('win-x64','win-arm64')] [string] $RuntimeIdentifier = 'win-x64',
  [Parameter()] [switch] $FrameworkDependent,
  [Parameter()] [switch] $WinAppRuntimeDependent,
  [Parameter()] [switch] $SingleFile,
  [Parameter()] [switch] $EnglishOnly,
  [Parameter()] [switch] $KeepPdb
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$proj = Join-Path $repoRoot 'src\GcExtensionAuditMaui\GcExtensionAuditMaui.csproj'
if (-not (Test-Path -LiteralPath $proj)) { throw "Project not found: $proj" }

[xml]$xml = Get-Content -LiteralPath $proj -Raw
$version = $xml.Project.PropertyGroup.ApplicationDisplayVersion | Select-Object -First 1
if ([string]::IsNullOrWhiteSpace($version)) { $version = '0.0.0' }

$ts = (Get-Date).ToString('yyyyMMdd_HHmmss')
$distRoot = Join-Path $repoRoot 'dist'
$dotnetSelfContained = -not $FrameworkDependent
if ($FrameworkDependent -and $WinAppRuntimeDependent)
{
  throw "Invalid combination: -FrameworkDependent and -WinAppRuntimeDependent cannot both be set."
}

$windowsAppSdkSelfContained = $dotnetSelfContained -and (-not $WinAppRuntimeDependent)

$scTag = if ($dotnetSelfContained) { 'selfcontained' } else { 'frameworkdependent' }
$winTag = if ($windowsAppSdkSelfContained) { 'winappsdk_selfcontained' } else { 'winappsdk_frameworkdependent' }
$outDir = Join-Path $distRoot ("GcExtensionAuditMaui_{0}_{1}_{2}_{3}_{4}" -f $version, $ts, $RuntimeIdentifier, $scTag, $winTag)

New-Item -ItemType Directory -Path $outDir -Force | Out-Null

Write-Host "Publishing..." -ForegroundColor Cyan
Write-Host "  Project: $proj"
Write-Host "  Config : $Configuration"
Write-Host "  RID    : $RuntimeIdentifier"
Write-Host "  .NET   : SelfContained=$dotnetSelfContained"
Write-Host "  WinApp : SelfContained=$windowsAppSdkSelfContained"
Write-Host "  Single : $SingleFile"
Write-Host "  Lang   : EnglishOnly=$EnglishOnly"
Write-Host "  PDB    : Keep=$KeepPdb"
Write-Host "  Output : $outDir"

$scArg = $dotnetSelfContained.ToString().ToLowerInvariant()
$winAppSc = $windowsAppSdkSelfContained.ToString().ToLowerInvariant()
$singleFileArg = $SingleFile.ToString().ToLowerInvariant()

dotnet publish $proj `
  -c $Configuration `
  -r $RuntimeIdentifier `
  --self-contained $scArg `
  -p:WindowsAppSDKSelfContained=$winAppSc `
  -p:PublishSingleFile=$singleFileArg `
  -p:IncludeNativeLibrariesForSelfExtract=$singleFileArg `
  -o $outDir

function Get-DirBytes([string] $path)
{
  return (Get-ChildItem -Recurse -Force -LiteralPath $path | Measure-Object -Property Length -Sum).Sum
}

if (-not $KeepPdb)
{
  Get-ChildItem -Force -LiteralPath $outDir -Filter *.pdb -File | Remove-Item -Force -ErrorAction SilentlyContinue
}

if ($EnglishOnly)
{
  $rootDirs = Get-ChildItem -Force -LiteralPath $outDir -Directory
  $cultureDirs = $rootDirs | Where-Object {
    # Common satellite folder names: fr, fr-FR, zh-Hans, pt-BR, etc.
    $_.Name -match '^[a-z]{2,3}(-[A-Za-z]{2,4}){0,2}$'
  }

  foreach ($d in $cultureDirs)
  {
    if ($d.Name -match '^en($|-)' ) { continue }
    Remove-Item -Recurse -Force -LiteralPath $d.FullName -ErrorAction SilentlyContinue
  }
}

$bytes = Get-DirBytes $outDir
Write-Host ("Output size: {0:N1} MB" -f ($bytes / 1MB)) -ForegroundColor DarkCyan

Write-Host "Done." -ForegroundColor Green
Write-Host "Run the app from:" -ForegroundColor Green
Write-Host "  $outDir" -ForegroundColor Green
