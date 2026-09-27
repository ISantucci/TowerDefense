using System;

namespace ClashDefense.Core
{
    /// <summary>Punto o vector en el plano de juego (X, Z). La altura es solo presentación (GDS-001.0).</summary>
    [Serializable]
    public struct Vec2
    {
        public float x;
        public float z;

        public Vec2(float x, float z) { this.x = x; this.z = z; }

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.x + b.x, a.z + b.z);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.x - b.x, a.z - b.z);
        public static Vec2 operator *(Vec2 a, float k) => new Vec2(a.x * k, a.z * k);

        public float SqrMagnitude => x * x + z * z;
        public float Magnitude => (float)Math.Sqrt(x * x + z * z);

        public static float Dot(Vec2 a, Vec2 b) => a.x * b.x + a.z * b.z;
        public static float SqrDistance(Vec2 a, Vec2 b) { float dx = a.x - b.x, dz = a.z - b.z; return dx * dx + dz * dz; }
        public static float Distance(Vec2 a, Vec2 b) => (float)Math.Sqrt(SqrDistance(a, b));

        /// <summary>Mueve 'from' hacia 'to' como mucho 'maxStep'.</summary>
        public static Vec2 MoveTowards(Vec2 from, Vec2 to, float maxStep)
        {
            Vec2 d = to - from;
            float len = d.Magnitude;
            if (len <= maxStep || len <= 1e-6f) return to;
            return from + d * (maxStep / len);
        }

        /// <summary>Distancia de un punto a un segmento.</summary>
        public static float DistanceToSegment(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 ab = b - a;
            float len2 = ab.SqrMagnitude;
            if (len2 <= 1e-9f) return Distance(p, a);
            float t = Dot(p - a, ab) / len2;
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return Distance(p, a + ab * t);
        }

        public override string ToString() => $"({x:0.##}, {z:0.##})";
    }
}
