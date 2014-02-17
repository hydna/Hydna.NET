using System;
using System.IO;
using System.Text;

namespace Hydna.Net
{

    /// <summary>
    /// Represents a Channel which can be used bi-directional data
    /// between client and a Hydna server.
    /// </summary>
    public class Channel : IDisposable
    {
        /// <summary>
        /// Indiciates the maximum size that could be sent over the
        /// network in one chunk.
        /// </summary>
        public const int PayloadMaxSize = Frame.PayloadMaxSize;

        /// <summary>
        /// Open is triggered once that channel is open.
        /// </summary>
        public event EventHandler<ChannelEventArgs> Open
        {
            add { _openInvoker += value; }
            remove { _openInvoker -= value; }
        }

        /// <summary>
        /// Data is triggered once that channel recevies data.
        /// </summary>
        public event EventHandler<ChannelDataEventArgs> Data
        {
            add { _dataInvoker += value; }
            remove { _dataInvoker -= value; }
        }

        /// <summary>
        /// Signal is triggered once that channel recevies a signal.
        /// </summary>
        public event EventHandler<ChannelEventArgs> Signal
        {
            add { _signalInvoker += value; }
            remove { _signalInvoker -= value; }
        }

        /// <summary>
        /// Close is triggered once that channel is closed. 
        /// </summary>
        public event EventHandler<ChannelCloseEventArgs> Closed
        {
            add { _closedInvoker += value; }
            remove { _closedInvoker -= value; }
        }
 
        internal byte[] token = null;
        internal ContentType ctoken = ContentType.Utf;

        internal byte[] outro = null;
        internal ContentType coutro = ContentType.Utf;

        private uint _ptr = 0;

        private Uri _uri = null;
        private ChannelMode _mode = ChannelMode.Listen;
        private ChannelState _state = ChannelState.Closed;

        EventHandler<ChannelEventArgs> _openInvoker;
        EventHandler<ChannelDataEventArgs> _dataInvoker;
        EventHandler<ChannelEventArgs> _signalInvoker;
        EventHandler<ChannelCloseEventArgs> _closedInvoker;

        private Connection _connection;

        void IDisposable.Dispose()
        {
            try {
                Close();
            }
            catch (Exception) {
            }
        }

        /// <summary>
        /// Initializes a new instance of the Channel class.
        /// </summary>
        public Channel ()
        {
        }


        /// <summary>
        /// Gets the absolute <b>path</b> for this channel
        /// </summary>
        /// <value>The <b>path</b> if connecting/connected; otherwise
        /// <b>null</b> </value>
        public string AbsolutePath
        {
            get { return _uri == null ? null : _uri.AbsolutePath; }
        }

        /// <summary>
        /// Gets a value indicating what mode that this channel instance
        /// is opened in.
        /// </summary>
        /// <value>A value indiciating the channel mode</value>
        public ChannelMode Mode
        {
            get { return _mode; }
        }

        /// <summary>
        /// Gets a value indicating whether this channel is readable.
        /// </summary>
        /// <value><c>true</c> if data is recieved on this Channel instance;
        /// otherwise, <c>false</c>.</value>
        public bool Readable
        {
            get
            {
                return _state == ChannelState.Open &&
                       (_mode & ChannelMode.Read) == ChannelMode.Read;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this channel can send data.
        /// </summary>
        /// <value><c>true</c> if data can be sent over this Channel instance;
        /// otherwise, <c>false</c>.</value>
        public bool Writable
        {
            get
            {
                return _state == ChannelState.Open &&
                       (_mode & ChannelMode.Write) == ChannelMode.Write;
            }
        }

        /// <summary>
        /// Gets a value indicating whether this channel can emit signals.
        /// </summary>
        /// <value><c>true</c> if signals can be emitted over this Channel
        /// instance; otherwise, <c>false</c>.</value>
        public bool Emittable
        {
            get
            {
                return _state == ChannelState.Open &&
                       (_mode & ChannelMode.Emit) == ChannelMode.Emit;
            }
        }

        /// <summary>
        /// Gets the <c>uri</c> for this channel instance.
        /// </summary>
        /// <value>The <c>uri</c> which this channel instance is
        /// connecting/conntected to; otherwise <c>null</c></value>
        public Uri Uri
        {
            get
            {
                return _uri;
            }
        }

        /// <summary>
        /// Gets the <c>state</c> for this channel instance.
        /// </summary>
        /// <value>The current <c>state</c></value>
        public ChannelState State
        {
            get
            {
                return _state;
            }
        }

        /// <summary>
        /// Connect to a remote server at specified <c>url</c>, in mode
        /// <c>read</c>.
        /// </summary>
        /// <param name="url">The <c>url</c> (e.g. public.hydna.net)</param>
        public void Connect (string url)
        {
            Connect(url, ChannelMode.Read);
        }

        /// <summary>
        /// Connect to a remote server at specified <c>url</c>, in specified
        /// <c>mode</c>.
        /// </summary>
        /// <param name="url">The <c>url</c> (e.g. public.hydna.net)</param>
        /// <param name="mode">The <c>mode</c> to open channel in.</param>
        public void Connect (string url, ChannelMode mode)
        {
            Uri uri;

            if (url == null) {
                throw new ArgumentNullException("url", "Expected an Url");
            }

            if (url.Contains("://") == false) {
                UriBuilder builder = new UriBuilder("http://" + url);
                uri = builder.Uri;
            }
            else {
                uri = new Uri(url);
            }

            Connect(uri, mode);
        }

        /// <summary>
        /// Connect to a remote server at specified <c>uri</c>, in mode
        /// <c>read</c>.
        /// </summary>
        /// <param name="uri">The <c>uri</c> to server</param>
        public void Connect (Uri uri)
        {
            Connect(uri, ChannelMode.Read);
        }

        /// <summary>
        /// Connect to a remote server at specified <c>uri</c>, in specified
        /// <c>mode</c>.
        /// </summary>
        /// <param name="uri">The <c>uri</c> to server</param>
        /// <param name="mode">The <c>mode</c> to open channel in.</param>
        public void Connect (Uri uri, ChannelMode mode)
        {

            if (_state != ChannelState.Closed) {
                throw new Exception("Channel is already connecting/connected");
            }

            if (uri == null) {
                throw new ArgumentNullException("uri", "Expected an Uri");
            }

            if (uri.Scheme != "http" && uri.Scheme != "https") {
                throw new ArgumentException("uri", "Unsupported Scheme");
            }

            if (uri.Scheme == "https" && Connection.hasTlsSupport == false) {
                throw new ArgumentException("uri", "TLS is not supported");
            }

            if (uri.Query != null && uri.Query.Length > 0) {
                token = Encoding.UTF8.GetBytes(uri.Query.Substring(1));
            }

            _mode = mode;
            _uri = uri;

            try {
                _connection = Connection.create(this, uri);
            }
            catch (Exception ex) {
                token = null;
                _mode = ChannelMode.Listen;
                throw ex;
            }

            _state = ChannelState.Connecting;
        }

        /// <summary>
        /// Sends an UTF8 encoded string to this channel instance.
        /// </summary>
        /// <param name="data">The <c>string</c> that should be sent</param>
        public void Send(string data)
        {
            Send(data, DeliveryPriority.Guaranteed);
        }

        /// <summary>
        /// Sends an UTF8 encoded string to this channel instance, with
        /// specified priority.
        /// </summary>
        /// <param name="data">The <c>string</c> that should be sent</param>
        /// <param name="priority">The priority of this message</param>
        public void Send(string data, DeliveryPriority prio)
        {
            if (data == null) {
                throw new ArgumentNullException("data", "Cannot be null");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(data);
            Send(ContentType.Utf, buffer, prio);
        }

        /// <summary>
        /// Sends binary data to this channel instance.
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// written to the channel.</param>
        public void Send(byte[] buffer)
        {
            Send(buffer, DeliveryPriority.Guaranteed);
        }

        /// <summary>
        /// Sends binary data to this channel instance.
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// written to the channel.</param>
        /// <param name="offset">The zero-based location in buffer at which to
        /// begin reading bytes to be written to the channel.</param>
        /// <param name="count">A value that specifies the number of bytes to
        /// read from buffer.</param>
        public void Send(byte[] buffer, int offset, int count)
        {
            Send(buffer, offset, count, DeliveryPriority.Guaranteed);
        }

        /// <summary>
        /// Sends binary data to this channel instance, with specified
        /// priority.
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// written to the channel.</param>
        /// <param name="priority">The priority of this message</param>
        public void Send(byte[] buffer, DeliveryPriority prio)
        {
            Send(ContentType.Binary, buffer, prio);
        }

        /// <summary>
        /// Sends binary data to this channel instance, with specified
        /// priority.
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// written to the channel.</param>
        /// <param name="offset">The zero-based location in buffer at which to
        /// begin reading bytes to be written to the channel.</param>
        /// <param name="count">A value that specifies the number of bytes to
        /// read from buffer.</param>
        /// <param name="priority">The priority of this message</param>
        public void Send(byte[] buffer,
                         int offset,
                         int count,
                         DeliveryPriority prio)
        {
            if (offset < 0 || offset + count > buffer.Length) {
                throw new ArgumentException("Index out of bounds");
            }

            byte[] clone = new byte[count - offset];
            Buffer.BlockCopy(buffer, offset, clone, 0, count);

            Send(ContentType.Binary, clone, prio);
        }

        void Send(ContentType ctype, byte[] buffer, DeliveryPriority prio)
        {
            if (_state != ChannelState.Open) {
                throw new InvalidOperationException("Channel is not open");
            }

            if (Writable == false) {
                throw new InvalidOperationException("Channel is not writable");
            }

            if (buffer == null) {
                throw new ArgumentNullException("buffer", "Cannot be null");
            }

            if (buffer.Length > Frame.PayloadMaxSize) {
                throw new ArgumentException("buffer", "Data buffer is to large");
            }

            Frame frame = Frame.Create(_ptr, prio, ctype, buffer);
            _connection.Send(frame);
        }


        /// <summary>
        /// Emitts a signal to the channel 
        /// </summary>
        /// <param name="data">A string that supplies the data to be
        /// emitted to the channel.</param>
        public void Emit(string data)
        {
            if (data == null) {
                throw new ArgumentNullException("data", "Cannot be null");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(data);
            Emit(ContentType.Utf, buffer);
        }

        /// <summary>
        /// Emitts a signal to the channel 
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// emitted to the channel.</param>
        public void Emit(byte[] buffer)
        {
            Emit(ContentType.Binary, buffer);
        }

        /// <summary>
        /// Emitts a signal to the channel 
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// emitted to the channel.</param>
        /// <param name="offset">The zero-based location in buffer at which to
        /// begin reading bytes to be written to the channel.</param>
        /// <param name="count">A value that specifies the number of bytes to
        /// read from buffer.</param>
        public void Emit(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || offset + count > buffer.Length) {
                throw new ArgumentException("Index out of bounds");
            }

            byte[] clone = new byte[count - offset];
            Buffer.BlockCopy(buffer, offset, clone, 0, count);

            Emit(ContentType.Binary, clone);
        }

        void Emit(ContentType ctype, byte[] buffer)
        {
            if (_state != ChannelState.Open) {
                throw new InvalidOperationException("Channel is not open");
            }

            if (Emittable == false) {
                throw new InvalidOperationException("Channel is not writable");
            }

            if (buffer == null) {
                throw new ArgumentNullException("buffer", "Cannot be null");
            }

            if (buffer.Length > Frame.PayloadMaxSize) {
                throw new ArgumentException("buffer", "Data buffer is to large");
            }

            Frame end = Frame.Create(_ptr, SignalFlag.Emit, ctype, buffer);
            _connection.Send(end);
        }

        /// <summary>
        /// Closes the channel by sending an end signal to the remote
        /// connection. The event Close is triggered once that channel is
        /// completely closed.
        /// </summary>
        public void Close()
        {
            Close(ContentType.Utf, null);
        }

        /// <summary>
        /// Closes the channel by sending an end signal, with specified string,
        /// to the remote connection. The event Close is triggered once that
        /// channel is completely closed.
        /// </summary>
        /// <param name="data">A string that supplies the data to be
        /// emitted to the channel.</param>
        public void Close(string data)
        {
            if (data == null) {
                throw new ArgumentNullException("data", "Cannot be null");
            }

            byte[] buffer = Encoding.UTF8.GetBytes(data);
            Close(ContentType.Utf, buffer);
        }

        /// <summary>
        /// Closes the channel by sending an end signal, with specified data,
        /// to the remote connection. The event Close is triggered once that
        /// channel is completely closed.
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// emitted to the channel.</param>
        public void Close(byte[] buffer)
        {
            Close(ContentType.Binary, buffer);
        }

        /// <summary>
        /// Closes the channel by sending an end signal, with specified data,
        /// to the remote connection. The event Close is triggered once that
        /// channel is completely closed.
        /// </summary>
        /// <param name="buffer">A Byte array that supplies the bytes to be
        /// emitted to the channel.</param>
        /// <param name="offset">The zero-based location in buffer at which to
        /// begin reading bytes to be written to the channel.</param>
        /// <param name="count">A value that specifies the number of bytes to
        /// read from buffer.</param>
        public void Close(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || offset + count > buffer.Length) {
                throw new ArgumentException("Index out of bounds");
            }

            byte[] clone = new byte[count - offset];
            Buffer.BlockCopy(buffer, offset, clone, 0, count);

            Close(ContentType.Binary, clone);
        }

        void Close(ContentType ctype, byte[] buffer)
        {
            if (_connection == null ||
                _state == ChannelState.Closed ||
                _state == ChannelState.Closing) {
              throw new InvalidOperationException("Channel is already closed");
            }

            if (buffer != null && buffer.Length > Frame.PayloadMaxSize) {
                throw new ArgumentException("buffer", "Data buffer is to large");
            }
            
            ChannelState oldState = _state;

            _state = ChannelState.Closing;

            if (oldState == ChannelState.Connecting ||
                oldState == ChannelState.Resolved) {
                // Open request is not responded to yet. Wait to send
                // ENDSIG until we get an OPENRESP.
                coutro = ctype;
                outro = buffer;
                return;
            }

            Frame end = Frame.Create(_ptr, SignalFlag.End, ctype, buffer);
            _connection.Send(end);
        }

        internal uint Ptr
        {
            get { return _ptr; }
        }

        internal void handleResolved(uint ptr)
        {
            _ptr = ptr;
            _state = ChannelState.Resolved;
        }

        internal void handleOpen(Frame frame)
        {

            if (_state == ChannelState.Closing) {
                // User has called close before it was open. Send
                // the pending ENDSIG.
                Frame end = Frame.Create(_ptr, SignalFlag.End, coutro, outro);
                outro = null;
                _connection.Send(end);
                return;
            }

            _state = ChannelState.Open;

            if (_openInvoker == null)
                return;

            ChannelEventArgs e;
            e = new ChannelEventArgs(frame.ContentType, frame.Payload);

            _openInvoker(this, e);
        }

        internal void handleData(Frame frame)
        {
            if (_dataInvoker == null)
                return;

            ChannelDataEventArgs e;
            e = new ChannelDataEventArgs(frame.ContentType,
                                         frame.Payload,
                                         frame.PriorityFlag);
            _dataInvoker(this, e);
        }

        internal void handleSignal(Frame frame)
        {
            if (_signalInvoker == null)
                return;

            ChannelEventArgs e;
            e = new ChannelEventArgs(frame.ContentType,
                                     frame.Payload);
            _signalInvoker(this, e);
        }

        internal void handleClose(Frame frame)
        {
            handleClose(frame, "Unknown reason");
        }

        internal void handleClose(string reason)
        {
            handleClose(null, reason);
        }

        internal void handleClose(Frame frame, string reason)
        {
            if (_state == ChannelState.Closed) {
                return;
            }

            _state = ChannelState.Closed;
            _ptr = 0;

            if (_closedInvoker == null)
                return;

            ChannelCloseEventArgs e;

            if (frame == null) {
                e = new ChannelCloseEventArgs(false, false, reason);
            }
            else {
                bool wasDenied = frame.OpCode == OpCode.Open;
                bool wasClean = wasDenied ? false
                                          : frame.SignalFlag == SignalFlag.End;

                e = new ChannelCloseEventArgs(frame.ContentType,
                                              frame.Payload,
                                              wasDenied,
                                              wasClean,
                                              reason);
            }

            _closedInvoker(this, e);
        }
    }
}
