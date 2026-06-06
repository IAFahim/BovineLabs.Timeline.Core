#if UNITY_EDITOR || BL_DEBUG
using Unity.Entities;

namespace BovineLabs.Timeline.Core.Debug
{
    /// <summary>
    ///     A generic rolling history buffer that evicts records older than a configurable window.
    ///     Eliminates the duplicated retention-culling logic across debug systems.
    /// </summary>
    public static class RollingHistoryBuffer
    {
        /// <summary>
        ///     Removes entries from the front of the buffer that are older than
        ///     <paramref name="currentTime" /> - <paramref name="retentionWindow" />.
        ///     The buffer must have a <see cref="ITimestampedRecord" /> element.
        ///     Entries are assumed to be sorted oldest-first.
        /// </summary>
        public static void Cull<T>(this DynamicBuffer<T> buffer, double currentTime, double retentionWindow)
            where T : unmanaged, IBufferElementData, ITimestampedRecord
        {
            while (buffer.Length > 0 && currentTime - buffer[0].Timestamp > retentionWindow)
                buffer.RemoveAt(0);
        }
    }

    /// <summary>
    ///     Interface for buffer elements that carry a timestamp, enabling
    ///     generic retention-culling via <see cref="RollingHistoryBuffer.Cull{T}" />.
    /// </summary>
    public interface ITimestampedRecord
    {
        double Timestamp { get; }
    }
}
#endif