#if UNITY_EDITOR || BL_DEBUG
using Unity.Mathematics;

namespace BovineLabs.Timeline.Core.Debug
{
    public static class PlaneIntersect
    {
        public static bool TryThreePlaneIntersect(in float4 p1, in float4 p2, in float4 p3, out float3 point)
        {
            float3 n1 = p1.xyz, n2 = p2.xyz, n3 = p3.xyz;
            var n2xn3 = math.cross(n2, n3);
            var n3xn1 = math.cross(n3, n1);
            var n1xn2 = math.cross(n1, n2);

            var denom = math.dot(n1, n2xn3);
            if (math.abs(denom) < 1e-6f)
            {
                point = default;
                return false;
            }

            point = (-p1.w * n2xn3 + -p2.w * n3xn1 + -p3.w * n1xn2) / denom;
            return true;
        }
    }
}
#endif
