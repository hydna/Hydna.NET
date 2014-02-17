using System;

namespace Hydna.Net
{
    delegate void ConnectEventHandler();
    delegate void CloseEventHandler(string reason);
    delegate void FrameEventHandler(Frame frame);
    
    interface IConnectionAdapter : IDisposable
    {       
        ConnectEventHandler OnConnect { get; set; }
        CloseEventHandler OnClose  { get; set; }
        FrameEventHandler OnFrame  { get; set; }

        void Connect (Uri uri);
        void Send (Frame frame);
        void Close ();
    }
}