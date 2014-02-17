namespace Hydna.Net
{

    using System;

    using NUnit.Framework;

    using Hydna.Net;

    [TestFixture]
    public class ChannelCloseDataTest : BaseTest
    {
        const string Message = "CLOSE_MESSAGE_NET";

        Channel emitListener;
        Channel closingChannel;

        [Test]
        public void Test()
        {
            Timeout(10000);

            emitListener = CreateChannel("", ChannelMode.Read);

            closingChannel = CreateChannel("emit-back-on-close",
                                           ChannelMode.Read);

            Wait();
        }

        protected override void Open(Channel channel, ChannelEventArgs e)
        {
            if (channel == closingChannel) {
                channel.Close(Message);
            }
        }

        protected override void Signal(Channel channel, ChannelEventArgs e)
        {
			Assert.AreEqual(e.Text, Message);
            channel.Close();
        }

        protected override void Closed(Channel channel, ChannelCloseEventArgs e)
        {
            Assert.IsTrue(e.WasClean);
            Assert.IsFalse(e.WasDenied);
            
            if (channel == emitListener) {
                Done();
            }
        }

    }
}


