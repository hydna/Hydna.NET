using System;
using System.Text;

namespace Hydna.Net
{
    /// <summary>
    /// Represents the arguments for a channel event.
    /// </summary>
    public class ChannelEventArgs : EventArgs
    {
        private ContentType _ctype;
        private byte[] _payload;
        private string _text;

        internal ChannelEventArgs() {}

        internal ChannelEventArgs(ContentType ctype, byte[] payload)
        {
            _ctype = ctype;
            _payload = payload;
        }


        /// <summary>
        /// Gets the content type of this instance payload
        /// </summary>
        /// <value>The content type</value>
        public ContentType ContentType
        {
            get { return _ctype; }
        }

        /// <summary>
        /// Gets the assigned payload as raw bytes.
        /// </summary>
        /// <value>The assigned payload; otherwise <c>null</c></value>
        public byte[] Payload
        {
            get { return _payload; }
        }

        /// <summary>
        /// Gets the assigned payload as an UTF-8 encoded string.
        ///
        /// <c>null</c> is returned if decoding failed.
        /// </summary>
        /// <value>A string representing the payload; or <c>null</c></value>
        public String Text
        {
            get
            {
                if (_text != null)
                    return _text;

                if (_payload == null)
                    return null;

                try {
                    _text = Encoding.UTF8.GetString(_payload);
                }
                catch (EncoderFallbackException) {
                }

                return _text;
            }
        }
    }

    /// <summary>
    /// Represents the arguments for a Close event
    /// </summary>
    public class ChannelCloseEventArgs : ChannelEventArgs
    {

        private bool _wasClean;
        private bool _wasDenied;
        private string _reason;

        internal ChannelCloseEventArgs(bool wasClean,
                                       bool wasDenied,
                                       string reason)
            : base()
        {
            _wasClean = wasClean;
            _wasDenied = wasDenied;
            _reason = reason;
        }

        internal ChannelCloseEventArgs(ContentType ctype,
                                       byte[] payload,
                                       bool wasDenied,
                                       bool wasClean,
                                       string reason)
            : base(ctype, payload)
        {
            _wasDenied = wasDenied;
            _wasClean = wasClean;
            _reason = reason;
        }

        /// <summary>
        /// Gets an indication if channel was closed in a clean mather.
        /// </summary>
        /// <value><c>true</c> if channel did a clean close; otherwise
        /// <c>false</c></value>
        public bool WasClean
        {
            get { return _wasClean; }
        }

        /// <summary>
        /// Gets an indication if channel was closed due to an open deny.
        /// </summary>
        /// <value><c>true</c> if channel closed due to an open deny; otherwise
        /// <c>false</c></value>
        public bool WasDenied
        {
            get { return _wasDenied; }
        }

        /// <summary>
        /// Gets a <c>string</c> representing why the channel was closed.
        /// </summary>
        /// <value>A <c>string</c> representing why channel was closed.</value>
        public string Reason
        {
            get
            {
                string reason = null;

                if (ContentType == ContentType.Utf)
                    reason = Text;

                if (reason == null)
                    reason = _reason;

                if (reason == null)
                    reason = "Unknown reason";

                return reason;
            }
        }
    }



    /// <summary>
    /// Represents the arguments for a Data event
    /// </summary>
    public class ChannelDataEventArgs : ChannelEventArgs
    {
        private DeliveryPriority _priority;

        internal ChannelDataEventArgs(ContentType ctype,
                                      byte[] payload,
                                      DeliveryPriority prio)
            : base(ctype, payload)
        {
            _priority = prio;
        }

        /// <summary>
        /// Gets the delivery priority for this data event
        /// </summary>
        public DeliveryPriority Priority
        {
            get { return _priority; }
        }
    }
}