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
    public class Test_NpcNameFinderTarget
    {
        private readonly ILogger logger;
        private readonly NpcNameFinder npcNameFinder;
        private readonly NpcNameTargeting npcNameTargeting;
        private readonly RectProvider rectProvider;
        private readonly BitmapCapturer capturer;

        private readonly bool saveImage;
        private readonly Stopwatch stopwatch = new();

        public Test_NpcNameFinderTarget(ILogger logger, bool saveImage)
        {
            this.logger = logger;
            this.saveImage = saveImage;

            MockWoWProcess mockWoWProcess = new MockWoWProcess();
            rectProvider = new RectProvider();
            rectProvider.GetRectangle(out var rect);
            capturer = new BitmapCapturer(rect);

            npcNameFinder = new NpcNameFinder(logger, capturer, new AutoResetEvent(false));
            npcNameTargeting = new NpcNameTargeting(logger, new(), npcNameFinder, mockWoWProcess);

            //npcNameFinder.ChangeNpcType(NpcNames.Enemy | NpcNames.Neutral);
            npcNameFinder.ChangeNpcType(NpcNames.Friendly);
        }

        public void Execute()
        {
            stopwatch.Restart();
            capturer.Capture();
            stopwatch.Stop();
            logger.LogInformation($"Capture: {stopwatch.ElapsedMilliseconds}ms");

            stopwatch.Restart();
            npcNameFinder.Update();
            stopwatch.Stop();
            logger.LogInformation($"Update: {stopwatch.ElapsedMilliseconds}ms");

            if (saveImage)
            {
                var bitmap = capturer.Bitmap;
                var graphics = Graphics.FromImage(bitmap);
                Font font = new Font("Arial", 10);
                SolidBrush brush = new SolidBrush(Color.White);

                if (npcNameFinder.Npcs.Count > 0)
                {
                    using (var whitePen = new Pen(Color.White, 1))
                    {
                        graphics.DrawRectangle(whitePen, npcNameFinder.Area);

                        npcNameFinder.Npcs.ForEach(n =>
                        {
                            npcNameTargeting.locTargetingAndClickNpc.ForEach(l =>
                            {
                                graphics.DrawEllipse(whitePen, l.X + n.ClickPoint.X, l.Y + n.ClickPoint.Y, 5, 5);
                            });
                        });

                        npcNameFinder.Npcs.ForEach(n => graphics.DrawRectangle(whitePen, new Rectangle(n.Min, new Size(n.Width, n.Height))));
                        npcNameFinder.Npcs.ForEach(n => graphics.DrawString(npcNameFinder.Npcs.IndexOf(n).ToString(), font, brush, new PointF(n.Min.X - 20f, n.Min.Y)));
                    }
                }

                brush.Dispose();
                font.Dispose();
                graphics.Dispose();

                bitmap.Save("target_names.png");
            }

            StringBuilder sb = new();
            npcNameFinder.Npcs.ForEach(n =>
            {
                sb.AppendLine($"{npcNameFinder.Npcs.IndexOf(n),2} -> rect={new Rectangle(n.Min.X, n.Min.Y, n.Width, n.Height)} ClickPoint={{{n.ClickPoint.X,4},{n.ClickPoint.Y,4}}}");
            });
            logger.LogInformation($"\n{sb}\n");
        }
    }
}
