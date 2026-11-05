# Design notes and rationale

This file records **why** RadeonTune looks the way it does, and — importantly — which statements are
observations, which are analysis, and which are inference.

Status legend: **Observed** = reproduced locally; **Analysis** = reasoned from how the software is
built; **Inference** = plausible but NOT measured.

## 1. Why replace the vendor panel instead of extending it

- **Analysis — the vendor panel is a WebView2/browser front end hosted by the Windows shell.**
  Its window is owned by the shell process, its UI is HTML/JS rendered by WebView2, and its data
  comes from a local service. Consequences:
  - restarting Explorer takes the panel with it;
  - a wedged WebView2 process leaves the panel blank or unresponsive;
  - the panel cannot be driven reliably from automation.
- **Observed (by the author, on the target machine): the panel occasionally fails to open or shows a
  blank surface until the shell/WebView2 state is reset.**
- **Observed: overclocking presets stored by the driver's profile mechanism get overwritten or cleared**
  after driver updates, profile reloads, or panel re-initialisation. Values that were applied are not
  guaranteed to come back.

RadeonTune's answer is structural, not cosmetic: a native ADLX/ADL core, a plain WPF UI with no WebView,
and presets stored in files the tool owns (`data/presets.json`).

## 2. Why there is no VRAM overclocking

**Status: inference, not measurement.** No VRAM overclocking experiment was run on this project, and
no black/white screen was produced by this tool.

The reasoning is:

- A memory clock change on these GPUs forces a **UCLK switch** (the memory controller changes its
  operating point rather than running at a single fixed frequency).
- The driver pairs that with its own **anti-droop / load-line style voltage regulation**.
- The combination of a dynamic memory clock and that regulation loop is the classic source of
  display-link instability (black screen, white screen) when it goes wrong.

Because the failure mode is hard to bound and would require many risky experiments on the user's only
GPU, the feature was left out on purpose. Core clock, power limit, voltage offset and the fan curve do
not require UCLK switching and are implemented.

If someone later wants to revisit VRAM tuning, the experiment must be designed with a fallback display
path (iGPU or second GPU output) and a documented recovery procedure — not added casually.

## 3. Where these notes come from

- Panel architecture: analysis of how the vendor software is packaged (WebView2 + shell-hosted window),
  cross-checked against the behaviour observed on the target machine.
- Preset loss: repeated, user-visible behaviour on the target machine.
- VRAM/UCLK: architectural reasoning about how memory clock changes are executed; explicitly **not**
  a measurement, and not presented as one.
