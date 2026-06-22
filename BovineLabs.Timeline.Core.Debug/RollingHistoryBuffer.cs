#if UNITY_EDITOR || BL_DEBUG
using Unity.Entities;

namespace BovineLabs.Timeline.Core.Debug
{
    public static class RollingHistoryBuffer
    {
        public static void Cull<T>(this DynamicBuffer<T> buffer, double currentTime, double retentionWindow)
            where T : unmanaged, IBufferElementData, ITimestampedRecord
        {
            var expired = 0;
            while (expired < buffer.Length && currentTime - buffer[expired].Timestamp > retentionWindow)
                expired++;

            if (expired > 0)
                buffer.RemoveRange(0, expired);
        }
    }

    public interface ITimestampedRecord
    {
        double Timestamp { get; }
    }
}
#endif