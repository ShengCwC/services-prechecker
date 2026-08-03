# Product Design QA — v1.5.2

## Evidence

- Source visual truth: `C:\Users\Sheng\.codex\generated_images\019fad2c-7e72-79f2-bf67-eefce03f8f8d\call_OfhPMelcKwk7m0WApfWoCzIp.png`
- Rendered implementation: `D:\Data\Docs\services-prechecker\qa\implementation-v1.2-final.png`
- Pending-restart state: `D:\Data\Docs\services-prechecker\qa\implementation-v1.2-pending.png`
- Restart modal state: `D:\Data\Docs\services-prechecker\qa\implementation-v1.2-modal.png`
- Full-view comparison: `D:\Data\Docs\services-prechecker\qa\design-comparison-v1.2.png`
- Focused hero comparison: `D:\Data\Docs\services-prechecker\qa\design-comparison-v1.2-hero.png`
- Logo comparison: `D:\Data\Docs\services-prechecker\qa\logo-comparison-v1.2.png`
- New icon scale strip: `D:\Data\Docs\services-prechecker\qa\app-logo-small-sizes.png`
- Windows Shell icon extraction: `D:\Data\Docs\services-prechecker\qa\shell-icon-v1.2.1-final.png`
- HWID footer at 1140 × 760: `D:\Data\Docs\services-prechecker\qa\implementation-v1.3.0-hwid-1140x760.png`
- HWID footer at minimum 980 × 700: `D:\Data\Docs\services-prechecker\qa\implementation-v1.3.0-hwid-980x700.png`
- Update modal at 1140 × 760: `D:\Data\Docs\services-prechecker\qa\implementation-v1.4.0-update-1140x760.png`
- Update modal at minimum 980 × 700: `D:\Data\Docs\services-prechecker\qa\implementation-v1.4.0-update-980x700.png`
- v1.4.0 runtime small icon: `D:\Data\Docs\services-prechecker\qa\taskbar-icon-v1.4.1\before-small.png`
- v1.4.1 runtime small icon: `D:\Data\Docs\services-prechecker\qa\taskbar-icon-v1.4.1\after-small.png`
- v1.4.0 runtime large icon: `D:\Data\Docs\services-prechecker\qa\taskbar-icon-v1.4.1\before-big.png`
- v1.4.1 runtime large icon: `D:\Data\Docs\services-prechecker\qa\taskbar-icon-v1.4.1\after-big.png`
- v1.4.2 update modal at 1140 × 760: `D:\Data\Docs\services-prechecker\qa\implementation-v1.4.2-update-1140x760.png`
- v1.4.2 update modal at minimum 980 × 700: `D:\Data\Docs\services-prechecker\qa\implementation-v1.4.2-update-980x700.png`
- v1.5.0 forensic-source readiness at 1140 × 840: `D:\Data\Docs\services-prechecker\qa\implementation-v1.5.0-artifacts-1140x840.png`
- v1.5.0 forensic-source readiness at minimum 980 × 700: `D:\Data\Docs\services-prechecker\qa\implementation-v1.5.0-artifacts-980x700.png`
- v1.5.0 default-scrollbar audit capture: `D:\Data\Docs\services-prechecker\qa\audit-v1.5.0-scrollbar.png`
- v1.5.1 themed scrollbar at 1140 × 840: `D:\Data\Docs\services-prechecker\qa\implementation-v1.5.1-scrollbar-1140x840.png`
- v1.5.1 themed scrollbar at minimum 980 × 700: `D:\Data\Docs\services-prechecker\qa\implementation-v1.5.1-scrollbar-980x700.png`
- Same-state scrollbar comparison: `D:\Data\Docs\services-prechecker\qa\comparison-v1.5.1-scrollbar-before-after.png`
- v1.5.2 synchronized data-source copy at 1140 × 840: `D:\Data\Docs\services-prechecker\qa\implementation-v1.5.2-copy-1140x840.png`

The source visual is 1536 × 1024 pixels. The rendered WPF window is 1710 × 1140 pixels for a 1140 × 760 logical-pixel window at 150% Windows display scaling. The implementation was aspect-preservingly normalized to 1536 × 1024 before the side-by-side comparison. Both views represent the normal service-scan state in the same dark theme.

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: Microsoft YaHei UI preserves the Chinese display hierarchy and Segoe UI/Consolas provide the intended editorial and forensic labels. The hero title, required restart sentence, persistent warning, service states, and modal remain legible without clipping or awkward wrapping.
- Spacing and layout rhythm: the hero now matches the selected option’s major-region proportion. It is one continuous 218-logical-pixel surface with a compact identity zone, primary copy, and readiness ledger. There is no hard image/text split, detached left card, or duplicate oversized logo.
- Colors and visual tokens: the near-black, warm-ivory, muted-bronze, amber, and semantic service-state colors are consistent across the hero, cards, notice, modal, and controls. The generated map texture stays intentionally low contrast behind all UI copy.
- Image quality and asset fidelity: the selected technical-map art direction is implemented with a dedicated raster hero texture rather than a stretched or awkwardly cropped Banner. The redesigned app mark uses a transparent high-resolution PNG inside the UI and a contrast-safe dark squircle ICO for Windows shell surfaces. No handcrafted SVG, inline drawing, placeholder asset, or transparency halo is present.
- Copy and content: the exact required sentence appears in the hero with “重启系统” emphasized. Pending and modal states continue to explain that the current boot session is invalid and only later checks after restart are effective.
- Icons: the former thin nested-square icon was replaced with a bold standalone double-S forensic-gate mark. The 16, 20, 24, 32, 48, and 64-pixel previews preserve a recognizable silhouette and amber state accent.
- Responsiveness and accessibility: the intended 1140 × 760 viewport has no overlap, clipping, or hidden controls. The minimum window size remains 980 × 700. Restart meaning is communicated through text as well as color, and all primary actions remain keyboard-focusable and exposed through Windows UI Automation.
- HWID integration: the complete versioned identifier is geometrically centered in the existing 44-pixel footer between the ellipsized local-status message and right-aligned version label. “点击复制” remains plain text with no detached panel or copy button. At both tested sizes the identifier is complete, readable, and does not move adjacent content.
- HWID states and accessibility: the text exposes a Button/Invoke pattern, keyboard focus, visible focus treatment, hand cursor, tooltip, and automation name/help text. Success, failure, and restoration use the same footprint, so feedback does not cause layout shift.
- Update modal: current/latest versions, the first-party `dl.screenshare.cn` destination, “稍后”, and “前往下载” remain visible without wrapping or clipping at both supported sizes. A visible amber keyboard-focus ring is present, the obscured page is disabled while the modal is open, and restart warnings retain priority over update prompts.
- Runtime taskbar icon: the window now decodes the largest frame from the multi-resolution ICO instead of flattening the first 16-pixel frame. At 150% display scaling, the 24-pixel small icon fills 22 × 22 pixels and the 48-pixel large icon fills 44 × 44 pixels; v1.4.0 filled only about 14 × 14 pixels on either canvas.
- Forensic-source integration: ShimCache, Amcache, and UserAssist use the same card grammar as the seven service checks, bringing the summary to ten readiness conditions without introducing a separate panel. The cards remain readable in four columns at both supported widths and move inside the existing vertical scroll region when height is constrained.
- Minimum-size footer behavior: the centered HWID and right-aligned version remain complete at 980 × 700. The lower-priority privacy sentence may ellipsize on the left at this minimum width, but it does not overlap the interactive HWID or version label; its full wording is available at the default 1140 × 840 size.
- Scrollbar integration: the light Windows-default gutter and arrow buttons have been replaced with a 12-pixel dark forensic rail and centered five-pixel warm-gray thumb. The control now recedes behind card states at rest, becomes clearer on hover, and uses the existing amber warning color while dragging or keyboard-focused. The rail retains native WPF Track paging commands and a 38-pixel minimum thumb rather than becoming a decorative or undersized affordance.
- Data-source wording: the primary action now reads “一键启用全部数据源”, matching the combined seven-service and three-artifact readiness model without forcing a longer forensic qualifier into the button. The loading label and README use the same broader data-source terminology.

Focused comparison was required because the Banner composition, logo sharpness, small forensic labels, and restart copy were too small to judge reliably in the full-view comparison. The full hero crop and icon scale strip were inspected separately.

## Comparison History

1. Initial implementation — P2: the hero remained shorter than the selected visual target, weakening the selected masthead hierarchy. Fix: increased the hero from 190 to 218 logical pixels. Post-fix evidence in `design-comparison-v1.2.png` aligns the hero boundary and service summary with the reference.
2. Initial implementation — P2: the 27-pixel information-card icon was being decoded from ICO and looked soft. Fix: use the transparent PNG logo for in-app surfaces and retain the ICO only for Windows executable metadata. Post-fix evidence in `implementation-v1.2-final.png` and `logo-comparison-v1.2.png` shows a crisp mark.
3. Post-fix comparison: no overlapping text, awkward Banner crop, competing focal point, blurred in-app logo, or actionable fidelity drift remains.
4. User-reported regression — P1: Windows Explorer continued showing the previous icon because the replacement EXE kept both the old `1.1.0.0` file version and the same cache key filename. Fix: update file/product metadata to `1.2.1`, build a uniquely named `ServicesPrechecker-v1.2.1.exe`, use a high-contrast shell-specific icon tile, and verify the result through `SHGetFileInfo`, the same Windows Shell icon path used by Explorer. Post-fix evidence is `shell-icon-v1.2.1-final.png`.
5. User-reported regression — P1: the running application appeared with an abnormally small taskbar icon at high DPI. The generic `BitmapImage` path decoded only the ICO's first 16 × 16 frame, then centered it without scaling inside Windows' 24 × 24 and 48 × 48 runtime icon canvases. Fix: decode the full ICO, select and freeze its 256 × 256 frame, and let WPF generate the DPI-sized window icons. `WM_GETICON` pixel-bound checks confirm the corrected fill ratio.
6. User-reported regression — P1: v1.4.0 could suppress a newly available update for 24 hours after either an earlier failed request or a successful request that cached the then-current version. Fix: remove the cross-process registry cache and failure backoff. Each normal application launch now makes one fresh asynchronous attempt, while the elevated service helper remains offline and failures stay silent.
7. User-reported visual mismatch — P1: the system-default light scrollbar created the brightest vertical element in the card area, introduced legacy arrow buttons, and conflicted with the custom dark chrome. Fix: use a compact native-Track template with a dark rounded rail, warm-gray thumb, hover/drag/focus states, and no arrow glyphs. The same-state comparison confirms that the content edge is now visually continuous with the dashboard.

## Primary Interactions Tested

- Application startup and seven-service scan: passed.
- “重新检测” through Windows UI Automation: passed.
- Pending-restart marker and service-level “等待重启” states: passed.
- Restart modal appearance and “我知道了” dismissal: passed.
- Custom maximize/restore control: passed.
- Custom close control: passed.
- Windows Shell extraction of the signed, versioned EXE icon: passed.
- Runtime `WM_GETICON` small/large icon dimensions and visible-content bounds at 150% scaling: passed.
- Deterministic HWID vectors, source priority, normalization, placeholder rejection, standard 128-bit Crockford encoding, and exact public format: passed.
- WMI exception/timeout injection, four-second total budget, pre-read MachineGuid fallback, and abandoned-task fault observation: passed.
- Two independent application starts returned the same locally generated HWID: passed.
- HWID control format, full untruncated text, UI Automation exposure, keyboard focusability, and feedback restoration: passed.
- Clipboard contention recovery: passed. A separate thread held the Windows clipboard for 650 ms; the application serialized the click, retried with progressive backoff after contention was released, and copied only the exact `USS1-…` value.
- Update checker request contract, per-launch refresh, numeric comparison, strict JSON parsing, 4-second timeout, fixed first-party download URL, rate limits, invalid tags, offline failures, and recovery on the next launch: passed.
- Live GitHub latest-release check: passed and returned `v1.4.1` during v1.4.2 QA. Two separate processes using `v1.4.0` as the comparison version both discovered the update despite an existing recent `LastFailureUtc` registry value; the obsolete registry values remained unchanged and no longer affected behavior. An injected newer result continues to render the update modal at both supported sizes.
- Healthy-service restart policy: passed. A successful no-op no longer invalidates the current boot; configuration, runtime-state, and driver restart changes still establish the restart gate.
- Forensic-source policy tests: passed. Coverage includes disabled/invalid AppCompat policies, optional missing AeLookupSvc, enabled/disabled/missing Application Experience tasks, unavailable Amcache components, original-user SID targeting across elevation, unloaded user hives, Explorer-shell absence, and restart aggregation.
- v1.5.0 responsive UI: passed at 1140 × 840 and 980 × 700. All ten conditions are reachable, the action buttons remain visible, the content scrolls independently of the footer, and no card or footer control overlaps.
- v1.5.1 scrollbar template: passed. Runtime parsing, 12-pixel lane, dark rail, five-pixel resting thumb, 38-pixel minimum thumb, native Page Up/Page Down track commands, and removal of legacy arrow content are covered by an automated WPF test. ScrollViewer mouse-wheel, touch panning, keyboard focus, and accessible name/help text remain enabled.
- Actual service mutation was not invoked during design QA to avoid changing the test computer’s service policy. The elevated enable path compiles and uses the verified restart marker, notice, pending states, and modal.

## Follow-up Polish

No blocking polish remains. If the community later commissions a formal brand system, the new application mark can be redrawn as a production vector master while keeping this approved silhouette.

final result: passed
