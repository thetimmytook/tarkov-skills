# Desktop OAuth

## Open P1 review finding: submission authorization

User decision: create the desktop-auth PR now, retaining the current UX. Address this
finding with the real submission API integration in a follow-up. Explicitly disclose
the open P1 in the PR; do not treat this stage as secured or attributed submission.

The current Submit gate checks only a locally stored OAuth credential and its expiry.
No access token is sent to a trusted submission backend. The public Google Form can
be opened directly, cannot attribute submissions to a verified Clerk user, and is not
protected by desktop sign-in. A remotely revoked token may still pass the local check
until its saved expiry requires refresh. Rechecking local state detects shared local
logout, not remote revocation. This gate is not an authorization boundary.

Follow-up scope and acceptance criteria:

- Replace the Google Form handoff with the agreed real submission API; do not invent
  owner endpoints before the backend contract is available.
- Validate authorization on the trusted server at submission time and derive result
  ownership from the verified Clerk identity, never a client-supplied owner ID.
- Reject missing, invalid, expired or revoked credentials according to the provider's
  supported server verification mechanism. Test direct requests that bypass the UI.
- If a browser handoff remains necessary, agree a server-validated, signed, one-time
  handoff design instead of treating a public form URL as protected submission.
- Confirm publication only from the backend response. The existing local `submitted`
  flag and Google Form acknowledgement are not proof of API publication.
- Keep upload explicit; test cancellation, authorization failure and retry without
  automatic publication or disclosure of tokens or personal data in logs.

## Shared sign-in decision and live-test checkpoint

Work resumed on 2026-09-24: the user explicitly retained consent and requested a
shared desktop sign-in for Benchmark and Toolkit. No Clerk setting change is needed.

Current direction:

- Keep the OAuth consent/Allow step for the shared first-party desktop client.
  Keep system-browser authorization, email-code entry
  on Clerk, public-client S256 PKCE and state validation.
- Make Benchmark and Toolkit behave as one application for sign-in: one user login
  should make both products usable without a second desktop login. Sharing the
  OAuth client ID and browser SSO already works, but does not meet this stronger
  requirement. Both products now share an encrypted credential and an exclusive file
  lease. Sign-out in either product signs out both. See storage details below.
- Request sign-in when the user chooses Submit; remove the persistent account panel
  from ordinary collection/inspection screens. Implemented in the shared Submit
  dialog; opening an ordinary app window no longer initializes authentication.

Earlier separate-store live evidence uses local Release executables with the common **production** OAuth
configuration, not installed Microsoft-signed packages:

| Scenario | Benchmark | Toolkit |
| --- | --- | --- |
| Saved credential survives restart; browser does not open | Pass, app state observed and user confirmed | Pass, app state observed and user confirmed |
| No app credential, existing browser session: consent without another email code | Pass, user confirmed | Pass, user confirmed |
| No app credential or browser session: email → code → consent → signed in | Pass, user confirmed each step | Pending |

Production Account Portal sign-in is available. The user changed the production
OAuth consent route from the unpublished application page to Clerk Account Portal.
The Academy application website itself is not deployed; these results establish
Clerk Account Portal/browser session reuse, not deployed Academy website integration.
One early callback failed after the three-minute desktop attempt expired during
configuration discussion; a fresh attempt succeeded. Do not replay old callback URLs.

Shared-store live test on 2026-09-24, using local production Release builds:

- Both apps started signed out. The user signed in only in Benchmark, saw consent,
  accepted it, and confirmed both apps signed in. Both UI states were also observed.
- Both apps were closed and relaunched. Both restored Signed in, observed in their
  UI and confirmed by the user. The user confirmed the states but did not separately
  answer the question about whether a new browser tab appeared.
- Sign out only in Toolkit resulted in both apps signed out, confirmed by the user.
- Both apps were then closed for the Submit-only UI rebuild. On relaunch, the
  permanent account panel was absent in both apps (observed in the UI).
- Benchmark Submit opened the new dialog and Clerk consent. After Allow, the user
  confirmed Signed in and an enabled Open form, without the Google Form opening.
- Cancel closed the Benchmark dialog. Toolkit Submit then showed the shared signed-in
  account and Sign out without a second login, confirmed by the user.
- Sign out inside the Toolkit Submit dialog showed Signed out and disabled Open form,
  without automatically reopening browser sign-in, confirmed by the user.
- Open form was deliberately not clicked in this test; no form was submitted and no
  benchmark was uploaded. Natural expiry with live Clerk and signed-package checks
  remain pending; expiry/failure handling was exercised by the automated tests below.

Recovery tests now combine the real Clerk adapter, real DPAPI storage, two sessions,
synthetic HTTP responses and controlled time. They verify the 60-second refresh
boundary, retention/retry after a network failure, shared refresh rotation, confirmed
invalid_grant, and partial revocation followed by restart and retry from the other app.
These are deterministic local tests, not a claim of live production failure testing.

Guide one manual step at a time, state the expected result, wait
for the user's reply, and investigate deviations. Do not print codes, tokens or
personal identity data. No remote CI and no commits without the user's review and
explicit instruction. Existing uncommitted implementation/configuration changes
must be preserved.

## Shared implementation

`TarkovSkills.Core/Authentication` contains the protocol, Windows credential storage
and session lifecycle. Both WPF hosts use the same account control in
`TarkovBenchmark.Feature`. The console proof of concept is not a project or runtime
dependency. No backend identity probe, publication or owner endpoint is included.

The system browser receives an authorization request with S256 PKCE and fresh state.
A one-use Kestrel listener binds `127.0.0.1` on an OS-assigned port, validates host,
path, method, state, optional issuer and parameter multiplicity, and rejects token
parameters. Invalid callbacks do not consume the attempt. Login expires after three
minutes; cancellation and completion dispose the listener. Kestrel logging providers
are disabled. No provider body, authorization code, token or personal identity is
returned through the public session interface or sent to application reports.

The tested Clerk adapter uses opaque access tokens, `email offline_access`, and
public `client_id` form fields without a secret. HTTP redirects/cookies are disabled;
responses and requests have size/time bounds. No identity claims are persisted.

Credentials are encrypted with Windows DPAPI CurrentUser, with version/issuer/client
binding shared by both products. Both MSIX manifests declare the same publisher cache
folder, `TarkovDesktopAuth`, resolved through `ApplicationData.GetPublisherCacheFolder`.
There is no fallback to package-local storage if that Windows API fails. Portable
builds use `%LOCALAPPDATA%/TarkovSkills/shared-auth-v1`; portable and Store sessions
are intentionally separate. Development and Production are separate bindings.
Only ciphertext is written, with atomic replacement and an exclusive per-binding
file lease across login, refresh and logout. A competing app waits asynchronously
up to five seconds, then reports Busy and polls again; cancellation releases the wait.
After acquiring the lease every operation rereads the shared credential, so a stale
Sign in action reuses another app's grant without reopening the browser. A crashed
process releases its OS file lock; no lock-file deletion is required.
DPAPI does not isolate credentials from other processes under the same Windows
user; this is not protection against a compromised user account.

The earlier experimental per-product stores are not read, copied or migrated. One
new shared sign-in is required. Old builds retain their old grants until sign-out or
revocation in Clerk; sign out there before retiring those builds. This avoids a stale
per-product credential restoring a session after shared logout. Benchmark history,
reports and headless aliases still use their existing package-local data paths.
Windows retains publisher storage until the last app from that publisher is removed;
uninstalling just one app does not sign out the other. Portable storage requires
explicit sign-out or removal.

Restore checks local expiry and refreshes within one minute of expiration. Rotated
refresh tokens replace the previous value; an omitted refresh token preserves it.
Confirmed `invalid_grant` clears the credential and requires sign-in. Network errors
retain the encrypted credential and report unavailable, without claiming successful
refresh. A local restored credential is not evidence of a verified backend account.

Sign-out applies to both products and persists revocation intent before requesting revocation of both refresh and
access tokens. A failed request leaves a retryable pending sign-out across restarts;
such a grant cannot refresh or be replaced by login. Only acknowledged revocation
removes the local credential. Browser sign-out is independent. If persistence of a
new/rotated credential fails, cleanup attempts remote revocation and reports a storage
failure. An outage during cleanup can leave remote state unconfirmed.

## Clerk Dashboard: one desktop client per environment

Benchmark and Toolkit share **one public desktop OAuth client** in each environment:
the same issuer and client ID in Development, and the same production issuer and
client ID in Production. Development and Production remain separate environments.
Do not register a second client just for Toolkit. Within each distribution type and
Windows user, the common issuer/client selects one shared credential.

For the common client in each environment:

1. Enable **Public**, **Require PKCE** and consent. Keep the hosted Account Portal
   consent route; the application-domain consent page is not deployed.
2. Allow only `email` and `offline_access`.
3. Register `http://127.0.0.1/callback` without a fixed port. Each login uses its own
   OS-assigned loopback port.
4. Select **Opaque access tokens**, matching the tested revocation flow.
5. Leave device authorization and dynamic registration disabled unless separately
   required by another product.
6. Record the instance Frontend API HTTPS origin and the OAuth **Client ID**, not
   the OAuth application object ID. Never include a client secret.

Keep Academy's email-code sign-in and consent page on the same instance. Browser
SSO can avoid another email code, though consent may still appear. Once a desktop
credential is shared, the other product does not open a browser at all. Test shared
revocation with both products running.

## Shared configuration and build selection

Both JSON files have exactly two public string fields: `issuer` and `clientId`.

| Use | Repository-relative source | Selection |
| --- | --- | --- |
| Development | `config/desktop-auth.development.local.json` | Default Debug build |
| Production | `config/desktop-auth.production.json` | Every Release build and both MSIX builders, even with `-Configuration Debug` |

Copy `config/desktop-auth.development.example.json` to the local development filename
and fill it in once for both products. That exact local path is ignored by Git.
A Debug build without the local file keeps sign-in unavailable and removes stale
`desktop-auth.json` from build/publish output.

The production JSON is versioned and contains the production HTTPS issuer and the
common desktop client ID. These public values belong in the file; secrets and tokens
never do. Missing, empty or invalid
production configuration fails the build, even if development configuration exists.
Release cannot select Development. Production has no fallback to local files,
per-product property overrides, or environment-provided credential values.

`build/DesktopAuth.props` selects the shared source and copies it as
`desktop-auth.json`. Both MSIX scripts explicitly pass
`-p:DesktopAuthEnvironment=Production`; package inspection verifies the embedded file
byte-for-byte against the versioned production JSON and rejects local/development
configuration or credential files in the archive. Build-only PowerShell validation
is not shipped in either application. The Toolkit headless CLI never loads auth
configuration; its MSIX contains the GUI's common production configuration.

Debug can explicitly select Production with `-p:DesktopAuthEnvironment=Production`
for local verification, but this still requires complete production values. An
ordinary local Debug build requires no extra flags and uses the shared local
development file. Changing issuer/client ID selects a different credential binding;
sign out of the old configuration before switching or revoke the old grant in Clerk.

The Account panel exists only inside the shared Submit dialog. On opening, the dialog
restores the shared credential and refreshes it if needed; only missing/rejected
credentials start browser sign-in. While the dialog is open, it checks shared state
every five seconds. Ordinary app launches, collection, inspection and headless aliases
do not initialize auth or open the browser. The next Submit restores the saved session.
Failures offer explicit Retry or Sign out rather than a background retry loop.
Browser login offers Cancel sign-in. Closing the dialog cancels pending work and
awaits cleanup before returning. Open form is disabled unless signed in and idle,
and rechecks the shared session before continuing to catch logout in the other app.
JSON is copied and the existing Google Form is opened only after the user explicitly
chooses Open form; completing authentication alone does neither. The existing manual
form-return acknowledgement and local submitted marker keep their previous meaning.

The panel reports account state without displaying email or identity claims. A
pending sign-out remains clearly incomplete and can be retried with Sign out.
Closing the process or losing the network during a provider request can leave remote
state unconfirmed; no UI status treats that as confirmed revocation.

Sources: [Clerk OAuth settings](https://clerk.com/docs/guides/configure/auth-strategies/oauth/how-clerk-implements-oauth)
and [Clerk loopback setup](https://clerk.com/blog/adding-clerk-auth-to-your-cli).
Windows sharing and uninstall lifecycle:
[GetPublisherCacheFolder](https://learn.microsoft.com/en-us/uwp/api/windows.storage.applicationdata.getpublishercachefolder).

## Validation boundaries

Automated tests exercise actual loopback sockets, RFC S256, one-time callback/state
checks, DPAPI encryption/tampering/environment binding, shared leases, lifecycle,
refresh rotation, restart, cancellation and sanitized failures. Provider requests use
synthetic fixtures. These tests do not establish live Clerk behavior for product client
IDs or packaged Windows execution; those need an interactive test of each signed app.

Before release, test fresh email-code login, browser SSO, shared login and shared
logout, restart, natural expiry/refresh, revoked grants, interrupted browser flows,
network failure and pending-revocation retry. Check package persistence across updates.
Confirm collection, aliases, local history and Google Form behavior remain unchanged.
No form acknowledgement or local `submitted` flag proves publication in a new backend.
