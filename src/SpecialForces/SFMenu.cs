using System;
using RoR2;
using RoR2.UI;
using RoR2.UI.MainMenu;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpecialForces
{
    // Main menu > Alternate game modes gets two buttons: "Special Forces" (solo) and "Special Forces: Host Lobby"
    // (sets the game mode, then opens the normal multiplayer screen to host for up to 12 players).
    // Built by copying one of the screen's own buttons, so they look like Simulacrum's and Eclipse's.
    internal static class SFMenu
    {
        private static bool pendingMultiplayer;

        public static void Init()
        {
            On.RoR2.UI.MainMenu.MainMenuController.Start += (orig, self) =>
            {
                orig(self);
                try
                {
                    // Coming back to the title: put the normal game mode back, unless we just set ours for hosting.
                    if (!pendingMultiplayer) Submit("gamemode ClassicRun");
                    pendingMultiplayer = false;
                    AddButtons(self);
                }
                catch (Exception e)
                {
                    SpecialForcesPlugin.Log.LogError("Could not add the Special Forces menu buttons: " + e);
                }
            };
        }

        private static void Submit(string cmd) => RoR2.Console.instance.SubmitCmd((NetworkUser)null, cmd, false);

        private static void AddButtons(MainMenuController menu)
        {
            BaseMainMenuScreen screen = menu.extraGameModeMenuScreen;
            if (!screen)
            {
                SpecialForcesPlugin.Log.LogWarning("Alternate game modes screen not found; start Special Forces with the console: gamemode SpecialForcesRun");
                return;
            }
            if (screen.transform.Find("SpecialForcesSolo")) return;
            HGButton template = null;
            foreach (var b in screen.GetComponentsInChildren<HGButton>(true))
            {
                if (!b.GetComponentInChildren<LanguageTextMeshController>(true)) continue;
                template = b;
                // Prefer the Simulacrum button (same kind of entry).
                var label = b.GetComponentInChildren<LanguageTextMeshController>(true);
                if (label.token != null && label.token.ToUpperInvariant().Contains("INFINITETOWER")) break;
            }
            if (!template)
            {
                SpecialForcesPlugin.Log.LogWarning("No button to copy on the alternate game modes screen.");
                return;
            }

            MakeButton(template, "SpecialForcesSolo", Tokens.Prefix + "MENU_SOLO", Tokens.Prefix + "GAMEMODE_DESCRIPTION", () =>
            {
                Submit($"sv_maxplayers {SFConfig.MaxPlayers.Value}");
                Submit("transition_command \"gamemode " + SFGameMode.RunName + "; host 0;\"");
            });
            MakeButton(template, "SpecialForcesHost", Tokens.Prefix + "MENU_HOST", Tokens.Prefix + "MENU_HOST_DESCRIPTION", () =>
            {
                pendingMultiplayer = true;
                Submit("gamemode " + SFGameMode.RunName);
                Submit($"sv_maxplayers {SFConfig.MaxPlayers.Value}");
                if (menu.multiplayerMenuScreen) menu.SetDesiredMenuScreen(menu.multiplayerMenuScreen);
            });
            SpecialForcesPlugin.Log.LogInfo("Special Forces buttons added to Alternate game modes.");
        }

        private static void MakeButton(HGButton template, string name, string token, string hoverToken, Action onClick)
        {
            GameObject copy = Object.Instantiate(template.gameObject, template.transform.parent);
            copy.name = name;
            copy.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);
            var button = copy.GetComponent<HGButton>();
            button.onClick = new Button.ButtonClickedEvent();    // drop the copied persistent console command
            button.onClick.AddListener(() => onClick());
            button.hoverToken = hoverToken;
            foreach (var label in copy.GetComponentsInChildren<LanguageTextMeshController>(true)) label.token = token;
            copy.SetActive(true);
        }
    }
}
