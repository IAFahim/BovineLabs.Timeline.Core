// <copyright file="GameBootstrap.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

namespace BovineLabs.Timeline.Core
{
    using BovineLabs.Nerve;
    using BovineLabs.Core;

    /// <summary>
    /// The project's concrete <see cref="BovineLabsBootstrap" />. Without a concrete subclass the BovineLabs world
    /// topology never engages and SubScene world-<em>targeting</em> silently does nothing: the ServiceWorld never gets
    /// created, so SubScenes flagged <c>Service</c> have no world to stream into. (The loading systems themselves run in
    /// any world matching <c>Worlds.SimulationService | Worlds.Menu</c> — the GameWorld included, via its Simulation flag —
    /// so raw untargeted SubScenes still load on Unity's plain default world even with no bootstrap.)
    /// <para>
    /// This gives a standard single-player layout: a persistent <c>ServiceWorld</c> (scene streaming, debug, services)
    /// plus a <c>GameWorld</c> for gameplay. SubScenes flagged <c>Game</c> stream into the GameWorld, <c>Service</c>
    /// into the ServiceWorld (see SubSceneSettings → SubSceneSet.TargetWorld).
    /// </para>
    /// The GameWorld is created with the same <c>WorldSystemFilterFlags.Default</c> system set Unity puts in its plain
    /// default world, so existing scenes that just press Play behave exactly as before; the ServiceWorld is additive and
    /// excludes simulation systems, so gameplay still only runs once, in the GameWorld.
    /// </summary>
    public class GameBootstrap : BovineLabsBootstrap
    {
        /// <inheritdoc />
        protected override void Initialize()
        {
            base.Initialize(); // creates the ServiceWorld + applies app.target-frame-rate / app.fixed-update ConfigVars

#if !UNITY_NETCODE
            // ponytail: single-player, create the GameWorld immediately. A menu-driven game would instead defer this to
            // CreateGameWorld()/CreateMenuWorld() on "start game". Under NetCode the client/server worlds are driven separately.
            this.CreateGameWorld();
#endif
        }
    }
}
