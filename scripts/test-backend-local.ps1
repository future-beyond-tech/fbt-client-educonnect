#requires -Version 5.1
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'lib\local-env.ps1')

Load-RepoEnv
Set-BackendDefaults

$repoRoot = Get-RepoRoot
$testProject = Join-Path $repoRoot 'apps\api\tests\EduConnect.Api.Tests\EduConnect.Api.Tests.csproj'

Print-BackendSummary

& dotnet test $testProject -c Release
exit $LASTEXITCODE
