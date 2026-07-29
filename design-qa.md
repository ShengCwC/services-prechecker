# Product Design QA — v1.2.0

## Evidence

- Source visual truth: `C:\Users\Sheng\.codex\generated_images\019fad2c-7e72-79f2-bf67-eefce03f8f8d\call_OfhPMelcKwk7m0WApfWoCzIp.png`
- Rendered implementation: `D:\Data\Docs\services-prechecker\qa\implementation-v1.2-final.png`
- Pending-restart state: `D:\Data\Docs\services-prechecker\qa\implementation-v1.2-pending.png`
- Restart modal state: `D:\Data\Docs\services-prechecker\qa\implementation-v1.2-modal.png`
- Full-view comparison: `D:\Data\Docs\services-prechecker\qa\design-comparison-v1.2.png`
- Focused hero comparison: `D:\Data\Docs\services-prechecker\qa\design-comparison-v1.2-hero.png`
- Logo comparison: `D:\Data\Docs\services-prechecker\qa\logo-comparison-v1.2.png`
- New icon scale strip: `D:\Data\Docs\services-prechecker\qa\app-logo-small-sizes.png`

The source visual is 1536 × 1024 pixels. The rendered WPF window is 1710 × 1140 pixels for a 1140 × 760 logical-pixel window at 150% Windows display scaling. The implementation was aspect-preservingly normalized to 1536 × 1024 before the side-by-side comparison. Both views represent the normal service-scan state in the same dark theme.

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: Microsoft YaHei UI preserves the Chinese display hierarchy and Segoe UI/Consolas provide the intended editorial and forensic labels. The hero title, required restart sentence, persistent warning, service states, and modal remain legible without clipping or awkward wrapping.
- Spacing and layout rhythm: the hero now matches the selected option’s major-region proportion. It is one continuous 218-logical-pixel surface with a compact identity zone, primary copy, and readiness ledger. There is no hard image/text split, detached left card, or duplicate oversized logo.
- Colors and visual tokens: the near-black, warm-ivory, muted-bronze, amber, and semantic service-state colors are consistent across the hero, cards, notice, modal, and controls. The generated map texture stays intentionally low contrast behind all UI copy.
- Image quality and asset fidelity: the selected technical-map art direction is implemented with a dedicated raster hero texture rather than a stretched or awkwardly cropped Banner. The redesigned app mark uses a transparent high-resolution PNG in the UI and a multi-resolution ICO for Windows shell surfaces. No handcrafted SVG, inline drawing, placeholder asset, or transparency halo is present.
- Copy and content: the exact required sentence appears in the hero with “重启系统” emphasized. Pending and modal states continue to explain that the current boot session is invalid and only later checks after restart are effective.
- Icons: the former thin nested-square icon was replaced with a bold standalone double-S forensic-gate mark. The 16, 20, 24, 32, 48, and 64-pixel previews preserve a recognizable silhouette and amber state accent.
- Responsiveness and accessibility: the intended 1140 × 760 viewport has no overlap, clipping, or hidden controls. The minimum window size remains 980 × 700. Restart meaning is communicated through text as well as color, and all primary actions remain keyboard-focusable and exposed through Windows UI Automation.

Focused comparison was required because the Banner composition, logo sharpness, small forensic labels, and restart copy were too small to judge reliably in the full-view comparison. The full hero crop and icon scale strip were inspected separately.

## Comparison History

1. Initial implementation — P2: the hero remained shorter than the selected visual target, weakening the selected masthead hierarchy. Fix: increased the hero from 190 to 218 logical pixels. Post-fix evidence in `design-comparison-v1.2.png` aligns the hero boundary and service summary with the reference.
2. Initial implementation — P2: the 27-pixel information-card icon was being decoded from ICO and looked soft. Fix: use the transparent PNG logo for in-app surfaces and retain the ICO only for Windows executable metadata. Post-fix evidence in `implementation-v1.2-final.png` and `logo-comparison-v1.2.png` shows a crisp mark.
3. Post-fix comparison: no overlapping text, awkward Banner crop, competing focal point, blurred in-app logo, or actionable fidelity drift remains.

## Primary Interactions Tested

- Application startup and seven-service scan: passed.
- “重新检测” through Windows UI Automation: passed.
- Pending-restart marker and service-level “等待重启” states: passed.
- Restart modal appearance and “我知道了” dismissal: passed.
- Custom maximize/restore control: passed.
- Custom close control: passed.
- Actual service mutation was not invoked during design QA to avoid changing the test computer’s service policy. The elevated enable path compiles and uses the verified restart marker, notice, pending states, and modal.

## Follow-up Polish

No blocking polish remains. If the community later commissions a formal brand system, the new application mark can be redrawn as a production vector master while keeping this approved silhouette.

final result: passed
