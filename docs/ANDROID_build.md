# Android builds — how-to for this project

Project facts (verified in `ProjectSettings/`): Unity **2020.3.49f1**,
Android app id `com.GlowPuff.ImperialCommander2.tacai` (test builds, see
below), minSdk **22** (Android 5.1+), targetSdk **30**, architectures
ARMv7+ARM64, scripting backend **IL2CPP** (`scriptingBackend: Android: 1`
in `ProjectSettings.asset` — already IL2CPP in the upstream original, so the
NDK from the build-support module is genuinely needed, not optional), five build scenes (Title first),
merged manifest at `Assets/Plugins/Android/AndroidManifest.xml` (leave it
untouched — Unity injects the id and merges the rest at build time). The
project has been Android-built before; the settings are inherited, this
document only re-activates them.

## 1. What to install (once)

Unity Hub → *Installed* → gear icon on **2020.3.49f1** (exactly this patch,
it is the project's version) → *Add modules*:

- **Android Build Support** (take its sub-items **Android SDK & NDK Tools**
  and **OpenJDK** along). Roughly 4–6 GB.

After that, in the editor: *Preferences → External Tools* — the SDK/NDK/
OpenJDK fields should be filled by Unity itself (tick "Specify OS-default
SDK and NDK tools version" / the Unity-provided option). Do not install a
separate Android Studio for the APK route; it is only needed if you ever
want to open the generated Gradle project.

License note: Android builds use the same Unity Personal/Pro license as any
other build target — nothing to activate per platform.

## 2. First build

1. Open the project → *File → Build Settings* → platform **Android** →
   **Switch Platform**. All assets are re-imported for Android; this takes
   a while on first run. The Console must stay free of errors afterwards.
2. *Build*, choose an output folder. As committed, the
   *Build Separate APKs Per Architecture* option is ON
   (`AndroidBuildApkPerCpuArchitecture: 1`), so you get **two** APKs:
   `armeabi-v7a` (32-bit) and `arm64-v8a` (64-bit). Install **arm64-v8a**
   on anything sold since ~2018 (newest devices are 64-bit-only and reject
   the v7 file); v7 is only for old/cheap 32-bit hardware. For one
   universal APK instead, untick the option — slightly bigger file, works
   everywhere. Backend is IL2CPP: C++ compile makes builds slower than
   Mono; the first Gradle run additionally downloads the toolchain (§7).
   (Verified in a real build, 2026-11-04.)
3. Install on a device: copy the APK over and open it (allow "install from
   unknown sources"), or `adb install <apk>`. USB debugging must be enabled
   on the device for adb.

## 3. App id — why it is `.tacai` (and why NOT reusing the original)

Test builds from this repository ship as `com.GlowPuff.ImperialCommander2.tacai`
(commit `252147a`). Same id as the official store app is not usable in
practice: the Play Store re-signs apps with its own key, so our test APK
(signed with our keystore) hitting an installed store version gives
`INSTALL_FAILED_UPDATE_INCOMPATIBLE` — the official app would have to be
uninstalled first, **which wipes its saves anyway**, and every test APK
spread around would risk replacing real installs with an experimental
build. A distinct id installs side by side, cannot break anything, and
costs one adb move to bring saves along (next section). For an official
release GlowPuff decides the id (the base id would restore updatable
Play-Store continuity) — that decision is deliberately not taken here.

## 4. Save games live in the app-id folder (and how to bring them over)

`Application.persistentDataPath` on Android is per app id:
`/sdcard/Android/data/<appId>/files`, containing `SagaCampaigns`,
`SagaSession`, `CampaignSession`, `Session`, `Defaults`, `CustomMissions`,
`ImportedCharacters`, `CustomCampaigns` (see `FileManager.cs`). A new id
starts with an empty folder.

An app cannot read a neighbour's data folder on Android 11+ (sandbox) —
this is why there is no in-game "import saves" button and never will be
one. **adb can**, and that is the supported migration:

```sh
adb pull /sdcard/Android/data/com.GlowPuff.ImperialCommander2/files ./ic2_saves
# keep the 'files' subfolders, push them into the new app:
adb push ./ic2_saves/. /sdcard/Android/data/com.GlowPuff.ImperialCommander2.tacai/files/
```

Start the `.tacai` build once before the push so the target folder exists.
Works on every Android version; moves campaigns, sessions, custom missions
and imported characters in one go.

## 5. Release signing

The original afval-era keystore is gone; the `.tacai` test line is signed
with a freshly generated key (created 2026-11-04, PKCS12, alias `ic2tacai`,
RSA-2048/SHA-256, valid to 2054-03-22, certificate fingerprint
`1E:F5:94:45:24:69:38:63:8E:37:C4:76:82:C9:3A:33:12:78:67:D8:77:BC:44:C1:CA:5B:91:98:92:05:E2:E0`).

Wiring (committed): `ProjectSettings.asset` references `user.keystore`
(resolved relative to the `ProjectSettings/` folder) with alias `ic2tacai`.
The keystore file itself is **git-ignored** (`*.keystore`, `*.pass`): the
private key must never enter the repository.

- File location per machine: drop the keystore file as
  `<repo>/ImperialCommander2/ProjectSettings/user.keystore`.
  Master copy on the build box: `~/work/ia-ai/keys/ic2-tacai.keystore`
  (+ `ic2-tacai-keystore.pass` with the password, mode 600). **Copy both to
  a second medium immediately** (NAS backup): a lost signing key means no
  further updates for installed test apps, and a leaked one means anyone
  can ship "official" updates to them.
- Unity asks for the keystore and alias password once per machine under
  *Player Settings → Publishing Settings* and stores them locally
  encrypted; the command-line route takes
  `-keystorePassword <p> -keyaliasPassword <p>` as build parameters.
- From now on the same keystore signs every release of this line: the
  signing key, not the app id, decides who may update an installed app.
- GlowPuff's store key for the base id is untouched by all this — if an
  official release ever happens under the base id, that key (and only
  that key) signs it.
- Unsigned/debug builds are fine for testing, not for distribution.

## 6. IL2CPP / AAB / Google Play — the honest status

The backend is already IL2CPP (see top of this document), so a sideload or
itch.io-style APK works as is. For a Google Play release you would still
need an AAB build and a recent target API; Google's official requirement (developer.android.com/google/play/requirements/target-sdk,
checked 2026-11-04): new apps and updates must target **API 35** since
2025-08-31, **API 36** from 2026-08-31. Unity 2020.3 tops out far below
that — a Play release therefore requires upgrading the project to a newer
Unity LTS first (plus licence/keystore housekeeping). Sideload and
direct-download distribution are unaffected by this.

## 7. Gotchas

- First Gradle build downloads its toolchain — slow, keep the machine on
  the network.
- Keep the custom `AndroidManifest.xml` in `Assets/Plugins/Android` as is.
- Scene list order matters: `Title.unity` must stay slot 0.
- Changing `productName` changes the launcher label and the APK file name —
  cosmetic, but the store id (section 3) is the thing that actually
  identifies the app.
- If Unity asks to "reimport/upgrade" packages when opening: for the test
  build stay on 2020.3.49f1, do not let a newer editor upgrade the project.
