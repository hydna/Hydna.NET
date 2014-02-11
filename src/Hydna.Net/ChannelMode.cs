using System;

namespace Hydna.Net
{
    /// <summary>
    /// The mode that channel is, or should be opened in.
    /// </summary>
    public enum ChannelMode : byte {

        /// <summary>
        /// Indicates that only signals are recivied on the Channel.
        /// </summary>
        Listen = 0x0,

        /// <summary>
        /// Indicates that signals and data is recieved on the Channel.
        /// </summary>
        Read = 0x1,

        /// <summary>
        /// Indicates that signals is recived and that data can be
        /// sent to the Channel.
        /// </summary>
        Write = 0x2,

        /// <summary>
        /// Indicates that signals and data are recived and that data can be
        /// sent to the Channel.
        /// </summary>
        ReadWrite = 0x3,

        /// <summary>
        /// Indicates that signals is recived and that signals can be
        /// emitted to the Channel.
        /// </summary>
        Emit = 0x4,

        /// <summary>
        /// Indicates that signals and data are recived and that signals can be
        /// emitted to the Channel.
        /// </summary>
        ReadEmit = 0x5,

        /// <summary>
        /// Indicates that signals is recived and that signals can be
        /// emitted, and that data can be sent to the Channel.
        /// </summary>
        WriteEmit = 0x6,

        /// <summary>
        /// Indicates that signals and data are recived and that signals can be
        /// emitted, and that data can be sent to the Channel.
        /// </summary>
        ReadWriteEmit = 0x7
    }
}
