[CmdletBinding()]
param([string] $AcademyRoot = 'C:\projects\timmy-academy')

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$backendRoot = [IO.Path]::GetFullPath($AcademyRoot)
if (-not (Test-Path -LiteralPath (Join-Path $backendRoot 'packages\contracts\src\submission.ts'))) {
    throw 'The Academy source contract checkout is required for this development check.'
}
$temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
$work = Join-Path $temporaryRoot ('tarkov-resource-contract-' + [Guid]::NewGuid().ToString('N'))
$previousExport = $env:ACADEMY_RESOURCE_CONTRACT_OUTPUT
New-Item -ItemType Directory -Path $work | Out-Null
try {
    $env:ACADEMY_RESOURCE_CONTRACT_OUTPUT = $work
    $project = Join-Path $repositoryRoot 'apps\tarkov-performance-toolkit\tests\TarkovSkills.Core.Tests\TarkovSkills.Core.Tests.csproj'
    & dotnet test $project -c Debug --filter 'FullyQualifiedName~ResourceSubmissionTests|FullyQualifiedName~ResourceOutboxTests|FullyQualifiedName~WindowsTelemetryTests.NativeCounters' --logger 'console;verbosity=minimal'
    if ($LASTEXITCODE -ne 0) { throw 'Desktop resource contract tests failed.' }
    $validator = Join-Path $work 'validate.mjs'
    @'
import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
const { submissionRequestSchema } = await import(pathToFileURL(path.join(process.argv[2], 'packages/contracts/src/submission.ts')).href);
const directory = process.argv[3];
let accepted = 0, rejected = 0, failed = 0;
for (const file of fs.readdirSync(directory).filter(f => f.endsWith('.json')).sort()) {
  const result = submissionRequestSchema.safeParse(JSON.parse(fs.readFileSync(path.join(directory, file), 'utf8')));
  const expected = !file.startsWith('invalid-');
  if (result.success !== expected) {
    failed++;
    console.error(file, result.success ? 'unexpected acceptance' : JSON.stringify(result.error.issues));
  } else if (expected) accepted++; else rejected++;
}
console.log(JSON.stringify({ accepted, rejected, failed }));
if (failed || accepted === 0 || rejected === 0) process.exitCode = 1;
'@ | Set-Content -LiteralPath $validator -Encoding utf8
    # Uses the existing Academy development dependencies and reads TS sources directly.
    # No backend build, database, network request or application upload is performed.
    Push-Location -LiteralPath $backendRoot
    try {
        & node --import tsx $validator $backendRoot $work
        if ($LASTEXITCODE -ne 0) { throw 'C# JSON does not match the Academy source contract.' }
    } finally { Pop-Location }
} finally {
    $env:ACADEMY_RESOURCE_CONTRACT_OUTPUT = $previousExport
    $resolvedWork = [IO.Path]::GetFullPath($work)
    if (-not $resolvedWork.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedWork) -notmatch '^tarkov-resource-contract-[0-9a-f]{32}$') {
        throw 'Refusing cleanup outside the uniquely owned contract-test directory.'
    }
    Remove-Item -LiteralPath $resolvedWork -Recurse -Force
}
