# Design notes and rationale

This file records **why** RadeonTune looks the way it does, and — importantly — which statements are
observations, which are analysis, and which are inference.

Status legend: **Observed** = reproduced locally; **Analysis** = reasoned from how the software is
built; **Inference** = plausible but NOT measured.

## 1. The vendor panel: what actually hangs on Explorer

**Observed on the target machine (module list of `explorer.exe`):**

- `amdihk64.dll` is loaded into `explorer.exe` and registered as **"Radeon Settings: Host Service"**,
  CLSID `{CCB5F028-A16A-4F57-B8FA-CF5A6E1A8507}`.
- That DLL is the host for the desktop right-click entry ("AMD Software: Adrenalin Edition") and part of
  the hotkey handling. It has to live inside `explorer.exe` because that is how shell extensions work.
- `amdxc64.dll` / `atidxx64.dll` / `amdenc64.dll` in the same process are D3D/encoder UMds — every D3D
  program loads those, that part is normal.

**Consequence:** a fault in the shell extension takes `explorer.exe` down with it — the desktop flashes
and reloads. After restarting Explorer the AMD entry is gone until the Adrenalin service re-injects it.

**Correction to an earlier oversimplification:** the *panel itself* (`RadeonSoftware.exe`) is a separate
process. "The panel hangs on Explorer" is true for its **entry points** (context menu, hotkeys), not for
the whole application.

**Presets:** the overclocking presets are stored by the driver's profile mechanism and get overwritten or
cleared after driver updates, profile reloads, or panel re-initialisation — observed repeatedly on the
target machine.

RadeonTune's answer is structural: native ADLX/ADL core, plain WPF UI, presets in files the tool owns.

## 2. Why there is no VRAM overclocking: transient droop, not steady-state voltage

The relevant failure mode for memory tuning on these cards is **"it dies at load change / at the moment
the memory clock switches"** — that is a **transient droop** problem, not a steady-state undervolt.

- The driver's voltage offset is **static**; it cannot change the droop curve.
- The droop curve is set by the **VRM's load-line (RLL)**, plus IDROOP and TRIM behaviour.
- That is exactly why a memory overclock that looks fine at idle can black/white-screen the moment the
  UCLK switches under load.

**External data point (hardwareluxx forum thread, single case, not reproduced here):**

```
RLL 1.196 -> 1.296 (more droop, less overshoot)
Max Droop Compensation 25 mV -> 20 mV
Result: stable memory clock 2880 -> 2960 MHz
The 10 mV offset change (1.345 -> 1.335) was secondary; the droop tuning did the work.
```

**Status:** architectural reasoning plus one external case. This project did **not** run VRAM
overclocking experiments and did not produce those screens; the feature is absent on purpose, because
the failure mode needs VRM-level tuning the driver does not expose.

If someone revisits VRAM tuning: design it with a fallback display path (iGPU / second GPU) and a
recovery procedure, and treat RLL/droop as the primary variable, not the clock offset.

## 3. Where these notes come from

- Panel architecture: analysis of how the vendor software is packaged (WebView2 + shell-hosted window),
  cross-checked against the behaviour observed on the target machine.
- Preset loss: repeated, user-visible behaviour on the target machine.
- VRAM/UCLK: architectural reasoning about how memory clock changes are executed; explicitly **not**
  a measurement, and not presented as one.

### Provenance (updated 2026-11-05)

Both analyses are **verbatim from this project's own session transcript**, not external publications, and
they are reproduced with message timestamps and sequence numbers in
[docs/sources/2026-11-04-session.md](sources/2026-11-04-session.md):

- panel analysis — **2026-11-04 20:08, seq 2650** (module list of `explorer.exe`, `amdihk64.dll`
  registered as "Radeon Settings: Host Service", CLSID `{CCB5F028-A16A-4F57-B8FA-CF5A6E1A8507}`);
- transient-droop analysis — **2026-11-04 18:03, seq 2006** (RLL / IDROOP / TRIM reasoning).

The transcript lives outside the repo at
`%USERPROFILE%\.dsh\sessions\--C-Users-4521-...--\session-5f46c6c6-...\session.v4.jsonl.zstd`
(zstd-compressed; decompress with `7z x` before searching).

The specific numbers (RLL 1.196→1.296, Max Droop Compensation 25→20 mV, 2880→2960 MHz) come from a
**single hardwareluxx forum post** fetched during that session — a third-party case, not a measurement
performed here.
