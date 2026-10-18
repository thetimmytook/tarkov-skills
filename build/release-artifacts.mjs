// Build tooling only. No network or filesystem work happens when this module is imported.
import { createHash } from 'node:crypto';
import { readFileSync, writeFileSync, readdirSync, statSync, appendFileSync, mkdirSync } from 'node:fs';
import { resolve, join, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

export const repository = 'thetimmytook/tarkov-skills';
export const policies = {
  benchmark: { workflow: '.github/workflows/benchmark-app.yml', name: 'TarkovPerformanceBenchmark' },
  toolkit: { workflow: '.github/workflows/toolkit-app.yml', name: 'TarkovPerformanceToolkit' },
};
const flightWorkflow = '.github/workflows/store-flight.yml';
const requireThat = (condition, message) => { if (!condition) throw new Error(message); };
const same = (actual, expected, field) => requireThat(actual === expected, `${field} mismatch: expected '${expected}', got '${actual}'.`);
export function positive(value, field) {
  requireThat(/^[1-9][0-9]*$/.test(String(value)) && String(value).trim() === String(value), `${field} must be an explicit positive integer.`);
  return String(value);
}
function policy(product) {
  requireThat(Object.hasOwn(policies, product), `Unsupported product '${product}'.`);
  return policies[product];
}
export function artifactName(kind, product, runId, attempt) {
  policy(product);
  return `${kind}-${product}-${positive(runId, 'run ID')}-a${positive(attempt, 'attempt')}`;
}
export const sha256 = bytes => createHash('sha256').update(bytes).digest('hex');
const json = path => JSON.parse(readFileSync(path, 'utf8').replace(/^\uFEFF/, ''));
const writeJson = (path, value) => writeFileSync(path, JSON.stringify(value, null, 2) + '\n');
const workflowPath = path => String(path).split('@')[0];

export function assertConsumer(context, mainSha) {
  same(context.repository, repository, 'Executing repository');
  same(context.ref, 'refs/heads/main', 'Consumer ref');
  same(context.event, 'workflow_dispatch', 'Consumer event');
  same(mainSha, context.sha, 'Current main SHA');
}

export function assertRun(run, { kind, product, runId, attempt, sha, workflowId }) {
  requireThat(['build', 'flight'].includes(kind), `Unknown source kind '${kind}'.`);
  requireThat(/^[0-9a-f]{40}$/.test(sha), 'Source SHA must be a full commit SHA.');
  const expectedWorkflow = kind === 'build' ? policy(product).workflow : flightWorkflow;
  same(String(run.id), positive(runId, 'run ID'), 'Run ID');
  same(String(run.run_attempt), positive(attempt, 'attempt'), 'Run attempt');
  same(run.repository?.full_name, repository, 'Repository');
  same(run.head_repository?.full_name, repository, 'Head repository (forks are forbidden)');
  same(run.repository?.fork, false, 'Repository fork flag');
  same(workflowPath(run.path), expectedWorkflow, 'Workflow path');
  same(String(run.workflow_id), String(workflowId), 'Workflow ID');
  same(run.event, kind === 'build' ? 'push' : 'workflow_dispatch', 'Source event');
  same(run.head_branch, 'main', 'Source branch');
  same(run.head_sha, sha, 'Source SHA');
  same(run.status, 'completed', 'Run status');
  same(run.conclusion, 'success', 'Run conclusion');
}

export function selectArtifact(artifacts, name, run, now = Date.now()) {
  const matches = artifacts.filter(a => a.name === name);
  requireThat(matches.length === 1, `Expected one artifact '${name}', found ${matches.length}. It may be missing, deleted, or from another attempt; no rebuild fallback is allowed.`);
  const artifact = matches[0];
  positive(artifact.id, 'artifact ID');
  requireThat(artifact.expired === false && Number.isFinite(Date.parse(artifact.expires_at)) && Date.parse(artifact.expires_at) > now,
    `Artifact '${name}' has expired; select an available approved build explicitly. No rebuild fallback is allowed.`);
  same(String(artifact.workflow_run?.id), String(run.id), 'Artifact run ID');
  same(artifact.workflow_run?.head_sha, run.head_sha, 'Artifact SHA');
  same(artifact.workflow_run?.head_branch, 'main', 'Artifact branch');
  same(artifact.workflow_run?.head_repository_id, run.head_repository.id, 'Artifact head repository ID');
  same(artifact.workflow_run?.repository_id, run.repository.id, 'Artifact repository ID');
  return artifact;
}

export function createManifest({ product, version, source, directory, eligible }) {
  const definition = policy(product);
  const files = {};
  for (const [kind, name] of Object.entries({ msix: `${definition.name}_${version}_x64.msix`, portable: `${definition.name}-${version}-win-x64.zip` })) {
    const bytes = readFileSync(join(directory, name));
    files[kind] = { name, sha256: sha256(bytes), size: bytes.length };
  }
  return { schema: 1, product, version, source: structuredClone(source), eligible, files };
}

export function assertBundle(manifest, { product, version, source, directory }) {
  same(manifest.schema, 1, 'Manifest schema');
  same(manifest.eligible, true, 'Release eligibility');
  same(manifest.product, product, 'Product');
  same(manifest.version, version, 'Version');
  for (const field of ['repository', 'sha', 'workflow', 'event', 'branch', 'runId', 'attempt', 'artifactName']) {
    same(manifest.source?.[field], source[field], `Manifest source ${field}`);
  }
  const definition = policy(product);
  for (const [kind, expectedName] of Object.entries({ msix: `${definition.name}_${version}_x64.msix`, portable: `${definition.name}-${version}-win-x64.zip` })) {
    const file = manifest.files?.[kind];
    same(file?.name, expectedName, `${kind} filename`);
    const path = join(directory, expectedName);
    requireThat(statSync(path, { throwIfNoEntry: false })?.isFile(), `Missing ${kind} file '${expectedName}'; no rebuild fallback is allowed.`);
    const bytes = readFileSync(path);
    same(file.size, bytes.length, `${kind} size`);
    same(file.sha256, sha256(bytes), `${kind} SHA-256`);
  }
  const allowed = [manifest.files.msix.name, manifest.files.portable.name, 'release-manifest.json', 'flight-receipt.json'];
  requireThat(readdirSync(directory).every(name => allowed.includes(name)), 'Unexpected files in release bundle.');
  return manifest;
}

export function buildSource(run, artifact) {
  return { repository, sha: run.head_sha, workflow: workflowPath(run.path), event: run.event,
    branch: run.head_branch, runId: String(run.id), attempt: String(run.run_attempt), artifactName: artifact.name };
}

export function createReceipt(manifest, manifestBytes, selection, flight) {
  return { schema: 1, product: manifest.product, version: manifest.version, sha: manifest.source.sha,
    build: { runId: manifest.source.runId, attempt: manifest.source.attempt,
      artifactId: String(selection.artifact.id), artifactName: selection.artifact.name },
    flight, manifestSha256: sha256(manifestBytes), msixSha256: manifest.files.msix.sha256 };
}

export function assertReceipt(receipt, manifest, manifestBytes, selection, buildSelection) {
  same(receipt.schema, 1, 'Receipt schema');
  same(receipt.product, manifest.product, 'Receipt product');
  same(receipt.version, manifest.version, 'Receipt version');
  same(receipt.sha, manifest.source.sha, 'Receipt SHA');
  same(receipt.manifestSha256, sha256(manifestBytes), 'Receipt manifest checksum');
  same(receipt.msixSha256, manifest.files.msix.sha256, 'Receipt MSIX checksum');
  for (const [field, expected] of Object.entries({ repository, workflow: flightWorkflow,
    runId: String(selection.run.id), attempt: String(selection.run.run_attempt), artifactName: selection.artifact.name })) {
    same(receipt.flight?.[field], expected, `Flight receipt ${field}`);
  }
  for (const [field, expected] of Object.entries({ runId: String(buildSelection.run.id), attempt: String(buildSelection.run.run_attempt),
    artifactId: String(buildSelection.artifact.id), artifactName: buildSelection.artifact.name })) {
    same(receipt.build?.[field], expected, `Build receipt ${field}`);
  }
}

// Injected API boundary keeps all trust decisions testable without a GitHub account.
export function resolveSource(api, { kind, product, runId, attempt, sha, expectedArtifactId }) {
  requireThat(['build', 'flight'].includes(kind), `Unknown source kind '${kind}'.`);
  positive(runId, 'run ID'); positive(attempt, 'attempt'); policy(product);
  const path = kind === 'build' ? policy(product).workflow : flightWorkflow;
  const workflow = api(`actions/workflows/${basename(path)}`);
  same(workflow.path, path, 'Registered workflow path');
  const run = api(`actions/runs/${runId}/attempts/${attempt}`);
  assertRun(run, { kind, product, runId, attempt, sha, workflowId: workflow.id });
  const artifacts = [];
  // The artifacts endpoint spans attempts. Never choose the newest item or omit pagination.
  for (let page = 1; ; page++) {
    const result = api(`actions/runs/${runId}/artifacts?per_page=100&page=${page}`);
    artifacts.push(...result.artifacts);
    if (artifacts.length >= result.total_count) break;
    requireThat(result.artifacts.length > 0, 'Artifact pagination ended before the advertised count.');
  }
  const artifact = selectArtifact(artifacts, artifactName(kind === 'build' ? 'release' : 'store-flight', product, runId, attempt), run);
  if (expectedArtifactId) same(String(artifact.id), positive(expectedArtifactId, 'expected artifact ID'), 'Approved artifact ID');
  return { kind, product, run, artifact };
}

export function validateSelection(api, selection, directory, version, sha) {
  const manifestPath = join(directory, 'release-manifest.json');
  requireThat(statSync(manifestPath, { throwIfNoEntry: false })?.isFile(), 'Release manifest is missing. Legacy flight artifacts must finish on the existing pipeline before this change is merged.');
  const bytes = readFileSync(manifestPath);
  const manifest = json(manifestPath);
  let build = selection;
  if (selection.kind === 'flight') {
    const receiptPath = join(directory, 'flight-receipt.json');
    requireThat(statSync(receiptPath, { throwIfNoEntry: false })?.isFile(), 'Successful flight receipt is missing; production requires the submitted package.');
    const receipt = json(receiptPath);
    build = resolveSource(api, { kind: 'build', product: selection.product, runId: receipt.build?.runId, attempt: receipt.build?.attempt, sha });
    assertReceipt(receipt, manifest, bytes, selection, build);
  } else {
    requireThat(!statSync(join(directory, 'flight-receipt.json'), { throwIfNoEntry: false }), 'Build artifact must not contain a flight receipt.');
  }
  return assertBundle(manifest, { product: selection.product, version, source: buildSource(build.run, build.artifact), directory });
}

function githubApi(path) {
  try { return JSON.parse(execFileSync('gh', ['api', `repos/${repository}/${path}`], { encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] })); }
  catch { throw new Error(`Could not read GitHub ${path}. The selected run/artifact may be missing or inaccessible; no rebuild fallback is allowed.`); }
}
function currentMain() {
  same(process.env.GITHUB_REPOSITORY, repository, 'Executing repository');
  assertConsumer({ repository: process.env.GITHUB_REPOSITORY, ref: process.env.GITHUB_REF,
    event: process.env.GITHUB_EVENT_NAME, sha: process.env.GITHUB_SHA }, githubApi('git/ref/heads/main').object.sha);
}
function output(values) {
  for (const [name, value] of Object.entries(values)) {
    requireThat(!/[\r\n]/.test(String(value)), `Invalid output '${name}'.`);
    if (process.env.GITHUB_OUTPUT) appendFileSync(process.env.GITHUB_OUTPUT, `${name}=${value}\n`);
  }
}
function cli([command, product, directory = 'store-package']) {
  const definition = policy(product);
  const version = json(`apps/tarkov-performance-${product}/packaging/store-release.json`).packageVersion;
  if (command === 'manifest') {
    const eligible = process.env.GITHUB_EVENT_NAME === 'push' && process.env.GITHUB_REF === 'refs/heads/main' && process.env.GITHUB_REPOSITORY === repository;
    const name = artifactName(eligible ? 'release' : 'check', product, process.env.GITHUB_RUN_ID, process.env.GITHUB_RUN_ATTEMPT);
    const source = { repository: process.env.GITHUB_REPOSITORY, sha: process.env.GITHUB_SHA, workflow: definition.workflow,
      event: process.env.GITHUB_EVENT_NAME, branch: process.env.GITHUB_REF_NAME,
      runId: process.env.GITHUB_RUN_ID, attempt: process.env.GITHUB_RUN_ATTEMPT, artifactName: name };
    writeJson(join(directory, 'release-manifest.json'), createManifest({ product, version, source, directory, eligible }));
    output({ artifact_name: name });
    return;
  }
  currentMain();
  if (command === 'resolve') {
    // Store stages must be manually dispatched from main. App releases also dispatch from main.
    const selection = resolveSource(githubApi, { kind: process.env.SOURCE_KIND, product,
      runId: process.env.SOURCE_RUN_ID, attempt: process.env.SOURCE_ATTEMPT, sha: process.env.GITHUB_SHA,
      expectedArtifactId: process.env.EXPECTED_ARTIFACT_ID });
    mkdirSync('artifacts', { recursive: true });
    writeJson('artifacts/artifact-selection.json', selection);
    output({ artifact_id: selection.artifact.id, artifact_name: selection.artifact.name });
  } else if (command === 'validate') {
    const selection = json('artifacts/artifact-selection.json');
    const manifest = validateSelection(githubApi, selection, directory, version, process.env.GITHUB_SHA);
    output({ package_path: `${directory}/${manifest.files.msix.name}`, msix_sha256: manifest.files.msix.sha256 });
  } else if (command === 'receipt') {
    const selection = json('artifacts/artifact-selection.json');
    same(selection.kind, 'build', 'Receipt source');
    const manifest = validateSelection(githubApi, selection, directory, version, process.env.GITHUB_SHA);
    const flight = { repository, workflow: flightWorkflow, runId: process.env.GITHUB_RUN_ID, attempt: process.env.GITHUB_RUN_ATTEMPT,
      artifactName: artifactName('store-flight', product, process.env.GITHUB_RUN_ID, process.env.GITHUB_RUN_ATTEMPT) };
    writeJson(join(directory, 'flight-receipt.json'), createReceipt(manifest, readFileSync(join(directory, 'release-manifest.json')), selection, flight));
    output({ artifact_name: flight.artifactName });
  } else throw new Error(`Unknown command '${command}'.`);
}
if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url)) {
  try { cli(process.argv.slice(2)); }
  catch (error) { console.error(`Release artifact validation failed: ${error.message}`); process.exitCode = 1; }
}
