/*
  This file is part of ppather.

    PPather is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PPather is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with ppather.  If not, see <http://www.gnu.org/licenses/>.

*/

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using WowTriangles;
using SharedLib.Data;
using System.Numerics;
using System.IO;

#pragma warning disable 162

namespace PPather.Graph
{
    public class PathGraph
    {
        public static bool SearchEnabled;

        private static readonly object m_LockObject = new();

        public enum eSearchScoreSpot
        {
            OriginalPather,
            A_Star,
            A_Star_With_Model_Avoidance,
        }

        public static int gradiantMax = 5;

        public eSearchScoreSpot searchScoreSpot = eSearchScoreSpot.A_Star_With_Model_Avoidance;
        public const int sleepMSBetweenSpots = 0;

        public const float toonHeight = 2.0f;
        public const float toonSize = 0.5f;

        public const float MinStepLength = 2f;
        public const float WantedStepLength = 3f;
        public const float MaxStepLength = 5f;

        public Path lastReducedPath;

        public static float IsCloseToModelRange = 2;

        /*
		public const float IndoorsWantedStepLength = 1.5f;
		public const float IndoorsMaxStepLength = 2.5f;
		*/

        public const float CHUNK_BASE = 100000.0f; // Always keep positive
        public const float MaximumAllowedRangeFromTarget = 100;

        private readonly string chunkDir;

        private float MapId;

        private readonly SparseMatrix2D<GraphChunk> chunks;

        public ChunkedTriangleCollection triangleWorld;
        public TriangleCollection paint;

        private readonly List<GraphChunk> ActiveChunks = new();
        private long LRU;

        public int GetTriangleClosenessScore(Vector3 loc)
        {
            if (!triangleWorld.IsCloseToModel(loc.X, loc.Y, loc.Z, 3))
            {
                return 0;
            }

            if (!triangleWorld.IsCloseToModel(loc.X, loc.Y, loc.Z, 2))
            {
                return 8;
            }

            if (!triangleWorld.IsCloseToModel(loc.X, loc.Y, loc.Z, 1))
            {
                return 64;
            }

            return 256;
        }

        public int GetTriangleGradiantScore(Vector3 loc, int gradiantMax)
        {
            if (triangleWorld.GradiantScore(loc.X, loc.Y, loc.Z, 1) > gradiantMax)
            {
                return 256;
            }

            if (triangleWorld.GradiantScore(loc.X, loc.Y, loc.Z, 2) > gradiantMax)
            {
                return 64;
            }

            if (triangleWorld.GradiantScore(loc.X, loc.Y, loc.Z, 3) > gradiantMax)
            {
                return 8;
            }

            return 0;
        }

        public static int TimeoutSeconds = 20;
        public static int ProgressTimeoutSeconds = 10;

        private readonly ILogger logger;

        public PathGraph(float mapId,
                         ChunkedTriangleCollection triangles,
                         TriangleCollection paint, ILogger logger, DataConfig dataConfig)
        {
            this.logger = logger;
            this.MapId = mapId;
            this.triangleWorld = triangles;
            this.paint = paint;

            chunkDir = System.IO.Path.Join(dataConfig.PathInfo, ContinentDB.IdToName[MapId]);
            if (!Directory.Exists(chunkDir))
                Directory.CreateDirectory(chunkDir);

            chunks = new SparseMatrix2D<GraphChunk>(8);
        }

        public void Close()
        {
            triangleWorld.Close();
        }

        private static void GetChunkCoord(float x, float y, out int ix, out int iy)
        {
            ix = (int)((CHUNK_BASE + x) / GraphChunk.CHUNK_SIZE);
            iy = (int)((CHUNK_BASE + y) / GraphChunk.CHUNK_SIZE);
        }

        private static void GetChunkBase(int ix, int iy, out float bx, out float by)
        {
            bx = (float)ix * GraphChunk.CHUNK_SIZE - CHUNK_BASE;
            by = (float)iy * GraphChunk.CHUNK_SIZE - CHUNK_BASE;
        }

        private GraphChunk GetChunkAt(float x, float y)
        {
            GetChunkCoord(x, y, out int ix, out int iy);
            GraphChunk c = chunks.Get(ix, iy);
            if (c != null)
                c.LRU = LRU++;
            return c;
        }

        private void CheckForChunkEvict()
        {
            lock (this)
            {
                if (ActiveChunks.Count < 512)
                    return;

                GraphChunk evict = null;
                foreach (GraphChunk gc in ActiveChunks)
                {
                    if (evict == null || gc.LRU < evict.LRU)
                    {
                        evict = gc;
                    }
                }

                evict.Save();
                ActiveChunks.Remove(evict);
                chunks.Clear(evict.ix, evict.iy);
                evict.Clear();
            }
        }

        public void Save()
        {
            lock (m_LockObject)
            {
                Stopwatch sw = Stopwatch.StartNew();
                foreach (GraphChunk gc in chunks.GetAllElements())
                {
                    if (gc.modified)
                    {
                        gc.Save();
                    }
                }

                if (logger.IsEnabled(LogLevel.Trace))
                    logger.LogTrace($"Saved GraphChunks {sw.ElapsedMilliseconds} ms");
            }
        }

        // Create and load from file if exisiting
        private void LoadChunk(float x, float y)
        {
            GraphChunk gc = GetChunkAt(x, y);
            if (gc == null)
            {
                int ix, iy;
                GetChunkCoord(x, y, out ix, out iy);

                float base_x, base_y;
                GetChunkBase(ix, iy, out base_x, out base_y);

                gc = new GraphChunk(base_x, base_y, ix, iy, logger, chunkDir);
                gc.LRU = LRU++;

                CheckForChunkEvict();

                gc.Load();
                chunks.Set(ix, iy, gc);
                ActiveChunks.Add(gc);
            }
        }

        public Spot AddSpot(Spot s)
        {
            LoadChunk(s.Loc.X, s.Loc.Y);
            GraphChunk gc = GetChunkAt(s.Loc.X, s.Loc.Y);
            return gc.AddSpot(s);
        }

        // Connect according to MPQ data
        public Spot AddAndConnectSpot(Spot s)
        {
            s = AddSpot(s);
            List<Spot> close = FindAllSpots(s.Loc, MaxStepLength);
            if (!s.IsFlagSet(Spot.FLAG_MPQ_MAPPED))
            {
                foreach (Spot cs in close)
                {
                    if (cs.HasPathTo(this, s) && s.HasPathTo(this, cs) || cs.IsBlocked())
                    {
                    }
                    else if (!triangleWorld.IsStepBlocked(s.Loc.X, s.Loc.Y, s.Loc.Z, cs.Loc.X, cs.Loc.Y, cs.Loc.Z, toonHeight, toonSize, null))
                    {
                        float mid_x = (s.Loc.X + cs.Loc.X) / 2;
                        float mid_y = (s.Loc.Y + cs.Loc.Y) / 2;
                        float mid_z = (s.Loc.Z + cs.Loc.Z) / 2;
                        float stand_z;
                        int flags;
                        if (triangleWorld.FindStandableAt(mid_x, mid_y, mid_z - WantedStepLength * .75f, mid_z + WantedStepLength * .75f, out stand_z, out flags, toonHeight, toonSize))
                        {
                            s.AddPathTo(cs);
                            cs.AddPathTo(s);
                        }
                    }
                }
            }
            return s;
        }

        public Spot GetSpot(float x, float y, float z)
        {
            LoadChunk(x, y);
            GraphChunk gc = GetChunkAt(x, y);
            return gc.GetSpot(x, y, z);
        }

        public Spot GetSpot2D(float x, float y)
        {
            LoadChunk(x, y);
            GraphChunk gc = GetChunkAt(x, y);
            return gc.GetSpot2D(x, y);
        }

        public Spot GetSpot(Vector3 l)
        {
            // Null?
            //if (l == null)
            //    return null;
            return GetSpot(l.X, l.Y, l.Z);
        }

        // this can be slow...

        public Spot FindClosestSpot(Vector3 l_d)
        {
            return FindClosestSpot(l_d, 30.0f, null);
        }

        public Spot FindClosestSpot(Vector3 l_d, HashSet<Spot> Not)
        {
            return FindClosestSpot(l_d, 30.0f, Not);
        }

        public Spot FindClosestSpot(Vector3 l, float max_d)
        {
            return FindClosestSpot(l, max_d, null);
        }

        public Spot FindClosestSpot(string description, Vector3 l, float max_d)
        {
            try
            {
                return FindClosestSpot(l, max_d, null);
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Failed to find closest spot to {description}: {l.X},{l.Y} - {ex.Message}");
                return null;
            }
        }

        // this can be slow...
        public Spot FindClosestSpot(Vector3 l, float max_d, HashSet<Spot> Not)
        {
            Spot closest = null;
            float closest_d = 1E30f;
            int d = 0;
            while (d <= max_d + 0.1f)
            {
                for (int i = -d; i <= d; i++)
                {
                    float x_up = l.X + d;
                    float x_dn = l.X - d;
                    float y_up = l.Y + d;
                    float y_dn = l.Y - d;

                    Spot s0 = GetSpot2D(x_up, l.Y + i);
                    Spot s2 = GetSpot2D(x_dn, l.Y + i);

                    Spot s1 = GetSpot2D(l.X + i, y_dn);
                    Spot s3 = GetSpot2D(l.X + i, y_up);
                    Spot[] sv = { s0, s1, s2, s3 };
                    foreach (Spot s in sv)
                    {
                        Spot ss = s;
                        while (ss != null)
                        {
                            float di = ss.GetDistanceTo(l);
                            if (di < max_d && !ss.IsBlocked() &&
                                (di < closest_d))
                            {
                                closest = ss;
                                closest_d = di;
                            }
                            ss = ss.next;
                        }
                    }
                }

                if (closest_d < d) // can't get better
                {
                    //Log("Closest2 spot to " + l + " is " + closest);
                    return closest;
                }
                d++;
            }
            //Log("Closest1 spot to " + l + " is " + closest);
            return closest;
        }

        public List<Spot> FindAllSpots(Vector3 l, float max_d)
        {
            List<Spot> sl = new();

            int d = 0;
            while (d <= max_d + 0.1f)
            {
                for (int i = -d; i <= d; i++)
                {
                    float x_up = l.X + d;
                    float x_dn = l.X - d;
                    float y_up = l.Y + d;
                    float y_dn = l.Y - d;

                    Spot[] sv = {
                        GetSpot2D(x_up, l.Y + i),
                        GetSpot2D(x_dn, l.Y + i),
                        GetSpot2D(l.X + i, y_dn),
                        GetSpot2D(l.X + i, y_up)
                    };

                    foreach (Spot s in sv)
                    {
                        Spot ss = s;
                        while (ss != null)
                        {
                            float di = ss.GetDistanceTo(l);
                            if (di < max_d)
                            {
                                sl.Add(ss);
                            }
                            ss = ss.next;
                        }
                    }
                }
                d++;
            }
            return sl;
        }


        public Spot TryAddSpot(Spot wasAt, Vector3 isAt)
        {
            //if (IsUnderwaterOrInAir(isAt)) { return wasAt; }
            Spot isAtSpot = FindClosestSpot(isAt, WantedStepLength);
            if (isAtSpot == null)
            {
                isAtSpot = GetSpot(isAt);
                if (isAtSpot == null)
                {
                    Spot s = new Spot(isAt);
                    s = AddSpot(s);
                    isAtSpot = s;
                }
                if (isAtSpot.IsFlagSet(Spot.FLAG_BLOCKED))
                {
                    isAtSpot.SetFlag(Spot.FLAG_BLOCKED, false);
                    if (logger.IsEnabled(LogLevel.Debug))
                        logger.LogDebug("Cleared blocked flag");
                }
                if (wasAt != null)
                {
                    wasAt.AddPathTo(isAtSpot);
                    isAtSpot.AddPathTo(wasAt);
                }

                List<Spot> sl = FindAllSpots(isAtSpot.Loc, MaxStepLength);
                int connected = 0;
                foreach (Spot other in sl)
                {
                    if (other != isAtSpot)
                    {
                        other.AddPathTo(isAtSpot);
                        isAtSpot.AddPathTo(other);
                        connected++;
                        // Log("  connect to " + other.location);
                    }
                }
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("Learned a new spot at " + isAtSpot.Loc + " connected to " + connected + " other spots");
                wasAt = isAtSpot;
            }
            else
            {
                if (wasAt != null && wasAt != isAtSpot)
                {
                    // moved to an old spot, make sure they are connected
                    wasAt.AddPathTo(isAtSpot);
                    isAtSpot.AddPathTo(wasAt);
                }
                wasAt = isAtSpot;
            }

            return wasAt;
        }

        private static bool LineCrosses(Vector3 line0, Vector3 line1, Vector3 point)
        {
            //float LineMag = line0.GetDistanceTo(line1); // Magnitude( LineEnd, LineStart );
            float LineMag = Vector3.DistanceSquared(line0, line1);

            float U =
                (((point.X - line0.X) * (line1.X - line0.X)) +
                  ((point.Y - line0.Y) * (line1.Y - line0.Y)) +
                  ((point.Z - line0.Z) * (line1.Z - line0.Z))) /
                (LineMag * LineMag);

            if (U < 0.0f || U > 1.0f)
                return false;

            float InterX = line0.X + U * (line1.X - line0.X);
            float InterY = line0.Y + U * (line1.Y - line0.Y);
            float InterZ = line0.Z + U * (line1.Z - line0.Z);

            float Distance = Vector3.DistanceSquared(point, new(InterX, InterY, InterZ));
            if (Distance < 0.5f)
                return true;
            return false;
        }

        //////////////////////////////////////////////////////
        // Searching
        //////////////////////////////////////////////////////

        public Spot currentSearchStartSpot;
        public Spot currentSearchSpot;

        private static float TurnCost(Spot from, Spot to)
        {
            Spot prev = from.traceBack;
            if (prev == null) { return 0.0f; }
            return TurnCost(prev.Loc.X, prev.Loc.Y, prev.Loc.Z, from.Loc.X, from.Loc.Y, from.Loc.Z, to.Loc.X, to.Loc.Y, to.Loc.Z);
        }

        private static float TurnCost(float x0, float y0, float z0, float x1, float y1, float z1, float x2, float y2, float z2)
        {
            float v1x = x1 - x0;
            float v1y = y1 - y0;
            float v1z = z1 - z0;

            float v1l = MathF.Sqrt(v1x * v1x + v1y * v1y + v1z * v1z);
            v1x /= v1l;
            v1y /= v1l;
            v1z /= v1l;

            float v2x = x2 - x1;
            float v2y = y2 - y1;
            float v2z = z2 - z1;

            float v2l = MathF.Sqrt(v2x * v2x + v2y * v2y + v2z * v2z);
            v2x /= v2l;
            v2y /= v2l;
            v2z /= v2l;

            float ddx = v1x - v2x;
            float ddy = v1y - v2y;
            float ddz = v1z - v2z;
            return MathF.Sqrt(ddx * ddx + ddy * ddy + ddz * ddz);
        }

        // return null if failed or the last spot in the path found

        //SearchProgress searchProgress;
        //public SearchProgress SearchProgress
        //{
        //    get
        //    {
        //        return searchProgress;
        //    }
        //}
        private int searchID;

        private float heuristicsFactor = 5f;

        public Spot ClosestSpot;
        public Spot PeekSpot;

        private readonly Stopwatch searchDuration = new();
        private readonly Stopwatch timeSinceProgress = new();

        private Spot Search(Spot fromSpot, Spot destinationSpot, float minHowClose, ILocationHeuristics locationHeuristics)
        {
            searchDuration.Restart();
            timeSinceProgress.Restart();

            float closest = 99999f;
            ClosestSpot = null;

            currentSearchStartSpot = fromSpot;
            searchID++;
            int currentSearchID = searchID;
            //searchProgress = new SearchProgress(fromSpot, destinationSpot, searchID);

            // lowest first queue
            PriorityQueue<Spot, float> prioritySpotQueue = new();
            prioritySpotQueue.Enqueue(fromSpot, fromSpot.GetDistanceTo(destinationSpot) * heuristicsFactor);

            fromSpot.SearchScoreSet(currentSearchID, 0.0f);
            fromSpot.traceBack = null;
            fromSpot.traceBackDistance = 0;

            // A* -ish algorithm
            while (prioritySpotQueue.TryDequeue(out currentSearchSpot, out _))
            {
                if (sleepMSBetweenSpots > 0) { Thread.Sleep(sleepMSBetweenSpots); } // slow down the pathing

                // force the world to be loaded
                _ = triangleWorld.GetChunkAt(currentSearchSpot.Loc.X, currentSearchSpot.Loc.Y);

                if (currentSearchSpot.SearchIsClosed(currentSearchID))
                {
                    continue;
                }
                currentSearchSpot.SearchClose(currentSearchID);

                //update status
                //if (!searchProgress.CheckProgress(currentSearchSpot)) { break; }

                // are we there?

                //float distance = currentSearchSpot.location.GetDistanceTo(destinationSpot.location);
                float distance = Vector3.DistanceSquared(currentSearchSpot.Loc, destinationSpot.Loc);

                if (distance <= minHowClose)
                {
                    return currentSearchSpot; // got there
                }

                if (distance < closest)
                {
                    // spamming as hell
                    //logger.WriteLine($"Closet spot is {distance} from the target");
                    closest = distance;
                    ClosestSpot = currentSearchSpot;
                    PeekSpot = ClosestSpot;
                    timeSinceProgress.Restart();
                }

                if (timeSinceProgress.Elapsed.TotalSeconds > ProgressTimeoutSeconds || searchDuration.Elapsed.TotalSeconds > TimeoutSeconds)
                {
                    logger.LogWarning("search failed, 10 seconds since last progress, returning the closest spot.");
                    return ClosestSpot;
                }

                //Find spots to link to
                CreateSpotsAroundSpot(currentSearchSpot);

                //score each spot around the current search spot and add them to the queue
                foreach (Spot spotLinkedToCurrent in currentSearchSpot.GetPathsToSpots(this))
                {
                    if (spotLinkedToCurrent != null && !spotLinkedToCurrent.IsBlocked() && !spotLinkedToCurrent.SearchIsClosed(currentSearchID))
                    {
                        ScoreSpot(spotLinkedToCurrent, destinationSpot, currentSearchID, locationHeuristics, prioritySpotQueue);
                    }
                }
            }

            //we ran out of spots to search
            //searchProgress.LogStatus("  search failed. ");

            if (ClosestSpot != null && closest < MaximumAllowedRangeFromTarget)
            {
                logger.LogWarning("search failed, returning the closest spot.");
                return ClosestSpot;
            }
            return null;
        }

        private void ScoreSpot(Spot spotLinkedToCurrent, Spot destinationSpot, int currentSearchID, ILocationHeuristics locationHeuristics, PriorityQueue<Spot, float> prioritySpotQueue)
        {
            switch (searchScoreSpot)
            {
                case eSearchScoreSpot.A_Star:
                    ScoreSpot_A_Star(spotLinkedToCurrent, destinationSpot, currentSearchID, locationHeuristics, prioritySpotQueue);
                    break;

                case eSearchScoreSpot.A_Star_With_Model_Avoidance:
                    ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance(spotLinkedToCurrent, destinationSpot, currentSearchID, locationHeuristics, prioritySpotQueue);
                    break;

                case eSearchScoreSpot.OriginalPather:
                default:
                    ScoreSpot_Pather(spotLinkedToCurrent, destinationSpot, currentSearchID, locationHeuristics, prioritySpotQueue);
                    break;
            }
        }

        public void ScoreSpot_A_Star(Spot spotLinkedToCurrent, Spot destinationSpot, int currentSearchID, ILocationHeuristics locationHeuristics, PriorityQueue<Spot, float> prioritySpotQueue)
        {
            //score spot
            float G_Score = currentSearchSpot.traceBackDistance + currentSearchSpot.GetDistanceTo(spotLinkedToCurrent);//  the movement cost to move from the starting point A to a given square on the grid, following the path generated to get there.
            float H_Score = spotLinkedToCurrent.GetDistanceTo2D(destinationSpot) * heuristicsFactor;// the estimated movement cost to move from that given square on the grid to the final destination, point B. This is often referred to as the heuristic, which can be a bit confusing. The reason why it is called that is because it is a guess. We really don�t know the actual distance until we find the path, because all sorts of things can be in the way (walls, water, etc.). You are given one way to calculate H in this tutorial, but there are many others that you can find in other articles on the web.
            float F_Score = G_Score + H_Score;

            if (spotLinkedToCurrent.IsFlagSet(Spot.FLAG_WATER)) { F_Score += 30; }

            if (!spotLinkedToCurrent.SearchScoreIsSet(currentSearchID) || F_Score < spotLinkedToCurrent.SearchScoreGet(currentSearchID))
            {
                // shorter path to here found
                spotLinkedToCurrent.traceBack = currentSearchSpot;
                spotLinkedToCurrent.traceBackDistance = G_Score;
                spotLinkedToCurrent.SearchScoreSet(currentSearchID, F_Score);
                prioritySpotQueue.Enqueue(spotLinkedToCurrent, F_Score);
            }
        }

        public void ScoreSpot_A_Star_With_Model_And_Gradient_Avoidance(Spot spotLinkedToCurrent, Spot destinationSpot, int currentSearchID, ILocationHeuristics locationHeuristics, PriorityQueue<Spot, float> prioritySpotQueue)
        {
            //score spot
            float G_Score = currentSearchSpot.traceBackDistance + currentSearchSpot.GetDistanceTo(spotLinkedToCurrent);//  the movement cost to move from the starting point A to a given square on the grid, following the path generated to get there.
            float H_Score = spotLinkedToCurrent.GetDistanceTo2D(destinationSpot) * heuristicsFactor;// the estimated movement cost to move from that given square on the grid to the final destination, point B. This is often referred to as the heuristic, which can be a bit confusing. The reason why it is called that is because it is a guess. We really don�t know the actual distance until we find the path, because all sorts of things can be in the way (walls, water, etc.). You are given one way to calculate H in this tutorial, but there are many others that you can find in other articles on the web.
            float F_Score = G_Score + H_Score;

            if (spotLinkedToCurrent.IsFlagSet(Spot.FLAG_WATER)) { F_Score += 30; }

            int score = GetTriangleClosenessScore(spotLinkedToCurrent.Loc);
            score += GetTriangleGradiantScore(spotLinkedToCurrent.Loc, gradiantMax);
            F_Score += score * 2;

            if (!spotLinkedToCurrent.SearchScoreIsSet(currentSearchID) || F_Score < spotLinkedToCurrent.SearchScoreGet(currentSearchID))
            {
                // shorter path to here found
                spotLinkedToCurrent.traceBack = currentSearchSpot;
                spotLinkedToCurrent.traceBackDistance = G_Score;
                spotLinkedToCurrent.SearchScoreSet(currentSearchID, F_Score);
                prioritySpotQueue.Enqueue(spotLinkedToCurrent, F_Score);
            }
        }

        public void ScoreSpot_Pather(Spot spotLinkedToCurrent, Spot destinationSpot, int currentSearchID, ILocationHeuristics locationHeuristics, PriorityQueue<Spot, float> prioritySpotQueue)
        {
            //score spots
            float currentSearchSpotScore = currentSearchSpot.SearchScoreGet(currentSearchID);
            float linkedSpotScore = 1E30f;
            float new_score = currentSearchSpotScore + currentSearchSpot.GetDistanceTo(spotLinkedToCurrent) + TurnCost(currentSearchSpot, spotLinkedToCurrent);

            if (locationHeuristics != null) { new_score += locationHeuristics.Score(currentSearchSpot.Loc.X, currentSearchSpot.Loc.Y, currentSearchSpot.Loc.Z); }
            if (spotLinkedToCurrent.IsFlagSet(Spot.FLAG_WATER)) { new_score += 30; }

            if (spotLinkedToCurrent.SearchScoreIsSet(currentSearchID))
            {
                linkedSpotScore = spotLinkedToCurrent.SearchScoreGet(currentSearchID);
            }

            if (new_score < linkedSpotScore)
            {
                // shorter path to here found
                spotLinkedToCurrent.traceBack = currentSearchSpot;
                spotLinkedToCurrent.SearchScoreSet(currentSearchID, new_score);
                prioritySpotQueue.Enqueue(spotLinkedToCurrent, (new_score + spotLinkedToCurrent.GetDistanceTo(destinationSpot) * heuristicsFactor));
            }
        }

        public void CreateSpotsAroundSpot(Spot currentSearchSpot)
        {
            CreateSpotsAroundSpot(currentSearchSpot, currentSearchSpot.IsFlagSet(Spot.FLAG_MPQ_MAPPED));
        }

        public void CreateSpotsAroundSpot(Spot currentSearchSpot, bool mapped)
        {
            if (!mapped)
            {
                //mark as mapped
                currentSearchSpot.SetFlag(Spot.FLAG_MPQ_MAPPED, true);

                float PI = (float)Math.PI;

                //loop through the spots in a circle around the current search spot
                for (float radianAngle = 0; radianAngle < PI * 2; radianAngle += PI / 8)
                {
                    //calculate the location of the spot at the angle
                    float nx = currentSearchSpot.Loc.X + (MathF.Sin(radianAngle) * WantedStepLength);// *0.8f;
                    float ny = currentSearchSpot.Loc.Y + (MathF.Cos(radianAngle) * WantedStepLength);// *0.8f;

                    PeekSpot = new Spot(nx, ny, currentSearchSpot.Loc.Z);

                    //find the spot at this location, stop if there is one already
                    if (GetSpot(nx, ny, currentSearchSpot.Loc.Z) != null) { continue; } //found a spot so don't create a new one

                    // TODO:
                    //see if there is a close spot, stop if there is
                    if (FindClosestSpot(new(nx, ny, currentSearchSpot.Loc.Z), MinStepLength) != null)
                    {
                        continue;
                    } // TODO: this is slow

                    // check we can stand at this new location
                    if (!triangleWorld.FindStandableAt(nx, ny, currentSearchSpot.Loc.Z - WantedStepLength * .75f, currentSearchSpot.Loc.Z + WantedStepLength * .75f, out float new_z, out int flags, toonHeight, toonSize))
                    {
                        continue;
                    }

                    // TODO: 
                    //see if a spot already exists at this location
                    if (FindClosestSpot(new(nx, ny, new_z), MinStepLength) != null)
                    {
                        continue;
                    }

                    //if the step is blocked then stop
                    if (triangleWorld.IsStepBlocked(currentSearchSpot.Loc.X, currentSearchSpot.Loc.Y, currentSearchSpot.Loc.Z, nx, ny, new_z, toonHeight, toonSize, null))
                    {
                        continue;
                    }

                    //create a new spot and connect it
                    Spot newSpot = AddAndConnectSpot(new Spot(nx, ny, new_z));
                    //PeekSpot = newSpot;

                    //check flags return by triangleWorld.FindStandableA
                    if ((flags & ChunkedTriangleCollection.TriangleFlagDeepWater) != 0)
                    {
                        newSpot.SetFlag(Spot.FLAG_WATER, true);
                    }
                    if (((flags & ChunkedTriangleCollection.TriangleFlagModel) != 0) || ((flags & ChunkedTriangleCollection.TriangleFlagObject) != 0))
                    {
                        newSpot.SetFlag(Spot.FLAG_INDOORS, true);
                    }
                    if (triangleWorld.IsCloseToModel(newSpot.Loc.X, newSpot.Loc.Y, newSpot.Loc.Z, IsCloseToModelRange))
                    {
                        newSpot.SetFlag(Spot.FLAG_CLOSETOMODEL, true);
                    }
                }
            }
        }

        private Spot lastCurrentSearchSpot;

        public List<Spot> CurrentSearchPath()
        {
            if (lastCurrentSearchSpot == currentSearchSpot)
            {
                return null;
            }

            lastCurrentSearchSpot = currentSearchSpot;
            return FollowTraceBack(currentSearchStartSpot, currentSearchSpot);
        }

        private static List<Spot> FollowTraceBack(Spot from, Spot to)
        {
            List<Spot> path = new();
            Spot backtrack = to;
            while (backtrack != from && backtrack != null)
            {
                path.Insert(0, backtrack);
                backtrack = backtrack.traceBack;
            }
            path.Insert(0, from);
            return path;
        }

        public bool IsUnderwaterOrInAir(Vector3 l)
        {
            int flags;
            float z;
            if (triangleWorld.FindStandableAt(l.X, l.Y, l.Z - 50.0f, l.Z + 5.0f, out z, out flags, toonHeight, toonSize))
            {
                if ((flags & ChunkedTriangleCollection.TriangleFlagDeepWater) != 0)
                    return true;
                else
                    return false;
            }
            //return true;
            return false;
        }

        /*
        public bool IsUnderwaterOrInAir(Spot s)
        {
            return IsUnderwaterOrInAir(s.GetLocation());
        }
        */

        public static bool IsInABuilding(Vector3 l)
        {
            //int flags;
            //float z;
            //if (triangleWorld.FindStandableAt(l.X, l.Y, l.Z +12.0f, l.Z + 50.0f, out z, out  flags, toonHeight, toonSize))
            //{
            //   return true;
            //    //return false;
            //}
            //return triangleWorld.IsCloseToModel(l.X,l.Y,l.Z,1);
            //return true;
            return false;
        }

        public Path LastPath;

        private Path CreatePath(Spot from, Spot to, float minHowClose, ILocationHeuristics locationHeuristics)
        {
            Spot newTo = Search(from, to, minHowClose, locationHeuristics);
            if (newTo != null)
            {
                if (newTo.GetDistanceTo(to) <= MaximumAllowedRangeFromTarget)
                {
                    List<Spot> path = FollowTraceBack(from, newTo);
                    LastPath = new Path(path);
                    return LastPath;
                }
                else
                {
                    logger.LogWarning($"Closest spot is too far from target. {newTo.GetDistanceTo(to)}>{MaximumAllowedRangeFromTarget}");
                    return null;
                }
            }
            return null;
        }

        private Vector3 GetBestLocations(Vector3 location)
        {
            float newZ = 0;
            int flags = 0;
            bool getOut = false;
            float[] a = new float[] { 0, -1f, -0.5f, 0.5f, 1 };

            foreach (var z in a)
            {
                if (getOut) break;
                foreach (var x in a)
                {
                    if (getOut) break;
                    foreach (var y in a)
                    {
                        if (getOut) break;
                        if (triangleWorld.FindStandableAt(
                            location.X, location.Y,
                            location.Z + 1 - WantedStepLength * .75f, location.Z + 1 + WantedStepLength * .75f,
                            out newZ, out flags, toonHeight, toonSize))
                            getOut = true;
                    }
                }
            }
            if (Math.Abs(newZ - location.Z) > 5) { newZ = location.Z; }

            return new(location.X, location.Y, newZ);
        }

        private readonly Stopwatch sw = new();

        public Path CreatePath(Vector3 fromLoc, Vector3 toLoc, float howClose, ILocationHeuristics locationHeuristics)
        {
            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"Creating Path from {fromLoc} to {toLoc}");

            sw.Restart();

            fromLoc = GetBestLocations(fromLoc);
            toLoc = GetBestLocations(toLoc);

            Spot from = FindClosestSpot("fromLoc", fromLoc, MinStepLength);
            Spot to = FindClosestSpot("toLoc", toLoc, MinStepLength);

            if (from == null)
            {
                from = AddAndConnectSpot(new Spot(fromLoc));
            }
            if (to == null)
            {
                to = AddAndConnectSpot(new Spot(toLoc));
            }

            Path rawPath = CreatePath(from, to, howClose, locationHeuristics);

            if (rawPath != null && paint != null)
            {
                Vector3 prev = Vector3.Zero;
                for (int i = 0; i < rawPath.Count; i++)
                {
                    Vector3 l = rawPath[i];
                    paint.AddBigMarker(l.X, l.Y, l.Z);
                    if (prev != Vector3.Zero)
                    {
                        paint.PaintPath(l.X, l.Y, l.Z, prev.X, prev.Y, prev.Z);
                    }
                    prev = l;
                }
            }

            if (logger.IsEnabled(LogLevel.Trace))
                logger.LogTrace($"CreatePath took {sw.ElapsedMilliseconds}ms");

            if (rawPath == null)
            {
                return null;
            }
            else
            {
                Vector3 last = rawPath.GetLast;
                //if (last.GetDistanceTo(toLoc) > 1.0) 
                if (Vector3.DistanceSquared(last, toLoc) > 1.0)
                {
                    rawPath.Add(toLoc);
                }
            }
            LastPath = rawPath;
            return rawPath;
        }
    }
}