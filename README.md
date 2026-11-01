# Fart Together Cheat

A BepInEx (Harmony) cheat menu for **Fart Together Demo** (Steam), a Unity/Mono
game using FishNet networking.

Built for messing around in **lobbies you host yourself**. Gas, boat, and seat
cheats are server-authoritative (FishNet), so they only take effect when you
are the host — see the in-game menu, it shows your current role.

## For your friends: one-click install

1. Download `install.bat` and `install.ps1` from this repo (or just clone/download
   the whole repo as a zip).
2. Keep both files in the same folder.
3. Double-click `install.bat`.
4. First run: it installs BepInEx and asks you to launch the game once, then
   run the installer again.
5. Second run: it downloads the latest `FartTogetherCheat.dll` from this
   repo's Releases page and drops it into `BepInEx\plugins`.
6. Start the game, host a lobby, click the small `>` tab on the left edge of
   the screen (or press F1 / Insert) to open the menu.

No .NET SDK, no Visual Studio, nothing else required on their end.

## For you: building and releasing a new version

The game's own DLLs (`Assembly-CSharp.dll`, `FishNet.Runtime.dll`,
`BepInEx.dll`, ...) are **not** included in this repo — they're the game's
copyrighted code, so GitHub can't build this automatically in CI. You build
locally, where the actual game files are, and upload the result yourself:

```
cd FartTogetherCheat
dotnet build -c Release
```

This produces `bin\Release\net472\FartTogetherCheat.dll` and also copies it
straight into your own game's `BepInEx\plugins` folder (see the `csproj`).

To ship a new version to your friends:

1. Bump the version string in `Plugin.cs` (the `[BepInPlugin(...)]` line).
2. `dotnet build -c Release`
3. On GitHub: **Releases -> Draft a new release**, tag it (e.g. `v0.4.0`),
   and attach `bin\Release\net472\FartTogetherCheat.dll` as a binary asset.
4. Publish the release. `install.ps1` always grabs whatever is tagged
   "latest", so your friends just re-run `install.bat` to update.

## What's in the menu

- Gas: infinite gas, no regen delay, live tuning sliders.
- Boat: invulnerable, gas boost, ignore debuffs, speed boost, max speed
  multiplier, size, items, reset progress.
- Seat: no stun, pass through crewmates, no sector limit.
- "Affect everyone" switch applies the above to the whole lobby, not just you
  (host only).
- Per-player panel: stun/unstun, refill/empty gas, teleport, force ready,
  apply boat statuses, give items, change size — targeted at one selected
  player or all of them at once.
- Lobby: custom name, auto-ready (these two also work as a client).
- Explorer: dumps every type/scene actually compiled into the game, and lets
  you load any scene by its build index, so you can check whether something
  like football or cosmetics exists in this build at all.
- Experimental race-wide tuning (turn speed, swirl bias, gas force, sector
  lock, crew blocking) via reflection on `RaceState`, found by type name at
  runtime since its source hasn't been reviewed yet.

## Setting up the repo yourself

```
git init
git add .
git commit -m "Initial commit"
git branch -M main
git remote add origin https://github.com/<you>/FartTogetherCheat.git
git push -u origin main
```

Then edit the top of `install.ps1` and set `$GitHubUser` / `$GitHubRepo` to
match, commit, and push again before sharing the installer.

## Disclaimer

Only use this in lobbies you host with people who are okay with it. It does
not and cannot affect lobbies hosted by someone else — FishNet's
server-authoritative design doesn't allow that from the client side, and this
project intentionally doesn't try.
