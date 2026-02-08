using HarmonyLib;
using Vintagestory.API.Common;

namespace HoneyBatchSqueeze;

public class HoneyBatchSqueeze : ModSystem {
    public static ILogger Logger { get; private set; }
    public static ICoreAPI Api { get; private set; }

    private Harmony harmony;

    public override void Start(ICoreAPI api) {
        base.StartPre(api);
        Logger = Mod.Logger;
        Api = api;

        harmony = new Harmony(Mod.Info.ModID);
        harmony.PatchAll();
    }

    public override void Dispose() {
        harmony?.UnpatchAll(Mod.Info.ModID);
        harmony = null;
        Logger = null;
        Api = null;
        base.Dispose();
    }
}
