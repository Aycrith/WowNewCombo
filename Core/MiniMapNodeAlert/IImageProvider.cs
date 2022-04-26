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
        public Point Point { get; }

        public int Amount { get; }

        public NodeEventArgs(Point point, int amount)
        {
            Point = point;
            Amount = amount;
        }
    }
}