using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

using Core.Database;

using SharedLib;

using WowheadDB;

namespace Core
{
    public readonly struct RouteInfoPoi
    {
        public readonly Vector3 MapLoc;
        public readonly string Name;
        public readonly string Color;
        public readonly float Radius;

        public RouteInfoPoi(NPC npc, string color)
        {
            MapLoc = npc.MapCoords[0];
            Name = npc.name;
            Color = color;
            Radius = 1;
        }

        public RouteInfoPoi(Vector3 mapLoc, string name, string color, float radius)
        {
            MapLoc = mapLoc;
            Name = name;
            Color = color;
            Radius = radius;
        }
    }

    public sealed class RouteInfo : IDisposable
    {
        public Vector3[] Route { get; private set; }

        public Vector3[] RouteToWaypoint => pathedRoutes.Any()
                    ? pathedRoutes.OrderByDescending(x => x.LastActive).First().PathingRoute()
                    : Array.Empty<Vector3>();

        private readonly IEnumerable<IRouteProvider> pathedRoutes;
        private readonly AreaDB areaDB;
        private readonly PlayerReader playerReader;
        private readonly WorldMapAreaDB worldmapAreaDB;

        public List<RouteInfoPoi> PoiList { get; } = new();

        private float min;
        private float diff;

        private float addY;
        private float addX;

        private int margin;
        private int canvasSize;

        private float pointToGrid;

        private const int dSize = 2;

        public RouteInfo(Vector3[] route, IEnumerable<IRouteProvider> pathedRoutes, PlayerReader playerReader, AreaDB areaDB, WorldMapAreaDB worldmapAreaDB)
        {
            this.Route = route;
            this.pathedRoutes = pathedRoutes;
            this.playerReader = playerReader;
            this.areaDB = areaDB;
            this.worldmapAreaDB = worldmapAreaDB;

            this.areaDB.Changed += OnZoneChanged;
            OnZoneChanged();

            CalculateDiffs();
        }

        public void Dispose()
        {
            areaDB.Changed -= OnZoneChanged;
        }

        public void UpdateRoute(Vector3[] newMapRoute)
        {
            Route = newMapRoute;

            foreach (IEditedRouteReceiver receiver in pathedRoutes.OfType<IEditedRouteReceiver>())
            {
                receiver.ReceivePath(Route);
            }
        }

        public void SetMargin(int margin)
        {
            this.margin = margin;
            CalculatePointToGrid();
        }

        public void SetCanvasSize(int size)
        {
            this.canvasSize = size;
            CalculatePointToGrid();
        }

        public void CalculatePointToGrid()
        {
            pointToGrid = ((float)canvasSize - (margin * 2)) / diff;
            CalculateDiffs();
        }

        public int ToCanvasPointX(float value)
        {
            return (int)(margin + ((value + addX - min) * pointToGrid));
        }

        public int ToCanvasPointY(float value)
        {
            return (int)(margin + ((value + addY - min) * pointToGrid));
        }

        public float DistanceToGrid(int value)
        {
            return value / 100f * pointToGrid;
        }

        private void OnZoneChanged()
        {
            if (areaDB.CurrentArea == null)
                return;

            PoiList.Clear();
            /*
            // Visualize the zone pois
            for (int i = 0; i < areaDB.CurrentArea.vendor?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.vendor[i];
                PoiList.Add(new RouteInfoPoi(npc, "green"));
            }

            for (int i = 0; i < areaDB.CurrentArea.repair?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.repair[i];
                PoiList.Add(new RouteInfoPoi(npc, "purple"));
            }

            for (int i = 0; i < areaDB.CurrentArea.innkeeper?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.innkeeper[i];
                PoiList.Add(new RouteInfoPoi(npc, "blue"));
            }

            for (int i = 0; i < areaDB.CurrentArea.flightmaster?.Count; i++)
            {
                NPC npc = areaDB.CurrentArea.flightmaster[i];
                PoiList.Add(new RouteInfoPoi(npc, "orange"));
            }
            */
        }

        private void CalculateDiffs()
        {
            /*
            TODO> route disappears after time?
            Vector3[] pois = PoiList.Select(p => p.MapLoc).ToArray();
            Vector3[] route = Route.ToArray();
            Vector3[] navigation = RouteToWaypoint.ToArray();
            worldmapAreaDB.ToMap_FlipXY(playerReader.UIMapId.Value, ref navigation);

            Vector3[] allPoints = new Vector3[route.Length + navigation.Length + pois.Length];
            Array.Copy(Route, allPoints, route.Length);
            Array.ConstrainedCopy(navigation, 0, allPoints, route.Length, navigation.Length);
            Array.ConstrainedCopy(pois, 0, allPoints, route.Length + navigation.Length, pois.Length);
            allPoints[^1] = playerReader.MapPos;
            */

            var allPoints = Route.ToList();

            Vector3[] navigation = RouteToWaypoint.ToArray();
            worldmapAreaDB.ToMap_FlipXY(playerReader.UIMapId.Value, ref navigation);
            allPoints.AddRange(navigation);

            var pois = PoiList.Select(p => p.MapLoc);
            allPoints.AddRange(pois);

            allPoints.Add(playerReader.MapPos);

            float maxX = allPoints.Max(s => s.X);
            float minX = allPoints.Min(s => s.X);
            float diffX = maxX - minX;

            float maxY = allPoints.Max(s => s.Y);
            float minY = allPoints.Min(s => s.Y);
            float diffY = maxY - minY;

            this.addY = 0;
            this.addX = 0;

            if (diffX > diffY)
            {
                this.addY = minX - minY;
                this.min = minX;
                this.diff = diffX;
            }
            else
            {
                this.addX = minY - minX;
                this.min = minY;
                this.diff = diffY;
            }
        }

        public string RenderPathLines(Vector3[] path)
        {
            StringBuilder sb = new();
            for (int i = 0; i < path.Length - 1; i++)
            {
                Vector3 p1 = path[i];
                Vector3 p2 = path[i + 1];
                sb.AppendLine($"<line x1 = '{ToCanvasPointX(p1.X)}' y1 = '{ToCanvasPointY(p1.Y)}' x2 = '{ToCanvasPointX(p2.X)}' y2 = '{ToCanvasPointY(p2.Y)}' />");
            }
            return sb.ToString();
        }

        private const string first = "<br><b>First</b>";
        private const string last = "<br><b>Last</b>";

        public string RenderPathPoints(Vector3[] path)
        {
            StringBuilder sb = new();
            for (int i = 0; i < path.Length; i++)
            {
                Vector3 p = path[i];
                float x = p.X;
                float y = p.Y;
                sb.AppendLine($"<circle onmousedown=\"pointClick(evt,{x},{y},{i});\"  onmousemove=\"showTooltip(evt,'{x},{y}{(i == 0 ? first : i == path.Length - 1 ? last : string.Empty)}');\" onmouseout=\"hideTooltip();\"  cx = '{ToCanvasPointX(p.X)}' cy = '{ToCanvasPointY(p.Y)}' r = '{dSize}' />");
            }
            return sb.ToString();
        }

        public Vector3 NextPoint()
        {
            var route = pathedRoutes.OrderByDescending(s => s.LastActive).FirstOrDefault();
            if (route == null || !route.HasNext())
                return Vector3.Zero;

            return route.NextMapPoint();
        }

        public string RenderNextPoint()
        {
            Vector3 pt = NextPoint();
            if (pt == Vector3.Zero)
                return string.Empty;

            return $"<circle cx = '{ToCanvasPointX(pt.X)}' cy = '{ToCanvasPointY(pt.Y)}'r = '{dSize + 1}' />";
        }

        public string DeathImage(Vector3 pt)
        {
            var size = this.canvasSize / 25;
            return pt == Vector3.Zero ? string.Empty : $"<image href = '_content/Frontend/death.svg' x = '{ToCanvasPointX(pt.X) - size / 2}' y = '{ToCanvasPointY(pt.Y) - size / 2}' height='{size}' width='{size}' />";
        }

        public string DrawPoi(RouteInfoPoi poi)
        {
            return $"<circle onmousemove=\"showTooltip(evt, '{poi.Name}<br/>{poi.MapLoc.X},{poi.MapLoc.Y}');\" onmouseout=\"hideTooltip();\" cx='{ToCanvasPointX(poi.MapLoc.X)}' cy='{ToCanvasPointY(poi.MapLoc.Y)}' r='{(poi.Radius == 1 ? dSize : DistanceToGrid((int)poi.Radius))}' " + (poi.Radius == 1 ? $"fill='{poi.Color}'" : $"stroke='{poi.Color}' stroke-width='1' fill='none'") + " />";
        }
    }
}