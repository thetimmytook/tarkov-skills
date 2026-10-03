namespace TarkovSkills.InteractiveE2E;

public sealed partial class InteractiveSuite
{
    private async Task<string> AccountAsync(CancellationToken cancellation)
    {
        output.WriteLine("Guided Store UI test: only installation is checked automatically. Login is confirmed by you, not inferred from Enter.");
        output.WriteLine("Uses your real shared account. Final sign-out affects BOTH products. No Send, Check status or comparison actions.");
        await gate.ReadyAsync("This test first prepares a saved GUI run in each app, then checks login. " +
            "Existing saved runs may be reused. Nothing needs to be sent. This is benchmark collection, NOT compiling the apps. " +
            "Type ready only if you permit real login and final shared sign-out.", cancellation);
        await gate.ConfirmAsync("STEP 1 - Prepare Toolkit: open Toolkit > Benchmark. If Submit is enabled, reuse the existing saved run. " +
            "Otherwise launch Tarkov yourself, enter a Local test raid, press Start collection, stay in the raid for two minutes, " +
            "then complete the context dialog and save. Verify a latest result and enabled Submit. " +
            "CLI capture tests do NOT add a run to GUI history. Do not open Submit or send anything yet.", cancellation);
        await gate.ConfirmAsync("STEP 2 - Prepare standalone Benchmark: open the standalone app. If Submit is enabled, reuse its existing saved run. " +
            "Otherwise enter or stay in a test raid, press Start collection, wait two minutes, complete context and save. " +
            "Verify a latest result and enabled Submit. The apps have SEPARATE local histories; Toolkit's run is not copied here. " +
            "Do not open Submit or send anything yet.", cancellation);
        await gate.ConfirmAsync("STEP 3 - Sign in: close standalone Benchmark. Open Toolkit > Benchmark > Submit. Opening Submit may open the sign-in browser automatically. " +
            "If already signed in, choose Sign out, then Sign in. Complete login yourself (including MFA). " +
            "Verify Account · Signed in, enabled Sign out, and no displayed email/token. Do NOT click Send or Check status.", cancellation);
        await gate.ConfirmAsync("STEP 4 - Restore/shared session: close Toolkit and reopen it; open Benchmark > Submit. Verify Account · Signed in without a second login. " +
            "Close Toolkit again. Open standalone Benchmark > Submit. Verify the same signed-in state without another login. Do NOT send any run.", cancellation);
        await gate.ConfirmAsync("STEP 5 - Shared sign-out: in standalone Benchmark choose Sign out. Verify Account · Signed out. " +
            "A pending revocation, unavailable state or storage error is NOT success. Close its dialog and app.", cancellation);
        await gate.ConfirmAsync("STEP 6 - Verify sign-out in Toolkit: reopen Toolkit > Benchmark > Submit. Verify the signed-out state (it may open the browser again; cancel that sign-in). " +
            "No second login should be completed. Close both apps and verify no run was marked submitted.", cancellation);
        return "User confirmed saved GUI runs in both apps, browser login, session restoration, cross-product sharing and shared sign-out. No automated auth/token or network-upload proof; no Send requested.";
    }
}
