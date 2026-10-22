using System.Collections.Generic;
using R2API;
using RoR2;

namespace SpecialForces
{
    internal static class Tokens
    {
        public const string Prefix = "SPECIALFORCES_";
        private static readonly Dictionary<string, string> text = new Dictionary<string, string>();

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            string token = key.StartsWith(Prefix) ? key : Prefix + key;
            string s = Language.GetString(token);
            if (s != token) return s;
            return text.TryGetValue(token, out var t) ? t : key;
        }

        private static void Add(string key, string value)
        {
            text[Prefix + key] = value;
            LanguageAPI.Add(Prefix + key, value);
        }

        public static void Init()
        {
            Add("GAMEMODE_NAME", "Special Forces");
            Add("GAMEMODE_DESCRIPTION", "Up to 12 operatives drop into Stormreach Valley to defend the control tower holding back a living hurricane. " +
                                       "Hold the outpost against ever-growing waves, build turrets, walls and supply depots, destroy Storm Nests for items, " +
                                       "and push north to kill the Hurricane. The run ends if the tower falls.");
            Add("MENU_SOLO", "Special Forces");
            Add("MENU_HOST", "Special Forces: Host Lobby");
            Add("MENU_HOST_DESCRIPTION", "Host a Special Forces lobby for up to 12 players (opens the multiplayer screen with Special Forces selected).");
            Add("MAP_NAME", "Stormreach Valley");
            Add("MAP_SUBTITLE", "Eye of the Storm");
            Add("VICTORY", "The storm breaks. Stormreach Valley is quiet.");

            Add("SFCONTROLTOWER_NAME", "Control Tower");
            Add("SFSTORMNEST_NAME", "Storm Nest");
            Add("SFBARRIERWALL_NAME", "Barrier Wall");
            Add("SFSUPPLYDEPOT_NAME", "Supply Depot");
            Add("HURRICANE_NAME", "The Hurricane");
            Add("HURRICANE_SUBTITLE", "Eye of the Storm");
            Add("HURRICANECLAW_NAME", "Thunder Claw");
            Add("HURRICANEBARRAGE_NAME", "Lightning Barrage");
            Add("HURRICANECYCLONE_NAME", "Cyclone");
            Add("HURRICANEROAR_NAME", "Storm Roar");

            Add("RADIO_1", "<color=#ffd27a>COMMAND:</color> Team, you must defend the control tower.");
            Add("RADIO_2", "<color=#ffd27a>COMMAND:</color> It is the only thing keeping this rainstorm at bay.");
            Add("RADIO_3", "<color=#ffd27a>COMMAND:</color> Deploy and defend it, push out to find the cause of this, and put that scum down for good.");

            Add("WAVE_INCOMING", "<color=#7fd4ff>Wave {0} is coming out of the storm.</color>");
            Add("NEST_DESTROYED", "<color=#9fe39f>A tier {0} Storm Nest has been destroyed!</color>");
            Add("LAIR_CLEARED", "<color=#ffd27a>A storm lair has been cleared!</color>");
            Add("REFILL", "<color=#c8c8c8>Supply drops have landed across the valley.</color>");
            Add("RESPAWNED", "<color=#9fe39f>{0} has redeployed at the outpost.</color>");
            Add("HURRICANE_AWAKE", "<color=#7fd4ff>The storm takes shape... The Hurricane has woken!</color>");
            Add("HURRICANE_PHASE", "<color=#7fd4ff>The Hurricane calls a squall!</color>");
            Add("HURRICANE_DOWN", "<color=#ffd27a>The Hurricane has fallen!</color>");

            Add("HUD_TOWER", "CONTROL TOWER");
            Add("HUD_TOWER_DOWN", "CONTROL TOWER LOST");
            Add("HUD_FIRST_WAVE", "First wave in {0}");
            Add("HUD_WAVE", "Wave {0}  -  next in {1}");
            Add("HUD_NESTS", "Storm Nests {0}/{1}");
            Add("HUD_BUILD_TITLE", "<color=#ffd27a>BUILD</color>");
            Add("HUD_BUILD_KEYS", "[1-5] choose   [{0}] place   [{1}] rotate   [{2}] close");
            Add("HUD_BUILD_HINT", "<color=#9a9a9a>[{0}] Build</color>");

            Add("BUILD_TURRET", "Auto Turret");
            Add("BUILD_WALL", "Barrier Wall");
            Add("BUILD_DEPOT", "Supply Depot");
            Add("BUILD_DEFENDERS", "Defender Squad");
            Add("BUILD_ESCORT", "Escort Squad");
            Add("BUILD_NO_TOWER", "The control tower is gone.");
            Add("BUILD_LIMIT", "You've reached the limit for that.");
            Add("BUILD_MONEY", "Not enough money.");
            Add("BUILD_AIM", "Aim at the ground.");
            Add("BUILD_RANGE", "Too far from the control tower.");
            Add("BUILD_SLOPE", "Too steep here.");
            Add("BUILD_BLOCKED", "Something is in the way.");
            Add("BUILD_FAILED", "Couldn't build that here.");
            Add("BUILD_OK_TURRET", "Auto Turret deployed.");
            Add("BUILD_OK_WALL", "Barrier Wall built.");
            Add("BUILD_OK_DEPOT", "Supply Depot built.");
            Add("BUILD_OK_DEFENDERS", "Defender Squad is guarding the tower.");
            Add("BUILD_OK_ESCORT", "Escort Squad is with you.");
        }
    }
}
