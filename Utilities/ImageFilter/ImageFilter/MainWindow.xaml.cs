using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using Color = System.Drawing.Color;

using Serilog;
using Serilog.Extensions.Logging;
using SharedLib;
using SharedLib.NpcFinder;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using System.Windows.Media;
using Point = System.Drawing.Point;

namespace ImageFilter
{
    public partial class MainWindow : Window
    {
        private readonly Timer timer;
        private int redwidth = 10;

        private readonly Microsoft.Extensions.Logging.ILogger logger;
        private readonly BitmapCapturer capturer;
        private readonly NpcNameFinder npcNameFinder;

        private readonly Bitmap bitmap;
        private readonly Graphics graphics;

        private readonly Font drawFont = new Font("Arial", 10);
        private readonly SolidBrush drawBrush = new SolidBrush(Color.White);
        private readonly System.Drawing.Pen whitePen = new(Color.White, 1);
        private readonly System.Drawing.Pen greyPen = new(Color.Gray, 1);

        private readonly Stopwatch stopwatch = new();

        private bool firstRender;
        private const bool showAdds = false;

        public MainWindow()
        {
            var logConfig = new LoggerConfiguration()
                .WriteTo.Debug()
                .CreateLogger();

            Log.Logger = logConfig;
            logger = new SerilogLoggerProvider(Log.Logger).CreateLogger(nameof(MainWindow));

            var rect = new Rectangle(0, 0, 1920, 1080);
            capturer = new(rect);

            npcNameFinder = new NpcNameFinder(logger, capturer, new(false));
            npcNameFinder.ChangeNpcType(NpcNames.Neutral | NpcNames.Friendly);

            bitmap = new Bitmap(capturer.Rect.Width, capturer.Rect.Height);
            graphics = Graphics.FromImage(bitmap);

            timer = new Timer(1000);
            timer.Elapsed += OnTimedEvent;
            timer.AutoReset = true;
            timer.Enabled = false;

            this.Initialized += MainWindow_Initialized;

            InitializeComponent();
            firstRender = true;
            InitSliders();
        }

        private void MainWindow_Initialized(object sender, EventArgs e)
        {
            timer.Enabled = true;
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            stopwatch.Restart();

            capturer.Capture();
            npcNameFinder.Update();

            graphics.DrawImage(capturer.Bitmap, PointF.Empty);

            if (npcNameFinder.Npcs.Count > 0)
            {
                graphics.DrawRectangle(whitePen, npcNameFinder.Area);

                if (showAdds)
                {
                    var fist = npcNameFinder.Npcs[0];
                    var Area = npcNameFinder.Area;

                    // target area
                    graphics.DrawLine(whitePen, new Point(fist.screenMid - fist.screenTargetBuffer, Area.Top), new Point(fist.screenMid - fist.screenTargetBuffer, Area.Bottom));
                    graphics.DrawLine(whitePen, new Point(fist.screenMid + fist.screenTargetBuffer, Area.Top), new Point(fist.screenMid + fist.screenTargetBuffer, Area.Bottom));

                    // adds area
                    graphics.DrawLine(greyPen, new Point(fist.screenMid - fist.screenAddBuffer, Area.Top), new Point(fist.screenMid - fist.screenAddBuffer, Area.Bottom));
                    graphics.DrawLine(greyPen, new Point(fist.screenMid + fist.screenAddBuffer, Area.Top), new Point(fist.screenMid + fist.screenAddBuffer, Area.Bottom));

                }

                npcNameFinder.Npcs.ForEach(n =>
                {
                    graphics.DrawEllipse(whitePen, n.ClickPoint.X, n.ClickPoint.Y, 5, 5);
                });

                npcNameFinder.Npcs.ForEach(n => graphics.DrawRectangle(showAdds ? (n.IsAdd ? greyPen : whitePen) : whitePen, n.Rect));
                npcNameFinder.Npcs.ForEach(n => graphics.DrawString(npcNameFinder.Npcs.IndexOf(n).ToString(), drawFont, drawBrush, new PointF(n.Left - 20f, n.Top)));
            }

            Application.Current.Dispatcher.Invoke(Update);
        }

        private void Update()
        {
            this.Screenshot.Source = ImageSourceForBitmap(bitmap);
            Duration.Content = "Duration: " + stopwatch.ElapsedMilliseconds + "ms";
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                redwidth = int.Parse((sender as TextBox).Text);
            }
            catch
            {
            }
        }

        [DllImport("gdi32.dll", EntryPoint = "DeleteObject")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool DeleteObject([In] IntPtr hObject);

        private static ImageSource ImageSourceForBitmap(Bitmap bmp)
        {
            var handle = bmp.GetHbitmap();
            try
            {
                ImageSource newSource = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                DeleteObject(handle);
                return newSource;
            }
            catch
            {
                DeleteObject(handle);
                return null;
            }
        }


        #region Cursor Input 

        public static void SetCursorPosition(System.Drawing.Point position)
        {
            //SetCursorPos(position.X, position.Y);
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetCursorPos(int x, int y);

        #endregion


        #region sliders

        private void InitSliders()
        {
            npcPosYOffset.Value = npcNameFinder.npcPosYOffset;
            lnpcPosYOffset.Content = "npcPosYOffset: " + npcNameFinder.npcPosYOffset;

            npcPosYHeightMul.Value = npcNameFinder.npcPosYHeightMul;
            lnpcPosYHeightMul.Content = "npcPosYHeightMul: " + npcNameFinder.npcPosYHeightMul;

            npcNameMaxWidth.Value = npcNameFinder.npcNameMaxWidth;
            lnpcNameMaxWidth.Content = "npcNameMaxWidth: " + npcNameFinder.npcNameMaxWidth;

            LinesOfNpcMinLength.Value = npcNameFinder.LinesOfNpcMinLength;
            lLinesOfNpcMinLength.Content = "LinesOfNpcMinLength: " + npcNameFinder.LinesOfNpcMinLength;

            LinesOfNpcLengthDiff.Value = npcNameFinder.LinesOfNpcLengthDiff;
            lLinesOfNpcLengthDiff.Content = "LinesOfNpcLengthDiff: " + npcNameFinder.LinesOfNpcLengthDiff;

            DetermineNpcsHeightOffset1.Value = npcNameFinder.DetermineNpcsHeightOffset1;
            lDetermineNpcsHeightOffset1.Content = "DetermineNpcsHeightOffset1: " + npcNameFinder.DetermineNpcsHeightOffset1;

            DetermineNpcsHeightOffset2.Value = npcNameFinder.DetermineNpcsHeightOffset2;
            lDetermineNpcsHeightOffset2.Content = "DetermineNpcsHeightOffset2: " + npcNameFinder.DetermineNpcsHeightOffset2;

            incX.Value = npcNameFinder.incX;
            lincX.Content = "incX: " + npcNameFinder.incX;

            incY.Value = npcNameFinder.incY;
            lincY.Content = "incY: " + npcNameFinder.incY;
        }

        public void npcPosYOffset_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.npcPosYOffset = (int)e.NewValue;
            lnpcPosYOffset.Content = "npcPosYOffset: " + npcNameFinder.npcPosYOffset;
        }

        public void npcPosYHeightMul_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.npcPosYHeightMul = (int)e.NewValue;
            lnpcPosYHeightMul.Content = "npcPosYHeightMul: " + npcNameFinder.npcPosYHeightMul;
        }

        public void npcNameMaxWidth_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.npcNameMaxWidth = (int)e.NewValue;
            lnpcNameMaxWidth.Content = "npcNameMaxWidth: " + npcNameFinder.npcNameMaxWidth;
        }

        public void LinesOfNpcMinLength_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.LinesOfNpcMinLength = (int)e.NewValue;
            lLinesOfNpcMinLength.Content = "LinesOfNpcMinLength: " + npcNameFinder.LinesOfNpcMinLength;
        }

        public void LinesOfNpcLengthDiff_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.LinesOfNpcLengthDiff = (int)e.NewValue;
            lLinesOfNpcLengthDiff.Content = "LinesOfNpcLengthDiff: " + npcNameFinder.LinesOfNpcLengthDiff;
        }

        public void DetermineNpcsHeightOffset1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.DetermineNpcsHeightOffset1 = (int)e.NewValue;
            lDetermineNpcsHeightOffset1.Content = "DetermineNpcsHeightOffset1: " + npcNameFinder.DetermineNpcsHeightOffset1;
        }

        public void DetermineNpcsHeightOffset2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.DetermineNpcsHeightOffset2 = (int)e.NewValue;
            lDetermineNpcsHeightOffset2.Content = "DetermineNpcsHeightOffset2: " + npcNameFinder.DetermineNpcsHeightOffset2;
        }

        public void incX_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.incX = (int)e.NewValue;
            lincX.Content = "incX: " + npcNameFinder.incX;
        }

        public void incY_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!firstRender) return;
            npcNameFinder.incY = (int)e.NewValue;
            lincY.Content = "incY: " + npcNameFinder.incY;
        }

        #endregion
    }
}