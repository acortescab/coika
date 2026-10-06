# Issue #39: device checklist

**No Android device was available when the code of issue #39 was written, so nothing below has been measured.** Every box is open. Do not tick the 60 FPS criterion from the Editor. Record the device model, Android version and build id with the results, and link the evidence in the PR.

Build a **development** build (so the debug overlay and the Profiler are available): `./Tools/build-android.ps1` makes a release package, so enable *Development Build* and *Autoconnect Profiler* for this pass, then install with `adb install -r Builds/android/coika.apk`.

## Performance (target: a 2019-class mid-range phone)

- [ ] 2-minute run with the Profiler attached: **0 GC allocations per frame** ("GC Allocation In Frame Count" stays 0 during play; the overlay figure is inflated by IMGUI, use the Profiler).
- [ ] 10-minute run: **60 FPS median** and 95th-percentile frame time **at most 20 ms**; no frame above 33 ms in normal play. Attach the Profiler capture or the exported CSV.
- [ ] Worst case seen: **at most 60 active rigidbodies** (overlay "Pieces") and **at most 20 draw calls** (Frame Debugger or Profiler Rendering module).
- [ ] Memory: note the peak from the Profiler Memory module.
- [ ] Battery drop and device temperature over the 10-minute run, noted in the PR.
- [ ] Very Low quality level, AA off and no HDR confirmed in the build (Profiler / `adb logcat`).

## Low-end tier

- [ ] Overlay shows `Normal` on the test phone, or `Low` on a phone below 4 processors or 3000 MB.
- [ ] On a `Low` device the Volume is off and the particle bursts are visibly smaller (or temporarily test it in the Editor by lowering the thresholds).

## Pixel quality (GDD §19)

Screenshots (`adb shell screencap -p /sdcard/x.png`, then `adb pull`; do not use `exec-out`), looking for blur, gaps or shimmer, also during shake and squash/stretch:

- [ ] 1080x1920
- [ ] 1080x2400
- [ ] 720x1280 (or the closest available, plus Device Simulator for the rest)

## Logs and leaks

- [ ] `adb logcat` filtered by the app: **0 errors and 0 warnings** during a full session.
- [ ] Addressables leak check (C-01): return to Retry/Menu and start **10 consecutive runs**; the Addressables Event Viewer shows no growing ref-counts.
- [ ] The debug overlay is **absent from the release package** (`./Tools/build-android.ps1`, install, confirm nothing is drawn).
