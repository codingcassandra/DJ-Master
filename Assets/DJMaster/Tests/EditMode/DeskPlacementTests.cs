using NUnit.Framework;
using UnityEngine;

namespace DJMaster.Tests
{
    public class DeskPlacementTests
    {
        const float BoardHeight = 0.82f, BoardForward = 0.40f;
        static readonly Vector3 Head = new Vector3(0, 1.2f, 0);

        static DeskPlacement.Table Desk(float x, float y, float z, float width = 1.2f, float depth = 0.6f, float turn = 0, float floorY = float.NaN)
            => new DeskPlacement.Table(new Vector3(x, y, z), Quaternion.Euler(0, turn, 0), new Vector2(width, depth), floorY);

        static bool Place(DeskPlacement.Table table, out Vector3 board, out Quaternion rot, Vector3? head = null)
        {
            bool ok = DeskPlacement.TryPlace(head ?? Head, Vector3.forward, table, BoardHeight, BoardForward, out var root, out rot, out _);
            board = root + rot * new Vector3(0, BoardHeight, BoardForward);
            return ok;
        }

        [Test]
        public void DeskInFrontPutsBoardOnTheTop()
        {
            Assert.IsTrue(Place(Desk(0, 0.75f, 0.55f), out var board, out var rot));
            Assert.AreEqual(0.75f + DeskPlacement.BoardBottom, board.y, 1e-4f);
            Assert.AreEqual(0f, board.x, 1e-4f);
            // Faces away from you, so the front edge is on your side
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.forward, Vector3.forward), 1e-4f);
            // Front edge is on the table, not hanging off toward you
            Assert.GreaterOrEqual(board.z - DeskPlacement.BoardFront, 0.25f - 1e-4f);
        }

        [Test]
        public void TurnedTableStillFacesYou()
        {
            // Same desk, but the scan says it's turned 90 degrees with width and depth swapped
            Assert.IsTrue(Place(Desk(0, 0.75f, 0.55f, 0.6f, 1.2f, 90), out var board, out var rot));
            Assert.AreEqual(1f, Vector3.Dot(rot * Vector3.forward, Vector3.forward), 1e-4f);
            Assert.AreEqual(0f, board.x, 1e-4f);
        }

        [Test]
        public void DeepTableKeepsBoardNearYourEdge()
        {
            Assert.IsTrue(Place(Desk(0, 0.75f, 0.9f, 1.6f, 1.2f), out var board, out _));
            // Near edge is at z = 0.3, front of the board sits just in from it
            Assert.AreEqual(0.3f + DeskPlacement.EdgeGap, board.z - DeskPlacement.BoardFront, 1e-4f);
        }

        [Test]
        public void LongDeskLinesUpWithYouButStaysOnIt()
        {
            Assert.IsTrue(Place(Desk(0, 0.75f, 0.55f, 2f), out var board, out _, new Vector3(0.5f, 1.2f, 0)));
            Assert.AreEqual(0.5f, board.x, 1e-4f);
            Assert.IsTrue(Place(Desk(0, 0.75f, 0.55f, 2f), out board, out _, new Vector3(0.9f, 1.2f, 0)));
            Assert.AreEqual(1f - DeskPlacement.BoardHalfWidth, board.x, 1e-4f);
        }

        [Test]
        public void TableBehindYouIsSkipped()
        {
            Assert.IsFalse(Place(Desk(0, 0.75f, -0.55f), out _, out _));
        }

        [Test]
        public void TableOutOfReachIsSkipped()
        {
            Assert.IsFalse(Place(Desk(0, 0.75f, 2f), out _, out _));
        }

        [TestCase(1.3f)]  // above your eyes
        [TestCase(1.1f)]  // right under your chin
        [TestCase(0.1f)]  // way down by the floor
        public void BadHeightIsSkipped(float top)
        {
            Assert.IsFalse(Place(Desk(0, top, 0.55f), out _, out _));
        }

        [TestCase(0.2f, 0.2f)]  // a stool or a bad scan
        [TestCase(5f, 1f)]      // way too big to be a real table
        public void BadSizeIsSkipped(float width, float depth)
        {
            Assert.IsFalse(Place(Desk(0, 0.75f, 0.55f, width, depth), out _, out _));
        }

        [Test]
        public void TableTooLowOffTheFloorIsSkipped()
        {
            var sitOnFloor = new Vector3(0, 0.7f, 0);
            Assert.IsTrue(Place(Desk(0, 0.2f, 0.55f), out _, out _, sitOnFloor));
            Assert.IsFalse(Place(Desk(0, 0.2f, 0.55f, floorY: 0f), out _, out _, sitOnFloor));
        }

        [Test]
        public void PicksTheClosestTableInFront()
        {
            var tables = new[] { Desk(0, 0.75f, -0.55f), Desk(0, 0.75f, 0.85f), Desk(0, 0.75f, 0.55f), Desk(0, 0.75f, 3f) };
            Assert.IsTrue(DeskPlacement.PickBest(Head, Vector3.forward, tables, BoardHeight, BoardForward, out var root, out var rot));
            var board = root + rot * new Vector3(0, BoardHeight, BoardForward);
            Assert.AreEqual(0.55f - (DeskPlacement.BoardBack - DeskPlacement.BoardFront) / 2f, board.z, 1e-4f);
        }

        [Test]
        public void NoGoodTableMeansNoPlacement()
        {
            var tables = new[] { Desk(0, 0.75f, -0.55f), Desk(0, 1.3f, 0.55f) };
            Assert.IsFalse(DeskPlacement.PickBest(Head, Vector3.forward, tables, BoardHeight, BoardForward, out _, out _));
            Assert.IsFalse(DeskPlacement.PickBest(Head, Vector3.forward, new DeskPlacement.Table[0], BoardHeight, BoardForward, out _, out _));
        }
    }
}
