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
    public string codeName
    {
        get => _codeName;
        set => _codeName = value;
    }
    private string _codeName;

    [DisplayName("Above Sea Level Curve Control Points")]
    public CurvePoints landCurve
    {
        get => _landCurve;
        set => _landCurve = value;
    }
    private CurvePoints _landCurve;

    [DisplayName("Below Sea Level Curve Control Points")]
    public CurvePoints seaCurve
    {
        get => _seaCurve;
        set => _seaCurve = value;
    }
    private CurvePoints _seaCurve;
}

class ModConfig
{
    public static ModConfig Instance { get; set; } = new ModConfig();

    public static void LoadConfig(ICoreAPI api)
    {
        try
        {
            ModConfig file;
            if ((file = api.LoadModConfig<ModConfig>("LNCurves.json")) == null)
            {
                api.StoreModConfig<ModConfig>(ModConfig.Instance, "LNCurves.json");
            }
            else
            {
                ModConfig.Instance = file;
            }
        }
        catch
        {
            api.StoreModConfig<ModConfig>(ModConfig.Instance, "LNCurves.json");
        }
    }

    [Browsable(true)]
    [DisplayName("Apply Changes")]
    public static void ApplyChanges()
    {
        // Not that autoconfiglib eats exceptions...
        
        LNCurvesMod.api.Logger.Event("Applying changes");
        
        MethodInfo lerpThresholds = typeof(LandformVariant).GetMethod("LerpThresholds", BindingFlags.NonPublic | BindingFlags.Instance);
        
        for (int landformIndex = 0; landformIndex < LNCurvesMod._savedLandforms.Count; landformIndex++)
        {
            var landform = LNCurvesMod._savedLandforms[landformIndex];
            landform.OriginalYKeyPositions.CopyTo(landform.Landform.TerrainYKeyPositions, 0);
            
            LNCurvesMod.ApplyCurveToLandform(landform.Landform);
            lerpThresholds.Invoke(landform.Landform,
                new object[] { ((ICoreServerAPI)(LNCurvesMod.api)).WorldManager.MapSizeY });
        }

        // Force recopying of lerped thresholds in GenTerra
        try
        {
            GenTerra genTerraMod = LNCurvesMod.api.ModLoader.GetModSystem<GenTerra>();
            FieldInfo genTerraLandforms = typeof(GenTerra).GetField("landforms", BindingFlags.NonPublic | BindingFlags.Instance);
            genTerraLandforms.SetValue(genTerraMod, null);
        }
        catch (Exception e)
        {
            LNCurvesMod.api.Logger.Error("Failed to reset GenTerra lerped thresholds: " + e.Message);
        }
    }

    [DisplayName("Default Above Sea Level Curve Control Points")]
    public CurvePoints DefaultLandCurveControlPoints { get; set; } = new CurvePoints(0, 0, 1, 1);
    [DisplayName("Default Below Sea Level Curve Control Points")]
    public CurvePoints DefaultSeaCurveControlPoints { get; set; } = new CurvePoints(0, 0, 1, 1);

    public Dictionary<string, LandformOverride> LandformOverrides { get; set; } = new Dictionary<string, LandformOverride>();
}