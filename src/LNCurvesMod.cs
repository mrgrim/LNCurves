using HarmonyLib;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods.NoObf;

namespace LNCurves;

public class SavedLandform
{
    public LandformVariant Landform;
    public float[] OriginalYKeyPositions;

    public SavedLandform(LandformVariant landform, float[] originalYKeyPositions)
    {
        Landform = landform;
        OriginalYKeyPositions = originalYKeyPositions;
    }
}

[HarmonyPatch]
public class LNCurvesMod : ModSystem
{
    public static ICoreAPI? api;
    public required Harmony harmony;
    private static double SeaLevelRatio = 22.0 / 51.0;
    public static List<SavedLandform> _savedLandforms = new List<SavedLandform>();

    public override void StartPre(ICoreAPI apiIn)
    {
        api = apiIn;
        ModConfig.LoadConfig(api);
    }

    public override void Start(ICoreAPI apiIn)
    {
        api = apiIn;
        
        if (api.Side != EnumAppSide.Server) return;
        
        harmony = new Harmony(Mod.Info.ModID);
        api.Logger.Event("Applying LNCurvesMod patches.");
        harmony.PatchAll(); // Applies all harmony patches
    }
    
    public static void ApplyCurveToLandform(LandformVariant landform)
    {
        CubicBezierEasing land_curve;
        CubicBezierEasing sea_curve;
            
        if (ModConfig.Instance.LandformOverrides.ContainsKey(landform.Code.Path))
        {
            land_curve = new CubicBezierEasing(ModConfig.Instance.LandformOverrides[landform.Code.Path].landCurve);
            sea_curve = new CubicBezierEasing(ModConfig.Instance.LandformOverrides[landform.Code.Path].seaCurve);
        }
        else
        {
            land_curve = new CubicBezierEasing(ModConfig.Instance.DefaultLandCurveControlPoints);
            sea_curve = new CubicBezierEasing(ModConfig.Instance.DefaultSeaCurveControlPoints);
        }

        api?.Logger.Event("Applying curves to landform " + landform.Code.Path+ ".");
        api?.Logger.Event("Starting Y Key Positions: [" + String.Join(", ", landform.TerrainYKeyPositions) + "].");

        for (int pos_index = 0; pos_index < landform.TerrainYKeyPositions.Length; pos_index++)
        {
            double pos = landform.TerrainYKeyPositions[pos_index];

            if (pos >= SeaLevelRatio)
            {
                double adjPos = (pos - SeaLevelRatio) / (1.0d - SeaLevelRatio);
                adjPos = land_curve.GetYforX(adjPos);

                landform.TerrainYKeyPositions[pos_index] = (float)Math.Clamp(adjPos * (1.0d - SeaLevelRatio) + SeaLevelRatio, SeaLevelRatio, 1);
            }
            else
            {
                double adjPos = pos / SeaLevelRatio;
                adjPos = sea_curve.GetYforX(adjPos);

                landform.TerrainYKeyPositions[pos_index] = (float)Math.Clamp(adjPos * SeaLevelRatio, 0, SeaLevelRatio);
            }
        }
        
        api?.Logger.Event("Adjusted Y Key Positions: [" + String.Join(", ", landform.TerrainYKeyPositions) + "].");
    }
    
    // Applies a symmetric box-blur low-pass filter to the landform's per-block-Y threshold
    // profile in place. This softens locally-steep segments produced by the bezier remap of
    // TerrainYKeyPositions, which would otherwise shrink GenTerra's noise-decided band enough
    // to occasionally produce isolated floating solid blocks (and the dirt-air-stone artifact
    // the soil-deposition pass then builds on top of them).
    //
    // Kernel radius is taken from ModConfig.SmoothingRadius and is measured in Y-blocks
    // (absolute, not fraction-of-mapsizeY), since the noise oscillation the kernel must mask
    // is anchored to absolute Y as well. Edges are clamp-padded.
    public static void SmoothLandformThresholds(LandformVariant landform)
    {
        var thresholds = landform.TerrainYThresholds;
        if (thresholds == null || thresholds.Length == 0) return;

        var radius = ModConfig.Instance.SmoothingRadius;
        if (radius <= 0) return;

        var n = thresholds.Length;
        var window = 2 * radius + 1;

        // Running-sum box blur with clamp padding. One scratch alloc per landform variant
        var result = new float[n];

        // Seed the running sum with the leftmost window (positions -radius..+radius), clamped.
        var sum = 0.0;
        for (var k = -radius; k <= radius; k++)
        {
            var idx = k < 0 ? 0 : (k >= n ? n - 1 : k);
            sum += thresholds[idx];
        }
        result[0] = (float)(sum / window);

        for (var i = 1; i < n; i++)
        {
            var outgoing = i - radius - 1;
            var incoming = i + radius;
            var outIdx = outgoing < 0 ? 0 : (outgoing >= n ? n - 1 : outgoing);
            var inIdx = incoming < 0 ? 0 : (incoming >= n ? n - 1 : incoming);
            
            sum += thresholds[inIdx] - thresholds[outIdx];
            result[i] = (float)(sum / window);
        }

        // Write back in place so GenTerra's cached references (which point at this same
        // array) see the smoothed values without needing cache invalidation.
        Array.Copy(result, thresholds, n);
    }
        
    [HarmonyPatch(typeof(LandformVariant), "Init")]
    class LandformVariantInitPatch
    {
        static void Prefix(LandformVariant __instance, IWorldManagerAPI api, int index)
        {
            // We need to copy positions to mutations prior to modification to avoid applying the curve twice to mutations
            if (__instance.Mutations != null)
            {
                for (int mut_index = 0; mut_index < __instance.Mutations.Length; mut_index++)
                {
                    if (__instance.Mutations[mut_index].TerrainYKeyPositions == null)
                        __instance.Mutations[mut_index].TerrainYKeyPositions = __instance.TerrainYKeyPositions.Clone() as float[];
                }
            }
            
            _savedLandforms.Add(new SavedLandform(__instance, __instance.TerrainYKeyPositions.Clone() as float[] ?? []));
            
            ApplyCurveToLandform(__instance);
        }
        
        static void Postfix(LandformVariant __instance)
        {
            // Runs after vanilla Init -> LerpThresholds has populated __instance.TerrainYThresholds.
            // Fires once per parent variant and once per mutation (NoiseLandforms.LoadLandforms
            // calls Init on each mutation independently), so every variant's distinct threshold
            // array gets smoothed without a special mutation loop.
            SmoothLandformThresholds(__instance);
        }
    }
}