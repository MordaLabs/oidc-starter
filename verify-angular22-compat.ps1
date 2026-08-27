[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$env:NG_CLI_ANALYTICS = 'false'

$ExpectedVersion = '0.2.0'
$ExpectedPeers = '^20.3.0 || ^21.0.0 || ^22.0.0'

function Run {
    param([string]$Label,[string]$Exe,[string[]]$CommandArgs,[string]$Cwd)
    Write-Host ""
    Write-Host "==> $Label" -ForegroundColor Cyan
    Push-Location $Cwd
    try {
        & $Exe @CommandArgs
        if ($LASTEXITCODE -ne 0) { throw "$Label failed with exit code $LASTEXITCODE." }
    } finally {
        Pop-Location
    }
    Write-Host "PASS: $Label" -ForegroundColor Green
}

function Test-Consumer {
    param([int]$Major,[string]$TempRoot,[string]$Tarball)

    $relativeDir = "angular-$Major-consumer"
    $consumer = Join-Path $TempRoot $relativeDir
    $name = "compat-angular-$Major"

    Run "Create Angular $Major consumer" 'npm' @(
        'exec','--yes',"--package=@angular/cli@$Major",'--','ng','new',$name,
        '--directory',$relativeDir,
        '--standalone','--strict','--routing',
        '--style=css','--skip-git','--skip-install',
        '--package-manager=npm','--ssr=false','--zoneless=false','--defaults'
    ) $TempRoot

    Run "Install Angular $Major baseline" 'npm' @('install') $consumer
    Run "Install packed auth package in Angular $Major" 'npm' @('install',$Tarball,'--save-exact') $consumer

    $appConfig = Join-Path $consumer 'src\app\app.config.ts'
    @"
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { BffAuthService, provideBffAuth } from '@flying-bee/oidc-starter-auth';
import { routes } from './app.routes';

export const bffServiceTypeCheck: typeof BffAuthService = BffAuthService;

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(),
    provideBffAuth({ authPath: '/api/auth' })
  ]
};
"@ | Set-Content -LiteralPath $appConfig -Encoding utf8

    Run "Build Angular $Major BFF consumer" 'npm' @('run','build') $consumer
    Run "Resolve Angular $Major peer graph" 'npm' @(
        'ls','@flying-bee/oidc-starter-auth','@angular/core','@angular/common',
        'angular-auth-oidc-client','rxjs'
    ) $consumer
}

Write-Host 'OIDC Starter - Angular 20/21/22 package compatibility final gate [R6-minimal]' -ForegroundColor Yellow

$repo = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or (Split-Path -Leaf $repo) -ne 'oidc-starter') {
    throw "Run from the oidc-starter repository."
}
if ([IO.Path]::GetFullPath((Get-Location).Path).TrimEnd('\') -ne [IO.Path]::GetFullPath($repo).TrimEnd('\')) {
    throw "Run from repository root: $repo"
}

$frontend = Join-Path $repo 'src\frontend'
$pkgPath = Join-Path $frontend 'projects\oidc-starter-auth\package.json'
$pkg = Get-Content -LiteralPath $pkgPath -Raw | ConvertFrom-Json

if ($pkg.version -ne $ExpectedVersion) { throw "Expected package version $ExpectedVersion, got $($pkg.version)." }
if ($pkg.peerDependencies.'@angular/core' -ne $ExpectedPeers) { throw "Unexpected @angular/core peer range." }
if ($pkg.peerDependencies.'@angular/common' -ne $ExpectedPeers) { throw "Unexpected @angular/common peer range." }

Write-Host "PASS: Package metadata is $ExpectedVersion with Angular 20/21/22 peer range" -ForegroundColor Green

# We already have repeated green workspace build/test evidence from R2-R4.
# This final gate intentionally validates only the missing packed-consumer matrix.
Run 'Build library for consumer tarball' 'npm' @('exec','--','ng','build','oidc-starter-auth') $frontend

$dist = Join-Path $frontend 'dist\oidc-starter-auth'
if (-not (Test-Path (Join-Path $dist 'package.json'))) { throw "Built package output missing." }

$temp = Join-Path ([IO.Path]::GetTempPath()) ("oidc-auth-compat-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $temp | Out-Null

try {
    Run 'Pack 0.2.0 artifact' 'npm' @('pack',$dist,'--pack-destination',$temp) $repo
    $tarball = Get-ChildItem $temp -Filter '*.tgz' -File | Select-Object -First 1
    if ($null -eq $tarball) { throw "Tarball not created." }

    # Angular 21 is tested because the declared peer range claims it.
    Test-Consumer 21 $temp $tarball.FullName

    # Angular 22 is the actual downstream Offer Case requirement.
    Test-Consumer 22 $temp $tarball.FullName
}
finally {
    Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
}

Run 'git diff --check' 'git' @('diff','--check') $repo

Write-Host ""
Write-Host '============================================================' -ForegroundColor Green
Write-Host 'FINAL COMPATIBILITY GATE PASSED.' -ForegroundColor Green
Write-Host 'Angular 21 consumer: PASS' -ForegroundColor Green
Write-Host 'Angular 22 consumer: PASS' -ForegroundColor Green
Write-Host 'No --force or --legacy-peer-deps used.' -ForegroundColor Green
Write-Host 'No package was published and no commit/tag was created.' -ForegroundColor Green
Write-Host '============================================================' -ForegroundColor Green
