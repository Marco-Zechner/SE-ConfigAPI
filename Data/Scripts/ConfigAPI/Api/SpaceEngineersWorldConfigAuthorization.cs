using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace MarcoZechner.ConfigAPI.V2.Api
{
    public sealed class SpaceEngineersWorldConfigAuthorization : IWorldConfigAuthorization
    {
        private readonly Func<ulong, MyPromoteLevel?> _promoteLevelResolver;

        public SpaceEngineersWorldConfigAuthorization() : this(ResolvePromoteLevel) { }

        public SpaceEngineersWorldConfigAuthorization(Func<ulong, MyPromoteLevel?> promoteLevelResolver)
        {
            if (promoteLevelResolver == null)
                throw new ArgumentNullException(nameof(promoteLevelResolver));

            _promoteLevelResolver = promoteLevelResolver;
        }

        public bool IsAdmin(ulong playerId)
        {
            MyPromoteLevel? promoteLevel = _promoteLevelResolver(playerId);
            return promoteLevel.HasValue && promoteLevel.Value >= MyPromoteLevel.Admin;
        }

        private static MyPromoteLevel? ResolvePromoteLevel(ulong playerId)
        {
            IMyMultiplayer multiplayer = MyAPIGateway.Multiplayer;
            if (multiplayer == null || multiplayer.Players == null)
                return null;

            var players = new List<IMyPlayer>();
            multiplayer.Players.GetPlayers(players);

            for (int index = 0; index < players.Count; index++)
            {
                IMyPlayer player = players[index];
                if (player != null && player.SteamUserId == playerId)
                    return player.PromoteLevel;
            }

            return null;
        }
    }
}
