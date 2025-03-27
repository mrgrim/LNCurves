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
    public static ICoreAPI api;
    public Harmony harmony;
    private static double SeaLevelRatio = 22.0 / 51.0;
    public static List<SavedLandform> _savedLandforms = new List<SavedLandform>();

    public override void StartPre(ICoreAPI api)
    {
        LNCurvesMod.api = api;
        ModConfig.LoadConfig(api);
    }

    public override void Start(ICoreAPI api)
    {
        LNCurvesMod.api = api;
        
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

        api.Logger.Event("Applying curves to landform " + landform.Code.Path+ ".");
        api.Logger.Event("Starting Y Key Positions: [" + String.Join(", ", landform.TerrainYKeyPositions) + "].");

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
        
        api.Logger.Event("Adjusted Y Key Positions: [" + String.Join(", ", landform.TerrainYKeyPositions) + "].");
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
            
            _savedLandforms.Add(new SavedLandform(__instance, __instance.TerrainYKeyPositions.Clone() as float[]));
            
            ApplyCurveToLandform(__instance);
        }
    }
}