namespace Hydna.Net
{

    using System;
    using NUnit.Framework;

    using Hydna.Net;

    [TestFixture]
    public class HttpApiClientTest
    {

        static string URL = "testing.hydna.net";

        [Test]
        public void TestSendA()
        {
            HttpApiClient client = HttpApiClient.create(URL);
            client.Send("Test");
        }

        [Test]
        public void TestSendAsync () {

            HttpApiClient client = HttpApiClient.create(URL);
            IAsyncResult result = client.BeginSend("Test", null, null);
            result.AsyncWaitHandle.WaitOne();
            client.EndSend(result);   
        }

        [Test]
        public void TestDeny () {
			bool didThrow = false;

            HttpApiClient client = HttpApiClient.create(URL + "/open-deny");

            try
            {
                client.Send("Test");
            }
            catch (HttpApiException ex) 
            {
                Assert.AreEqual(ex.DenyMessage, "DENIED");
				didThrow = true;
            }

			Assert.IsTrue(didThrow);
        }
    }
}


