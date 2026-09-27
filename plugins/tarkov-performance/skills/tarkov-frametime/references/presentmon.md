# PresentMon Collection

Tarkov Performance Toolkit bundles the exact PresentMon version tested by this repository. Skills must not search PATH, execute user-provided copies, or instruct the player to download PresentMon separately.

The Toolkit starts an external ETW trace for the selected duration and targets `EscapeFromTarkov.exe`. It does not read process memory, inject code, automate input, or provide an overlay.

A capture is valid only when Tarkov is running, logs indicate an active raid, the requested duration completes, and enough frame samples are present. Cancellation, raid exit, game exit, capture conflict, or permission failure discards partial data.

Without Toolkit, this skill may interpret an existing CSV export supplied by the user, but it cannot perform automated capture.
