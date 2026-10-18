# Privacy Policy

Tarkov Performance Benchmark and Tarkov Performance Toolkit process data locally on the user's Windows device. Tarkov Performance skills for ChatGPT and Claude analyze only information that the user explicitly pastes or attaches to a conversation.

## ChatGPT And Claude Plugin Data

The skills-only plugin has no MCP server, user account, analytics service, advertising service, or direct network connection operated by the project author. It receives no data directly from the user's device. The plugin processes only conversation text and files that the user explicitly provides to ChatGPT or Claude for settings analysis, FPS and frametime interpretation, benchmark comparison, or performance tuning.

The project author does not receive, sell, share, or retain that conversation data. OpenAI or Anthropic processes and retains data according to the user's account settings and the applicable service privacy policy. Users control plugin data by choosing what to paste or attach and may delete chats or uploaded files using the controls provided by that service. Support requests submitted through GitHub are public unless the user uses another private contact channel, so users should remove personal or sensitive information before filing an issue.

The applications may read Escape from Tarkov graphics settings and logs, non-identifying Windows hardware and driver information, and FPS/frametime data produced by the bundled PresentMon utility. Capture telemetry in updated builds includes summary CPU and logical-processor utilization, whole-adapter graphics utilization and dedicated/shared GPU memory, physical RAM, pagefiles and system commit. Raw resource samples are transient and are not saved in benchmark reports. Reports exclude process IDs, other application names and device identifiers. The applications do not read game process memory, automate gameplay, collect game account credentials, or include user names, host names, local paths, IP addresses, serial numbers, or machine identifiers in generated reports.

No benchmark or diagnostic data is uploaded automatically by the applications or sent to the project author by the skills. Exporting benchmark data, attaching files, and submitting reports require an explicit user action. Local diagnostics and support reports may be saved automatically on failure, and the short sanitized support report is copied to the clipboard. When a user provides data to ChatGPT, Claude, Microsoft Store, GitHub, or an external form, that service's privacy terms also apply.

Capture diagnostics remain local in ordinary builds. They record fixed diagnostic
events and numeric summaries, not raw frametime/resource series, application paths,
process IDs, account identifiers, credentials or unrestricted exception text. Up to
20 capture logs and 10 support ZIPs/text reports are retained. On capture/save errors,
the app prepares a local support ZIP with a short sanitized report and revalidated
recent logs, then offers a GitHub issue and access to that archive. Opening the issue
prefills only the sanitized report; the ZIP is attached only by the user's manual
action. GitHub issues and attachments are public. Reports use temporary local storage
if the normal data directory is unavailable. Fatal managed failures may leave a pending
report offered at the next GUI launch. No report is transmitted automatically.

Builds with Windows capture access setup offer an optional, explicitly requested
administrator operation to add the requesting account to the Windows Performance
Log Users group. This is a persistent account permission that other applications
running as that account can also use. It is not automatically removed when the
application is uninstalled. An explicit removal action removes only that account's
direct membership; access through other groups is unchanged. Ordinary Windows
permissions reflect the change at the next Windows sign-in. To allow immediate
capture without signing out, the user may approve a temporary elevated collector
that runs only the bundled PresentMon and its trace cleanup. It is available until
the application closes; reopening before the next Windows sign-in may require
administrator approval again. Capture starts with ordinary rights and requests
approval only after Windows denies tracing access. The main application, settings,
history and account/submission handling continue to run with ordinary user rights.
Permissions can be inspected and revoked in Settings. A local latest-attempt diagnostic records only the
action, outcome, Windows error code, time and Windows version; it contains no
account identifiers and is not automatically uploaded. Packaged setup remains
subject to signed Store validation before release.

Copied or exported completed runs include their sanitized telemetry summary when available. Updated application builds send the selected run's allowlisted capture-resource summary to Timmy Academy only after explicit Send for review. CPU, RAM, pagefile and commit measurements describe the whole system; GPU measurements describe the whole selected adapter, including use by other applications, without identifying those applications. The summary becomes public only after moderator approval. A missing legacy measurement is explicitly marked not_collected; no measured values are invented.

Deleting an Academy publication removes its resource telemetry. A smaller measurement without dates, source IDs or the user's account link remains in a closed internal analysis archive; it does not retain telemetry. Private authentication and ownership information remain separate from the benchmark DTO and public projection.

Builds with account sign-in enabled offer optional authentication through Clerk in the system browser. Clerk handles the email verification and consent flow and receives the information supplied there, along with normal connection information such as the IP address. The desktop application exchanges a one-time authorization code for access and refresh credentials; it does not store the user's email or account profile. Clerk's privacy terms also apply to authentication.

Benchmark and Toolkit share a desktop sign-in credential for the current Windows user and Clerk environment. The credential is encrypted with Windows DPAPI and stored separately from benchmark reports. Store builds use shared publisher storage; portable builds use a separate shared user-local folder. Uninstalling one Store app does not clear the shared sign-in while another app from the publisher remains installed; Windows cleans publisher storage when the last app from that publisher is uninstalled. Portable credentials remain until sign-out or manual removal. A signed-in application contacts Clerk to refresh expiring credentials, including after restart. Sign out in either app requests revocation for both apps and removes the shared credential after acknowledgement. If revocation cannot be confirmed, both apps disable access, retain the encrypted credential for retry, and show sign-out as incomplete. Desktop sign-out does not sign out the browser. Authentication credentials and personal account information are not included in diagnostic reports, benchmark exports, or application logs. Signing in does not publish any benchmark results.

Local application data is stored in the Microsoft Store package's LocalState directory. Windows preserves it during application updates and removes it when the package is uninstalled, subject to Windows behavior and user backups.

Project source and support: https://github.com/thetimmytook/tarkov-skills
