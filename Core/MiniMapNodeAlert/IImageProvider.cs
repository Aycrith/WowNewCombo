using System;
using System.Drawing;

namespace Core
{
    public interface IImageProvider
    {
        event EventHandler<NodeEventArgs> NodeEvent;
    }

    public class NodeEventArgs : EventArgs
    {
        public Bitmap Bitmap { get; }
        public Point Point { get; }

        public NodeEventArgs(Bitmap bitmap, Point point)
        {
            Bitmap = bitmap;
            Point = point;
        }
    }
}