using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PPather;
using PPather.Data;
using PPather.Graph;
using SharedLib;
using System.Collections.Generic;
using System.Linq;
using WowTriangles;
using System.Numerics;
using SharedLib.Data;

namespace PathingAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PPatherController : ControllerBase
    {
        private readonly PPatherService service;
        private readonly ILogger logger;

        private static bool isBusy;
        private static bool initialised;

        public PPatherController(PPatherService service, ILogger logger)
        {
            this.service = service;
            this.logger = logger;
        }

        /// <summary>
        /// Allows a route to be calculated from one point to another using minimap coords.
        /// </summary>
        /// <remarks>
        /// uimap1 and uimap2 are the map ids. See https://wow.gamepedia.com/API_C_Map.GetBestMapForUnit
        ///
        ///     /dump C_Map.GetBestMapForUnit("player")
        ///
        ///     Dump: value=_Map.GetBestMapForUnit("player")
        ///     [1]=1451
        ///
        /// x and y are the map coordinates for the zone (same as the mini map). See https://wowwiki.fandom.com/wiki/API_GetPlayerMapPosition
        ///
        ///     local posX, posY = GetPlayerMapPosition("player");
        /// </remarks>
        /// <param name="uimap1">from map e.g. 1451</param>
        /// <param name="x1">from X e.g. 46.8</param>
        /// <param name="y1">from Y e.g. 54.2</param>
        /// <param name="uimap2">to map e.g. 1451</param>
        /// <param name="x2">to X e.g. 51.2</param>
        /// <param name="y2">to Y e.g. 38.9</param>
        /// <returns>A list of x,y,z and mapid</returns>
        [HttpGet("MapRoute")]
        [Produces("application/json")]
        public JsonResult MapRoute(int uimap1, float x1, float y1, int uimap2, float x2, float y2)
        {
            isBusy = true;
            service.SetLocations(service.ToWorld(uimap1, x1, y1), service.ToWorld(uimap2, x2, y2));
            var path = service.DoSearch(PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);
            if (path != null)
            {
                service.Save();
            }

            var localPositions = path == null ?
                new List<Vector3>() :
                path.locations.Select(s => service.ToLocal(s, (int)service.SearchFrom.Value.W, uimap1));

            isBusy = false;

            return new JsonResult(localPositions);
        }

        /// <summary>
        /// Allows a route to be calculated from one point to another using world coords.
        /// e.g. -896, -3770, 11, (Barrens,Rachet) to -441, -2596, 96, (Barrens,Crossroads,Barrens)
        /// </summary>
        /// <param name="x1">from X e.g. -896</param>
        /// <param name="y1">from Y e.g. -3770</param>
        /// <param name="z1">from Y e.g. 11</param>
        /// <param name="x2">to X e.g. -441</param>
        /// <param name="y2">to Y e.g. -2596</param>
        /// <param name="z2">from Y e.g. 96</param>
        /// <param name="mapid">from ["Azeroth=0", "Kalimdor=1", "Northrend=??", "Expansion01=530"] e.g. Kalimdor</param>
        /// <returns>A list of x,y,z</returns>
        [HttpGet("WorldRoute")]
        [Produces("application/json")]
        public JsonResult WorldRoute(float x1, float y1, float z1, float x2, float y2, float z2, float mapid)
        {
            isBusy = true;
            service.SetLocations(new(x1, y1, z1, mapid), new(x2, y2, z2, mapid));
            var path = service.DoSearch(PathGraph.eSearchScoreSpot.A_Star_With_Model_Avoidance);
            if (path != null)
            {
                service.Save();
            }
            isBusy = false;
            return new JsonResult(path);
        }

        /// <summary>
        /// Draws lines on the landscape
        /// Used by the client to show the grind path and the spirit healer path.
        /// </summary>
        /// <param name="lines"></param>
        /// <returns></returns>
        [HttpPost("Drawlines")]
        [Produces("application/json")]
        public bool Drawlines(List<LineArgs> lines)
        {
            if (isBusy) { return false; }
            isBusy = true;

            lines.ForEach(l =>
            {
                var locations = CreateLocations(l);
                service.OnLinesAdded?.Invoke(new LinesEventArgs(l.Name, locations, l.Colour));
            });

            isBusy = false;
            initialised = true;
            return true;
        }

        /// <summary>
        /// Draws spheres on the landscape.
        ///  Used by the client to show the player's location.
        /// </summary>
        /// <param name="sphere"></param>
        /// <returns></returns>
        [HttpPost("DrawSphere")]
        [Produces("application/json")]
        public bool DrawSphere(SphereArgs sphere)
        {
            if (isBusy || !initialised) { return false; }
            isBusy = true;

            Vector4 location = service.ToWorld(sphere.MapId, sphere.Spot.X, sphere.Spot.Y);
            service.OnSphereAdded?.Invoke(new SphereEventArgs(sphere.Name, location, sphere.Colour));

            isBusy = false;
            return true;
        }

        private List<Vector4> CreateLocations(LineArgs lines)
        {
            List<Vector4> result = new();
            foreach (var s in lines.Spots)
            {
                result.Add(service.ToWorld(lines.MapId, s.X, s.Y, s.Z));
            }
            return result;
        }

        /// <summary>
        /// Returns true to indicate that the server is listening.
        /// </summary>
        /// <returns></returns>
        [HttpGet("SelfTest")]
        [Produces("application/json")]
        public JsonResult SelfTest()
        {
            var mpqFiles = MPQTriangleSupplier.GetArchiveNames(DataConfig.Load());

            int countOfMPQFiles = mpqFiles.Count(f => System.IO.File.Exists(f));
            if (countOfMPQFiles == 0)
            {
                logger.LogInformation("Some of these MPQ files should exist!");
                mpqFiles.ToList().ForEach(l => logger.LogInformation(l));
                logger.LogInformation("No MPQ files found, refer to the Readme to download them.");
            }
            else
            {
                logger.LogInformation("MPQ files exist.");
            }

            return new JsonResult(countOfMPQFiles > 0);
        }

        [HttpPost("DrawPathTest")]
        [Produces("application/json")]
        public bool DrawPathTest()
        {
            float mapId = ContinentDB.NameToId["Azeroth"]; // Azeroth
            List<float[]> coords = new()
            {
                new float[] { -5609.00f,-479.00f,397.49f },
                new float[] { -5609.33f,-444.00f,405.22f },
                new float[] { -5609.33f,-438.40f,406.02f },
                new float[] { -5608.80f,-427.73f,404.69f },
                new float[] { -5608.80f,-426.67f,404.69f },
                new float[] { -5610.67f,-405.33f,402.02f },
                new float[] { -5635.20f,-368.00f,392.15f },
                new float[] { -5645.07f,-362.67f,385.49f },
                new float[] { -5646.40f,-362.13f,384.69f },
                new float[] { -5664.27f,-355.73f,378.29f },
                new float[] { -5696.00f,-362.67f,366.02f },
                new float[] { -5758.93f,-385.87f,366.82f },
                new float[] { -5782.00f,-394.00f,366.09f }
            };

            if (isBusy) { return false; }
            isBusy = true;

            service.DrawPath(mapId, coords);

            isBusy = false;
            return true;
        }
    }
}