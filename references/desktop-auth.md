# Desktop OAuth

## Shared implementation

`TarkovSkills.Core/Authentication` contains the protocol, Windows credential storage
and session lifecycle. Both WPF hosts will use the same account control in
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

Credentials are encrypted with Windows DPAPI CurrentUser, with product/issuer/client
binding. Packaged storage is `LocalState/TarkovSkills/auth/<product>`. Portable
storage uses the existing application data root with distinct product subdirectories.
Only ciphertext is written, with atomic replacement and an exclusive per-binding
lease. DPAPI does not isolate credentials from other processes under the same Windows
user; this is not protection against a compromised user account.

Restore checks local expiry and refreshes within one minute of expiration. Rotated
refresh tokens replace the previous value; an omitted refresh token preserves it.
Confirmed `invalid_grant` clears the credential and requires sign-in. Network errors
retain the encrypted credential and report unavailable, without claiming successful
refresh. A local restored credential is not evidence of a verified backend account.

Sign-out persists revocation intent before requesting revocation of both refresh and
access tokens. A failed request leaves a retryable pending sign-out across restarts;
such a grant cannot refresh or be replaced by login. Only acknowledged revocation
removes the local credential. Browser sign-out is independent. If persistence of a
new/rotated credential fails, cleanup attempts remote revocation and reports a storage
failure. An outage during cleanup can leave remote state unconfirmed.

## Clerk Dashboard: separate desktop clients

Create **Tarkov Performance Benchmark** and **Tarkov Performance Toolkit** OAuth
applications in the same intended Clerk instance so they can reuse its browser
session. Do not share their client IDs or copy the PoC client into a product build.

For each client:

1. Enable **Public** and **Require PKCE**; keep the consent screen enabled.
2. Allow only `email` and `offline_access`.
3. Register `http://127.0.0.1/callback` without a fixed port. The listener supplies
   the ephemeral port in each authorization request and code exchange.
4. Select **Opaque access tokens**, matching the tested immediate-revocation flow.
5. Leave device authorization and dynamic registration disabled unless separately
   required by another product.
6. Record the instance Frontend API HTTPS origin and that application's **Client ID**
   (not its OAuth application object ID). No client secret belongs in the desktop app.

Keep Academy's email-code sign-in and consent page configured on the same instance.
The second desktop client does not need its own user pool or browser session. Consent
can still appear even when browser SSO avoids another email code.

The product config contains only `issuer` and `clientId`; real values are supplied
outside committed source. `desktop-auth.local.json` is ignored. Production custom
HTTPS issuer origins are supported; development-only domain restrictions from the
PoC are intentionally removed.

Sources: [Clerk OAuth settings](https://clerk.com/docs/guides/configure/auth-strategies/oauth/how-clerk-implements-oauth)
and [Clerk loopback setup](https://clerk.com/blog/adding-clerk-auth-to-your-cli).

## Validation boundaries

Automated tests exercise actual loopback sockets, RFC S256, one-time callback/state
checks, DPAPI encryption/tampering/product binding, exclusive leases, lifecycle,
refresh rotation, restart, cancellation and sanitized failures. Provider requests use
synthetic fixtures. These tests do not establish live Clerk behavior for product client
IDs or packaged Windows execution; those need an interactive test of each signed app.

Before release, test fresh email-code login, browser SSO between apps, independent
logout, restart, natural expiry/refresh, revoked grants, interrupted browser flows,
network failure and pending-revocation retry. Check package persistence across updates.
Confirm collection, aliases, local history and Google Form behavior remain unchanged.
No form acknowledgement or local `submitted` flag proves publication in a new backend.
