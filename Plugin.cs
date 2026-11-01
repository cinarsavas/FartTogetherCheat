using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using PuffPirates.Boats;
using PuffPirates.Networking;
using PuffPirates.Players;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FartTogetherCheat
{
    [BepInPlugin("farttogether.cheat", "Fart Together Cheat", "0.4.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log;

        internal static bool Everyone = false;

        // Gas
        internal static bool InfiniteGas = true;
        internal static bool FastRegen = false;
        internal static bool OthersCantHoldGas = false;

        // Boat
        internal static bool Invulnerable = false;
        internal static bool GasBoost = false;
        internal static bool NoDebuffs = false;
        internal static bool SpeedBoost = false;
        internal static float MaxSpeedMul = 1f;

        // Seat
        internal static bool NoStun = false;
        internal static bool NoCrewBlock = false;
        internal static bool NoSectorLimit = false;

        // Lobby
        internal static bool AutoReady = false;
        internal static string CustomName = "";

        // Bypasses our own patches (used by menu actions)
        internal static bool Bypass = false;

        private bool _menuOpen = false; // starts closed; use the tab or a hotkey
        private Rect _window = new Rect(20f, 20f, 400f, 720f);
        private Vector2 _scroll;
        private float _nextReady;
        private int _selectedClientId = int.MinValue;
        private int _sceneIndexInput = 0;

        private ConfigEntry<KeyboardShortcut> _menuKey;
        private ConfigEntry<KeyboardShortcut> _menuKeyAlt;
        private ConfigEntry<KeyboardShortcut> _hkInfiniteGas;
        private ConfigEntry<KeyboardShortcut> _hkInvulnerable;
        private ConfigEntry<KeyboardShortcut> _hkSpeedBoost;
        private ConfigEntry<KeyboardShortcut> _hkNoStun;
        private ConfigEntry<KeyboardShortcut> _hkEveryone;

        private void Awake()
        {
            Log = Logger;

            _menuKey = Config.Bind("Menu", "ToggleMenu", new KeyboardShortcut(KeyCode.F1), "Shows/hides the menu");
            _menuKeyAlt = Config.Bind("Menu", "ToggleMenuAlt", new KeyboardShortcut(KeyCode.Insert), "Second key that shows/hides the menu");

            _hkInfiniteGas = Config.Bind("Hotkeys", "InfiniteGas", new KeyboardShortcut(KeyCode.F2), "Toggle infinite gas");
            _hkInvulnerable = Config.Bind("Hotkeys", "Invulnerable", new KeyboardShortcut(KeyCode.F3), "Toggle invulnerable");
            _hkSpeedBoost = Config.Bind("Hotkeys", "SpeedBoost", new KeyboardShortcut(KeyCode.F4), "Toggle speed boost");
            _hkNoStun = Config.Bind("Hotkeys", "NoStun", new KeyboardShortcut(KeyCode.F5), "Toggle no stun");
            _hkEveryone = Config.Bind("Hotkeys", "AffectEveryone", new KeyboardShortcut(KeyCode.F6), "Toggle 'affect everyone'");

            InfiniteGas = Config.Bind("Saved", "InfiniteGas", true).Value;
            FastRegen = Config.Bind("Saved", "FastRegen", false).Value;
            Invulnerable = Config.Bind("Saved", "Invulnerable", false).Value;
            GasBoost = Config.Bind("Saved", "GasBoost", false).Value;
            NoDebuffs = Config.Bind("Saved", "NoDebuffs", false).Value;
            NoStun = Config.Bind("Saved", "NoStun", false).Value;
            NoCrewBlock = Config.Bind("Saved", "NoCrewBlock", false).Value;
            NoSectorLimit = Config.Bind("Saved", "NoSectorLimit", false).Value;
            Everyone = Config.Bind("Saved", "Everyone", false).Value;

            new Harmony("farttogether.cheat").PatchAll();
            Logger.LogInfo("Fart Together Cheat 0.4.0 loaded. Click the tab in the corner, or press " +
                            _menuKey.Value + " / " + _menuKeyAlt.Value + ", to open the menu.");
        }

        internal static bool IsMyBoat(NetworkBoat boat)
        {
            PiratePlayer me = PiratePlayer.Local;
            return me != null && boat != null && me.Boat == boat;
        }

        internal static bool IsMyGas(PirateGas gas)
        {
            PiratePlayer me = PiratePlayer.Local;
            return me != null && gas != null && me.Gas == gas;
        }

        internal static bool AffectsBoat(NetworkBoat boat) => boat != null && (Everyone || IsMyBoat(boat));
        internal static bool AffectsGas(PirateGas gas) => gas != null && (Everyone || IsMyGas(gas));
        internal static bool AffectsPlayer(PiratePlayer p) => p != null && (Everyone || p == PiratePlayer.Local);

        internal static void WithBypass(Action a)
        {
            Bypass = true;
            try { a(); } finally { Bypass = false; }
        }

        private void Update()
        {
            TryToggle(_menuKey);
            TryToggle(_menuKeyAlt);
            TryHotkey(_hkInfiniteGas, () => InfiniteGas = !InfiniteGas);
            TryHotkey(_hkInvulnerable, () => Invulnerable = !Invulnerable);
            TryHotkey(_hkSpeedBoost, () => SpeedBoost = !SpeedBoost);
            TryHotkey(_hkNoStun, () => NoStun = !NoStun);
            TryHotkey(_hkEveryone, () => Everyone = !Everyone);

            if (_menuOpen)
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }

            if (AutoReady && Time.unscaledTime >= _nextReady)
            {
                _nextReady = Time.unscaledTime + 1f;
                PiratePlayer me = PiratePlayer.Local;
                if (me != null && !me.IsReady) me.RpcSetReady(true);
            }

            RaceTuning.ApplyEachFrame();
        }

        private void TryToggle(ConfigEntry<KeyboardShortcut> key)
        {
            try { if (key.Value.IsDown()) _menuOpen = !_menuOpen; }
            catch { /* ignore, the on-screen tab still works */ }
        }

        private void TryHotkey(ConfigEntry<KeyboardShortcut> key, Action toggle)
        {
            try { if (key.Value.IsDown()) toggle(); }
            catch { /* ignore this one hotkey, others still work */ }
        }

        private void OnGUI()
        {
            // Always-visible tab: guaranteed to work even if hotkeys don't (new Input System, remapped keys, etc.)
            if (GUI.Button(new Rect(0f, 60f, 26f, 90f), _menuOpen ? "<" : ">"))
            {
                _menuOpen = !_menuOpen;
            }

            if (!_menuOpen) return;
            _window = GUILayout.Window(0x46415254, _window, DrawWindow, "Fart Together Cheat 0.4");
        }

        private void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            PiratePlayer me = PiratePlayer.Local;
            bool host = me != null && me.IsServerInitialized;
            NetworkBoat boat = me != null ? me.Boat : null;

            if (me == null) GUILayout.Label("No local pirate yet (join or host a lobby).");
            else
            {
                GUILayout.Label(host ? "Role: HOST (everything works)" : "Role: CLIENT (only name / auto-ready work)");
                if (me.Gas != null)
                    GUILayout.Label("Gas: " + me.Gas.GasEnergy.ToString("F1") + " / " + me.Gas.MaxGas.ToString("F0")
                                    + (me.Gas.GasDepleted ? "  [DEPLETED]" : ""));
            }

            GUILayout.Space(4f);
            Everyone = GUILayout.Toggle(Everyone, "AFFECT EVERYONE in the lobby (F6)");

            Header("Gas (host only)");
            InfiniteGas = GUILayout.Toggle(InfiniteGas, "Infinite gas (F2)");
            FastRegen = GUILayout.Toggle(FastRegen, "No regen delay");
            OthersCantHoldGas = GUILayout.Toggle(OthersCantHoldGas, "Others can't hold-fart (taps still work)");
            PirateGasConfig cfg = GasTuning.GetConfig();
            if (cfg != null)
            {
                GUILayout.Label("Changes the shared gas config (all pirates on this host).");
                GasTuning.Slider("Max gas", cfg, "_maxGas", 1f, 1000f);
                GasTuning.Slider("Regen per second", cfg, "_gasRegenPerSec", 0f, 500f);
                GasTuning.Slider("Regen delay (s)", cfg, "_gasRegenDelay", 0f, 3f);
                GasTuning.Slider("Recover fraction", cfg, "_gasRecoverPercent", 0.05f, 0.9f);
            }

            Header("Boat (host only)");
            Invulnerable = GUILayout.Toggle(Invulnerable, "Invulnerable (F3)");
            GasBoost = GUILayout.Toggle(GasBoost, "Gas power x1.5");
            NoDebuffs = GUILayout.Toggle(NoDebuffs, "Ignore debuffs");
            SpeedBoost = GUILayout.Toggle(SpeedBoost, "Constant speed boost (F4)");
            GUILayout.Label("Max speed x" + MaxSpeedMul.ToString("F2"));
            MaxSpeedMul = GUILayout.HorizontalSlider(MaxSpeedMul, 1f, 4f);

            if (host && boat != null)
            {
                GUILayout.Label("My boat size: " + boat.SizeId + " (of " + BoatSizes.Count + ")");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Size -")) boat.SetSize(boat.SizeId - 1);
                if (GUILayout.Button("Size +")) boat.SetSize(boat.SizeId + 1);
                if (GUILayout.Button("Reset my progress")) boat.ResetProgress();
                GUILayout.EndHorizontal();

                GUILayout.Label("Give myself item (0 = none, 1-6):");
                GUILayout.BeginHorizontal();
                for (int i = 0; i <= 6; i++) if (GUILayout.Button(i.ToString())) boat.SetHeldItem(i);
                GUILayout.EndHorizontal();
            }

            Header("Seat (host only)");
            NoStun = GUILayout.Toggle(NoStun, "No stun (F5)");
            NoCrewBlock = GUILayout.Toggle(NoCrewBlock, "Pass through crewmates");
            NoSectorLimit = GUILayout.Toggle(NoSectorLimit, "No sector limit");

            Header("Race tuning (host only, experimental)");
            RaceTuning.DrawUI();

            Header("Lobby");
            AutoReady = GUILayout.Toggle(AutoReady, "Auto ready (me)");
            GUILayout.Label("Custom name (max 16, empty = Steam name):");
            CustomName = GUILayout.TextField(CustomName ?? "", 16);

            Header("Explore (find hidden/beta content)");
            if (GUILayout.Button("Dump matching types to file")) Dumper.RunTypeDump();
            if (GUILayout.Button("Dump ALL scenes in build to file")) Dumper.RunSceneDump();
            if (!string.IsNullOrEmpty(Dumper.LastResult)) GUILayout.Label(Dumper.LastResult);

            GUILayout.Label("Load scene by build index:");
            GUILayout.BeginHorizontal();
            string txt = GUILayout.TextField(_sceneIndexInput.ToString(), 4);
            int.TryParse(txt, out _sceneIndexInput);
            if (GUILayout.Button("Load")) SafeLoadScene(_sceneIndexInput);
            GUILayout.EndHorizontal();
            GUILayout.Label("Scenes 0.." + (SceneManager.sceneCountInBuildSettings - 1) + " exist in the build (not all are reachable from the menu).");

            Header("Players");
            PiratePlayer selected = null;
            for (int i = 0; i < PiratePlayer.Active.Count; i++)
            {
                PiratePlayer p = PiratePlayer.Active[i];
                if (p == null) continue;
                string gasText = p.Gas != null ? p.Gas.GasEnergy.ToString("F0") : "-";
                string line = p.PlayerName + " | team " + p.TeamId + " | boat " + p.BoatIndex + " | gas " + gasText
                              + (p.IsStunned ? " | STUNNED" : "") + (p.IsReady ? " | ready" : "");
                bool isSel = p.ClientId == _selectedClientId;
                if (isSel) selected = p;
                bool nowSel = GUILayout.Toggle(isSel, line, "Button");
                if (nowSel && !isSel) { _selectedClientId = p.ClientId; selected = p; }
            }

            if (host)
            {
                DrawEveryoneActions(me, boat);
                if (selected != null) DrawSelectedActions(selected, me, boat);
            }
            else GUILayout.Label("(player actions need you to be the host)");

            GUILayout.Space(8f);
            GUILayout.Label("Menu key: " + _menuKey.Value + " / " + _menuKeyAlt.Value + "  (or the tab on the left edge)");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static void SafeLoadScene(int index)
        {
            try
            {
                if (index < 0 || index >= SceneManager.sceneCountInBuildSettings)
                {
                    Dumper.LastResult = "No scene at index " + index + ".";
                    return;
                }
                SceneManager.LoadScene(index);
            }
            catch (Exception e)
            {
                Dumper.LastResult = "Load failed: " + e.Message;
                Log.LogError(e);
            }
        }

        private static void DrawEveryoneActions(PiratePlayer me, NetworkBoat myBoat)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Actions on ALL other players:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stun 3s")) ForEachOther(me, p => WithBypass(() => p.Stun(3f)));
            if (GUILayout.Button("Unstun")) ForEachOther(me, p => p.IsStunned = false);
            if (GUILayout.Button("Refill gas")) ForEachOther(me, RefillGas);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Empty gas")) ForEachOther(me, EmptyGas);
            if (GUILayout.Button("Ready all")) ForEachOther(me, p => p.HostSetReady(true));
            if (GUILayout.Button("Teleport to me") && myBoat != null)
                ForEachOther(me, p => { NetworkBoat b = p.Boat; if (b != null && b != myBoat) b.Teleport(myBoat.transform.position + Vector3.up * 1.5f, myBoat.transform.rotation); });
            GUILayout.EndHorizontal();
        }

        private static void DrawSelectedActions(PiratePlayer p, PiratePlayer me, NetworkBoat myBoat)
        {
            GUILayout.Space(6f);
            GUILayout.Label("Selected: " + p.PlayerName);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Stun 3s")) WithBypass(() => p.Stun(3f));
            if (GUILayout.Button("Stun 10s")) WithBypass(() => p.Stun(10f));
            if (GUILayout.Button("Unstun")) p.IsStunned = false;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Refill gas")) RefillGas(p);
            if (GUILayout.Button("Empty gas")) EmptyGas(p);
            if (GUILayout.Button("Force ready")) p.HostSetReady(true);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Squeeze seat")) p.SetSector(p.AttachmentAngle, 10f);
            if (GUILayout.Button("Reset seat")) p.SetSector(0f, 180f);
            GUILayout.EndHorizontal();

            NetworkBoat b = p.Boat;
            if (b == null) { GUILayout.Label("(no boat yet)"); return; }

            GUILayout.Label("Their boat: size " + b.SizeId + ", item " + b.HeldItem);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Size -")) b.SetSize(b.SizeId - 1);
            if (GUILayout.Button("Size +")) b.SetSize(b.SizeId + 1);
            if (GUILayout.Button("Mini 8s")) WithBypass(() => b.ApplyMini(8f, 0));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Ink 5s")) WithBypass(() => b.ApplyInk(5f));
            if (GUILayout.Button("Speed boost 5s")) b.ApplySpeedBoost(5f);
            if (GUILayout.Button("Invuln 5s")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.Invulnerable, BoatStatusFlags.Invulnerable, 5f));
            GUILayout.EndHorizontal();
            GUILayout.Label("Apply status for 5s:");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Slow")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.Slow, BoatStatusFlags.Slow, 5f));
            if (GUILayout.Button("Gas nerf")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.GasNerf, BoatStatusFlags.GasNerf, 5f));
            if (GUILayout.Button("Gas boost")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.GasBoost, BoatStatusFlags.GasBoost, 5f));
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Heavy")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.MassHeavy, BoatStatusFlags.MassHeavy, 5f));
            if (GUILayout.Button("Light")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.MassLight, BoatStatusFlags.MassLight, 5f));
            if (GUILayout.Button("Plank")) WithBypass(() => b.ApplyStatus(BoatStatusSlot.Plank, BoatStatusFlags.Plank, 5f));
            GUILayout.EndHorizontal();
            GUILayout.Label("Give item to their boat:");
            GUILayout.BeginHorizontal();
            for (int i = 0; i <= 6; i++) if (GUILayout.Button(i.ToString())) b.SetHeldItem(i);
            GUILayout.EndHorizontal();
            if (myBoat != null && b != myBoat)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Teleport them to me")) b.Teleport(myBoat.transform.position + Vector3.up * 1.5f, myBoat.transform.rotation);
                if (GUILayout.Button("Teleport me to them")) myBoat.Teleport(b.transform.position + Vector3.up * 1.5f, b.transform.rotation);
                GUILayout.EndHorizontal();
            }
        }

        private static void ForEachOther(PiratePlayer me, Action<PiratePlayer> action)
        {
            for (int i = 0; i < PiratePlayer.Active.Count; i++)
            {
                PiratePlayer p = PiratePlayer.Active[i];
                if (p != null && p != me) action(p);
            }
        }

        private static void RefillGas(PiratePlayer p)
        {
            if (p.Gas == null) return;
            p.Gas._svGasEnergy.Value = p.Gas.MaxGas;
            p.Gas._svGasDepleted.Value = false;
        }

        private static void EmptyGas(PiratePlayer p)
        {
            if (p.Gas == null) return;
            WithBypass(() => p.Gas.ConsumeGas(p.Gas.MaxGas));
        }

        private static void Header(string text)
        {
            GUILayout.Space(8f);
            GUILayout.Label("---- " + text + " ----");
        }
    }

    // ---- Explore: type + scene dumps --------------------------------------

    internal static class Dumper
    {
        internal static string LastResult = "";

        private static readonly string[] Keywords =
        {
            "lobby", "launcher", "racestate", "connection", "mode", "football", "ball", "goal", "countdown", "match",
            "cosmetic", "shop", "unlock", "currency", "coin", "beta", "debug", "test", "dev"
        };

        private static bool Matches(string name)
        {
            string n = name.ToLowerInvariant();
            for (int i = 0; i < Keywords.Length; i++) if (n.Contains(Keywords[i])) return true;
            return false;
        }

        internal static void RunTypeDump()
        {
            try
            {
                string path = System.IO.Path.Combine(Paths.BepInExRootPath, "FartTogether_types.txt");
                var sb = new System.Text.StringBuilder();
                Type[] types;
                try { types = typeof(PiratePlayer).Assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { types = ex.Types; }

                const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                                          BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                int count = 0;
                foreach (Type t in types)
                {
                    if (t == null || t.FullName == null || t.FullName.Contains("<")) continue;
                    if (!Matches(t.Name)) continue;
                    try
                    {
                        count++;
                        sb.AppendLine("=== " + t.FullName + " : " + (t.BaseType != null ? t.BaseType.Name : "-"));
                        if (t.IsEnum) { sb.AppendLine("  enum: " + string.Join(", ", Enum.GetNames(t))); sb.AppendLine(); continue; }
                        foreach (FieldInfo f in t.GetFields(All))
                            if (!f.Name.Contains("<")) sb.AppendLine("  field  " + (f.IsStatic ? "static " : "") + f.FieldType.Name + " " + f.Name);
                        foreach (PropertyInfo p in t.GetProperties(All))
                            sb.AppendLine("  prop   " + p.PropertyType.Name + " " + p.Name);
                        foreach (MethodInfo m in t.GetMethods(All))
                        {
                            if (m.IsSpecialName || m.Name.Contains("<") || m.Name.Contains("___")) continue;
                            var ps = m.GetParameters();
                            string args = "";
                            for (int k = 0; k < ps.Length; k++) args += (k > 0 ? ", " : "") + ps[k].ParameterType.Name + " " + ps[k].Name;
                            sb.AppendLine("  method " + (m.IsStatic ? "static " : "") + (m.IsPublic ? "public " : "private ") + m.ReturnType.Name + " " + m.Name + "(" + args + ")");
                        }
                        sb.AppendLine();
                    }
                    catch (Exception e) { sb.AppendLine("  (failed: " + e.Message + ")"); }
                }
                System.IO.File.WriteAllText(path, sb.ToString());
                LastResult = "Wrote " + count + " types to BepInEx\\FartTogether_types.txt";
                Plugin.Log.LogInfo(LastResult);
            }
            catch (Exception e) { LastResult = "Dump failed: " + e.Message; Plugin.Log.LogError(e); }
        }

        internal static void RunSceneDump()
        {
            try
            {
                string path = System.IO.Path.Combine(Paths.BepInExRootPath, "FartTogether_scenes.txt");
                var sb = new System.Text.StringBuilder();
                int n = SceneManager.sceneCountInBuildSettings;
                for (int i = 0; i < n; i++)
                {
                    string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                    sb.AppendLine(i + ": " + scenePath);
                }
                System.IO.File.WriteAllText(path, sb.ToString());
                LastResult = "Wrote " + n + " scenes to BepInEx\\FartTogether_scenes.txt";
                Plugin.Log.LogInfo(LastResult);
            }
            catch (Exception e) { LastResult = "Scene dump failed: " + e.Message; Plugin.Log.LogError(e); }
        }
    }

    // ---- Gas config tuning (reflection on private fields) ------------------

    internal static class GasTuning
    {
        private static readonly FieldInfo ConfigField = AccessTools.Field(typeof(PirateGas), "_config");

        internal static PirateGasConfig GetConfig()
        {
            PiratePlayer me = PiratePlayer.Local;
            if (me == null || me.Gas == null || ConfigField == null) return null;
            return ConfigField.GetValue(me.Gas) as PirateGasConfig;
        }

        internal static void Slider(string label, PirateGasConfig cfg, string field, float min, float max)
        {
            FieldInfo f = AccessTools.Field(typeof(PirateGasConfig), field);
            if (f == null) return;
            float cur = (float)f.GetValue(cfg);
            GUILayout.Label(label + ": " + cur.ToString("F2"));
            float next = GUILayout.HorizontalSlider(cur, min, max);
            if (!Mathf.Approximately(next, cur)) f.SetValue(cfg, next);
        }
    }

    // ---- Race-wide tuning via reflection (RaceState type found by name) ----
    // NOTE: experimental — RaceState's own source hasn't been reviewed, so the
    // member names below are guesses based on how PiratePlayer/BoatPhysicsController
    // use them (instance.TurnSpeedDeg, instance.SwirlBias, instance.NormalGasForce,
    // race.SectorLock, race.CrewBlocking, race.BoundaryLeanEnabled, race.LockRotX/Y,
    // race.CanMove). Any member that isn't found or can't be set is silently skipped.

    internal static class RaceTuning
    {
        internal static bool OverrideTurnSpeed = false;
        internal static float TurnSpeedDeg = 140f;
        internal static bool OverrideSwirl = false;
        internal static float SwirlBias = 0f;
        internal static bool OverrideGasForce = false;
        internal static float NormalGasForce = 20f;
        internal static bool NoSectorLock = false;
        internal static bool NoCrewBlocking = false;
        internal static bool NoBoundaryLean = false;

        private static Type _type;
        private static PropertyInfo _instanceProp;
        private static bool _resolved;

        private static object GetInstance()
        {
            if (!_resolved)
            {
                _resolved = true;
                try
                {
                    foreach (Type t in typeof(PiratePlayer).Assembly.GetTypes())
                    {
                        if (t.Name == "RaceState") { _type = t; break; }
                    }
                    if (_type != null)
                        _instanceProp = _type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                }
                catch (Exception e) { Plugin.Log.LogWarning("RaceTuning: couldn't resolve RaceState: " + e.Message); }
            }
            if (_instanceProp == null) return null;
            try { return _instanceProp.GetValue(null); } catch { return null; }
        }

        private static bool TrySet(object instance, string name, object value)
        {
            try
            {
                PropertyInfo p = _type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null && p.CanWrite) { p.SetValue(instance, value); return true; }
                FieldInfo f = _type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) { f.SetValue(instance, value); return true; }
            }
            catch { /* member shape differs from our guess; skip silently */ }
            return false;
        }

        private static object TryGet(object instance, string name)
        {
            try
            {
                PropertyInfo p = _type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (p != null) return p.GetValue(instance);
                FieldInfo f = _type.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                if (f != null) return f.GetValue(instance);
            }
            catch { }
            return null;
        }

        internal static void ApplyEachFrame()
        {
            PiratePlayer me = PiratePlayer.Local;
            if (me == null || !me.IsServerInitialized) return;
            object inst = GetInstance();
            if (inst == null) return;

            if (OverrideTurnSpeed) TrySet(inst, "TurnSpeedDeg", TurnSpeedDeg);
            if (OverrideSwirl) TrySet(inst, "SwirlBias", SwirlBias);
            if (OverrideGasForce) TrySet(inst, "NormalGasForce", NormalGasForce);
            if (NoSectorLock) TrySet(inst, "SectorLock", false);
            if (NoCrewBlocking) TrySet(inst, "CrewBlocking", false);
            if (NoBoundaryLean) TrySet(inst, "BoundaryLeanEnabled", false);
        }

        internal static void DrawUI()
        {
            object inst = GetInstance();
            if (_type == null)
            {
                GUILayout.Label("RaceState wasn't found (menu scene, or name differs).");
                return;
            }
            if (inst == null)
            {
                GUILayout.Label("RaceState.Instance is null right now (not in a race).");
                return;
            }

            GUILayout.BeginHorizontal();
            OverrideTurnSpeed = GUILayout.Toggle(OverrideTurnSpeed, "Turn speed");
            TurnSpeedDeg = GUILayout.HorizontalSlider(TurnSpeedDeg, 20f, 600f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            OverrideSwirl = GUILayout.Toggle(OverrideSwirl, "Swirl bias");
            SwirlBias = GUILayout.HorizontalSlider(SwirlBias, -2f, 2f);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            OverrideGasForce = GUILayout.Toggle(OverrideGasForce, "Gas force");
            NormalGasForce = GUILayout.HorizontalSlider(NormalGasForce, 0f, 200f);
            GUILayout.EndHorizontal();

            NoSectorLock = GUILayout.Toggle(NoSectorLock, "Disable sector lock (race-wide)");
            NoCrewBlocking = GUILayout.Toggle(NoCrewBlocking, "Disable crew blocking (race-wide)");
            NoBoundaryLean = GUILayout.Toggle(NoBoundaryLean, "Disable boundary lean torque");

            object canMove = TryGet(inst, "CanMove");
            if (canMove is bool cm) GUILayout.Label("CanMove: " + cm);
        }
    }

    // ---- Gas patches ---------------------------------------------------------

    [HarmonyPatch(typeof(PirateGas), nameof(PirateGas.ConsumeGas))]
    internal static class ConsumeGasPatch
    {
        private static bool Prefix(PirateGas __instance)
        {
            if (Plugin.Bypass) return true;
            if (Plugin.InfiniteGas && Plugin.AffectsGas(__instance)) return false;
            return true;
        }

        private static void Postfix(PirateGas __instance)
        {
            if (Plugin.Bypass) return;
            if (Plugin.FastRegen && __instance.IsServerInitialized && Plugin.AffectsGas(__instance))
                __instance._svRegenDelayTimer.Value = 0f;
        }
    }

    [HarmonyPatch(typeof(PirateGas), nameof(PirateGas.RegenGas))]
    internal static class RegenGasPatch
    {
        private static void Postfix(PirateGas __instance)
        {
            if (!Plugin.InfiniteGas || !__instance.IsServerInitialized || !Plugin.AffectsGas(__instance)) return;
            if (__instance._svGasEnergy.Value < __instance.MaxGas) __instance._svGasEnergy.Value = __instance.MaxGas;
            if (__instance._svGasDepleted.Value) __instance._svGasDepleted.Value = false;
        }
    }

    [HarmonyPatch(typeof(PirateGas), nameof(PirateGas.HasGas), MethodType.Getter)]
    internal static class HasGasPatch
    {
        private static void Postfix(PirateGas __instance, ref bool __result)
        {
            if (Plugin.OthersCantHoldGas && !Plugin.IsMyGas(__instance)) __result = false;
        }
    }

    // ---- Boat status / speed -------------------------------------------------

    [HarmonyPatch(typeof(NetworkBoat), nameof(NetworkBoat.HasStatus))]
    internal static class HasStatusPatch
    {
        private static void Postfix(NetworkBoat __instance, BoatStatusFlags flag, ref bool __result)
        {
            if (Plugin.Bypass || !Plugin.AffectsBoat(__instance)) return;
            if (Plugin.Invulnerable && flag == BoatStatusFlags.Invulnerable) { __result = true; return; }
            if (Plugin.GasBoost && flag == BoatStatusFlags.GasBoost) { __result = true; return; }
            if (Plugin.NoDebuffs &&
                (flag == BoatStatusFlags.Slow || flag == BoatStatusFlags.GasNerf || flag == BoatStatusFlags.MassHeavy ||
                 flag == BoatStatusFlags.Plank || flag == BoatStatusFlags.ForcedGas || flag == BoatStatusFlags.Electric ||
                 flag == BoatStatusFlags.Knockback))
                __result = false;
        }
    }

    [HarmonyPatch(typeof(NetworkBoat), nameof(NetworkBoat.IsSpeedBoosted), MethodType.Getter)]
    internal static class SpeedBoostPatch
    {
        private static void Postfix(NetworkBoat __instance, ref bool __result)
        {
            if (Plugin.SpeedBoost && Plugin.AffectsBoat(__instance)) __result = true;
        }
    }

    [HarmonyPatch(typeof(NetworkBoat), nameof(NetworkBoat.MaxSpeed), MethodType.Getter)]
    internal static class MaxSpeedPatch
    {
        private static void Postfix(NetworkBoat __instance, ref float __result)
        {
            if (Plugin.MaxSpeedMul > 1.001f && Plugin.AffectsBoat(__instance)) __result *= Plugin.MaxSpeedMul;
        }
    }

    // ---- Seat ---------------------------------------------------------------

    [HarmonyPatch(typeof(PiratePlayer), nameof(PiratePlayer.Stun))]
    internal static class StunPatch
    {
        private static bool Prefix(PiratePlayer __instance)
        {
            if (Plugin.Bypass) return true;
            return !(Plugin.NoStun && Plugin.AffectsPlayer(__instance));
        }
    }

    [HarmonyPatch(typeof(PiratePlayer), "ApplyCrewBlocking")]
    internal static class CrewBlockPatch
    {
        private static bool Prefix(PiratePlayer __instance, float sectorLimitedTarget, ref float __result)
        {
            if (!Plugin.NoCrewBlock || !Plugin.AffectsPlayer(__instance)) return true;
            __result = sectorLimitedTarget;
            return false;
        }
    }

    [HarmonyPatch(typeof(PiratePlayer), "ApplySectorLimit")]
    internal static class SectorLimitPatch
    {
        private static bool Prefix(PiratePlayer __instance, float current, float delta, ref float excess, ref float __result)
        {
            if (!Plugin.NoSectorLimit || !Plugin.AffectsPlayer(__instance)) return true;
            excess = 0f;
            __result = Mathf.Repeat(current + delta, 360f);
            return false;
        }
    }

    // ---- Name ---------------------------------------------------------------

    [HarmonyPatch(typeof(PiratePlayer), "ResolveLocalName")]
    internal static class NamePatch
    {
        private static bool Prefix(ref string __result)
        {
            if (string.IsNullOrEmpty(Plugin.CustomName)) return true;
            string n = Plugin.CustomName;
            if (n.Length > 16) n = n.Substring(0, 16);
            __result = n;
            return false;
        }
    }
}
