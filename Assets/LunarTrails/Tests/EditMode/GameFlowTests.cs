using System;
using LunarTrails.Core.Flow;
using NUnit.Framework;

namespace LunarTrails.Core.Tests
{
    public class GameFlowTests
    {
        [Test]
        public void タイトルから選曲プレイリザルトと進める()
        {
            var flow = new GameFlow();
            Assert.AreEqual(GameScreen.Title, flow.Current);

            flow.Go(GameScreen.SongSelect);
            flow.Go(GameScreen.Play);
            flow.Go(GameScreen.Result);
            flow.Go(GameScreen.Play);   // もう一度
            flow.Go(GameScreen.Result);
            flow.Go(GameScreen.SongSelect);

            Assert.AreEqual(GameScreen.SongSelect, flow.Current);
        }

        [Test]
        public void 行けない画面へはInvalidOperationException()
        {
            var flow = new GameFlow();
            Assert.Throws<InvalidOperationException>(() => flow.Go(GameScreen.Play));
            Assert.Throws<InvalidOperationException>(() => flow.Go(GameScreen.Result));
            Assert.AreEqual(GameScreen.Title, flow.Current);
        }

        [Test]
        public void 設定を閉じると開く前の画面に戻る()
        {
            var flow = new GameFlow(GameScreen.SongSelect);
            flow.Go(GameScreen.Settings);
            Assert.IsFalse(flow.CanGo(GameScreen.Title));
            Assert.IsTrue(flow.Back());
            Assert.AreEqual(GameScreen.SongSelect, flow.Current);

            var fromTitle = new GameFlow();
            fromTitle.Go(GameScreen.Settings);
            fromTitle.Back();
            Assert.AreEqual(GameScreen.Title, fromTitle.Current);
        }

        [TestCase(GameScreen.SongSelect, GameScreen.Title)]
        [TestCase(GameScreen.Play, GameScreen.SongSelect)]
        [TestCase(GameScreen.Result, GameScreen.SongSelect)]
        public void 戻るの行き先(GameScreen from, GameScreen expected)
        {
            var flow = new GameFlow(from);
            Assert.AreEqual(expected, flow.BackTarget);
            flow.Back();
            Assert.AreEqual(expected, flow.Current);
        }

        [Test]
        public void タイトルでは戻れない()
        {
            var flow = new GameFlow();
            Assert.IsNull(flow.BackTarget);
            Assert.IsFalse(flow.Back());
        }

        [Test]
        public void 画面が変わるとChangedが呼ばれる()
        {
            var flow = new GameFlow();
            GameScreen? from = null, to = null;
            flow.Changed += (f, t) => { from = f; to = t; };

            flow.Go(GameScreen.SongSelect);

            Assert.AreEqual(GameScreen.Title, from);
            Assert.AreEqual(GameScreen.SongSelect, to);
        }
    }
}
