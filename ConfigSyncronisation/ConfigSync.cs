using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace BetterTames.ConfigSynchronization // <-- Korrigierter Namespace
{
    public class ConfigSync
    {
        public TamesConfig Tames { get; private set; }

        public ConfigSync(BetterTamesPlugin pluginInstance)
        {
            Tames = new TamesConfig(pluginInstance.Config);
        }

        public class TamesConfig
        {
            // Definiere die Sektionsnamen als Konstanten für bessere Wartbarkeit
            private const string SectionGeneral = "1. General";
            private const string SectionMakeCommandable = "2. MakeCommandable";
            private const string SectionTeleportFollow = "3. TeleportFollow";
            private const string SectionPetProtection = "4. PetProtection";

            // --- Properties für die Konfigurationseinträge ---
            public ConfigEntry<bool> ServerConfigLocked { get; private set; }
            public ConfigEntry<bool> DebugMakeCommandable { get; private set; }
            public ConfigEntry<int> MaxFollowingPets { get; private set; }
            public ConfigEntry<bool> TeleportFollowEnabled { get; private set; }
            public ConfigEntry<int> TeleportOnDistanceMaxRange { get; private set; }
            public ConfigEntry<bool> DebugTeleportFollow { get; private set; }
            public ConfigEntry<bool> PetProtectionEnabled { get; private set; }
            public ConfigEntry<int> PetProtectionStunDuration { get; private set; }
            public ConfigEntry<int> PetProtectionHealPercentage { get; private set; }
            public ConfigEntry<string> PetProtectionExceptionPrefabs { get; private set; }
            public ConfigEntry<bool> DebugPetProtection { get; private set; }

            public TamesConfig(ConfigFile cfg)
            {
                // Entferne vorherige Duplikate in der cfg-Datei, bevor neue Bind-Calls erfolgen.
                // Das stellt sicher, dass vorhandene Werte erhalten bleiben und nicht mehrfach in der Datei auftauchen.
                TryRemoveDuplicateConfigEntriesFile();

                // --- General ServerSync ---
                ServerConfigLocked = BindAndSync(cfg, SectionGeneral, "Lock Configuration", true, "If true on the server, this configuration file will be locked and synced to clients.");
                BetterTamesPlugin._configSync.AddLockingConfigEntry(ServerConfigLocked);

                // --- MakeCommandable ---
                MaxFollowingPets = BindAndSync(cfg, SectionMakeCommandable, "Max Following Pets", 5, new ConfigDescription("Maximum number of pets that can follow a player at the same time. -1 to disable.", new AcceptableValueRange<int>(-1, 20)));

                DebugMakeCommandable = cfg.Bind(SectionMakeCommandable, "Debug Logging", false, "Enables debug logging for this feature.");

                // --- TeleportFollow ---
                TeleportFollowEnabled = BindAndSync(cfg, SectionTeleportFollow, "Enable", true, "Enables pets to teleport to the player if they get too far or the player uses a portal/teleports.");
                TeleportOnDistanceMaxRange = BindAndSync(cfg, SectionTeleportFollow, "Max Distance For AutoTeleport", 64, new ConfigDescription("Maximum distance a pet can be from its owner before it attempts to teleport (if not in combat).", new AcceptableValueRange<int>(20, 64)));
                DebugTeleportFollow = cfg.Bind(SectionTeleportFollow, "Debug Logging", false, "Enables debug logging for teleport features.");

                // --- PetProtection ---
                PetProtectionEnabled = BindAndSync(cfg, SectionPetProtection, "Enable", true, "Prevents tamed creatures from dying by knocking them out instead. They recover after a set time.");
                PetProtectionStunDuration = BindAndSync(cfg, SectionPetProtection, "Stun Duration", 10, new ConfigDescription("How long the pet stays stunned/downed (seconds).", new AcceptableValueRange<int>(5, 300)));
                PetProtectionHealPercentage = BindAndSync(cfg, SectionPetProtection, "Heal After Stun Pct", 25, new ConfigDescription("Percentage of max HP the pet recovers after being downed. (0 = 1HP).", new AcceptableValueRange<int>(0, 100)));
                PetProtectionExceptionPrefabs = BindAndSync(cfg, SectionPetProtection, "Exception Prefabs", "SummonedGolem_TW,SummonedSurtling_TW,SummonedSeeker_TW,SummonedImp_TW,Troll_Summoned,Charred_Twitcher_Summoned,Skeleton_Friendly,JC_Skeleton,enemy_skeleton_summoned", "A comma-separated list of prefab names that should NOT receive pet protection.");
                DebugPetProtection = cfg.Bind(SectionPetProtection, "Debug Logging", false, "Enables debug logging for this feature.");

            }

            #region Helper Methods
            /// <summary>
            /// Erstellt eine Konfigurationseinstellung und registriert sie sofort bei ServerSync.
            /// </summary>
            private ConfigEntry<T> BindAndSync<T>(ConfigFile cfg, string section, string key, T defaultValue, string description)
            {
                ConfigEntry<T> entry = cfg.Bind(section, key, defaultValue, description);
                BetterTamesPlugin._configSync.AddConfigEntry(entry);
                return entry;
            }

            /// <summary>
            /// Overload für Konfigurationseinstellungen mit erweiterter Beschreibung (z.B. Wertebereich).
            /// </summary>
            private ConfigEntry<T> BindAndSync<T>(ConfigFile cfg, string section, string key, T defaultValue, ConfigDescription description)
            {
                ConfigEntry<T> entry = cfg.Bind(section, key, defaultValue, description);
                BetterTamesPlugin._configSync.AddConfigEntry(entry);
                return entry;
            }

            /// <summary>
            /// Liest die Plugin-cfg-Datei und entfernt doppelte Key-Definitionen innerhalb derselben Sektion.
            /// Es wird die erste gefundene Definition beibehalten (existierende Werte bleiben erhalten),
            /// spätere Duplikate (z. B. durch frühere fehlerhafte Updates) werden entfernt.
            /// Diese Methode ist intentionally tolerant und belässt Kommentare und leere Zeilen.
            /// </summary>
            private void TryRemoveDuplicateConfigEntriesFile()
            {
                try
                {
                    string cfgPath = Path.Combine(Paths.ConfigPath, $"{BetterTamesPlugin.PluginId}.cfg");
                    if (!File.Exists(cfgPath)) return;

                    var lines = File.ReadAllLines(cfgPath);
                    var output = new List<string>(lines.Length);

                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    string currentSection = "";

                    foreach (var rawLine in lines)
                    {

                        string line = rawLine;
                        string trimmed = line.Trim();

                        if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                        {
                            currentSection = trimmed.Substring(1, trimmed.Length - 2).Trim();
                            output.Add(line);
                            continue;
                        }


                        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("#"))
                        {
                            output.Add(line);
                            continue;
                        }


                        int equalsIndex = line.IndexOf('=');
                        if (equalsIndex > 0)
                        {
                            string keyPart = line.Substring(0, equalsIndex).Trim();
                            if (string.IsNullOrEmpty(keyPart))
                            {

                                output.Add(line);
                                continue;
                            }

                            string identifier = $"{currentSection}|{keyPart}";
                            if (seen.Contains(identifier))
                            {

                                continue;
                            }
                            else
                            {
                                seen.Add(identifier);
                                output.Add(line);
                                continue;
                            }
                        }

                        output.Add(line);
                    }


                    bool contentChanged = output.Count != lines.Length;
                    if (!contentChanged)
                    {
                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (lines[i] != output[i])
                            {
                                contentChanged = true;
                                break;
                            }
                        }
                    }

                    if (contentChanged)
                    {
                        File.WriteAllLines(cfgPath, output);
                    }
                }
                catch (Exception ex)
                {

                }
            }
            #endregion
        }
    }
}