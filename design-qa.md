# Product Design QA

- Source visual truth: `D:\Data\Docs\services-prechecker\assets\banner.png`
- Implementation screenshot: `D:\Data\Docs\services-prechecker\qa\implementation-final.png`
- Combined comparison: `D:\Data\Docs\services-prechecker\qa\design-comparison.png`
- Viewport: native WPF window at 1120 × 790 logical pixels
- Source pixels: 1697 × 956 at its native density
- Implementation pixels: 1680 × 1185, captured at 150% Windows display scaling
- Density normalization: implementation 1120 × 790 logical pixels × 1.5; both images were scaled into 840-pixel-wide comparison panels without changing aspect ratio
- State: initial service scan complete, four services running and three disabled on the test computer

## Findings

No actionable P0, P1, or P2 differences remain.

- Fonts and typography: the implementation uses Segoe UI for the forensic/English labels and Microsoft YaHei UI for Chinese product copy. Weight, hierarchy, line height, and small-label contrast remain readable at the intended viewport.
- Spacing and layout rhythm: the banner establishes the same wide cinematic composition as the source. The 4 × 2 service-card grid, toolbar, and footer use consistent 12–24 pixel spacing and restrained 3–4 pixel radii.
- Colors and visual tokens: near-black surfaces, warm silver accents, low-contrast borders, and muted secondary text match the supplied brand artwork. Semantic green, amber, red, and violet are reserved for service states.
- Image quality and asset fidelity: the supplied Banner is embedded directly and shown with an aspect-preserving crop. The supplied Logo is used to derive the executable icon; no placeholder or approximate logo was substituted.
- Copy and content: the seven requested services, their Windows identifiers, state labels, local-only privacy note, administrator-permission behavior, and BAM restart caveat are all present.
- Accessibility and interaction: state is communicated by text as well as color. The two primary buttons expose names and invoke actions through Windows UI Automation.

## Focused comparison evidence

The full-resolution implementation screenshot was used to inspect card labels, identifiers, status pills, controls, footer copy, and the derived logo icon. A separate crop was unnecessary because these elements remain legible in `implementation-final.png`.

## Comparison history

1. Initial capture: long names such as “Connected User Experiences” and “Program Compatibility Assistant” collided with their service identifiers in the four-column grid. Severity: P2.
2. Fix: separated the heading into dedicated flexible and fixed columns, then used the exact short names requested by the product brief: DPS, DiagTrack, PcaSvc, and BAM.
3. Post-fix evidence: `implementation-final.png` and `design-comparison.png` show clear labels with no overlap or unintended wrapping.

## Primary interactions tested

- Application startup and non-administrator service scan: passed.
- “重新检测” through Windows UI Automation: passed; the application remained open and refreshed its timestamp and state.
- Button names exposed to Windows UI Automation: passed.
- “一键启用全部服务” system mutation: not invoked during visual QA to avoid changing the test computer’s current service policy. The elevation and enable paths were inspected and compiled successfully.
- Unhandled application exit during the tested flow: none.

## Follow-up polish

No blocking polish remains. A future version could add a user-selectable compact layout for displays below 1080p.

final result: passed
