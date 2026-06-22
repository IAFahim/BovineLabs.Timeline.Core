#if UNITY_EDITOR || BL_DEBUG
using Unity.Entities;

namespace BovineLabs.Timeline.Core.Debug
{
    public static class RollingHistoryBuffer
    {
        public static void Cull<T>(this DynamicBuffer<T> buffer, double currentTime, double retentionWindow)
            where T : unmanaged, IBufferElementData, ITimestampedRecord
        {
            while (buffer.Length > 0 && currentTime - buffer[0].Timestamp > retentionWindow)
                buffer.RemoveAt(0);
        }
    }

    public interface ITimestampedRecord
    {
        double Timestamp { get; }
    }
}
#endif