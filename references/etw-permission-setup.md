# ETW Permission Setup

## Release preparation — 2026-10-18

The candidate work is on `fix/capture-access-and-diagnostics`, based on current
`origin/main` (`d71d5c5`). The source trees at its previous detached base and main
were identical, so the uncommitted work was retained without a merge or history
rewrite. The unrelated primary checkout and the separate release-pipeline worktree
are unchanged.

Candidate versions are Toolkit `1.1.0.0` / `toolkit-v1.1.0` and Benchmark
`1.1.0.0` / `benchmark-v1.1.0`, above the previously Store-installed telemetry
flights. Recorded public baselines are unchanged. Build ordinary Release packages
with diagnostic labeling/data isolation and the permission probe disabled; retain
bounded sanitized logging and the approved support UI. Use the versioned production
API/auth configuration. The production Clerk public environment was checked
read-only: its home and sign-in/sign-up URLs use timmy.academy/account domains,
not the legacy localhost paths found in the Development instance.

Before production, the new Microsoft-signed flight must verify the native UAC
helper path on supported Windows versions, immediate capture/reuse before a new
sign-in, the original requesting identity when another administrator authorizes,
and cancellation/host-close cleanup. Verify local support ZIP/issue actions and
normal package-local history after update in both hosts. Prior telemetry-flight
checks and the portable AMD result are distinct evidence, not substitutes for the
new packaged helper. The capability question below remains a flight gate; do not
add `allowElevation` or claim Store support without separately agreed evidence.

The user requested a pull request after reviewing the prepared candidates. Store
submission, tags, production promotion and backend deployment remain separate
actions. The local unsigned packages are review artifacts; CI builds and signed
flight validation follow the source review.

Local preparation after review passed 549 ordinary Release Core tests, 26 Release
Benchmark tests and 119 isolated Debug UI regressions. Both `1.1.0.0` MSIX packages passed
MakeAppx and package-content checks, with production configuration, matching host
assembly/manifest versions and verified pinned PresentMon. Their Core and Feature
hashes are identical across products. Inspection of the actual packaged DLLs
confirmed ordinary data paths, no diagnostic label/probe, hidden probe UI, and
FPS-only QPC capture flags for 120/240 seconds. No package was installed and no
UAC or group change was performed during these checks.

Local candidates: `C:\projects\Tarkov-Release-Candidate-1.1.0-20261018-review`, with
`candidates.json` provenance and `SHA256SUMS.txt`. Toolkit MSIX SHA-256:
`40E861CB9866BAA9855AE1F7461C48E908600390198859B57D1EBA4A351CD5CA`.
Benchmark MSIX SHA-256:
`0484B9CD47AABEC1DE377EF73D7F39D3E60DE43B427E6701B043B4EB7F681F46`.
These supersede the preserved pre-review packages in the directory without the
`-review` suffix. They are review artifacts from uncommitted source, not an
immutable CI release.

The PR review reproduced three failures before fixing them: Settings navigation
emitted a premature `cancelled` command result; an output/checkpoint failure after
a successful save escaped into the save-failure path and could emit a replacement
terminal result; disposing an in-flight helper pipe could escape as an unexpected
error instead of cancellation. Command cancellation now follows the owning window
closing, terminal output is attempted only once per collection, and output failure
leaves a saved run intact with local support visible. A disposed pipe maps to the
caller cancellation or a fixed disconnect message. The new regressions cover
navigation, successful/failed command output, closing and both pipe outcomes.

## PresentMon FPS tracking isolation — 2026-10-18

The `.2` AMD/Tarkov capture ran for 120.085 seconds and PresentMon exited 0, but
its CSV contained only 537 valid frames totaling 5.929582 seconds. All rows were
accepted, with no parsing errors or discarded long frame. ETW access denial,
cancellation and reported lost events/buffers were absent. The user confirmed
Tarkov remained active and rendered normally. This is separate from the earlier
ETW permission failure; the standard limited-process-query warning alone does
not diagnose access denial.

Direct tests of the same pinned PresentMon 2.5.1, without Toolkit/resource
collectors and with Tarkov active, produced:

| Requested | GPU frame tracking | Display tracking | Frames | Valid interval sum |
| --- | --- | --- | ---: | ---: |
| 30 seconds (A) | enabled | enabled | 105 | 1.259561 seconds |
| 30 seconds (B) | disabled | disabled | 3,133 | 29.983878 seconds |
| 30 seconds (C) | disabled | enabled | 195 | 7.125870 seconds |
| 120 seconds | disabled | disabled | 11,570 | 119.987195 seconds |

Every supplied row was valid. The full test had a 119.978717-second timestamp
span and no gaps between successive frame intervals above 1 ms. A and C included
`Hardware: Independent Flip` and `Composed: Flip`; these CSVs do not establish the
precise point at which tracking stopped relative to focus changes. Both successful
and unsuccessful tests had the same limited-process-query warning.

This localizes the reproduced failure to a PresentMon path involving display
tracking on this system, without establishing the internal driver/OS/PresentMon
defect. Upstream [issue #605](https://github.com/GameTechDev/PresentMon/issues/605)
describes related machine-specific frame completion failures; identity with this
case is unproven.

The shared Core runner now explicitly passes `--no_track_gpu --no_track_display`
for both GUI hosts, headless capture and the temporary elevated collector. FPS
still uses `MsBetweenPresents` with matching QPC interval edges; adapter-wide
utilization and memory collectors remain independent. The pinned dependency,
120/240-second completeness rules, API schema and Store versions are unchanged.
Regression tests cover the flags for both durations and complete aligned FPS-only
CSV parsing without GPU/display columns. The direct successful CSV is not a saved
app run; a separate integrated result was supplied afterward, as recorded below.

### Integrated AMD result after the FPS fix

The user confirmed the `.3` Toolkit capture completed and supplied its local Copy
results JSON on 2026-10-18. Windows 11 / Ryzen 7 3700X / Radeon RX 9070, Factory,
Local: 14,179 frames, 119.990 seconds, 118.17 average FPS. The resource window was
119.990206 seconds, aligned to PresentMon QPC valid frame intervals. ADLX selected
the same whole adapter and returned 117 utilization samples, 116.955713 valid
seconds / 97.471% coverage, average 89.367% and maximum 100%. All 16 logical CPU
processors were present at 99.1743% coverage; memory/pagefile/commit had 120 samples
and 99.387% coverage. The summary's only warning was `partial_coverage`, not a
collector failure. The JSON alone does not explain the missing utilization samples.

Dedicated VRAM capacity/use and shared memory were distinct. Installed/OS-usable
RAM, physical used/available RAM, system-managed pagefile allocation/use and system
commit/limit/headroom were present. RAM and commit arithmetic and reported coverage
ratios were consistent. Commit peaked at 96.248% of its limit (1.425 GiB minimum
headroom); this is diagnostic context, not proof of a performance bottleneck.

An isolated harness referencing the actual published `.3` Core deserialized this
run, created its submission projection, and passed the resource contract and strict
stored-request privacy allowlist locally. It sent no request and wrote no application
history. The run remained `submitted=false`. Live submission was blocked separately:
the supplied browser screenshot showed Balticom filtering staging, and read-only
inspection of the Development Clerk public environment found legacy
`127.0.0.1:5190` sign-in/sign-up paths. Neither is an FPS capture failure. No capture
log or second updated-app run was supplied with this JSON; this result does not
establish temporary-helper reuse or signed-package behavior on this AMD system.

## AMD/Tarkov Test Candidate — 2026-10-18

The requested full Toolkit candidate uses the approved Settings UI, normal 120/240s
capture APIs, retained sanitized logging, and no three-second permission control.
The probe now requires a separate explicit build opt-in (`CapturePermissionProbe=true`
alongside `CaptureDiagnostics=true`). Capture logging remains enabled in ordinary
builds independently; the diagnostic flag controls test labeling/data isolation.

Capture/save errors and insufficient valid duration offer shared GitHub support UI
in both hosts. A local support ZIP contains a sanitized short report, up to two
recent revalidated capture logs and the allowlisted permission report. It never
includes history, CSVs, settings, credentials, arbitrary exception text or paths.
The user opens a prefilled issue explicitly and attaches the ZIP manually. Logs
remain local. Cancel and expected raid/game exit do not generate bug offers.
There is a temporary local fallback when normal report storage is unavailable.
Retention keeps 20 capture logs and 10 support ZIPs/text reports. Unhandled GUI
errors offer support and terminate; fatal managed worker failures leave a pending
report offered at next activation when possible. Helpers never register this UI.

Validation: 547 Core tests, 26 Benchmark tests and 116 WPF regressions passed.
The new FPS argument regression failed for both durations before the two flags
were added and passed afterward. The published `.3` Core parsed the supplied
full CSV as 11,570 frames / 119.987 seconds with a 119.987195-second aligned window;
reflection checks verified both flags at 120/240 seconds in that same DLL.
A separate isolated process exercised the actual managed-fatal handler: nonzero
exit, a support ZIP and a next-launch marker were created without persisting the
private exception message. Failure-card renders at 150%/200% were inspected. No
GitHub issue, upload, account-group change, Store install or submission was made.
The integrated portable AMD result is recorded above. Signed-package helper
behavior remains a separate release gate.

Current portable artifact:
`C:\projects\Tarkov-Toolkit-AMD-Test-20261018-3-win-x64.zip`
(617 entries, 91,976,968 bytes), SHA-256
`6193F4F78F48E98CA3A491186AD82396775124E331E4690FA3004CDA60C1D84E`.
Published binaries were checked directly: test label `2026-10-18.3`, logging on,
probe API rejected before consent, hidden probe controls inert. Every ZIP entry
matches its published file; PresentMon 2.5.1 matches the pinned SHA-256. The public
API/auth configuration points to staging. No PDBs, scripts, private data, history
or diagnostic results are bundled. START-HERE describes two real Tarkov captures
and the manual support-archive/issue steps. Earlier ZIPs were preserved.

## Status — 2026-10-17

The user approved implementing shared permission recovery in both GUI hosts.
The current implementation is isolated from the Store candidate and the main
checkout's unrelated skill edits. No new capability, flight or production submission
has been made. Application/package versions have not been raised for this diagnostic.

On a separate Windows 11 computer with Terraria, direct pinned PresentMon capture
returned ETW `access denied` as an ordinary user. A manually elevated retry reported
recording start and stop. The user subsequently confirmed successful ordinary-user
capture after manual account-specific group setup, before diagnostic build `.4`.
The supplied console log contains recording start/stop without access denied; its
Terraria CSV has 1,787 positive finite `MsBetweenPresents` intervals totaling
29.919652 seconds, from one process and one swapchain. The remaining limited-process
query warning did not prevent this capture. This verifies the manual permission
approach, not the new application's helper, UAC flow or Store activation.

The subsequent portable Toolkit `.4` Windows 11 check passed the reported sequence:
revoke access, sign out/sign in, observe ETW access denied; add access through
Toolkit, sign out/sign in, capture successfully as an ordinary user. The supplied
archive contains both console logs and the successful Terraria CSV: 1,794 positive
finite `MsBetweenPresents` intervals, no invalid intervals, totaling 29.950498 seconds
at approximately 59.90 FPS, from one process and one swapchain. After revocation
the log explicitly reports failure to start the trace session due to access denied;
after Toolkit setup it reports recording start/stop without that error. This
confirms the portable recovery scenario. The archive does not contain the helper's
`permission-setup.log`; the application setup actions are confirmed by the user.
Different-administrator credentials, cancellation and signed-package activation
are not established by these files.

This does not explain the earlier AMD/Tarkov capture that ran for 120 seconds,
returned exit code 0 and contained only 13.8 seconds of accepted frametimes.

## Behavior

Both hosts expose **Settings → Access** directly through the shared Feature,
including when there are no completed runs or Tarkov is not installed/running.
There is no intermediate management button or modal. Revocation is always visible,
and actions wrap with consistent spacing. Diagnostic `.7` keeps only a short status
and introduction visible; explanations are under **How capture access works**,
collapsed by default with the same disclosure style as resource telemetry. Setup
and removal confirmations remain explicit. Toolkit's existing Goal editor is the
second Settings section; standalone Benchmark does not instantiate that section
or read Goal data. Opening Settings performs only a read-only access check. No UAC request is made
by normal startup, Check access, or a headless CLI command. Permission management
and revocation are not part of the capture screen. Ordinary capture is attempted
first; on ETW denial, the GUI explains the persistent change and asks for explicit
approval before UAC, then continues with fresh capture preparation.

Diagnostic `.6` applies this inline Settings layout. The shared view constructs only
the sections selected by its host: Access for Benchmark, Access and Goal for Toolkit.
The goal editor retains the existing local store and preserves notes when saving.
The diagnostic probe stays in Access; ordinary builds hide and reject it. Leaving
Settings cancels an unfinished access operation without preventing a later retry.

All 108 WPF regressions passed after this revision, including both host section
choices, absent Goal tab, confirmation/cancellation, retry after tab unload, permanent
revocation, 12-DIP action gaps on inline/wrapped rows, existing goal persistence,
validation, and narrow scrolling layouts. Rendered previews at 150% and 200% were
also inspected for clipping and spacing. These layout checks do not replace native
signed-package activation or the outstanding integrated AMD capture test.

Portable diagnostic artifact:
`C:\projects\AMD-Tarkov-Toolkit-CaptureLogs-20261017-6-win-x64.zip`
(617 entries, 91,969,850 bytes), SHA-256:
`1D7047CC01FEB23856CD3CF3F7B7A8203F59A728F0B3A414FC9526306DE01F61`.
Its public configuration points to staging; every ZIP entry was checked against
the published files and pinned PresentMon 2.5.1 was checksum-verified. No capture
data, credentials, runtime scripts or PDBs are included. Earlier diagnostic ZIPs
were preserved. Store versions, submissions and the main checkout were unchanged.

Compact Settings diagnostic `.7` passed all 110 WPF regressions, including help
expansion/collapse without launching setup or hiding any management action. The
expanded help still scrolls at small/scaled sizes, and rendered compact layouts
were inspected at 150% and 200%. The portable staging ZIP is
`C:\projects\AMD-Tarkov-Toolkit-CaptureLogs-20261017-7-win-x64.zip`
(617 entries, 91,970,036 bytes), SHA-256
`8E21BBAA819124C745028823C78C94B7DC5BAF4815ED0B0544BF8F3A264956A4`.
ZIP entries, public configuration and the pinned PresentMon checksum were verified
as above. `.6` remains available unchanged.

An explicit **Set up access** action explains that it adds the requesting Windows
account to Performance Log Users, changes permission for other applications using
that account and persists after uninstall. Ordinary access takes effect at the next
Windows sign-in. The user confirms this change before the Windows UAC prompt.
A temporary elevated helper remains available until the application closes and
runs pinned PresentMon and trace cleanup for immediate capture without signing out.
The normal UI, settings, inspection, resource sampling, history, authentication and
submission remain at their original integrity level. Closing and reopening before
the next Windows sign-in may require UAC again; the user explicitly accepted this.

The removal action explicitly removes only this account's direct membership.
Existing membership and inherited access are not silently removed. An account
already directly configured can still enable a temporary collector if its current
token has not picked up the permission. An existing temporary connection is reused.
Existing access through another group does not cause an extra direct membership
to be added. Revocation releases this application's helper before the direct
membership removal request; Windows permissions in existing tokens take effect
at the next sign-in.

Windows group lookup uses the built-in group SID, not an English group name.
Member identity, account names, pipe names and process identifiers are never included
in exported diagnostics or benchmark/API DTOs. No service, driver, overlay, script
runtime, frame metric, upload or benchmark grouping change is introduced.

## Helper boundary

- `--setup-permissions add|remove --pipe <random nonce>` is a strict internal mode.
  It accepts no account, group, executable, command or output path from arguments.
- `--capture-helper --pipe <random nonce>` uses the same authenticated setup
  boundary, then waits for bounded capture commands. Only fixed 120/240-second
  Tarkov captures and cancellation are allowed in ordinary builds. A readiness
  reply is required after creating the Windows job object, before reporting the
  temporary collector as available.
- Both GUI hosts handle it before normal window, mutex, authentication and history
  initialization. The CLI does not implement this mode or initiate elevation.
- The requesting process creates a local, exclusive named pipe, assigns its token
  user as owner, and grants access only to that user, Administrators and System.
- The elevated helper verifies the server executable, reads the kernel-controlled
  pipe owner and accepts only an account SID resolving as a user. This preserves
  the requesting account when a different administrator supplies UAC credentials.
- The ordinary host authenticates the helper using identification-level pipe
  impersonation and token elevation information. It does not open another account's
  process token, request SeDebugPrivilege or execute actions as the helper.
- A fixed-size protocol binds version and action and requires an apply acknowledgement
  after peer verification. A disconnected/cancelled caller before authorization
  cannot trigger a group change. Failure after authorization is reported as
  unconfirmed rather than claiming that no change occurred.
- Group operations affect only the verified owner. Existing/already-absent
  membership is idempotent. The capture helper runs only the checksum-verified
  bundled PresentMon and fixed trace cleanup. It receives no arbitrary process,
  path or command target. No game process is opened.
- Cancel is acknowledged after stopping the current capture and cleanup; the
  connection can then be reused. Disconnecting the parent cancels the active
  operation and ends the helper. A kill-on-close Windows job contains collector
  and cleanup child processes if the helper exits or crashes.
- The helper returns only FPS metrics and a bounded private QPC interval window.
  QPC uses the same clock as resource sampling in the normal host. No waiting for
  UAC is included in the new resource window: after elevation the whole capture
  service starts afresh, including raid/game validation and samplers.
- Diagnostic forwarding is bounded and revalidated against closed field/string
  allowlists before writing local logs; free-form messages and unknown keys are
  discarded. Raw CSV remains temporary inside the collector and is deleted.

The parent saves only the latest setup attempt to `reports/permission-setup.log`.
It contains action, outcome, Windows error code, UTC and Windows version, without
account identifiers or exception text. Storage failure does not change the outcome.
The helper itself never reads or writes application state. The GUI can open reports
even without benchmark history. Reports remain local until manually shared.

## Packaged launch and Store gate

Portable GUI builds request `runas` on their own executable. Packaged builds use
Windows Shell activation by their application ID, following Microsoft's Windows
Terminal elevation shim. This is implementation evidence for a possible launch path,
not evidence of successful native UAC execution in our packages or Store acceptance.
The current manifests still declare only the existing `runFullTrust` capability.

Before release, verify the native path in Microsoft-signed packages on Windows 10
and Windows 11, including a standard account with another administrator's UAC
credentials. If the supported path requires `allowElevation` or another capability,
prepare its justification and obtain the separately agreed Store approval before
declaring/submitting it. Do not copy helpers outside MSIX, weaken Windows policy,
install services or silently elevate a capture as a workaround.

## Manual validation matrix

The ordinary-user Windows 11 portable revoke/deny/setup/success sequence above has
passed. The full matrix still applies to both Microsoft-signed hosts and supported
Windows versions; unreported dialog, identity, cancellation and policy cases remain
open. A Terraria permission check does not replace integrated AMD/Tarkov coverage.

1. Clean Windows account: headless capture returns permission_required without
   UAC. GUI capture offers approval in the capture flow after ordinary ETW denial.
2. Open Check access; decline the application's confirmation and the UAC prompt in
   separate attempts. No membership or history changes.
3. Confirm setup, then inspect the requesting account's direct membership. With a
   different administrator, the administrator account must not become the target.
4. Before signing out, validate immediate capture through the temporary helper and
   repeated captures without another UAC in the same app session. Reopen the app
   before signing out: another UAC is allowed, followed by immediate capture.
5. Sign out/sign in, then capture without elevation and verify real frame coverage.
6. Reopen setup: existing direct/inherited access is preserved without extra changes.
7. Explicit removal affects only the requesting account's direct membership; after
   the next sign-in, verify access reflects the remaining Windows policy/groups.
8. Check local diagnostic availability, denied storage, short/scaled layouts and both
   hosts. Cancel/game/raid-exit behavior and saved histories/outboxes remain intact.
9. Complete the outstanding integrated AMD/Tarkov captures independently of this gate.

Diagnostic `.5` adds **Settings → Test capture access (3 sec)**. It needs no game,
uses the same permission recovery and helper connection, and runs pinned PresentMon
with fixed `--no_csv` arguments. Success requires exit code 0 and the timed interval;
`--no_csv` suppresses recording start/stop messages in pinned 2.5.1. No FPS metrics,
benchmark run, CSV or upload is produced. Logs use `permission-probe-*.log` in the
isolated reports directory. The command is rejected and the UI hidden in ordinary
builds. This is an ETW access test, not a Tarkov/AMD measurement substitute.

Automated tests exercise workflow and native read-only IPC. They do not perform real
group mutations, approve UAC, create Windows accounts or claim the manual checks passed.

The developer account was rechecked read-only: it is not directly in Performance
Log Users; it has effective access through the group's existing INTERACTIVE member.
Removing only that user therefore cannot reproduce denial. Removing INTERACTIVE
would affect all interactive accounts and is outside permission to remove only the
requesting user. No membership was changed for this check.

## Local verification — 2026-10-17

All 617 tests passed: 507 Core tests in Release with capture diagnostics, 24
Benchmark tests in Release, and 86 shared/UI regression tests in Debug. The native
pipe tests bind the requesting account and reject an unelevated client without
changing membership. A before/after snapshot confirmed that the local Performance
Log Users membership was unchanged. Invalid helper invocations were also exercised
against both actual GUI host executables before normal application initialization.

Portable diagnostic `AMD-Tarkov-Toolkit-CaptureLogs-20261017-4-win-x64.zip` contains
the shared setup UI and the existing capture logging. Its 617 entries were compared
by SHA-256 with the published files; the pinned PresentMon hash and the public
staging API/auth configuration were checked. It is unsigned, separate from Store
history, and does not establish the packaged launch. The subsequent user-run
portable permission result is recorded separately above.

## Immediate-capture revision verification — 2026-10-17

The Settings relocation regression was observed failing against the previous UI
before implementation. The revised code passed 535 Core tests (Release with capture
diagnostics), 24 Benchmark tests (Release), and 93 shared/UI tests (Debug): 652 total.
An additional focused ordinary-Release run passed 28 tests, including diagnostic
probe rejection/visibility boundaries and both internal helper arguments rejected
by the CLI without UAC.

Native read-only pipe tests cover repeated captures, cancellation followed by reuse,
host disconnection stopping an active collector, fixed sanitized failures, and a
Windows job terminating an owned harmless child. Synthetic metrics preserve QPC
window alignment across IPC. The actual pinned PresentMon three-second no-CSV
probe passed locally without a game or elevation; Windows group membership was
unchanged during the checked automated run. Neither this nor fake-collector IPC
tests establishes the new elevated capture path on Windows 11.

Both ordinary unsigned MSIX packages were built and validated with MakeAppx and
package-content checks. Their Core binaries exclude the diagnostic data directory;
existing capabilities are unchanged. No installation, new Store submission,
publication, commit or PR was performed. Diagnostic `.5` includes the new Settings
and no-game permission probe. Its immediate UAC and reuse path subsequently passed
the user's Windows 11 check below. Reopening before a new Windows sign-in remains
unverified by the supplied files. Signed-package behavior, different-account UAC,
integrated AMD captures and overhead of the elevated path remain release checks.

## Windows 11 immediate capture confirmed — 2026-10-17

The user reported success with diagnostic `.5` without signing out after setup.
The supplied first probe log shows ordinary PresentMon exiting with code 6 and
access denied, followed in the same operation by elevated PresentMon exiting with
code 0 after 3.049213 seconds and a completed result. The setup report records Add,
Added and no Windows error on Windows build 26200. The second probe goes directly
through the elevated path, exits with code 0 after 3.068212 seconds and completes.
This confirms immediate permission recovery and same-session helper reuse in the
portable Toolkit. No repeated setup is present in the second probe. It does not
establish a full FPS/resource capture, cancellation, reopening, another UAC account
or Microsoft-signed package activation.

References: [Windows ETW access](https://learn.microsoft.com/en-us/windows/win32/etw/controlling-event-tracing-sessions),
[PresentMon 2.5.1 access guidance](https://github.com/GameTechDev/PresentMon/blob/v2.5.1/README.md#user-access-denied),
[Windows Terminal elevation shim](https://github.com/microsoft/terminal/blob/main/src/cascadia/ElevateShim/elevate-shim.cpp),
[identification-level pipe authentication](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-impersonatenamedpipeclient),
[cross-account token restrictions](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocesstoken),
[Store capability requirements](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/app-capability-declarations).
