using System;

namespace Game
{
    public class ScreenChangeEventArgs : EventArgs
    {
        public string Screenshot { get; }

        public ScreenChangeEventArgs(string screenshot)
        {
            this.Screenshot = screenshot;
        }
    }
}