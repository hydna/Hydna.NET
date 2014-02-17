using System;

namespace Hydna.Net
{
    /// <summary>
    /// Channel state indication.
    /// </summary>
    public enum ChannelState
    {
        /// <summary>
        /// Indicates that channel is in a closed state
        /// </summary>
        Closed,

        /// <summary>
        /// Indicates that channel is connecting to a remote.
        /// </summary>
        Connecting,

        /// <summary>
        /// Indicates that the path has been resolved into a pointer
        /// </summary>
        Resolved,

        /// <summary>
        /// Indicates that channel is connected and ready to interact with.
        /// </summary>
        Open,

        /// <summary>
        /// Indicates that channel is closing.
        /// </summary>
        Closing
    }
}