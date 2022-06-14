using System.Drawing;
using Core.Goals;
using SharedLib.NpcFinder;
using Microsoft.Extensions.Logging;
using SharedLib;
using System.Threading;
using System.Text;
using System.Diagnostics;

namespace CoreTests
{
    public class Test_NpcNameFinder
    {
        private readonly ILogger logger;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly RectProvider rectProvider;
        private readonly BitmapCapturer capturer;

        private readonly bool saveImage;
        private readonly Stopwatch stopwatch = new();
        private readonly StringBuilder stringBuilder = new();

        private readonly Graphics paint;
        private readonly Bitmap paintBitmap;
        private readonly Font font = new("Arial", 10);
        private readonly SolidBrush brush = new(Color.White);
        private readonly Pen whitePen = new(Color.White, 1);

        public Test_NpcNameFinder(ILogger logger, NpcNames types, bool saveImage)
        {
            this.logger = logger;
            this.saveImage = saveImage;

            rectProvider = new();
            rectProvider.GetRectangle(out Rectangle rect);
            capturer = new(rect);

            npcNameFinder = new(logger, capturer, new AutoResetEvent(false));

            MockWoWProcess mockWoWProcess = new();
            MockWoWScreen mockWoWScreen = new();
            npcNameTargeting = new(logger, new(), mockWoWScreen, npcNameFinder, mockWoWProcess);

            npcNameFinder.ChangeNpcType(types);

            if (saveImage)
            {
                paintBitmap = capturer.Bitmap;
                paint = Graphics.FromImage(paintBitmap);
            }
        }

        public void Execute()
        {
            stopwatch.Restart();
            capturer.Capture();
            logger.LogInformation($"Capture: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            npcNameFinder.Update();
            logger.LogInformation($"Update: {stopwatch.ElapsedMilliseconds}ms");

            if (saveImage)
            {
                SaveImage();
            }

            stringBuilder.Length = 0;

            if (npcNameFinder.Npcs.Count > 0)
                stringBuilder.AppendLine();

            npcNameFinder.Npcs.ForEach(n =>
            {
                stringBuilder.Append($"{npcNameFinder.Npcs.IndexOf(n),2}");
                stringBuilder.Append(" -> rect=");
                stringBuilder.Append(n.Rect);
                stringBuilder.Append(" ClickPoint=");
                stringBuilder.AppendLine($"{{{n.ClickPoint.X,4},{n.ClickPoint.Y,4}}}");
            });

            logger.LogInformation(stringBuilder.ToString());
        }

        private void SaveImage()
        {
            if (npcNameFinder.Npcs.Count > 0)
            {
                paint.DrawRectangle(whitePen, npcNameFinder.Area);

                npcNameFinder.Npcs.ForEach(n =>
                {
                    foreach (var l in npcNameTargeting.locTargetingAndClickNpc)
                    {
                        paint.DrawEllipse(whitePen, l.X + n.ClickPoint.X, l.Y + n.ClickPoint.Y, 5, 5);
                    }
                });

                npcNameFinder.Npcs.ForEach(n => paint.DrawRectangle(whitePen, n.Rect));
                npcNameFinder.Npcs.ForEach(n => paint.DrawString(npcNameFinder.Npcs.IndexOf(n).ToString(), font, brush, new PointF(n.Left - 20f, n.Top)));
            }

            paintBitmap.Save("target_names.png");
        }
    }
}
