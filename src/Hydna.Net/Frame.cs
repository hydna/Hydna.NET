using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Hydna.Net
{
    public enum ContentType : byte
    {
        Utf = 0x0,
        Binary = 0x1
    }

    public enum DeliveryPriority : byte
    {
        Guaranteed = 0x0,
        Highest = 0x1,
        High = 0x2,
        Low = 0x3,
        Lowest = 0x4,
    }

    internal enum OpCode : byte
    {
        KeepAlive = 0x00,
        Open = 0x01,
        Data = 0x02,
        Signal = 0x03,
        Resolve = 0x04
    }

    internal enum OpenFlag : byte
    {
        Success = 0x0,
        Deny = 0x1
    }


    internal enum SignalFlag : byte
    {
        Emit = 0x0,
        End = 0x1,
        Error = 0x3      
    }

    internal enum ResolveFlag : byte
    {
        Success = 0x0,
        Error = 0x1,
    }

    internal class Frame
    {
        internal const int HeaderSize = 0x05;
        internal const int LengthSize = 0x02;
        internal const int PayloadMaxSize = 0xFFFFFF - HeaderSize;

        const byte FLAG_MASK = 0x7;

        const byte OP_POS = 3;
        const byte OP_MASK = (0x7 << OP_POS);

        const byte CTYPE_POS = 6;
        const byte CTYPE_MASK = (0x1 << CTYPE_POS);


        internal static Frame Create(byte[] data)
        {
            byte[] payload = null;
            byte[] ptrdata;
            uint ptr;

            if (BitConverter.IsLittleEndian){
                ptrdata = new byte[4];
                Buffer.BlockCopy(data, 0, ptrdata, 0, 4);
                Array.Reverse(ptrdata);
                ptr = BitConverter.ToUInt32(ptrdata, 0);
            }
            else {
                ptr = BitConverter.ToUInt32(data, 0);
            }

            ContentType ctype = (ContentType)((data[4] & CTYPE_MASK) >> CTYPE_POS);
            OpCode opcode = (OpCode)((data[4] & OP_MASK) >> OP_POS);
            byte flag = (byte)(data[4] & FLAG_MASK);

            if (data.Length > HeaderSize) {
                payload = new byte[data.Length - HeaderSize];
                Buffer.BlockCopy(data, HeaderSize, payload, 0, payload.Length);
            }

            return new Frame(ptr, opcode, flag, ctype, payload);
        }

        internal static Frame Create(uint ptr, ChannelMode mode)
        {
            return Create(ptr, mode, ContentType.Utf, null);
        }

        internal static Frame Create(uint ptr,
                                     ChannelMode mode,
                                     ContentType ctype,
                                     byte[] payload)
        {
            return new Frame(ptr, OpCode.Open, (byte)mode, ctype, payload);
        }

        internal static Frame Create(uint ptr,
                                     SignalFlag type,
                                     ContentType ctype,
                                     byte[] payload)
        {
            return new Frame(ptr, OpCode.Signal, (byte)type, ctype, payload);
        }

        internal static Frame Create(uint ptr,
                                     DeliveryPriority prio,
                                     ContentType ctype,
                                     byte[] payload)
        {
            return new Frame(ptr, OpCode.Data, (byte)prio, ctype, payload);
        }

        internal static Frame Resolve(byte[] path)
        {
            return new Frame(0, OpCode.Resolve, 0, ContentType.Utf, path);
        }


        private uint _ptr;
        private ContentType _ctype;
        private OpCode _opcode;
        private byte _flag;
        private byte[] _payload;

        Frame(uint ptr,
              OpCode opcode,
              byte flag,
              ContentType ctype,
              byte[] payload)
        {
            _ptr = ptr;
            _opcode = opcode;
            _flag = flag;
            _ctype = ctype;
            _payload = payload;
        }

        internal uint Ptr
        {
            get { return _ptr; }
        }

        internal OpCode OpCode
        {
            get { return _opcode; }
        }

        internal ContentType ContentType
        {
            get { return _ctype; }
        }

        internal OpenFlag OpenFlag
        {
            get { return (OpenFlag)_flag; }
        }

        internal SignalFlag SignalFlag
        {
            get { return (SignalFlag)_flag; }
        }

        internal ResolveFlag ResolveFlag
        {
            get { return (ResolveFlag)_flag; }
        }

        internal DeliveryPriority PriorityFlag
        {
            get { return (DeliveryPriority)_flag; }
        }

        internal byte[] Payload
        {
            get { return _payload; }
        }

        internal byte[] ToBytes()
        {
            byte[] bytes;
            int plen;
            ushort len;
            byte desc;
            byte[] buffer;

            plen = _payload != null && _payload.Length > 0 ? _payload.Length : 0;
            len = (ushort) (HeaderSize + plen);

            desc = (byte)(((byte)_ctype) << CTYPE_POS |
                          ((byte)_opcode) << OP_POS |
                          _flag);

            bytes = new byte[len + 2];

            buffer = BitConverter.GetBytes(len);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);

            Buffer.BlockCopy(buffer, 0, bytes, 0, 2);

            buffer = BitConverter.GetBytes(_ptr);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(buffer);

            Buffer.BlockCopy(buffer, 0, bytes, 2, 4);

            bytes[6] = desc;

            if (plen > 0)
                Buffer.BlockCopy(_payload, 0, bytes, 7, _payload.Length);

            return bytes;
        }

        internal Frame Clone()
        {
            byte[] payload = null;;

            if (_payload != null) {
                payload = new byte[_payload.Length];
                Buffer.BlockCopy(_payload, 0, payload, 0, _payload.Length);
            }

            return new Frame(_ptr, _opcode, _flag, _ctype, payload);
        }
        
        

        public override string ToString()
        {
            StringBuilder builder;

            builder = new StringBuilder();

            builder.Append("<Frame ptr=" + _ptr + ",");
            builder.Append(" op=" + _opcode + ",");

            switch (_opcode) {
                case OpCode.Open:
                builder.Append(" flag=" + OpenFlag + ",");
                break;

                case OpCode.Data:
                builder.Append(" priority=" + PriorityFlag + ",");
                break;

                case OpCode.Signal:
                builder.Append(" flag=" + SignalFlag + ",");
                break;

                case OpCode.Resolve:
                builder.Append(" flag=" + ResolveFlag + ",");
                break;

                default:
                builder.Append(" flag=UNKNOWN ,");
                break;
            }

            builder.Append(" ctype=" + _ctype + ",");

            int len = (_payload == null ? 0 : _payload.Length);

            builder.Append(" payloadlen=" + len);
            builder.Append(">");

            return builder.ToString();
        }
    }
}
