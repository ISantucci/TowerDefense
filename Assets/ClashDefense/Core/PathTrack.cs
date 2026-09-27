using System;

namespace ClashDefense.Core
{
    /// <summary>El recorrido del nivel como polilínea, parametrizado por distancia recorrida (GDS-001.0).</summary>
    public sealed class PathTrack
    {
        readonly Vec2[] points;
        readonly float[] cumulative; // distancia acumulada hasta cada punto

        public float Length { get; }
        public int PointCount => points.Length;
        public Vec2 this[int i] => points[i];
        public Vec2 Start => points[0];
        public Vec2 End => points[points.Length - 1];

        public PathTrack(Vec2[] pts)
        {
            if (pts == null || pts.Length < 2) throw new ArgumentException("el recorrido necesita al menos 2 puntos");
            points = (Vec2[])pts.Clone();
            cumulative = new float[points.Length];
            for (int i = 1; i < points.Length; i++)
                cumulative[i] = cumulative[i - 1] + Vec2.Distance(points[i - 1], points[i]);
            Length = cumulative[points.Length - 1];
        }

        /// <summary>Posición en el plano para una distancia recorrida d (acotada a [0, L]).</summary>
        public Vec2 Evaluate(float d)
        {
            if (d <= 0f) return points[0];
            if (d >= Length) return points[points.Length - 1];
            int lo = 0, hi = points.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (cumulative[mid] <= d) lo = mid; else hi = mid;
            }
            float seg = cumulative[hi] - cumulative[lo];
            float t = seg > 1e-6f ? (d - cumulative[lo]) / seg : 0f;
            return points[lo] + (points[hi] - points[lo]) * t;
        }

        /// <summary>Dirección del tramo en la distancia d (para orientar la presentación).</summary>
        public Vec2 Direction(float d)
        {
            int i = 1;
            while (i < points.Length - 1 && cumulative[i] < d) i++;
            Vec2 v = points[i] - points[i - 1];
            float m = v.Magnitude;
            return m > 1e-6f ? v * (1f / m) : new Vec2(1, 0);
        }

        /// <summary>Distancia mínima de un punto a la polilínea.</summary>
        public float DistanceTo(Vec2 p)
        {
            float best = float.MaxValue;
            for (int i = 1; i < points.Length; i++)
            {
                float d = Vec2.DistanceToSegment(p, points[i - 1], points[i]);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Largo de recorrido que queda a ≤ radius de un punto (muestreado). Instrumento de LDS.</summary>
        public float CoverageWithin(Vec2 p, float radius, float sampleStep = 0.1f)
        {
            float r2 = radius * radius, covered = 0f;
            for (float d = 0f; d < Length; d += sampleStep)
                if (Vec2.SqrDistance(Evaluate(d + sampleStep * 0.5f), p) <= r2) covered += Math.Min(sampleStep, Length - d);
            return covered;
        }
    }
}
