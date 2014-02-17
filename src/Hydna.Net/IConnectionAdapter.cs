using System;

namespace Hydna.Net
{
    delegate void ConnectEventHandler();
    delegate void CloseEventHandler(string reason);
    delegate void FrameEventHandler(Frame frame);
    
    interface IConnectionAdapter : IDisposable
    {       
        event ConnectEventHandler OnConnect;
        event CloseEventHandler OnClose;
        event FrameEventHandler OnFrame;

        void Connect (Uri uri);
        void Send (Frame frame);
        void Close ();
    }
}