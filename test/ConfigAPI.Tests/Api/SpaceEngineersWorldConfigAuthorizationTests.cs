using System;
using MarcoZechner.ConfigAPI.Api;
using NUnit.Framework;
using VRage.Game.ModAPI;

namespace MarcoZechner.ConfigAPI.Tests.V2.Api
{
    [TestFixture]
    public sealed class SpaceEngineersWorldConfigAuthorizationTests
    {
        [Test]
        public void IsAdmin_Uses_Requested_Player_And_Admin_Promote_Threshold()
        {
            ulong observedPlayerId = 0;
            var belowAdmin = (MyPromoteLevel)((int)MyPromoteLevel.Admin - 1);
            var aboveAdmin = (MyPromoteLevel)((int)MyPromoteLevel.Admin + 1);

            var denied = new SpaceEngineersWorldConfigAuthorization(playerId =>
            {
                observedPlayerId = playerId;
                return belowAdmin;
            });

            var admin = new SpaceEngineersWorldConfigAuthorization(playerId => MyPromoteLevel.Admin);
            var above = new SpaceEngineersWorldConfigAuthorization(playerId => aboveAdmin);
            var missing = new SpaceEngineersWorldConfigAuthorization(playerId => null);

            Assert.Multiple(() =>
            {
                Assert.That(denied.IsAdmin(76561198000000001UL), Is.False);
                Assert.That(observedPlayerId, Is.EqualTo(76561198000000001UL));
                Assert.That(admin.IsAdmin(1UL), Is.True);
                Assert.That(above.IsAdmin(2UL), Is.True);
                Assert.That(missing.IsAdmin(3UL), Is.False);
            });
        }

        [Test]
        public void Constructor_Rejects_Null_Promote_Level_Resolver()
        {
            Assert.Throws<ArgumentNullException>(() => new SpaceEngineersWorldConfigAuthorization(null));
        }
    }
}
