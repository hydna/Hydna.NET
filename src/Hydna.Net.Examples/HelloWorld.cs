using System;
using Hydna.Net;

using System.Threading;

namespace Hydna.Net.Examples.HelloWorld
{
    static class HelloWorld
    {
        static Channel channel;

        public static void Main(string[] args) 
        {
            channel = new Channel();

            channel.Connect("http://public.hydna.net/", ChannelMode.ReadWrite);

            channel.Open += c_Open;
            channel.Data += c_Data;
            channel.Closed += c_Closed;

            while (channel != null)
                Thread.SpinWait(1);
        }

        static void c_Open(object sender, ChannelEventArgs e)
        {
            Console.WriteLine("Channel is now open: " + e.Text);
            channel.Send("Hello world");
        }

        static void c_Data(object sender, ChannelEventArgs e)
        {
            Console.WriteLine("Received data: " + e.Text);
            channel.Close();
        }

        static void c_Closed(object sender, ChannelCloseEventArgs e)
        {
            Console.WriteLine("Channel is now closed, bye..");
            channel = null;
        }
    }
}

