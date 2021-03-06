namespace Hydna.Net
{
    using System;

    using NUnit.Framework;

    using Hydna.Net;

    [TestFixture]
    public class ChannelBurstTest : BaseTest
    {

        int count = 0;

        byte[] payload;

        [Test]
        public void Test()
        {
            Timeout(10000);

            CreateRandomChannel(ChannelMode.ReadWrite);
            payload = CreateRandomBuffer(512);

            Wait();
        }

        protected override void Open(Channel channel, ChannelEventArgs e)
        {
            for (int i = 0; i < 1000; i++) {
                channel.Send(payload);
            }
        }

        protected override void Data(Channel channel, ChannelDataEventArgs e)
        {
			Assert.IsTrue(Compare(payload, e.Payload));
            if (++count == 1000) {
                channel.Close();
            }
        }

        protected override void Closed(Channel channel, ChannelCloseEventArgs e)
        {
            Assert.IsTrue(e.WasClean);
            Assert.IsFalse(e.WasDenied);
            Done();
        }

    }
}


