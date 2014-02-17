using System;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Generic;

namespace Hydna.Net
{
    internal class Connection : IDisposable
    {

        internal static bool hasTlsSupport = true;

        private static Dictionary<string, List<Connection>> _connections;

        static Connection ()
        {
            _connections = new Dictionary<string, List<Connection>>();
        }


        internal static Connection create (Channel channel, Uri uri)
        {
            Connection connection = null;
            string connurl = null;

            connurl = uri.Scheme + "://" + uri.Host + ":" + uri.Port;

            if (_connections.ContainsKey(connurl)) {
                List<Connection> all = _connections[connurl];
                foreach (Connection conn in all) {
                    if (conn._channels.ContainsKey(uri.AbsolutePath) == false) {
                        connection = conn;
                        break;
                    }
                }
                
            }

            if (connection == null) {
                connection = new Connection(connurl, uri);

                if (_connections.ContainsKey(connurl) == false) {
                    _connections.Add(connurl, new List<Connection>());
                }

                _connections[connurl].Add(connection);
            }

            connection.allocChannel(channel);

            return connection;
        }


        private IConnectionAdapter _adapter;

        private Dictionary<string, Channel> _channels;
        private Dictionary<uint, Channel> _routes;

        private Queue<Channel> _openQueue;

        private string _id;
        private bool _handshaked;
        private int _refcount;

        void IDisposable.Dispose()
        {
            try {
                close();
            }
            catch (Exception) {
            }
        }

        internal Connection (string id, Uri uri)
        {
            _id = id;

            _refcount = 0;

            _channels = new Dictionary<string, Channel>();
            _routes = new Dictionary<uint, Channel>();

            _adapter = new TcpClientAdapter();
            _adapter.OnConnect += connectHandler;
            _adapter.OnClose += closeHandler;
            _adapter.OnFrame += frameHandler;
            _adapter.Connect(uri);

            _openQueue = new Queue<Channel>();
        }

        internal void Send(Frame frame)
        {
            if (_adapter == null)
                return;

            _adapter.Send(frame);
        }

        void allocChannel(Channel channel)
        {
            string path;

            path = channel.AbsolutePath == null ? "/"
                                                : channel.AbsolutePath;

            if (_channels.ContainsKey(path)) {
                throw new Exception("Channel already created");
            }

            _channels.Add(path, channel);
            _refcount++;

            if (_handshaked) {
                byte[] pathbuff = Encoding.UTF8.GetBytes(channel.AbsolutePath);
                Frame frame = Frame.Resolve(pathbuff);
                _adapter.Send(frame);
            }
            else {
                _openQueue.Enqueue(channel);
            }
        }

        void deallocChannel(Channel channel)
        {
            deallocChannel(channel, null, null);
        }

        void deallocChannel(Channel channel, Frame frame, string reason)
        {
            if (channel.AbsolutePath == null)
                return;

            if (_channels.ContainsKey(channel.AbsolutePath))
                _channels.Remove(channel.AbsolutePath);

            if (channel.Ptr != 0)
                _routes.Remove(channel.Ptr);

            _refcount--;
            if (_refcount == 0) {
                close();
            }

            channel.handleClose(frame, reason);
        }

        void connectHandler()
        {
            Channel channel;
            Frame frame;
            byte[] path;

            _handshaked = true;

            while (_openQueue.Count > 0) {
                channel = _openQueue.Dequeue();
                path = Encoding.UTF8.GetBytes(channel.AbsolutePath);
                frame = Frame.Resolve(path);
                _adapter.Send(frame);
            }
        }

        void frameHandler(Frame frame)
        {
            switch (frame.OpCode)
            {
                case OpCode.Open:
                openHandler(frame);
                break;

                case OpCode.Data:
                dataHandler(frame);
                break;

                case OpCode.Signal:
                signalHandler(frame);
                break;

                case OpCode.Resolve:
                resolveHandler(frame);
                break;
            }
        }

        void closeHandler(string reason)
        {
            close(reason);
        }

        void openHandler(Frame frame)
        {
            Channel channel;

            if (_routes.ContainsKey(frame.Ptr) == false) {
                close("Protocol error: Server sent invalid open packet");
                return;
            }

            channel = _routes[frame.Ptr];

            if (channel.State != ChannelState.Resolved) {
                close("Protocol error: Server sent open to an open channel");
                return;
            }

            if (frame.OpenFlag == OpenFlag.Success) {
                channel.handleOpen(frame);
                return;
            }

            deallocChannel(channel, frame, "Open was denied");
        }

        void dataHandler(Frame frame)
        {
            if (frame.Payload == null || frame.Payload.Length == 0) {
                close("Protocol error: Zero data packet sent received");
                return;
            }

            Channel channel;

            if (frame.Ptr == 0) {
                foreach (KeyValuePair<string, Channel> kvp in _channels) {
                    channel = kvp.Value;
                    if ((channel.Mode & ChannelMode.Read) == ChannelMode.Read) {
                        channel.handleData(frame.Clone());
                    }
                }
                return;
            }

            if (_routes.ContainsKey(frame.Ptr) == false)
                return;


            channel = _routes[frame.Ptr];

            if ((channel.Mode & ChannelMode.Read) == ChannelMode.Read) {
                channel.handleData(frame);
            }
        }

        void signalHandler(Frame frame)
        {
            Channel channel;

            if (frame.SignalFlag == SignalFlag.Emit) {
                if (frame.Ptr == 0) {
                    foreach (KeyValuePair<string, Channel> kvp in _channels) {
                        channel = kvp.Value;
                        channel.handleSignal(frame.Clone());
                    }
                }
                else {
                    if (_routes.ContainsKey(frame.Ptr) == false)
                        return;

                    channel = _routes[frame.Ptr];
                    channel.handleSignal(frame);
                }
            }
            else {
                if (frame.Ptr == 0) {
                    foreach (KeyValuePair<string, Channel> kvp in _channels) {
                        channel = kvp.Value;
                        deallocChannel(channel, frame.Clone(), null);
                    }
                }
                else {
                    if (_routes.ContainsKey(frame.Ptr) == false)
                        return;

                    channel = _routes[frame.Ptr];

                    if (channel.State != ChannelState.Closing) {
                        // We havent closed our channel yet. We therefor need
                        // to send an ENDSIG in response to this packet.
                        Frame end = Frame.Create(frame.Ptr,
                                                  SignalFlag.End,
                                                  ContentType.Utf,
                                                  null);
                        _adapter.Send(end);
                    }

                    deallocChannel(channel, frame, null);
                }
            }
        }

        void resolveHandler(Frame frame)
        {
            string path;

            try {
                path = Encoding.UTF8.GetString(frame.Payload);
            }
            catch (EncoderFallbackException) {
                // Ignore unresolved paths;
                return;
            }

            Channel channel;

            if (_channels.ContainsKey(path) == false) {
                return;
            }

            channel = _channels[path];

            if (channel.State == ChannelState.Closing) {
                deallocChannel(channel);
                return;
            }

            if (frame.ResolveFlag != ResolveFlag.Success) {
                deallocChannel(channel, frame, "Unable to resolve path");
                return;
            }

            _routes.Add(frame.Ptr, channel);
            channel.handleResolved(frame.Ptr);

            Frame open = Frame.Create(frame.Ptr,
                                      channel.Mode,
                                      channel.ctoken,
                                      channel.token);

            _adapter.Send(open);
        }

        void close(string reason)
        {
            Dictionary<string, Channel> channels;

            channels = new Dictionary<string, Channel>(_channels);

            foreach (KeyValuePair<string, Channel> kvp in channels) {
                Channel channel = kvp.Value;
                deallocChannel(channel, null, reason);
            }
            close();
        }

        void close()
        {
            if (_adapter != null) {
                _adapter.OnConnect -= connectHandler;
                _adapter.OnClose -= closeHandler;
                _adapter.OnFrame -= frameHandler;
                _adapter.Close();
                _adapter = null;
            }

            _channels = null;
            _routes = null;
            _handshaked = false;

            if (_id != null) {
                if (_connections.ContainsKey(_id)) {
                    List<Connection> list = _connections[_id];
                    list.Remove(this);

                    if (list.Count == 0)
                        _connections.Remove(_id);
                }
            }
        }
    }
}