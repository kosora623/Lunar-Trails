using LunarTrails.Core.Settings;
using LunarTrails.Core.Timing;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class GameSettingsTests
    {
        [Test]
        public void Sanitize_ClampsValuesIntoRange()
        {
            var s = new GameSettings { offsetMs = 9999, scrollSpeed = 1000f, pixelsPerLane = 1f };
            s.Sanitize();
            Assert.AreEqual(PlayerOffset.MaxMs, s.offsetMs);
            Assert.AreEqual(GameSettings.MaxScrollSpeed, s.scrollSpeed);
            Assert.AreEqual(GameSettings.MinPixelsPerLane, s.pixelsPerLane);
        }

        [Test]
        public void Sanitize_NaNBecomesDefault()
        {
            var s = new GameSettings { scrollSpeed = float.NaN };
            s.Sanitize();
            Assert.AreEqual(new GameSettings().scrollSpeed, s.scrollSpeed);
        }

        [TestCase("S,D,F")]          // 数が足りない
        [TestCase("S,D,F,J,K,")]     // 空がある
        [TestCase("S,S,F,J,K,L")]    // 重複
        public void Sanitize_InvalidKeysResetToDefault(string keys)
        {
            var s = new GameSettings { laneKeys = keys.Split(',') };
            s.Sanitize();
            CollectionAssert.AreEqual(GameSettings.DefaultLaneKeys, s.laneKeys);
        }

        [Test]
        public void Sanitize_NullKeysResetToDefault()
        {
            var s = new GameSettings { laneKeys = null };
            s.Sanitize();
            CollectionAssert.AreEqual(GameSettings.DefaultLaneKeys, s.laneKeys);
        }

        [Test]
        public void SetLaneKey_NewKey_Replaces()
        {
            var s = new GameSettings();
            s.SetLaneKey(0, "A");
            CollectionAssert.AreEqual(new[] { "A", "D", "F", "J", "K", "L" }, s.laneKeys);
        }

        [Test]
        public void SetLaneKey_KeyUsedByAnotherLane_Swaps()
        {
            var s = new GameSettings();
            s.SetLaneKey(0, "L");
            CollectionAssert.AreEqual(new[] { "L", "D", "F", "J", "K", "S" }, s.laneKeys);
        }

        [Test]
        public void Clone_DoesNotShareKeys()
        {
            var s = new GameSettings();
            var copy = s.Clone();
            copy.SetLaneKey(0, "A");
            Assert.AreEqual("S", s.laneKeys[0]);
        }

        [Test]
        public void DefaultKeys_AreNotChangedBySettings()
        {
            var s = new GameSettings();
            s.SetLaneKey(0, "A");
            Assert.AreEqual("S", GameSettings.DefaultLaneKeys[0]);
        }
    }
}
