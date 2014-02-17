using System;
using System.IO;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Collections.Generic;

using System.Reflection;

namespace Hydna.Net
{

    internal class ReflectClientAdapter : IConnectionAdapter
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

        private ConnectEventHandler _connectInvoker;
        private CloseEventHandler _closeInvoker;
        private FrameEventHandler _frameInvoker;

        private Object _client;
        private Stream _stream;
        private Queue<SendState> _sendQueue;
        private bool _sending;
        private bool _closing;
        private bool _disposed;

        private AsyncCallback _sendCallback;
        private AsyncCallback _receiveCallback;
        private RemoteCertificateValidationCallback _certCallback;

        static Type DnsType;

        static MethodInfo DnsBeginGetHostEntry;
        static MethodInfo DnsEndGetHostEntry;

        static Type IPAddressType;

        static Type IPHostEntryType;

        static MethodInfo IPHostEntryAddressList;

        static Type TcpClientType;
        
        static MethodInfo TcpClientBeginConnect;
        static MethodInfo TcpClientEndConnect;		
        static MethodInfo TcpClientGetStream;
        static MethodInfo TcpClientClose;

        static ReflectClientAdapter()
        {
            Assembly ass = Assembly.Load("System");

            DnsType = ass.GetType("System.Net.Dns");

            DnsBeginGetHostEntry = DnsType.GetMethod("BeginGetHostEntry",
                                                     new Type[] {
                                                         typeof(string),
                                                         typeof(AsyncCallback),
                                                         typeof(Object)
                                                     });

            DnsEndGetHostEntry = DnsType.GetMethod("EndGetHostEntry",
                                                    new Type[] {
                                                        typeof(IAsyncResult)
                                                    });


            IPAddressType = ass.GetType("System.Net.IPAddress");

            IPHostEntryType = ass.GetType("System.Net.IPHostEntry");
            IPHostEntryAddressList = IPHostEntryType.GetProperty("AddressList")
                                                                .GetGetMethod();


            TcpClientType = ass.GetType("System.Net.Sockets.TcpClient");
            TcpClientBeginConnect = TcpClientType.GetMethod("BeginConnect",
                                                    new Type[] {
                                                        IPAddressType,
                                                        typeof(Int32),
                                                        typeof(AsyncCallback),
                                                        typeof(Object)
                                                    });

            TcpClientEndConnect = TcpClientType.GetMethod("EndConnect",
                                                    new Type[] {
                                                        typeof(IAsyncResult)
                                                    });

            TcpClientGetStream = TcpClientType.GetMethod("GetStream");
            TcpClientClose = TcpClientType.GetMethod("Close");
            
        }

        void IDisposable.Dispose()
        {
            try {
                shutdown(null);
            }
            catch (Exception) {
            }
        }

        internal ReflectClientAdapter()
        {
            _sendQueue = new Queue<SendState>();

            _sendCallback = new AsyncCallback(sendHandler);
            _receiveCallback = new AsyncCallback(receiveHandler);
            _certCallback = new RemoteCertificateValidationCallback(ValidateCert);
        }


        void IConnectionAdapter.Connect (Uri uri)
        {
            AsyncCallback callback;

            callback = new AsyncCallback(resolvedHandler);
            DnsBeginGetHostEntry.Invoke(null, new object[] {
                uri.Host,
                callback,
                uri
            });
        }

        void IConnectionAdapter.Send(Frame frame)
        {
            SendState state;

            if (_closing || _disposed)
                return;

            state = new SendState();
            state.data = frame.ToBytes();

            if (_sending) {
                _sendQueue.Enqueue(state);
                return;
            }

            processSendState(state);
        }

        void IConnectionAdapter.Close()
        {
            shutdown(null);
        }

        ConnectEventHandler IConnectionAdapter.OnConnect {
            get { return _connectInvoker; }
            set { _connectInvoker = value ; }
        } 

        FrameEventHandler IConnectionAdapter.OnFrame {
            get { return _frameInvoker; }
            set { _frameInvoker = value ; }
        } 

        CloseEventHandler IConnectionAdapter.OnClose {
            get { return _closeInvoker; }
            set { _closeInvoker = value ; }
        } 

        void resolvedHandler (IAsyncResult ar)
        {
            object entry;
            object[] addresses;

            if (_closing || _disposed)
                return;

            try
            {
                entry = DnsEndGetHostEntry.Invoke(null, new object[] {ar});
                addresses = (object[])IPHostEntryAddressList.Invoke(entry, null);

                if (addresses.Length == 0)
                {
                    shutdown("Unable to resolve host");
                    return;
                }
            }
            catch (Exception ex)
            {
                shutdown(ex.Message);
                return;
            }

            _client = Activator.CreateInstance(TcpClientType);
            
            object[] parameters = { addresses[0],
                                    ((Uri)ar.AsyncState).Port,
                                    new AsyncCallback(connectHandler),
                                    ar.AsyncState };

            TcpClientBeginConnect.Invoke(_client, parameters);
        }

        void connectHandler(IAsyncResult ar)
        {
            if (_closing || _disposed)
                return;

            try
            {
                TcpClientEndConnect.Invoke(_client, new object[] { ar });
            }
            catch (Exception ex)
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

            Uri uri = (Uri)ar.AsyncState;

            if (uri.Scheme == "https") {
                Stream s = TcpClientGetStream.Invoke(_client, null) as Stream;
                SslStream sslStream = new SslStream(s,
                                                    false,
                                                    _certCallback,
                                                    null);
            
                _stream = sslStream;
            
                AsyncCallback callback = new AsyncCallback(authHandler);
                sslStream.BeginAuthenticateAsClient(uri.Host,
                                                    callback,
                                                    data);
                return;
            }

            _stream = TcpClientGetStream.Invoke(_client, null) as Stream;

            _stream.BeginWrite(data,
                               0,
                               data.Length,
                               new AsyncCallback(handshakeSendHandler),
                               null);
        }

        void authHandler(IAsyncResult ar)
        {
            if (_closing || _disposed)
                return;
            
            SslStream sslStream = (SslStream)_stream;
            
            try {
                sslStream.EndAuthenticateAsClient(ar);
            }
            catch (IOException e) {
                shutdown(e.Message);
                return;
            }
            catch (AuthenticationException e) {
                shutdown(e.Message);
                return;
            }
            
            byte[] data = (byte[])ar.AsyncState;
            
            _stream.BeginWrite(data,
                               0,
                               data.Length,
                               new AsyncCallback(handshakeSendHandler),
                               null);
        }

        void handshakeSendHandler(IAsyncResult ar)
        {
            if (_closing || _disposed)
                return;
            
            try
            {
                _stream.EndWrite(ar);
            }
            catch (Exception ex)
            {
                shutdown("Unable to resolve host(" + ex.Message +  ")");
                return;
            }

            HandshakeState state = new HandshakeState();

            _stream.BeginRead(state.data,
                              0,
                              state.data.Length,
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
                bytes = _stream.EndRead(ar);
            }
            catch (Exception ex)
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

                _stream.BeginRead(state.data,
                                  state.offset,
                                  state.data.Length - state.offset,
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

            if (_connectInvoker != null)
                _connectInvoker();

            receiveLength(null);
        }

        void sendHandler(IAsyncResult ar)
        {
            _sending = false;

            if (_closing || _disposed)
                return;

            try {
                _stream.EndWrite(ar);
            }
            catch (Exception ex) {
                shutdown(ex.Message);
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
                bytes = _stream.EndRead(ar);
            }
            catch (Exception ex) {
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

            if (_frameInvoker != null)
                _frameInvoker(frame);

            if (_closing || _disposed)
                return;

            receiveLength(null);
        }

        void shutdown(string reason)
        {
            if (_closing || _disposed)
                return;

            if (_client != null) {
                TcpClientClose.Invoke(_client, null);
                _client = null;
            }

            _closing = false;
            _disposed = true;

            if (_closeInvoker != null)
                _closeInvoker(reason);
        }

        void processSendState(SendState state)
        {
            _sending = true;
            _stream.BeginWrite(state.data,
                               0,
                               state.data.Length,
                               _sendCallback,
                               state);

        }

        void receiveLength(ReceiveState state)
        {
            int offset = state == null ? 0 : 1;

            state = state == null ? new ReceiveState() : state;

            _stream.BeginRead(state.len,
                              offset,
                              2 - offset,
                              _receiveCallback,
                              state);            
        }

        void receive(ReceiveState state)
        {
            _stream.BeginRead(state.data,
                              state.offset,
                              state.length - state.offset,
                              _receiveCallback,
                              state);            
        }

        public static bool ValidateCert(object sender,
                                        X509Certificate certificate,
                                        X509Chain chain,
                                        SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
                return true;
        
            return false;
        }
    }
}

