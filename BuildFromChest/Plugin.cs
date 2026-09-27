using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;

namespace BuildFromChest
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.frizzqol.buildfromchest";
        public const string PluginName = "FrizzQOL Build From Chest";
        public const string PluginVersion = "0.1.0";

        internal static Plugin Instance { get; private set; }

        internal static ConfigEntry<float> ChestRadius;

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            ChestRadius = Config.Bind(
                "General",
                "ChestRadius",
                10f,
                "Meters from the player to the nearest chest. 0 or less disables the pull.");
            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll();
                Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to patch placement: {ex.Message}");
            }
        }

        internal static float CurrentRadius()
        {
            return ChestRadius != null ? ChestRadius.Value : 10f;
        }

        internal static void LogWarning(string message)
        {
            if (Instance != null)
            {
                Instance.Logger.LogWarning(message);
            }
        }
    }
}
