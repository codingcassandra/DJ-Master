using System.Collections.Generic;
using UnityEngine;

namespace DJMaster
{
    // The math for putting the board on a real table. No headset stuff in here so the tests can run it in the editor
    public static class DeskPlacement
    {
        // A table top from the room scan: the middle of the top, which way it's turned and how big it is in meters
        // (size.x goes along its right, size.y along its forward). floorY is NaN if we don't know where the floor is
        public struct Table
        {
            public Vector3 center;
            public Quaternion rotation;
            public Vector2 size;
            public float floorY;

            public Table(Vector3 center, Quaternion rotation, Vector2 size, float floorY = float.NaN)
            {
                this.center = center;
                this.rotation = rotation;
                this.size = size;
                this.floorY = floorY;
            }
        }

        // Board size, same as the one SceneBuilder makes: the body is 0.78 wide and its bottom is 0.051 under the
        // board's origin. It goes 0.215 toward you and about 0.31 away from you (the screens at the back).
        public const float BoardBottom = 0.051f;
        public const float BoardHalfWidth = 0.39f;
        public const float BoardFront = 0.215f;
        public const float BoardBack = 0.31f;
        // Keep the front edge a little in from the table edge so it doesn't hang off
        public const float EdgeGap = 0.04f;

        // Comfy seated reach, measured flat from your head to the middle of the board
        public const float MinReach = 0.25f, MaxReach = 0.9f;
        // How far under your eyes the board can be. Never above them.
        public const float MinDrop = 0.2f, MaxDrop = 0.9f;
        // Smaller than this is probably a stool or a bad scan, bigger is probably not a real table
        public const float MinWidth = 0.35f, MinDepth = 0.25f, MaxSide = 4f;
        // Table top height above the floor, only checked when we know where the floor is
        public const float MinTableHeight = 0.35f, MaxTableHeight = 1.3f;
        // The board has to be within 60 degrees of where you look, so it's never next to you or behind you
        public const float MinFacing = 0.5f;

        // Where the World root goes so the board sits on this table, facing you, front edge toward you.
        // False if the table is no good. Lower score = better table.
        public static bool TryPlace(Vector3 headPos, Vector3 headForward, Table table, float boardHeight, float boardForward,
            out Vector3 rootPos, out Quaternion rootRot, out float score)
        {
            rootPos = Vector3.zero;
            rootRot = Quaternion.identity;
            score = float.MaxValue;

            float shortSide = Mathf.Min(table.size.x, table.size.y), longSide = Mathf.Max(table.size.x, table.size.y);
            if (shortSide < MinDepth || longSide < MinWidth || longSide > MaxSide) return false;
            if (!float.IsNaN(table.floorY))
            {
                float tableHeight = table.center.y - table.floorY;
                if (tableHeight < MinTableHeight || tableHeight > MaxTableHeight) return false;
            }
            float surfaceY = table.center.y + BoardBottom;
            float drop = headPos.y - surfaceY;
            if (drop < MinDrop || drop > MaxDrop) return false;

            Vector3 look = Flat(headForward);
            if (look == Vector3.zero) look = Vector3.forward;
            // Only the turn around up matters, so a slightly tilted scan doesn't tilt the board
            Vector3 tableFwd = Flat(table.rotation * Vector3.forward);
            if (tableFwd == Vector3.zero) return false;
            Vector3 tableRight = Vector3.Cross(Vector3.up, tableFwd);

            // The edge you sit at is the one you're furthest past, outward points from the table toward you
            Vector3 toHead = headPos - table.center;
            toHead.y = 0;
            Vector3 outward = tableFwd;
            float halfDepth = 0, halfLong = 0, best = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                bool alongFwd = i < 2;
                Vector3 n = (alongFwd ? tableFwd : tableRight) * (i % 2 == 0 ? 1 : -1);
                float half = (alongFwd ? table.size.y : table.size.x) / 2f;
                float past = Vector3.Dot(toHead, n) - half;
                if (past > best)
                {
                    best = past;
                    outward = n;
                    halfDepth = half;
                    halfLong = (alongFwd ? table.size.x : table.size.y) / 2f;
                }
            }

            // The board faces away from you, so its front edge is on your side
            Vector3 boardFwd = -outward;
            Vector3 side = Vector3.Cross(Vector3.up, boardFwd);
            // Slide it sideways to line up with you, but keep it on the table
            float slack = Mathf.Max(0f, halfLong - BoardHalfWidth);
            float across = Mathf.Clamp(Vector3.Dot(toHead, side), -slack, slack);
            // Front edge just in from the table edge. If the table isn't deep enough we center it instead.
            float toward = Mathf.Max(halfDepth - EdgeGap - BoardFront, (BoardBack - BoardFront) / 2f);
            Vector3 board = table.center + side * across + outward * toward;
            board.y = surfaceY;

            Vector3 toBoard = board - headPos;
            toBoard.y = 0;
            float reach = toBoard.magnitude;
            if (reach < MinReach || reach > MaxReach) return false;
            float facing = Vector3.Dot(toBoard / reach, look);
            if (facing < MinFacing || Vector3.Dot(boardFwd, look) < MinFacing || Vector3.Dot(toBoard, boardFwd) <= 0) return false;

            rootRot = Quaternion.LookRotation(boardFwd, Vector3.up);
            rootPos = board - rootRot * new Vector3(0, boardHeight, boardForward);
            score = reach + (1f - facing);
            return true;
        }

        // Rooms can have more than one table, so we go with the closest one that's in front of you
        public static bool PickBest(Vector3 headPos, Vector3 headForward, IList<Table> tables, float boardHeight, float boardForward,
            out Vector3 rootPos, out Quaternion rootRot)
        {
            rootPos = Vector3.zero;
            rootRot = Quaternion.identity;
            float bestScore = float.MaxValue;
            bool found = false;
            for (int i = 0; i < tables.Count; i++)
            {
                if (!TryPlace(headPos, headForward, tables[i], boardHeight, boardForward, out var pos, out var rot, out float score)) continue;
                if (score >= bestScore) continue;
                bestScore = score;
                rootPos = pos;
                rootRot = rot;
                found = true;
            }
            return found;
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0;
            return v.sqrMagnitude < 1e-6f ? Vector3.zero : v.normalized;
        }
    }
}
