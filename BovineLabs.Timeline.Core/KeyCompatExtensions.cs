// <copyright file="KeyCompatExtensions.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core
{
    /// <summary>
    /// Compatibility helpers for raw key values (StatKey.Value, IntrinsicKey.Value, ConditionKey.Value).
    /// The previous BovineLabs core exposed these as a BLID struct with IsNull/RawValue members;
    /// the current core uses raw ushort/int where 0 means "no key". These extensions restore the
    /// old call-site vocabulary so switching back later is a mechanical change.
    /// NOTE: extension *methods*, so call sites use key.IsNull() / key.RawValue() with parentheses.
    /// </summary>
    public static class KeyCompatExtensions
    {
        /// <summary> A key of 0 is the "null" / unset key. </summary>
        public static bool IsNull(this ushort key) => key == 0;

        /// <summary> A key of 0 is the "null" / unset key. </summary>
        public static bool IsNull(this int key) => key == 0;

        /// <summary> The raw numeric value of the key (identity for raw keys). </summary>
        public static ushort RawValue(this ushort key) => key;

        /// <summary> The raw numeric value of the key (identity for raw keys). </summary>
        public static int RawValue(this int key) => key;

        /// <summary> The id portion of the key (identity for raw keys; old BLID exposed .ID). </summary>
        public static ushort ID(this ushort key) => key;

        /// <summary> The id portion of the key (identity for raw keys; old BLID exposed .ID). </summary>
        public static int ID(this int key) => key;
    }
}
