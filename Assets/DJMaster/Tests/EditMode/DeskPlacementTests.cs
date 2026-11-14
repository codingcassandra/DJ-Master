using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    public class DeskPlacementTests
    {
        const float BoardHeight = 0.806f, BoardForward = 0.40f;
        static readonly Vector3 Head = new Vector3(0, 1.2f, 0);
        // Most desks here have their near edge 8 cm in front of your eyes, close enough to reach everything
        const float Edge = 0.08f;

        static DeskPlacement.Table Desk(float x, float y, float z, float width = 1.2f, float depth = 0.6f, float turn = 0, float floorY = float.NaN)
            => new DeskPlacement.Table(new Vector3(x, y, z), Quaternion.Euler(0, turn, 0), new Vector2(width, depth), floorY);

        // A desk in front of you with its near edge at z = edge
        static DeskPlacement.Table Near(float edge = Edge, float top = 0.78f, float width = 1.2f, float depth = 0.6f, float floorY = float.NaN)
            => Desk(0, top, edge + depth / 2f, width, depth, 0, floorY);

        static bool Place(DeskPlacement.Table table, out Vector3 board, out Quaternion rot, Vector3? head = null)
        {
            bool ok = DeskPlacement.TryPlace(head ?? Head, Vector3.forward, table, BoardHeight, BoardForward, out var root, out rot, out _);
            board = root + rot * new Vector3(0, BoardHeight, BoardForward);
            return ok;
        }

        [Test]
        public void DeskInFrontPutsBoardOnTheTop()
        {
            Assert.IsTrue(Place(Near(), out var board, out var rot));
            Assert.AreEqual(0.78f + DeskPlacement.BoardBottom, board.y, 1e-4f);
            Assert.AreEqual(0f, board.x, 1e-4f);
            // Faces away from you, so the front edge is on your side
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.forward, Vector3.forward), 1e-4f);
            // Front edge lines up with the table edge, not hanging off toward you
            Assert.AreEqual(Edge, board.z - DeskPlacement.BoardFront, 1e-4f);
        }

        [Test]
        public void TurnedTableStillFacesYou()
        {
            // Same desk, but the scan says it's turned 90 degrees with width and depth swapped
            Assert.IsTrue(Place(Desk(0, 0.78f, Edge + 0.3f, 0.6f, 1.2f, 90), out var board, out var rot));
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.forward, Vector3.forward), 1e-4f);
            Assert.AreEqual(0f, board.x, 1e-4f);
        }

        [Test]
        public void DeepTableKeepsBoardAtYourEdge()
        {
            Assert.IsTrue(Place(Near(depth: 1.2f, width: 1.6f), out var board, out _));
            Assert.AreEqual(Edge, board.z - DeskPlacement.BoardFront, 1e-4f);
        }

        [Test]
        public void LongDeskLinesUpWithYouButStaysOnIt()
        {
            Assert.IsTrue(Place(Near(width: 2f), out var board, out _, new Vector3(0.5f, 1.2f, 0)));
            Assert.AreEqual(0.5f, board.x, 1e-4f);
            Assert.IsTrue(Place(Near(width: 2f), out board, out _, new Vector3(0.78f, 1.2f, 0)));
            Assert.AreEqual(1f - DeskPlacement.BoardHalfWidth, board.x, 1e-4f);
        }

        [Test]
        public void TableBehindYouIsSkipped()
        {
            Assert.IsFalse(Place(Desk(0, 0.78f, -0.38f), out _, out _));
        }

        [Test]
        public void TableOutOfReachIsSkipped()
        {
            Assert.IsFalse(Place(Desk(0, 0.78f, 2f), out _, out _));
        }

        // The near edge 25 cm away puts the jog wheels about 0.7 m from your eyes. You'd have to lean in,
        // so the board stays in front of you instead
        [Test]
        public void DeskWhereYouHaveToLeanInIsSkipped()
        {
            Assert.IsTrue(Place(Near(0.12f), out _, out _));
            Assert.IsFalse(Place(Near(0.13f), out _, out _));
            Assert.IsFalse(Place(Near(0.25f, 0.75f), out _, out _));
        }

        [TestCase(1.3f)]  // above your eyes
        [TestCase(1.1f)]  // right under your chin
        [TestCase(0.1f)]  // way down by the floor
        public void BadHeightIsSkipped(float top)
        {
            Assert.IsFalse(Place(Near(top: top), out _, out _));
        }

        [TestCase(0.2f, 0.2f)]  // a stool or a bad scan
        [TestCase(5f, 1f)]      // way too big to be a real table
        public void BadSizeIsSkipped(float width, float depth)
        {
            Assert.IsFalse(Place(Near(width: width, depth: depth), out _, out _));
        }

        [Test]
        public void TableTooLowOffTheFloorIsSkipped()
        {
            var sitOnFloor = new Vector3(0, 0.7f, 0);
            Assert.IsTrue(Place(Near(top: 0.28f), out _, out _, sitOnFloor));
            Assert.IsFalse(Place(Near(top: 0.28f, floorY: 0f), out _, out _, sitOnFloor));
        }

        [Test]
        public void PicksTheClosestTableInFront()
        {
            var tables = new[] { Desk(0, 0.78f, -0.38f), Near(0.11f), Near(), Desk(0, 0.78f, 3f) };
            Assert.IsTrue(DeskPlacement.PickBest(Head, Vector3.forward, tables, BoardHeight, BoardForward, out var root, out var rot));
            var board = root + rot * new Vector3(0, BoardHeight, BoardForward);
            Assert.AreEqual(Edge + DeskPlacement.BoardFront, board.z, 1e-4f);
        }

        [Test]
        public void NoGoodTableMeansNoPlacement()
        {
            var tables = new[] { Desk(0, 0.78f, -0.38f), Near(top: 1.3f) };
            Assert.IsFalse(DeskPlacement.PickBest(Head, Vector3.forward, tables, BoardHeight, BoardForward, out _, out _));
            Assert.IsFalse(DeskPlacement.PickBest(Head, Vector3.forward, new DeskPlacement.Table[0], BoardHeight, BoardForward, out _, out _));
        }
    }
}
