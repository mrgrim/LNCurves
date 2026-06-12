using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.ServerMods;
using Vintagestory.ServerMods.NoObf;

namespace LNCurves;

public class CurvePoints
{
    public CurvePoints() : this(0, 0, 1, 1) { }

    public CurvePoints(double x1, double y1, double x2, double y2)
    {
        p1x = x1;
        p1y = y1;
        p2x = x2;
        p2y = y2;
    }

    [DisplayName("Control Point 1 X Coordinate")]
    [Range(0.0, 1.0)]
    [DefaultValue(0.0)]
    public double p1x
    {
        get { return _p1x; }
        set { _p1x = Math.Clamp(value, 0.0, 1.0); }
    }
    private double _p1x;
    
    [DisplayName("Control Point 1 Y Coordinate")]
    [Range(-0.6, 1.6)]
    [DefaultValue(0.0)]
    public double p1y
    {
        get { return _p1y; }
        set { _p1y = Math.Clamp(value, -0.6, 1.6); }
    }
    private double _p1y;

    [DisplayName("Control Point 2 X Coordinate")]
    [Range(0.0, 1.0)]
    [DefaultValue(1.0)]
    public double p2x
    {
        get { return _p2x; }
        set { _p2x = Math.Clamp(value, 0.0, 1.0); }
    }
    private double _p2x;
    
    [DisplayName("Control Point 2 Y Coordinate")]
    [Range(-0.6, 1.6)]
    [DefaultValue(1.0)]
    public double p2y
    {
        get { return _p2y; }
        set { _p2y = Math.Clamp(value, -0.6, 1.6); }
    }
    private double _p2y;
}

public class LandformOverride
{
    public LandformOverride() : this("landform name", new CurvePoints(), new CurvePoints()) { }
    
    public LandformOverride(string codeNameIn, CurvePoints landCurveIn, CurvePoints seaCurveIn)
    {
        codeName = codeNameIn;
        landCurve = landCurveIn;
        seaCurve = seaCurveIn;
    }

    [DisplayName("Landform Code Name")]
    [Description("The name of the \"code\" property for the landform in worldgen\\landforms.json")]
    public required string codeName { get; set; }

    [DisplayName("Above Sea Level Curve Control Points")]
    public CurvePoints landCurve { get; set; }

    [DisplayName("Below Sea Level Curve Control Points")]
    public CurvePoints seaCurve { get; set; }
}

class ModConfig
{
    public const int CurrentSchemaVersion = 2;
    public static ModConfig Instance { get; set; } = new ();

    public static void LoadConfig(ICoreAPI api)
    {
        try
        {
            ModConfig file;
            if ((file = api.LoadModConfig<ModConfig>("LNCurves.json")) == null)
            {
                Instance.Version = CurrentSchemaVersion;
                api.StoreModConfig(Instance, "LNCurves.json");
            }
            else
            {
                Instance = file;
                if (Migrate(api)) api.StoreModConfig(Instance, "LNCurves.json");
            }
        }
        catch
        {
            api.StoreModConfig(Instance, "LNCurves.json");
        }
    }

    private static bool Migrate(ICoreAPI? api)
    {
        var changed = false;
        var from = Instance.Version;

        if (from < 2)
        {
            Instance.SmoothingRadius = 0;
            api?.Logger.Event("LNCurves: migrated config from v" + from + " to v2 (SmoothingRadius set to 0 for compatibility " +
                              "with existing worlds; set it manually to enable slope smoothing to help with floating dirt and air " +
                              "gap artifacts. Recommend a value of 2 for 256 height worlds, increase for taller worlds).");
            changed = true;
        }

        if (Instance.Version == CurrentSchemaVersion) return changed;
        
        Instance.Version = CurrentSchemaVersion;
        return true;
    }
    
    [Browsable(true)]
    [DisplayName("Apply Changes")]
    public static void ApplyChanges()
    {
        // Note that autoconfiglib eats exceptions...

        if (LNCurvesMod.api is null)
            return; // Shouldn't happen, and our logging engine to report the error is in there. Also see above re exceptions.
        
        LNCurvesMod.api?.Logger.Event("Applying changes");
        
        var lerpThresholds = typeof(LandformVariant).GetMethod("LerpThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
        var genTerraLandforms = typeof(GenTerra).GetField("landforms", BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (lerpThresholds is null || genTerraLandforms is null)
        {
            LNCurvesMod.api?.Logger.Error("Failed obtaining reflection objects.");
            return;
        }

        foreach (var landform in LNCurvesMod._savedLandforms)
        {
            landform.OriginalYKeyPositions.CopyTo(landform.Landform.TerrainYKeyPositions, 0);
            
            LNCurvesMod.ApplyCurveToLandform(landform.Landform);
            lerpThresholds.Invoke(landform.Landform,
                [((ICoreServerAPI?)LNCurvesMod.api)?.WorldManager.MapSizeY]);

            LNCurvesMod.SmoothLandformThresholds(landform.Landform);
        }

        // Force recopying of lerped thresholds in GenTerra
        try
        {
            var genTerraMod = LNCurvesMod.api?.ModLoader.GetModSystem<GenTerra>();
            genTerraLandforms.SetValue(genTerraMod, null);
        }
        catch (Exception e)
        {
            LNCurvesMod.api?.Logger.Error("Failed to reset GenTerra lerped thresholds: " + e.Message);
        }
    }

    [DisplayName("Default Above Sea Level Curve Control Points")]
    public CurvePoints DefaultLandCurveControlPoints { get; set; } = new CurvePoints(0, 0, 1, 1);
    [DisplayName("Default Below Sea Level Curve Control Points")]
    public CurvePoints DefaultSeaCurveControlPoints { get; set; } = new CurvePoints(0, 0, 1, 1);

    [DisplayName("Threshold Smoothing Radius")]
    [Description(
        "Radius (in Y-blocks) of the symmetric box blur applied to each landform's per-block-Y threshold profile after the bezier remap. " +
        "Smooths out steep threshold slopes that would otherwise cause occasional floating-dirt artifacts. " +
        "0 disables smoothing. Calibrated for a world height of 256; raise proportionally for taller worlds. Typical range 0-8.")]
    [Range(0, 16)]
    [DefaultValue(2)]
    public int SmoothingRadius
    {
        get;
        set => field = Math.Clamp(value, 0, 16);
    } = 2;

    [Browsable(false)]
    public int Version { get; set; }

    public Dictionary<string, LandformOverride> LandformOverrides { get; set; } = new ();
}
