
namespace LNCurves;

public class CubicBezierEasing
{
    private readonly double[] _q0 = { 0.0, 0.0 };
    private double[] _q1 = new double[2];
    private double[] _q2 = new double[2];
    private readonly double[] _q3 = { 1.0, 1.0 };

    public CubicBezierEasing(CurvePoints curvePoints) : this(curvePoints.p1x, curvePoints.p1y, curvePoints.p2x,
        curvePoints.p2y)
    {
    }

    public CubicBezierEasing(double q1x, double q1y, double q2x, double q2y)
    {
        _q1[0] = Math.Clamp(q1x, 0.0, 1.0);
        _q1[1] = q1y;
        _q2[0] = Math.Clamp(q2x, 0.0, 1.0);
        _q2[1] = q2y;
    }

    public void SetControlPoints(double q1x, double q1y, double q2x, double q2y)
    {
        _q1[0] = Math.Clamp(q1x, 0.0, 1.0);
        _q1[1] = q1y;
        _q2[0] = Math.Clamp(q2x, 0.0, 1.0);
        _q2[1] = q2y;
    }

    private double crt(double x)
    {
        return x < 0 ? -Math.Pow(-x, 1.0 / 3.0) : Math.Pow(x, 1.0 / 3.0);
    }
    
    private double findRoot(double x)
    {
        x = Math.Clamp(x, 0.0, 1.0);

        var pa = _q0[0];
        var pb = _q1[0];
        var pc = _q2[0];
        var pd = _q3[0];

        var pa3 = 3.0 * pa;
        var pb3 = 3.0 * pb;
        var pc3 = 3.0 * pc;

        var a = -pa + pb3 - pc3 + pd;
        var b = pa3 - 2.0 * pb3 + pc3;
        var c = -pa3 + pb3; 
        var d = pa - x;

        double q, r1, r2;

        if (a == 0.0)
        {
            // this is not a cubic curve.
            if (b == 0.0)
            {
                // in fact, this is not a quadratic curve either.
                if (c == 0.0)
                {
                    // in fact in fact, there are no solutions.
                    // This shouldn't happen with the constraints imposed by clamping!
                    throw new Exception("CubicBezierEasing.findRoot() no solution found.");
                }
                
                // linear solution:
                return -d / c;
            }
            
            // quadratic solution:
            q = Math.Sqrt(c * c - 4.0 * b * d); 
            var b2 = 2.0 * b;

            r1 = (q - c) / b2;
            r2 = (-c - q) / b2;

            return (r1 is >= 0.0 and <= 1.0) ? r1 : r2;
        }

        b /= a;
        c /= a;
        d /= a;

        var b3 = b / 3.0;
        var p = (3.0 * c - b * b) / 3.0;
        var p3 = p / 3.0;
        q = (2.0 * b * b * b - 9.0 * b * c + 27.0 * d) / 27.0;
        var q2 = q / 2.0;
        var discriminant = q2 * q2 + p3 * p3 * p3; 
        double u1, v1;

        if (discriminant < 0)
        {
            var mp3 = -p / 3.0;
            var r = Math.Sqrt(mp3 * mp3 * mp3);
            var t = -q / (2.0 * r);
            var phi = Math.Acos(t < -1 ? -1 : t > 1 ? 1 : t);
            var t1 = 2.0 * crt(r);

            r1 = t1 * Math.Cos(phi / 3.0) - b3;
            if (r1 is >= 0.0 and <= 1.0) return r1;
            
            r2 = t1 * Math.Cos((phi + 2.0 * Math.PI) / 3) - b3;
            if (r2 is >= 0.0 and <= 1.0) return r2;
            
            return t1 * Math.Cos((phi + 4.0 * Math.PI) / 3) - b3;
        }
        
        if (discriminant == 0)
        {
            u1 = q2 < 0 ? crt(-q2) : -crt(q2);

            r1 = 2.0 * u1 - b3;
            if (r1 is >= 0.0 and <= 1.0) return r1;
            
            return -u1 - b3;
        }
        
        var sd = Math.Sqrt(discriminant);
        u1 = crt(-q2 + sd);
        v1 = crt(q2 + sd);

        return u1 - v1 - b3;
    }

    public double GetYForT(double t)
    {
        return Math.Pow(1.0 - t, 3) * _q0[1] + 3.0 * Math.Pow(1.0 - t, 2) * t * _q1[1] +
               3.0 * (1.0 - t) * Math.Pow(t, 2) * _q2[1] + Math.Pow(t, 3) * _q3[1];
    }

    public double GetTforX(double x)
    {
        return findRoot(x);
    }

    public double GetYforX(double x)
    {
        return GetYForT(GetTforX(x));
    }
}