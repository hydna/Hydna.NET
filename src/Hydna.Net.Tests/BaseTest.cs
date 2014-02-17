namespace Hydna.Net
{

    using System;
    using System.Threading;

    using NUnit.Framework;

    using Hydna.Net;

    public class BaseTest
    {
        protected static string URL = "testing.hydna.net";

        protected static string UniqueUrl()
        {
            byte[] bytes = new byte[64];
            Random rnd = new Random();
            rnd.NextBytes(bytes);
            return URL + "/" + Convert.ToBase64String(bytes);
        }

        protected static bool Compare(byte[] a, byte[] b)
        {
          if (a.Length != b.Length)
            return false;

          for (int i = 0; i < a.Length; i++)
            if(a[i] != b[i])
              return false;

          return true;
        }

        long timeout = 0;
        long endtime = 0;
        bool done = false;
        Exception error = null;

        protected void Done()
        {
            done = true;
        }

        protected void Wait()
        {
            done = false;
            error = null;
            endtime = DateTime.Now.Ticks + timeout;

            while (done == false && error == null && Timeouted() == false)
                Thread.SpinWait(1);

            if (error != null) {
                throw error;
            }

            if (Timeouted()) {
                throw new Exception("Timeouted");
            }
        }

        bool Timeouted()
        {
            if (timeout == 0) return false;
            return (DateTime.Now.Ticks > endtime);
        }

        protected void Timeout(int time)
        {
            timeout = ((long)time) * 10000;
        }


        protected Channel CreateChannel(string path, ChannelMode mode)
        {
            Channel channel = new Channel();
            bindChannel(channel);
            channel.Connect(URL + "/" + path, mode);
            return channel;
        }

        protected Channel CreateRandomChannel(ChannelMode mode)
        {
            return CreateRandomChannel(mode, false);
        }

        protected Channel CreateRandomChannel(ChannelMode mode, bool secure)
        {
            Channel channel = new Channel();
            bindChannel(channel);
            channel.Connect((secure ? "https://" : "") + UniqueUrl(), mode);
            return channel;
        }

        void bindChannel (Channel channel)
        {
            channel.Open += delegate(object sender, ChannelEventArgs e) {
                try {
                    Open((Channel) sender, e);
                }
                catch (Exception ex) {
                    error = ex;
                }
            };

            channel.Signal += delegate(object sender, ChannelEventArgs e) {
                try {
                    Signal((Channel) sender, e);
                }
                catch (Exception ex) {
                    error = ex;
                }
            };

            channel.Data += delegate(object sender, ChannelDataEventArgs e) {
                try {
                    Data((Channel) sender, e);
                }
                catch (Exception ex) {
                    error = ex;
                }
            };

            channel.Closed += delegate(object sender, ChannelCloseEventArgs e) {
                try {
                    Closed((Channel) sender, e);
                }
                catch (Exception ex) {
                    error = ex;
                }
            };
        }

        protected virtual void Open(Channel channel, ChannelEventArgs e){}
        protected virtual void Data(Channel channel, ChannelDataEventArgs e){}
        protected virtual void Signal(Channel channel, ChannelEventArgs e){}
        protected virtual void Closed(Channel channel, ChannelCloseEventArgs e){}

        protected byte[] CreateRandomBuffer(int size)
        {
            byte[] payload = new byte[size];
            Random rnd = new Random();
            rnd.NextBytes(payload);
            return payload;
        }
    }
}