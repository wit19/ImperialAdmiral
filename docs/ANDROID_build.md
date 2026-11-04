# Android builds — how-to for this project

Project facts (verified in `ProjectSettings/`): Unity **2020.3.49f1**,
Android app id `com.GlowPuff.ImperialCommander2.tacai` (test builds, see
below), minSdk **22** (Android 5.1+), targetSdk **30**, architectures
ARMv7+ARM64, scripting backend **Mono**, five build scenes (Title first),
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
2. *Build*, choose an output folder → you get `<productName>.apk` (Mono
   backend, both ABIs in one APK).
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

Player Settings → *Publishing Settings*: keystore `user.keystore` (the
original builder kept it at
`C:/Users/afval/Documents/Software_Development/Unity/ImperialCommander2/WORK/BUILD/`).
Passwords are never in the repository — enter keystore password, key alias
and key password locally. Keep using the same keystore across releases:
the signing key, not the id, defines who may update an installed app.
Unsigned/debug builds are fine for testing, not for distribution.

## 6. IL2CPP / AAB / Google Play — the honest status

Current backend Mono + both ABIs is fine for sideloading and itch.io-style
distribution. For a Google Play release you would need IL2CPP + AAB + a
recent target API, and Google's official requirement (developer.android.com/google/play/requirements/target-sdk,
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
