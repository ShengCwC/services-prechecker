# Product Design QA — v1.1.0

- Brand source: `D:\Data\Docs\services-prechecker\assets\banner.png`
- Previous implementation: `D:\Data\Docs\services-prechecker\qa\implementation-final.png`
- Redesigned normal state: `D:\Data\Docs\services-prechecker\qa\implementation-v1.1-initial.png`
- Redesigned pending-restart state: `D:\Data\Docs\services-prechecker\qa\implementation-v1.1-pending.png`
- Redesigned restart modal: `D:\Data\Docs\services-prechecker\qa\implementation-v1.1-modal.png`
- Combined comparison: `D:\Data\Docs\services-prechecker\qa\design-comparison-v1.1.png`
- Viewport: native WPF window at 1140 × 760 logical pixels
- Brand source pixels: 1697 × 956
- Previous implementation pixels: 1680 × 1185 at 150% Windows display scaling
- Redesigned implementation pixels: 1710 × 1140 at 150% Windows display scaling
- Density normalization: the redesigned capture is 1140 × 760 logical pixels multiplied by 1.5; all three full views were aspect-preservingly scaled into 640-pixel comparison panels
- States reviewed: normal scan, pending restart, restart modal

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: Segoe UI handles the desktop chrome and forensic labels; Microsoft YaHei UI handles Chinese product copy. The restart wording has a clear hierarchy from hero statement, to persistent warning, to modal title and action.
- Spacing and layout rhythm: the 44-pixel custom chrome, 184-pixel split hero, compact toolbar, restart notice, and 4 × 2 card grid fit without clipping at the intended viewport. The new split hero removes text/image competition.
- Colors and visual tokens: the near-black and warm-silver brand palette remains intact. Restart semantics use one consistent amber token across hero, notice, status pills, modal, and footer messaging.
- Image quality and asset fidelity: the supplied Banner remains the actual source asset. It is now isolated in the right hero panel with aspect-preserving fill and a controlled center crop, instead of being stretched or used as a text backdrop.
- Copy and content: the exact requested statement is present with “重启系统” emphasized. The persistent warning and modal both explain that the current boot session remains abnormal and only later checks after a Windows restart are valid.
- Icons and window controls: the executable’s supplied logo-derived icon is retained. The three native window controls have distinct close/minimize/maximize actions, tooltips, and Windows UI Automation names.
- Accessibility: restart meaning is communicated through text rather than color alone. Primary actions, modal confirmation, refresh, close, and maximize/restore are keyboard-focusable and exposed to UI Automation.

## Focused comparison evidence

- `implementation-v1.1-initial.png` was reviewed at full resolution for the custom chrome, split Banner crop, exact hero sentence, card density, and footer.
- `implementation-v1.1-pending.png` was reviewed for persistent restart messaging and service-level “等待重启” states.
- `implementation-v1.1-modal.png` was reviewed for overlay contrast, modal hierarchy, line wrapping, and confirmation action.
- Separate smaller crops were unnecessary because all important text and controls are legible in these full-resolution captures.

## Comparison history

1. Previous implementation — P2: the Windows system title bar conflicted with the dark branded surface. Fix: replaced it with custom frameless chrome and macOS-style traffic-light controls while keeping normal Windows resize, minimize, maximize, and close behavior.
2. Previous implementation — P2: the full-width Banner was heavily cropped and competed with overlaid copy. Fix: split the hero into a dedicated text panel and an independent brand-image panel.
3. Previous implementation — P1: restart requirements were limited to a small caveat and did not explain that the current check remains abnormal. Fix: added the exact hero statement, a persistent restart notice, a boot-session marker, service-level pending states, and a blocking confirmation modal.
4. Post-fix evidence: normal, pending, and modal captures show no overlap, clipping, broken wrapping, or ambiguous readiness state.

## Primary interactions tested

- Application startup and service scan: passed.
- “重新检测” through Windows UI Automation: passed.
- Restart marker persistence during the same Windows boot: passed.
- Restart modal appearance and “我知道了” dismissal: passed.
- Custom maximize/restore control: passed; UI Automation reported `Maximized`.
- Custom close control: passed; application exited normally.
- Window-control accessible names: passed.
- Actual service mutation was not invoked during design QA to avoid changing the test computer’s service policy. The elevated enable path compiles successfully and calls the same visually verified restart marker and modal flow after configuration.

## Follow-up polish

No blocking polish remains. A future version could add a compact two-column card layout for unusually small desktop work areas.

final result: passed
