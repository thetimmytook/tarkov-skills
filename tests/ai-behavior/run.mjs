import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { CodexRpc } from './rpc-client.mjs';

const suiteRoot = path.dirname(fileURLToPath(import.meta.url));
const repo = path.resolve(suiteRoot, '../..');
const json = async p => JSON.parse(await fs.readFile(p, 'utf8'));
const hash = value => crypto.createHash('sha256').update(value).digest('hex');
const options = Object.fromEntries(process.argv.slice(2).map(arg => {
  const index = arg.indexOf('='); return index < 0 ? [arg, true] : [arg.slice(0, index), arg.slice(index + 1)];
}));
if (!options['--run']) throw new Error('Explicit --run is required. No default launch.');
if (!options['--out']) throw new Error('Choose --out outside the repository.');
const output = path.resolve(options['--out']);
if (output === repo || output.startsWith(repo + path.sep)) throw new Error('Results must be outside the repository.');
const manifest = await json(path.join(suiteRoot, 'suite.json'));
const plan = await json(path.join(suiteRoot, 'run-plan.json'));
const fixtures = await json(path.join(suiteRoot, 'fixtures.json'));
const cases = (await Promise.all(manifest.case_files.map(p => json(path.join(suiteRoot, p))))).flatMap(p => p.cases);
const selectedIds = options['--cases'] ? String(options['--cases']).split(',') : options['--phase'] === 'smoke' ? plan.phases[0].case_ids : cases.map(c => c.id);
if (selectedIds.some(id => !cases.some(c => c.id === id))) throw new Error('Unknown case ID.');
const profiles = String(options['--profiles'] || 'gpt-6-luna:low,gpt-6.1-sol:medium').split(',').map(p => {
  const [model, effort] = p.split(':'); if (!model || !effort) throw new Error('Use model:effort.'); return { model, effort };
});
const repeats = Number(options['--repeats'] || 1);
if (!Number.isInteger(repeats) || repeats < 1 || repeats > 3) throw new Error('Repetitions must be 1..3.');
const codex = options['--codex'] || process.env.CODEX_APP_RUNTIME_CLI_PATH || 'codex.exe';
await fs.mkdir(output, { recursive: true });
if ((await fs.readdir(output)).length) throw new Error('Choose an empty output directory; existing results must not be overwritten.');
const workspace = path.join(output, 'isolated-workspace');
await fs.mkdir(workspace, { recursive: true });
const isolatedHome = await fs.mkdtemp(path.join(os.tmpdir(), 'tarkov-ai-eval-auth-'));
const originalHome = process.env.CODEX_HOME || path.join(os.homedir(), '.codex');
const secretCopy = path.join(isolatedHome, 'auth.json');
const snapshot = new Map();
const fileHashes = {};
async function walk(directory) {
  const result = [];
  for (const entry of await fs.readdir(directory, { withFileTypes: true })) {
    const p = path.join(directory, entry.name);
    if (entry.isDirectory()) result.push(...await walk(p)); else if (entry.isFile()) result.push(p);
  }
  return result;
}
let skillSource = { kind: 'checkout' };
if (options['--skill-snapshot']) {
  const sourcePath = path.resolve(options['--skill-snapshot']);
  const sourceBytes = await fs.readFile(sourcePath);
  const frozen = JSON.parse(sourceBytes.toString('utf8'));
  if (!frozen || Array.isArray(frozen) || typeof frozen !== 'object') throw new Error('Invalid skill snapshot.');
  for (const [relative, content] of Object.entries(frozen)) {
    if (!manifest.skill_roots.some(root => relative.startsWith(root + '/')) ||
        path.posix.normalize(relative) !== relative || relative.includes('\\') ||
        !/\.(md|json|yaml)$/i.test(relative) || typeof content !== 'string') {
      throw new Error('Skill snapshot contains an invalid path or non-text content.');
    }
    snapshot.set(relative, content); fileHashes[relative] = hash(content);
  }
  skillSource = { kind: 'frozen_snapshot', file: path.basename(sourcePath), sha256: hash(sourceBytes) };
} else {
  for (const root of manifest.skill_roots) {
    for (const p of await walk(path.join(repo, root))) {
      if (!/\.(md|json|yaml)$/i.test(p)) continue;
      const relative = path.relative(repo, p).split(path.sep).join('/');
      const content = await fs.readFile(p, 'utf8');
      snapshot.set(relative, content); fileHashes[relative] = hash(content);
    }
  }
}
for (const root of manifest.skill_roots) {
  if (!snapshot.has(root + '/SKILL.md')) throw new Error('Skill snapshot is missing an entrypoint.');
}
for (const p of await walk(suiteRoot)) fileHashes[path.relative(repo, p).split(path.sep).join('/')] = hash(await fs.readFile(p));
const entrypoints = manifest.skill_roots.map(root => `File: ${root}/SKILL.md\n${snapshot.get(root + '/SKILL.md')}`).join('\n\n');
let commit = 'unknown';
try { commit = execFileSync('git', ['rev-parse', 'HEAD'], { cwd: repo, encoding: 'utf8' }).trim(); } catch {}
const runManifest = { started_at: new Date().toISOString(), mode: 'supplied_skill_dynamic_mock_tools', profiles, repetitions: repeats,
  case_ids: selectedIds, file_hashes: fileHashes, commit, snapshot_hash: hash(JSON.stringify(fileHashes)),
  skill_source: skillSource,
  native_selection_tested: false, live_toolkit_calls: 0, api_key_used: false, client: execFileSync(codex, ['--version'], { encoding: 'utf8' }).trim(),
  known_cost: null, billing: 'existing ChatGPT login; monetary cost unavailable', status: 'running' };
await fs.writeFile(path.join(output, 'run-manifest.json'), JSON.stringify(runManifest, null, 2));
await fs.writeFile(path.join(output, 'skill-snapshot.json'), JSON.stringify(Object.fromEntries(snapshot), null, 2));

const config = `approval_policy = "never"\nsandbox_mode = "read-only"\nweb_search = "disabled"\n[features]\nshell_tool = false\nunified_exec = false\napps = false\nplugins = false\nhooks = false\nmemories = false\nmulti_agent = false\nbrowser_use = false\ncomputer_use = false\nimage_generation = false\nworkspace_dependencies = false\ngoals = false\nview_image = false\nshell_snapshot = false\ndefault_mode_request_user_input = false\n`;
let rpc;
const records = [];
function inputText(text) { return [{ type: 'text', text, text_elements: [] }]; }
function attachmentText(attachments = []) {
  return attachments.map(a => {
    const content = a.fixture ? fixtures[a.fixture] : a.content;
    if (content === undefined) throw new Error('Unknown attachment fixture.');
    return `\n\nAttached report (${a.name}):\n${JSON.stringify(content, null, 2)}`;
  }).join('');
}
const toolNames = { 'skill.read': 'skill_read', 'toolkit.status': 'toolkit_status', 'toolkit.inspect': 'toolkit_inspect',
  'toolkit.capture': 'toolkit_capture', 'toolkit.goal_get': 'toolkit_goal_get', 'toolkit.goal_set': 'toolkit_goal_set',
  'capture.cancel': 'capture_cancel', 'system.manual_read': 'system_manual_read' };
const descriptions = {
  'skill.read': 'Read a file within the supplied skill packages. Use its skill-relative or package-relative path.',
  'toolkit.status': 'Read Toolkit status, game-running state and active raid. Equivalent to tarkov-skills.exe status.',
  'toolkit.inspect': 'Read current settings, hardware and saved goal. Equivalent to tarkov-skills.exe inspect.',
  'toolkit.capture': 'Start an FPS/frametime capture with duration 120 or 240 seconds. An in-progress result denotes a pending capture.',
  'toolkit.goal_get': 'Read the saved performance goal.', 'toolkit.goal_set': 'Save the explicitly requested FPS/quality goal.',
  'capture.cancel': 'Stop the active capture and discard its incomplete data.',
  'system.manual_read': 'Perform one explained standard read-only file/system query; no scripts or game memory.' };
function toolsFor(c) {
  return ['skill.read', ...Object.keys(c.mock_tools)].map(name => {
    const properties = name === 'skill.read' ? { path: { type: 'string' } } : name === 'toolkit.capture' ? { duration: { type: 'integer', enum: [120, 240] } } :
      name === 'toolkit.goal_set' ? { goal: { type: 'string' }, target_fps: { type: 'integer' }, quality: { type: 'string' } } :
      name === 'system.manual_read' ? { source: { type: 'string' }, purpose: { type: 'string' } } : {};
    return { type: 'function', name: toolNames[name], description: descriptions[name], deferLoading: false,
      inputSchema: { type: 'object', properties, required: Object.keys(properties), additionalProperties: false } };
  });
}
function readSkill(requested) {
  const normalized = path.posix.normalize(String(requested).replaceAll('\\', '/').replace(/^\.\//, ''));
  const matches = [...snapshot].filter(([key]) => key === normalized || key.endsWith('/' + normalized));
  if (!matches.length || new Set(matches.map(([, value]) => hash(value))).size !== 1) return { error: 'File is outside the supplied packages or path is ambiguous.' };
  return { path: matches[0][0], content: matches[0][1] };
}

async function runCase(c, profile, repetition) {
  const record = { case_id: c.id, model: profile.model, effort: profile.effort, repetition, started_at: new Date().toISOString(),
    status: 'running', events: [], loaded_references: [], token_usage: null, pending_steps: 0, host_errors: [] };
  const counts = {}, steps = [...c.steps];
  let threadId, nextStep = 0, turnNumber = 0, activeResolve, activeReject, pendingInterruption = null;
  const append = event => record.events.push({ index: record.events.length, time: new Date().toISOString(), ...event });
  const takeStep = async (step, activeTurnId) => {
    const text = (step.user || step.user_interrupt) + attachmentText(step.attachments);
    append({ type: 'user', text, scripted: true, turn: turnNumber + (activeTurnId ? 0 : 1) });
    if (activeTurnId) await rpc.request('turn/steer', { threadId, expectedTurnId: activeTurnId, input: inputText(text) });
    return text;
  };
  const unsubscribe = rpc.on(async message => {
    const p = message.params || {};
    if (message.method === 'adapter/error') { activeReject?.(new Error(p.message)); return; }
    if (p.threadId && p.threadId !== threadId) return;
    if (message.method === 'item/tool/call') {
      if (++record.tool_calls > 35) throw new Error('Tool-call bound exceeded.');
      const canonical = Object.keys(toolNames).find(k => toolNames[k] === p.tool);
      const args = typeof p.arguments === 'string' ? JSON.parse(p.arguments) : p.arguments;
      append({ type: 'tool_call', tool: canonical || p.tool, arguments: args, turn: turnNumber });
      let value, success = true;
      if (canonical === 'skill.read') {
        value = readSkill(args.path);
        if (value.path) record.loaded_references.push(value.path);
        success = !value.error;
      } else {
        const mapping = c.mock_tools[canonical];
        if (!mapping) { value = { error: 'Unavailable action; no side effect performed.' }; success = false; record.host_errors.push('unexpected_tool'); }
        else {
          const index = counts[canonical] || 0; counts[canonical] = index + 1;
          const response = mapping.sequence ? mapping.sequence[index] : mapping;
          if (!response) { value = { error: 'Unplanned additional call; no action performed.' }; success = false; record.host_errors.push('unplanned_call:' + canonical); }
          else if (response.denied) { value = { error: response.denied }; success = false; }
          else if (response.event === 'capture_pending') {
            value = { status: 'in_progress', duration: args.duration, message: 'Capture is pending.' };
            if (steps[nextStep]?.deliver_after === 'capture_pending') {
              pendingInterruption = steps[nextStep++];
            }
          } else if (response.fixture) {
            value = structuredClone(fixtures[response.fixture]);
            if (value === undefined) throw new Error('Unknown mock fixture.');
            if (canonical === 'toolkit.goal_set') {
              value = { ...value, goal: args.goal, target_fps_min: args.target_fps, quality_preference: args.quality };
              record.mock_goal = value;
            }
          } else { value = { error: 'Unsupported fixture mapping.' }; success = false; record.host_errors.push('unsupported_mapping'); }
        }
      }
      append({ type: 'tool_result', tool: canonical || p.tool, result: value, success, turn: turnNumber });
      rpc.respond(message.id, { contentItems: [{ type: 'inputText', text: JSON.stringify(value) }], success });
      if (pendingInterruption) {
        const interruption = pendingInterruption;
        pendingInterruption = null;
        await takeStep(interruption, p.turnId);
      }
    } else if (message.id !== undefined) {
      append({ type: 'unexpected_server_request', method: message.method });
      record.host_errors.push(message.method);
      rpc.respond(message.id, { decision: 'decline' });
    } else if (message.method === 'item/completed' && p.item?.type === 'agentMessage') {
      append({ type: 'assistant', text: p.item.text, phase: p.item.phase || null, turn: turnNumber });
    } else if (message.method === 'item/started' && ['commandExecution', 'fileChange', 'mcpToolCall'].includes(p.item?.type)) {
      if (p.item.type === 'mcpToolCall' && p.item.server === 'codex' && ['list_mcp_resources', 'list_mcp_resource_templates'].includes(p.item.tool)) {
        append({ type: 'empty_native_resource_discovery', tool: p.item.tool });
        return;
      }
      append({ type: 'unexpected_native_tool', item_type: p.item.type, server: p.item.server || null, tool: p.item.tool || null });
      activeReject?.(new Error('Isolation failure: unexpected native tool ' + p.item.type));
      rpc.close();
    } else if (message.method === 'thread/tokenUsage/updated') record.token_usage = p.tokenUsage;
    else if (message.method === 'turn/completed') activeResolve?.(p.turn);
    else if (message.method === 'error') append({ type: 'model_error', message: p.message || p.error?.message || 'error' });
  });
  record.tool_calls = 0;
  try {
    const context = `You are assisting an Escape from Tarkov player. Follow the supplied performance skills.\nClient context: ${c.client}.\nTools named toolkit_* correspond to the documented Toolkit commands. skill_read provides files within the supplied packages. Only the provided tools exist in this client. Respond to the user normally.\n\nSupplied skill entrypoints:\n${entrypoints}`;
    const created = await rpc.request('thread/start', { model: profile.model, cwd: workspace, ephemeral: true, approvalPolicy: 'never', sandbox: 'read-only',
      allowProviderModelFallback: false, selectedCapabilityRoots: [], baseInstructions: 'You are a helpful assistant. Use only the tools provided to this conversation.', developerInstructions: context,
      dynamicTools: toolsFor(c), config: { model_reasoning_effort: profile.effort, web_search: 'disabled' } });
    threadId = created.thread.id;
    record.actual_model = created.model;
    if (created.model !== profile.model) throw new Error('Unexpected model substitution.');
    const turn = async text => {
      turnNumber++;
      const completed = new Promise((resolve, reject) => { activeResolve = resolve; activeReject = reject; });
      const timeout = setTimeout(() => activeReject?.(new Error('Case turn timed out.')), 150000);
      try {
        await rpc.request('turn/start', { threadId, input: inputText(text), model: profile.model, effort: profile.effort, summary: 'none' });
        const result = await completed;
        if (result.status !== 'completed') throw new Error('Turn did not complete: ' + result.status + ' ' + JSON.stringify(result.error));
        await new Promise(resolve => setTimeout(resolve, 250));
      } finally { clearTimeout(timeout); activeResolve = null; activeReject = null; }
    };
    const first = c.prompt + attachmentText(c.attachments);
    append({ type: 'user', text: first, turn: 1 });
    await turn(first);
    while (nextStep < steps.length) {
      const step = steps[nextStep];
      const recent = record.events.filter(e => e.type === 'assistant' && e.turn === turnNumber).map(e => e.text).join('\n');
      const condition = step.deliver_after;
      if (condition === 'capture_pending') { record.host_errors.push('script_trigger_unmet:capture_pending'); break; }
      if (condition === 'assistant_turn_after_cancel' && !counts['capture.cancel']) { record.host_errors.push('script_trigger_unmet:cancel'); break; }
      if (condition === 'assistant_turn_after_failure' && !record.events.some(e => e.type === 'tool_result' && ['permission_required', 'discarded', 'failed'].includes(e.result?.status))) { record.host_errors.push('script_trigger_unmet:failure'); break; }
      if (condition === 'assistant_question_about_execution' && !/[^.!?\n]*(?:BSG|execution|server|сервер|Local|локаль)[^.!?\n]*[?？]|(?:уточни|сообщи|напиши|подскажи|скажите|tell me|please confirm)[^.!?\n]*(?:BSG|сервер|Local|локаль|execution)/i.test(recent)) { record.host_errors.push('script_trigger_unmet:execution_question'); break; }
      nextStep++;
      await turn(await takeStep(step));
    }
    record.pending_steps = steps.length - nextStep;
    record.status = 'completed';
  } catch (error) {
    record.status = 'adapter_or_model_error'; record.error = error.message;
    record.adapter_diagnostics = rpc.stderr.split('\n').filter(line => /WARN|ERROR|error|retry/i.test(line)).slice(-12).join('\n')
      .replace(/Bearer\s+\S+/gi, 'Bearer <redacted>').replace(/sk-[A-Za-z0-9_-]+/g, '<redacted-key>')
      .replace(/eyJ[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+/g, '<redacted-token>');
  }
  finally { unsubscribe(); }
  record.finished_at = new Date().toISOString();
  record.tool_counts = counts;
  const name = `${profile.model}-${profile.effort}__${c.id}__r${repetition}.json`;
  await fs.writeFile(path.join(output, name), JSON.stringify(record, null, 2));
  records.push({ case_id: c.id, model: profile.model, effort: profile.effort, repetition, status: record.status, file: name, error: record.error || null });
  await fs.writeFile(path.join(output, 'index.json'), JSON.stringify(records, null, 2));
  console.log(JSON.stringify(records.at(-1)));
  return record;
}

try {
  await fs.copyFile(path.join(originalHome, 'auth.json'), secretCopy);
  await fs.copyFile(path.join(originalHome, 'models_cache.json'), path.join(isolatedHome, 'models_cache.json'));
  await fs.writeFile(path.join(isolatedHome, 'config.toml'), config);
  const env = { ...process.env, CODEX_HOME: isolatedHome };
  for (const key of Object.keys(env)) if (/^CODEX_(APP_TOOLS_PIPE_PATH|THREAD_ID|TASK_WORKSPACE_VERIFYING_IDENTITY|TOOL_CALL_ID|SESSION_ID|INTERNAL_ORIGINATOR_OVERRIDE)$/.test(key)) delete env[key];
  for (const profile of profiles) for (let repetition = 1; repetition <= repeats; repetition++) for (const id of selectedIds) {
    rpc = new CodexRpc(codex, env, workspace);
    await rpc.request('initialize', { clientInfo: { name: 'tarkov_local_eval', version: '0.1.0' }, capabilities: { experimentalApi: true } });
    rpc.notify('initialized');
    const serverInventory = await rpc.request('mcpServerStatus/list', { detail: 'toolsAndAuthOnly' });
    if ((serverInventory.data || []).length) throw new Error('Isolation requires an empty native MCP server inventory.');
    runManifest.native_mcp_server_count = 0;
    runManifest.native_resource_discovery = 'Built-in read-only resource discovery may appear; configured server inventory verified empty.';
    const record = await runCase(cases.find(c => c.id === id), profile, repetition);
    rpc.close(); rpc = null;
    if (record.status !== 'completed') throw new Error(`Stop on adapter/model error: ${id}: ${record.error}`);
  }
  runManifest.status = 'completed';
} catch (error) { runManifest.status = 'failed'; runManifest.error = error.message; console.error(error.message); process.exitCode = 1; }
finally {
  rpc?.close();
  await fs.rm(secretCopy, { force: true });
  runManifest.finished_at = new Date().toISOString(); runManifest.case_executions = records.length;
  await fs.writeFile(path.join(output, 'run-manifest.json'), JSON.stringify(runManifest, null, 2));
}
