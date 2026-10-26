import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, readFileSync, rmSync, copyFileSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { repository, policies, artifactName, assertRun, assertConsumer, positive, selectArtifact, createManifest,
  assertBundle, buildSource, createReceipt, resolveSource, validateSelection, sha256 } from '../release-artifacts.mjs';

const sha = 'd71d5c5bd333b8f71979c4818320de05deb0fab4';
const versions = { benchmark: '1.0.6.0', toolkit: '1.0.3.0' };
const clone = value => structuredClone(value);
function run(kind = 'build', product = 'benchmark', attempt = '1') {
  return { id: kind === 'build' ? 100 : 200, run_attempt: Number(attempt), workflow_id: kind === 'build' ? 10 : 20,
    repository: { id: 1, full_name: repository, fork: false }, head_repository: { id: 1, full_name: repository },
    path: kind === 'build' ? policies[product].workflow : '.github/workflows/store-flight.yml',
    head_branch: 'main', head_sha: sha, event: kind === 'build' ? 'push' : 'workflow_dispatch', status: 'completed', conclusion: 'success' };
}
function artifact(source, product = 'benchmark') {
  return { id: source.id + source.run_attempt, name: artifactName(source.id === 100 ? 'release' : 'store-flight', product, source.id, source.run_attempt),
    expired: false, expires_at: '2999-01-01T00:00:00Z', workflow_run: { id: source.id, head_sha: sha, head_branch: 'main', repository_id: 1, head_repository_id: 1 } };
}
function fixture(t, product = 'benchmark', attempt = '1') {
  const directory = mkdtempSync(join(tmpdir(), 'release-provenance-'));
  t.after(() => rmSync(directory, { recursive: true, force: true }));
  const sourceRun = run('build', product, attempt);
  const sourceArtifact = artifact(sourceRun, product);
  const selection = { kind: 'build', product, run: sourceRun, artifact: sourceArtifact };
  const name = policies[product].name, version = versions[product];
  writeFileSync(join(directory, `${name}_${version}_x64.msix`), Buffer.from([0, 255, 80, 75, 17, 1, 2, 3]));
  writeFileSync(join(directory, `${name}-${version}-win-x64.zip`), 'portable archive bytes');
  const source = buildSource(sourceRun, sourceArtifact);
  const manifest = createManifest({ product, version, source, directory, eligible: true });
  const save = () => writeFileSync(join(directory, 'release-manifest.json'), JSON.stringify(manifest));
  save();
  return { directory, product, version, selection, manifest, source, save };
}
function apiFor(...selections) {
  return path => {
    const workflow = selections.find(s => path === `actions/workflows/${s.run.path.split('/').at(-1)}`);
    if (workflow) return { id: workflow.run.workflow_id, path: workflow.run.path };
    const source = selections.find(s => path === `actions/runs/${s.run.id}/attempts/${s.run.run_attempt}`);
    if (source) return source.run;
    const artifacts = selections.filter(s => path.startsWith(`actions/runs/${s.run.id}/artifacts?`)).map(s => s.artifact);
    if (artifacts.length) return { total_count: artifacts.length, artifacts };
    throw new Error(`Missing explicit API fixture: ${path}`);
  };
}
function submitFlight(f, attempt = '1') {
  const flightRun = run('flight', f.product, attempt);
  const flightArtifact = artifact(flightRun, f.product);
  const selection = { kind: 'flight', product: f.product, run: flightRun, artifact: flightArtifact };
  const flight = { repository, workflow: flightRun.path, runId: String(flightRun.id), attempt,
    artifactName: flightArtifact.name };
  const receipt = createReceipt(f.manifest, readFileSync(join(f.directory, 'release-manifest.json')), f.selection, flight);
  writeFileSync(join(f.directory, 'flight-receipt.json'), JSON.stringify(receipt));
  return { selection, receipt };
}

for (const product of ['benchmark', 'toolkit']) {
  test(`${product}: explicit build -> submitted flight -> production preserves original bytes`, t => {
    const f = fixture(t, product, '2');
    const original = readFileSync(join(f.directory, f.manifest.files.msix.name));
    const buildApi = apiFor(f.selection);
    const build = resolveSource(buildApi, { kind: 'build', product, runId: '100', attempt: '2', sha });
    validateSelection(buildApi, build, f.directory, f.version, sha);
    const { selection: flight } = submitFlight(f, '3');
    const api = apiFor(f.selection, flight);
    const tested = resolveSource(api, { kind: 'flight', product, runId: '200', attempt: '3', sha });
    const production = join(f.directory, 'production');
    mkdirSync(production);
    for (const name of [f.manifest.files.msix.name, f.manifest.files.portable.name, 'release-manifest.json', 'flight-receipt.json']) {
      copyFileSync(join(f.directory, name), join(production, name));
    }
    validateSelection(api, tested, production, f.version, sha);
    assert.deepEqual(readFileSync(join(production, f.manifest.files.msix.name)), original);
    assert.equal(sha256(original), f.manifest.files.msix.sha256);
  });
}

for (const [label, mutate, message] of [
  ['PR', r => r.event = 'pull_request', /Source event/],
  ['pull_request_target', r => r.event = 'pull_request_target', /Source event/],
  ['manual build', r => r.event = 'workflow_dispatch', /Source event/],
  ['fork', r => r.head_repository.full_name = 'someone/fork', /forks are forbidden/],
  ['fork repository', r => r.repository.fork = true, /fork flag/],
  ['wrong repository', r => r.repository.full_name = 'someone/other', /Repository/],
  ['wrong workflow path', r => r.path = '.github/workflows/evil.yml', /Workflow path/],
  ['wrong workflow ID', r => r.workflow_id = 999, /Workflow ID/],
  ['failed run', r => r.conclusion = 'failure', /Run conclusion/],
  ['cancelled run', r => r.conclusion = 'cancelled', /Run conclusion/],
  ['unfinished run', r => r.status = 'in_progress', /Run status/],
  ['non-main', r => r.head_branch = 'feature', /Source branch/],
  ['wrong SHA', r => r.head_sha = 'a'.repeat(40), /Source SHA/],
  ['wrong run ID', r => r.id = 101, /Run ID/],
  ['wrong attempt', r => r.run_attempt = 2, /Run attempt/],
]) test(`reject ${label}`, () => {
  const source = run(); mutate(source);
  assert.throws(() => assertRun(source, { kind: 'build', product: 'benchmark', runId: '100', attempt: '1', sha, workflowId: 10 }), message);
});

test('GitHub workflow paths with @main suffix are accepted', () => {
  const source = run(); source.path += '@main';
  assert.doesNotThrow(() => assertRun(source, { kind: 'build', product: 'benchmark', runId: '100', attempt: '1', sha, workflowId: 10 }));
});

test('consumer must be a manual main run at the current remote main SHA', () => {
  const context = { repository, ref: 'refs/heads/main', event: 'workflow_dispatch', sha };
  assert.doesNotThrow(() => assertConsumer(context, sha));
  assert.throws(() => assertConsumer(context, 'a'.repeat(40)), /Current main SHA/);
  for (const [field, value] of [['repository', 'someone/fork'], ['ref', 'refs/heads/feature'], ['event', 'push']]) {
    assert.throws(() => assertConsumer({ ...context, [field]: value }, sha));
  }
});
test('production rejects non-manual flight events and commits outside current main', () => {
  const source = run('flight');
  const expected = { kind: 'flight', product: 'benchmark', runId: '200', attempt: '1', sha, workflowId: 20 };
  assert.doesNotThrow(() => assertRun(source, expected));
  assert.throws(() => assertRun({ ...source, event: 'push' }, expected), /Source event/);
  assert.throws(() => assertRun({ ...source, head_sha: 'a'.repeat(40) }, expected), /Source SHA/);
});
test('IDs and attempts reject empty values, aliases, traversal and trailing newlines', () => {
  for (const value of ['', undefined, 'latest', '0', '../1', '1\n', ' 1', '01']) {
    assert.throws(() => positive(value, 'attempt'), /explicit positive integer/);
  }
});

for (const [label, mutate, message] of [
  ['product', m => m.product = 'toolkit', /Product/],
  ['version', m => m.version = '2.0.0.0', /Version/],
  ['manifest SHA', m => m.source.sha = 'b'.repeat(40), /source sha/],
  ['manifest build attempt', m => m.source.attempt = '2', /source attempt/],
  ['manifest workflow', m => m.source.workflow = 'fake', /source workflow/],
  ['artifact name', m => m.source.artifactName = 'release-other', /artifactName/],
  ['PR eligibility', m => m.eligible = false, /eligibility/],
  ['MSIX checksum', m => m.files.msix.sha256 = '0'.repeat(64), /msix SHA-256/],
  ['portable checksum', m => m.files.portable.sha256 = '0'.repeat(64), /portable SHA-256/],
  ['size', m => m.files.msix.size++, /msix size/],
  ['path traversal', m => m.files.msix.name = '../evil.msix', /filename/],
  ['schema', m => m.schema = 2, /schema/],
]) test(`reject mismatched ${label}`, t => {
  const f = fixture(t); mutate(f.manifest);
  assert.throws(() => assertBundle(f.manifest, f), message);
});

test('reject altered MSIX bytes even when size is unchanged', t => {
  const f = fixture(t);
  const path = join(f.directory, f.manifest.files.msix.name);
  const bytes = readFileSync(path); bytes[0] ^= 255; writeFileSync(path, bytes);
  assert.throws(() => assertBundle(f.manifest, f), /msix SHA-256/);
});
for (const kind of ['msix', 'portable']) test(`reject missing ${kind} without fallback`, t => {
  const f = fixture(t); rmSync(join(f.directory, f.manifest.files[kind].name));
  assert.throws(() => assertBundle(f.manifest, f), /Missing .*no rebuild fallback/);
});
test('reject missing manifest and legacy flight bundle', t => {
  const f = fixture(t); rmSync(join(f.directory, 'release-manifest.json'));
  assert.throws(() => validateSelection(apiFor(f.selection), f.selection, f.directory, f.version, sha), /manifest is missing/);
});
test('reject extra files in a bundle', t => {
  const f = fixture(t); writeFileSync(join(f.directory, 'other.msix'), 'extra');
  assert.throws(() => assertBundle(f.manifest, f), /Unexpected files/);
});

test('reject missing, duplicate, expired and foreign artifacts', () => {
  const source = run(), a = artifact(source);
  assert.throws(() => selectArtifact([], a.name, source), /found 0.*no rebuild fallback/);
  assert.throws(() => selectArtifact([a, a], a.name, source), /found 2/);
  for (const mutate of [x => x.expired = true, x => x.expires_at = '2000-01-01', x => x.expires_at = null]) {
    const bad = clone(a); mutate(bad);
    assert.throws(() => selectArtifact([bad], a.name, source), /expired/);
  }
  for (const field of ['id', 'head_sha', 'head_branch', 'head_repository_id', 'repository_id']) {
    const bad = clone(a); bad.workflow_run[field] = 'foreign';
    assert.throws(() => selectArtifact([bad], a.name, source), /Artifact/);
  }
});
test('select the exact attempt among multiple artifacts; never reuse an earlier attempt', () => {
  const first = run(), second = run('build', 'benchmark', '2');
  const firstArtifact = artifact(first), secondArtifact = artifact(second);
  assert.equal(selectArtifact([firstArtifact, secondArtifact], secondArtifact.name, second).id, secondArtifact.id);
  assert.throws(() => selectArtifact([firstArtifact], secondArtifact.name, second), /another attempt/);
});
test('an explicitly selected successful historical attempt is isolated from a later failed rerun', () => {
  const first = { kind: 'build', product: 'benchmark', run: run(), artifact: artifact(run()) };
  const laterRun = run('build', 'benchmark', '2'); laterRun.conclusion = 'failure';
  const later = { kind: 'build', product: 'benchmark', run: laterRun, artifact: artifact(laterRun) };
  const api = apiFor(first, later);
  const expected = { kind: 'build', product: 'benchmark', runId: '100', sha };
  assert.equal(resolveSource(api, { ...expected, attempt: '1' }).artifact.id, first.artifact.id);
  assert.throws(() => resolveSource(api, { ...expected, attempt: '2' }), /Run conclusion/);
});
test('a successful failed-jobs-only rerun without its own artifacts cannot reuse earlier outputs', () => {
  const secondRun = run('build', 'benchmark', '2');
  const selection = { kind: 'build', product: 'benchmark', run: secondRun, artifact: artifact(run()) };
  assert.throws(() => resolveSource(apiFor(selection), { kind: 'build', product: 'benchmark', runId: '100', attempt: '2', sha }), /another attempt/);
});
test('resolve paginated artifacts by exact run and attempt rather than latest', () => {
  const source = run('build', 'benchmark', '2'), a = artifact(source);
  const paths = [];
  const selection = resolveSource(path => {
    paths.push(path);
    if (path.includes('/workflows/')) return { id: 10, path: policies.benchmark.workflow };
    if (path.includes('/attempts/2')) return source;
    if (path.endsWith('page=1')) return { total_count: 2, artifacts: [{ ...a, name: 'other-attempt' }] };
    if (path.endsWith('page=2')) return { total_count: 2, artifacts: [a] };
    throw new Error('Unexpected API request');
  }, { kind: 'build', product: 'benchmark', runId: '100', attempt: '2', sha });
  assert.equal(selection.artifact.id, a.id);
  assert.ok(paths.some(p => p.endsWith('page=2')));
});
test('after approval, an artifact replacement with the same name but a different ID is refused', t => {
  const f = fixture(t);
  const request = { kind: 'build', product: f.product, runId: '100', attempt: '1', sha,
    expectedArtifactId: String(f.selection.artifact.id) };
  assert.doesNotThrow(() => resolveSource(apiFor(f.selection), request));
  f.selection.artifact.id++;
  assert.throws(() => resolveSource(apiFor(f.selection), request), /Approved artifact ID/);
});
test('reject wrong workflow registration and invalid explicit IDs', () => {
  const api = () => ({ id: 10, path: 'evil' });
  assert.throws(() => resolveSource(api, { kind: 'build', product: 'benchmark', runId: '100', attempt: '1', sha }), /Registered workflow/);
  for (const attempt of ['', 'latest', '0', '../1', '1\n']) {
    assert.throws(() => resolveSource(api, { kind: 'build', product: 'benchmark', runId: '100', attempt, sha }));
  }
});

for (const [label, mutate, message] of [
  ['product', r => r.product = 'toolkit', /Receipt product/],
  ['version', r => r.version = '2.0.0.0', /Receipt version/],
  ['SHA', r => r.sha = 'b'.repeat(40), /Receipt SHA/],
  ['manifest checksum', r => r.manifestSha256 = '0'.repeat(64), /manifest checksum/],
  ['MSIX checksum', r => r.msixSha256 = '0'.repeat(64), /MSIX checksum/],
  ['build artifact ID', r => r.build.artifactId = '999', /Build receipt artifactId/],
  ['flight attempt', r => r.flight.attempt = '2', /Flight receipt attempt/],
  ['flight artifact name', r => r.flight.artifactName = 'other', /Flight receipt artifactName/],
  ['flight workflow', r => r.flight.workflow = 'other', /Flight receipt workflow/],
]) test(`production rejects receipt ${label} mismatch`, t => {
  const f = fixture(t), flight = submitFlight(f); mutate(flight.receipt);
  writeFileSync(join(f.directory, 'flight-receipt.json'), JSON.stringify(flight.receipt));
  assert.throws(() => validateSelection(apiFor(f.selection, flight.selection), flight.selection, f.directory, f.version, sha), message);
});
test('production requires a successful flight with a receipt', t => {
  const f = fixture(t), flight = submitFlight(f);
  rmSync(join(f.directory, 'flight-receipt.json'));
  assert.throws(() => validateSelection(apiFor(f.selection), flight.selection, f.directory, f.version, sha), /receipt is missing/);
  flight.selection.run.conclusion = 'failure';
  assert.throws(() => resolveSource(apiFor(flight.selection), { kind: 'flight', product: f.product, runId: '200', attempt: '1', sha }), /Run conclusion/);
});
test('production refuses an expired original build even if flight still contains its bytes', t => {
  const f = fixture(t), flight = submitFlight(f); f.selection.artifact.expired = true;
  assert.throws(() => validateSelection(apiFor(f.selection, flight.selection), flight.selection, f.directory, f.version, sha), /expired/);
});
test('production rejects a missing original build artifact', t => {
  const f = fixture(t), flight = submitFlight(f);
  const original = apiFor(f.selection, flight.selection);
  const api = path => path.includes('/100/artifacts') ? { total_count: 0, artifacts: [] } : original(path);
  assert.throws(() => validateSelection(api, flight.selection, f.directory, f.version, sha), /found 0/);
});
test('build bundles cannot masquerade as submitted flights', t => {
  const f = fixture(t); submitFlight(f);
  assert.throws(() => validateSelection(apiFor(f.selection), f.selection, f.directory, f.version, sha), /must not contain a flight receipt/);
});
