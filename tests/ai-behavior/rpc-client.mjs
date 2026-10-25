import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';

// Development-only JSON-RPC transport. This file is never part of a skill bundle.
export class CodexRpc {
  constructor(executable, env, cwd) {
    this.pending = new Map();
    this.nextId = 1;
    this.handlers = new Set();
    this.stderr = '';
    this.process = spawn(executable, ['app-server', '--stdio'], {
      env, cwd, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'],
    });
    this.process.stderr.on('data', chunk => { this.stderr = (this.stderr + chunk).slice(-12000); });
    this.process.on('error', error => this.fail(error));
    this.process.on('exit', code => this.fail(new Error(`App server exited (${code}).`)));
    createInterface({ input: this.process.stdout }).on('line', line => {
      let message;
      try { message = JSON.parse(line); } catch { return; }
      if (message.id !== undefined && !message.method) {
        const pending = this.pending.get(message.id);
        if (!pending) return;
        this.pending.delete(message.id);
        clearTimeout(pending.timer);
        message.error ? pending.reject(new Error(JSON.stringify(message.error))) : pending.resolve(message.result);
      } else {
        for (const handler of this.handlers) {
          Promise.resolve(handler(message)).catch(error => this.fail(error));
        }
      }
    });
  }

  send(message) { this.process.stdin.write(JSON.stringify(message) + '\n'); }

  request(method, params, timeoutMs = 120000) {
    const id = this.nextId++;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`RPC timeout: ${method}`));
      }, timeoutMs);
      this.pending.set(id, { resolve, reject, timer });
      this.send({ id, method, params });
    });
  }

  notify(method, params = {}) { this.send({ method, params }); }
  respond(id, result) { this.send({ id, result }); }
  on(handler) { this.handlers.add(handler); return () => this.handlers.delete(handler); }

  fail(error) {
    this.lastError = error;
    for (const pending of this.pending.values()) {
      clearTimeout(pending.timer);
      pending.reject(error);
    }
    this.pending.clear();
    for (const handler of this.handlers) Promise.resolve(handler({ method: 'adapter/error', params: { message: error.message } })).catch(() => {});
  }

  close() { this.handlers.clear(); this.process.stdin.end(); this.process.kill(); }
}
