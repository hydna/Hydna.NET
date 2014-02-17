namespace Hydna.Net
{

    using System;

    using NUnit.Framework;

    using Hydna.Net;

    [TestFixture]
    public class ChannelSignalTest : BaseTest
    {

        [Test]
        public void Test()
        {
            Timeout(10000);

            CreateChannel("ping-back", ChannelMode.ReadWriteEmit);

            Wait();
        }

        protected override void Open(Channel channel, ChannelEventArgs e)
        {
            channel.Emit("ping");
        }

        protected override void Signal(Channel channel, ChannelEventArgs e)
        {
			Assert.AreEqual(e.Text, "pong");
            channel.Close();
        }

        protected override void Closed(Channel channel, ChannelCloseEventArgs e)
        {
            Assert.IsTrue(e.WasClean);
            Assert.IsFalse(e.WasDenied);
            Done();
        }

    }
}


