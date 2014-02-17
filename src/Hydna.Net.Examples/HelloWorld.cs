using System;
using Hydna.Net;

using System.Threading;

namespace Hydna.Net.Examples.HelloWorld
{
    static class HelloWorld
    {
        public static void Main(string[] args) 
        {
            Channel channel = new Channel();

            channel.Open += c_Open;
            channel.Data += c_Data;
            channel.Closed += delegate(object sender, ChannelCloseEventArgs e) {
                Console.WriteLine("Channel is now closed, reason: " + e.Reason);
            };
            
            channel.Connect("https://testing.hydna.net/", ChannelMode.ReadWrite);

            Channel channel2 = new Channel();
            channel2.Open += c_Open;
            channel2.Connect("publdasdsa.hydna.net/notwo?lol", ChannelMode.ReadWrite);

            while (channel.State != ChannelState.Closed)
                Thread.SpinWait(10000000);
        }

        static void c_Open(object sender, ChannelEventArgs e)
        {
            Console.WriteLine("Channel is now open: " + e.Text);
            Channel channel = (Channel) sender;
            channel.Send("Hello world");
        }

        static void c_Data(object sender, ChannelEventArgs e)
        {
            Console.WriteLine("Received data: " + e.Text);
            Channel channel = (Channel) sender;
            channel.Close();
        }

        static void c_Closed(object sender, ChannelCloseEventArgs e)
        {
            Console.WriteLine("Channel is now closed");
        }
    }
}

