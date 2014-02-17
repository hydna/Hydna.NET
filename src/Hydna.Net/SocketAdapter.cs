using System;
using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Hydna.Net
{

    internal class SocketAdapter : IConnectionAdapter
    {

        internal class HandshakeState
        {
            internal byte[] data = new byte[1024];
            internal int offset = 0;
            internal string header = "";
        }

        internal class SendState
        {
            internal byte[] data = null;
            internal int offset = 0;
        }

        internal class ReceiveState
        {
            internal byte[] len = new byte[2];
            internal byte[] data = null;
            internal int length = 0;
            internal int offset = 0;
            internal byte first = 0;
        }

        const string protocolVersion = "winksock/1";

        event ConnectEventHandler OnConnect;
        event CloseEventHandler OnClose;
        event FrameEventHandler OnFrame;

        private Socket _socket;
        private Queue<SendState> _sendQueue;
        private bool _sending;
        private bool _closing;
        private bool _disposed;

        private AsyncCallback _sendCallback;
        private AsyncCallback _receiveCallback;

        void IDisposable.Dispose()
        {
            try {
                shutdown(null);
            }
            catch (Exception) {
            }
        }

        internal SocketAdapter()
        {
            _sendQueue = new Queue<SendState>();

            _sendCallback = new AsyncCallback(sendHandler);
            _receiveCallback = new AsyncCallback(receiveHandler);
        }


        void IConnectionAdapter.Connect (Uri uri)
        {
            AsyncCallback callback;

            callback = new AsyncCallback(resolvedHandler);
            Dns.BeginGetHostEntry(uri.Host, callback, uri);
        }

        void IConnectionAdapter.Send(Frame frame)
        {
            SendState state;

            if (_closing || _disposed)
                return;

            state = new SendState();
            state.data = frame.ToBytes();

            if (_sending)
            {
                _sendQueue.Enqueue(state);
                return;
            }

            processSendState(state);
        }

        void IConnectionAdapter.Close()
        {
            if (_closing || _disposed)
                return;

            _closing = true;

            _socket.BeginDisconnect(false,
                                    new AsyncCallback(disconnectHandler),
                                    null);
        }

        event ConnectEventHandler IConnectionAdapter.OnConnect
        {
            add { OnConnect += value; }
            remove { OnConnect -= value; }
        }

        event CloseEventHandler IConnectionAdapter.OnClose
        {
            add { OnClose += value; }
            remove { OnClose -= value; }
        }

        event FrameEventHandler IConnectionAdapter.OnFrame
        {
            add { OnFrame += value; }
            remove { OnFrame -= value; }
        }

        void resolvedHandler (IAsyncResult ar)
        {
            IPHostEntry entry;

            if (_closing || _disposed)
                return;

            try
            {
                entry = Dns.EndGetHostEntry(ar);

                if (entry.AddressList.Length == 0)
                {
                    shutdown("Unable to resolve host");
                    return;
                }
            }
            catch (SocketException ex)
            {
                shutdown("Unable to resolve host(" + ex.Message +  ")");
                return;
            }

            IPEndPoint endPoint = new IPEndPoint(entry.AddressList[0],
                                                 ((Uri)ar.AsyncState).Port);

            _socket = new Socket(endPoint.AddressFamily,
                                 SocketType.Stream,
                                 ProtocolType.Tcp);

            AsyncCallback callback = new AsyncCallback(connectHandler);

            _socket.BeginConnect(endPoint, callback, ar.AsyncState);
        }

        void connectHandler(IAsyncResult ar)
        {
            if (_closing || _disposed)
                return;

            try
            {
                _socket.EndConnect(ar);
            }
            catch (SocketException ex)
            {
                shutdown("Faild connect to remote(" + ex.Message +  ")");
                return;
            }

            StringBuilder request = new StringBuilder();

            request.Append("GET / HTTP/1.1\r\n");
            request.Append("Connection: Upgrade\r\n");
            request.Append("Upgrade: " +  protocolVersion + "\r\n");
            request.Append("Host: " + ((Uri)ar.AsyncState).Host + "\r\n");
            request.Append("\r\n");

            byte[] data = Encoding.ASCII.GetBytes(request.ToString());

            _socket.BeginSend(data,
                              0,
                              data.Length,
                              SocketFlags.None,
                              new AsyncCallback(handshakeSendHandler),
                              null);
        }

        void handshakeSendHandler(IAsyncResult ar)
        {
            if (_closing || _disposed)
                return;
            
            try
            {
                _socket.EndSend(ar);
            }
            catch (SocketException ex)
            {
                shutdown("Unable to resolve host(" + ex.Message +  ")");
                return;
            }

            HandshakeState state = new HandshakeState();

            _socket.BeginReceive(state.data,
                                 0,
                                 state.data.Length,
                                 SocketFlags.None,
                                 new AsyncCallback(handshakeHandler),
                                 state);
        }

        void handshakeHandler(IAsyncResult ar)
        {
            int bytes;

            if (_closing || _disposed)
                return;

            try
            {
                bytes = _socket.EndReceive(ar);
            }
            catch (SocketException ex)
            {
                shutdown("Unable to resolve host(" + ex.Message +  ")");
                return;
            }

            HandshakeState state = (HandshakeState)ar.AsyncState;

            if (state.offset + bytes > state.data.Length)
            {
                // Break on bigger buffers then data buff.
                shutdown("Unable to connect to host(BAD_HANDSHAKE)");
                return;
            }

            state.header += Encoding.ASCII.GetString(state.data,
                                                     state.offset,
                                                     state.offset + bytes);

            if (state.header.Contains("\r\n\r\n") == false)
            {
                // Consider the response to be uncomplete.
                state.offset += bytes;

                _socket.BeginReceive(state.data,
                                     state.offset,
                                     state.data.Length - state.offset,
                                     SocketFlags.None,
                                     new AsyncCallback(handshakeHandler),
                                     state);
                return;
            }

            if (state.header.Split('\r')[0].Contains("101") == false)
            {
                int idx = state.header.IndexOf("\r\n\r\n");
                string body = state.header.Substring(idx);

                if (body.Length > 1)
                {
                    shutdown(body.TrimStart('\n', '\r', ' '));
                }
                else
                {
                    shutdown("Bad handshake from server");
                }
                return;
            }

            if (OnConnect != null)
            {
                OnConnect();
            }

            receiveLength(null);
        }

        void sendHandler(IAsyncResult ar)
        {
            int bytes;

            _sending = false;

            if (_closing || _disposed)
                return;

            try {
                bytes = _socket.EndSend(ar);
            }
            catch (SocketException ex) {
                shutdown(ex.Message);
                return;
            }

            SendState state = (SendState)ar.AsyncState;

            if (state.offset + bytes < state.data.Length) {
                state.offset += bytes;
                _socket.BeginSend(state.data,
                                  state.offset,
                                  state.data.Length - state.offset,
                                  SocketFlags.None,
                                  _sendCallback,
                                  state);
                return;
            }

            if (_sendQueue.Count > 0)
                processSendState(_sendQueue.Dequeue());
        }

        void receiveHandler(IAsyncResult ar)
        {
            int bytes;

            if (_closing || _disposed)
                return;

            try {
                bytes = _socket.EndReceive(ar);
            }
            catch (SocketException ex) {
                shutdown(ex.Message);
                return;
            }

            ReceiveState state = (ReceiveState)ar.AsyncState;

            // Check if we are dealing with packet length
            if (state.length == 0) {
                if (bytes == 1) {
                    // Very unlikely, but could happend. Only managed
                    // to receive one byte.
                    receiveLength(state);
                    return;
                }

                if (BitConverter.IsLittleEndian) {
                    Array.Reverse(state.len);
                }

                state.length = (int)BitConverter.ToUInt16(state.len, 0);
                state.data = new byte[state.length];

                receive(state);
                return;
            }

            state.offset += bytes;

            if (state.offset != state.length) {
                receive(state);
                return;
            }

            Frame frame = Frame.Create(state.data);

            if (OnFrame != null)
                OnFrame(frame);

            receiveLength(null);
        }

        void disconnectHandler(IAsyncResult ar)
        {
            try {
                _socket.EndDisconnect(ar);
            }
            finally {
                shutdown("Disconnect by user");
            }
        }

        void shutdown(string reason)
        {
            if (_closing || _disposed)
                return;

            if (_socket != null) {
                _socket = null;
            }

            _closing = false;
            _disposed = true;

            if (OnClose != null)
                OnClose(reason);
        }

        void processSendState(SendState state)
        {
            _sending = true;
            _socket.BeginSend(state.data,
                              0,
                              state.data.Length,
                              SocketFlags.None,
                              _sendCallback,
                              state);

        }

        void receiveLength(ReceiveState state)
        {
            int offset = state == null ? 0 : 1;

            state = state == null ? new ReceiveState() : state;

            _socket.BeginReceive(state.len,
                                 offset,
                                 2 - offset,
                                 SocketFlags.None,
                                 _receiveCallback,
                                 state);            
        }

        void receive(ReceiveState state)
        {
            _socket.BeginReceive(state.data,
                                 state.offset,
                                 state.length - state.offset,
                                 SocketFlags.None,
                                 _receiveCallback,
                                 state);            
        }
    }
}

