using System;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;

namespace Hydna.Net
{


    /// <summary>
    /// Http API client for pushing data and signals to a Hydna Endpoint.
    /// </summary>
    /// <example>
    /// using Hydna.Net;
    ///
    /// HttpApiClient client;
    /// client = HttpApiClient.create("public.hydna.net");
    /// client.send("Hello world");
    /// </example>
    public class HttpApiClient
    {


        static int PAYLOAD_MAX_SIZE = 0xFFFF;


        Uri endPointUri;
        HttpWebRequest currentHttpRequest;
        ProgressResult currentProgressResult;
        IAsyncResult currentAsyncResult;


        enum RequestType { Data, Signal }
        enum ContentType { Text, Binary }


        private HttpApiClient (Uri endpoint)
        {
            endPointUri = endpoint;
            currentHttpRequest = null;
            currentProgressResult = null;
            currentAsyncResult = null;
        }


        /// <summary>
        /// Create a new HttpApiClient bound to the specified url.
        /// </summary>
        /// <param name="url">Url of the channel</param>
        public static HttpApiClient create(string url)
        {
            Uri uri;

            if (url == null)
            {
                throw new ArgumentNullException("url", "Expected an URL");
            }

            if (url.Contains("://") == false)
            {
                UriBuilder builder = new UriBuilder("http://" + url);
                uri = builder.Uri;
            }
            else
            {
                uri = new Uri(url);
            }

            return create(uri);
        }


        /// <summary>
        /// Create a new HttpApiClient bound to the specified url.
        /// </summary>
        /// <param name="url">Url of the channel</param>
        public static HttpApiClient create(Uri url)
        {
            if (url.Scheme != "http" && url.Scheme != "https")
            {
                throw new ArgumentNullException("url", "Unsupported Scheme");
            }

            return new HttpApiClient(url);
        }


        /// <summary>
        /// Send the specified buffer to the underlying Channel.
        /// </summary>
        /// <param name="buffer">An array of bytes</param>
        public void Send(byte[] buffer) {
            IAsyncResult result = BeginSend(buffer, null, null);
            result.AsyncWaitHandle.WaitOne();
            EndSend(result);
        }


        /// <summary>
        /// Send the specified buffer to the underlying Channel.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies count
        /// bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which
        /// to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the
        /// current stream.</param>
        public void Send(byte[] buffer, int offset, int count) {
            IAsyncResult result = BeginSend(buffer, offset, count, null, null);
            result.AsyncWaitHandle.WaitOne();
            EndSend(result);
        }


        /// <summary>
        /// Send the specified message to the underlying Channel.
        /// </summary>
        /// <param message="message">The message to send</param>
        public void Send(String message) {
            IAsyncResult result = BeginSend(message, null, null);
            result.AsyncWaitHandle.WaitOne();
            EndSend(result);
        }


        /// <summary>
        /// Emit the specified buffer to the underlying Channel.
        /// </summary>
        /// <param name="buffer">An array of bytes</param>
        public void Emit(byte[] buffer) {
            IAsyncResult result = BeginEmit(buffer, null, null);
            result.AsyncWaitHandle.WaitOne();
            EndEmit(result);
        }


        /// <summary>
        /// Emit the specified buffer to the underlying Channel.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies count
        /// bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which
        /// to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the
        /// current stream.</param>
        public void Emit(byte[] buffer, int offset, int count) {
            IAsyncResult result = BeginEmit(buffer, offset, count, null, null);
            result.AsyncWaitHandle.WaitOne();
            EndEmit(result);
        }


        /// <summary>
        /// Emit the specified message to the underlying Channel.
        /// </summary>
        /// <param message="message">The message to send</param>
        public void Emit(String message) {
            IAsyncResult result = BeginEmit(message, null, null);
            result.AsyncWaitHandle.WaitOne();
            EndEmit(result);
        }


        /// <summary>
        /// Send the specified message to the underlying Channel.
        /// </summary>
        /// <returns>An IAsyncResult that represents the asynchronous write,
        ///  which could still be pending.</returns>
        /// <param name="data">Data.</param>
        /// <param name="callback">Callback.</param>
        /// <param name="state">State.</param>
        public IAsyncResult BeginSend(byte[] data,
                                      AsyncCallback callback,
                                      Object state)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (data.Length == 0)
            {
                throw new ArgumentException("Cannot be zero-length", "data");
            }

            return BeginSend(data, 0, data.Length, callback, state);
        }


        /// <summary>
        /// Send the specified buffer to the underlying Channel.
        /// </summary>
        /// <returns>An IAsyncResult that represents the asynchronous write,
        /// which could still be pending.</returns>
        /// <param name="buffer">An array of bytes. This method copies count
        /// bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which
        ///  to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the
        /// current stream.</param>
        /// <param name="callback">Callback.</param>
        /// <param name="state">State.</param>
        public IAsyncResult BeginSend(byte[] buffer,
                                      int offset,
                                      int count,
                                      AsyncCallback callback,
                                      Object state)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("data");
            }

            if (count == 0)
            {
                throw new ArgumentException("Cannot be zero-length", "data");
            }

            if (offset + count > buffer.Length) {
                throw new IndexOutOfRangeException();
            }

            return BeginRequest(buffer,
                                offset,
                                count,
                                ContentType.Binary,
                                RequestType.Data,
                                callback,
                                state);
        }


        /// <summary>
        /// Begins the send data to specificed channel.
        /// </summary>
        /// <returns>An IAsyncResult that represents the asynchronous write,
        /// which could still be pending.</returns>
        /// <param name="message">The message to send.</param>
        /// <param name="callback">Callback.</param>
        /// <param name="state">State.</param>
        public IAsyncResult BeginSend(string message,
                                      AsyncCallback callback,
                                      Object state)
        {
            if (message == null)
            {
                throw new ArgumentNullException("data");
            }

            if (message.Length == 0)
            {
                throw new ArgumentException("Expected at least one char",
                                            "data");
            }

            byte[] data = Encoding.UTF8.GetBytes(message);

            return BeginRequest(data,
                                0,
                                data.Length,
                                ContentType.Text,
                                RequestType.Data,
                                callback,
                                state);
        }


        /// <summary>
        /// Emit the specified message to the underlying Channel.
        /// </summary>
        /// <returns>An IAsyncResult that represents the asynchronous write,
        /// which could still be pending.</returns>
        /// <param name="data">The data to send</param>
        /// <param name="callback">Callback.</param>
        /// <param name="state">State.</param>
        public IAsyncResult BeginEmit(byte[] data,
                                      AsyncCallback callback,
                                      Object state)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (data.Length == 0)
            {
                throw new ArgumentException("Cannot be zero-length", "data");
            }

            return BeginEmit(data, 0, data.Length, callback, state);
        }


        /// <summary>
        /// Emit the specified buffer to the underlying Channel.
        /// </summary>
        /// <returns>An IAsyncResult that represents the asynchronous write,
        ///  which could still be pending.</returns>
        /// <param name="buffer">An array of bytes. This method copies count
        ///  bytes from buffer to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in buffer at which
        ///  to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the
        /// current stream.</param>
        /// <param name="callback">Callback.</param>
        /// <param name="state">State.</param>
        public IAsyncResult BeginEmit(byte[] buffer,
                                      int offset,
                                      int count,
                                      AsyncCallback callback,
                                      Object state)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("data");
            }

            if (count == 0)
            {
                throw new ArgumentException("Cannot be zero-length", "data");
            }

            if (offset + count > buffer.Length) {
                throw new IndexOutOfRangeException();
            }

            return BeginRequest(buffer,
                                offset,
                                count,
                                ContentType.Binary,
                                RequestType.Signal,
                                callback,
                                state);
        }


        /// <summary>
        /// Emit the specified buffer to the underlying Channel.
        /// </summary>
        /// <returns>An IAsyncResult that represents the asynchronous write,
        /// which could still be pending.</returns>
        /// <param name="message">The message to emit.</param>
        /// <param name="callback">Callback.</param>
        /// <param name="state">State.</param>
        public IAsyncResult BeginEmit(string message,
                                      AsyncCallback callback,
                                      Object state)
        {
            if (message == null)
            {
                throw new ArgumentNullException("data");
            }

            if (message.Length == 0)
            {
                throw new ArgumentException("Expected at least one char",
                                            "data");
            }

            byte[] data = Encoding.UTF8.GetBytes(message);

            return BeginRequest(data,
                                0,
                                data.Length,
                                ContentType.Text,
                                RequestType.Signal,
                                callback,
                                state);
        }


        /// <summary>
        /// Ends an asynchronous Send operation.
        /// </summary>
        /// <param name="result">A reference to the outstanding asynchronous
        /// I/O request.</param>
        public void EndSend(IAsyncResult result)
        {
            ProgressResult progressResult;

            progressResult = (ProgressResult) result;

            if (progressResult == null ||
                progressResult.type != RequestType.Data)
            {
                throw new ArgumentException("Bad handler", "result");
            }

            EndRequest(progressResult);
        }


        /// <summary>
        /// Ends an asynchronous Emit operation.
        /// </summary>
        /// <param name="result">A reference to the outstanding asynchronous
        /// I/O request.</param>
        public void EndEmit(IAsyncResult result)
        {
            ProgressResult progressResult;

            progressResult = (ProgressResult) result;

            if (progressResult == null ||
                progressResult.type != RequestType.Signal)
            {
                throw new ArgumentException("Bad handler", "result");
            }

            EndRequest(progressResult);
        }


        IAsyncResult BeginRequest(byte[] data,
                                  int offset,
                                  int count,
                                  ContentType contentType,
                                  RequestType requestType,
                                  AsyncCallback userCallback,
                                  object userState)
        {
            AsyncCallback callback = null;
            IAsyncResult result = null;

            if (currentProgressResult != null)
            {
                throw new NotSupportedException ("HttpApiClient does not " +
                                                 "support concurrent I/O " +
                                                 "operations.");
            }

            if (count > PAYLOAD_MAX_SIZE)
            {
                throw new ArgumentException("Payload exceedes max size");
            }

            currentHttpRequest = (HttpWebRequest) WebRequest.Create(endPointUri);

            currentHttpRequest.Method = "POST";

            if (contentType == ContentType.Binary)
            {
                currentHttpRequest.ContentType = "application/octet-stream";
            }
            else
            {
                currentHttpRequest.ContentType = "text/plain";
            }

            if (requestType == RequestType.Signal)
            {
                currentHttpRequest.Headers.Add("X-Emit", "yes");
            }

            callback = new AsyncCallback(RequestCallback);
            result = currentHttpRequest.BeginGetRequestStream(callback, null);
            currentAsyncResult = result;

            currentProgressResult = new ProgressResult(requestType,
                                                       data,
                                                       offset,
                                                       count,
                                                       userCallback,
                                                       userState);

            return currentProgressResult;
        }


        void EndRequest(ProgressResult result)
        {
            ProgressResult progressResult = null;
            Exception exception = null;
            String denyMessage = null;

            progressResult = currentProgressResult;
            currentProgressResult = null;

            if (result == null)
            {
                throw new ArgumentNullException("result");
            }

            if (progressResult == null)
            {
                throw new ArgumentException("A handle to the pending " +
                                            "operation is not available.");
            }

            if (currentAsyncResult.IsCompleted == false)
            {
                exception = new WebException("Aborted",
                                             WebExceptionStatus.RequestCanceled);
                CompleteApiCall(exception, null);
            }

            denyMessage = progressResult.denyMessage;
            exception = progressResult.throwedException;

            if (denyMessage != null)
            {
                throw new HttpApiException(denyMessage, true);
            }

            if (exception is WebException)
            {
                throw new HttpApiException((WebException) exception);
            }

            if (exception != null)
            {
                Console.WriteLine(exception.GetType());
                throw new HttpApiException(exception);
            }
        }


        void RequestCallback(IAsyncResult result)
        {
            Stream stream = null;
            AsyncCallback callback = null;

            try
            {
                stream = currentHttpRequest.EndGetRequestStream(result);
            }
            catch (Exception exception)
            {
                CompleteApiCall(exception, null);
                return;
            }

            stream.Write(currentProgressResult.data,
                currentProgressResult.offset,
                    currentProgressResult.length);

            stream.Close();

            callback = new AsyncCallback(ResponseCallback);
            currentAsyncResult = currentHttpRequest.BeginGetResponse(callback,
                                                                     null);
        }


        void ResponseCallback(IAsyncResult result)
        {
            String denyMessage = null;

            currentAsyncResult = null;

            try
            {
                currentHttpRequest.EndGetResponse(result);
            }
            catch (WebException exception)
            {
                denyMessage = readOpenDeny((HttpWebResponse) exception.Response);
                CompleteApiCall(exception, denyMessage);
                return;
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception.GetType());
                CompleteApiCall(exception, null);
                return;
            }

            CompleteApiCall(null, null);
        }


        void CompleteApiCall(Exception exception, String denyMessage)
        {
            if (currentHttpRequest != null)
            {
                currentHttpRequest.Abort();
                currentHttpRequest = null;
            }

            if (currentProgressResult != null)
            {
                currentProgressResult.Compelete(exception, denyMessage);
            }
        }


        String readOpenDeny(HttpWebResponse response)
        {
            Stream stream = null;
            byte[] data = null;

            // TODO: Check that we are dealing with UTF8-data
            Console.WriteLine(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.BadRequest &&
                response.ContentLength > 0)
            {
                stream = response.GetResponseStream();
                if (stream.Length > 0)
                {
                    try
                    {
                        data = new byte[stream.Length];
                        stream.Read(data, 0, data.Length);
                        return Encoding.UTF8.GetString(data);
                    }
                    catch
                    {
                    }
                }
            }

            return null;
        }


        class ProgressResult : IAsyncResult
        {

            internal Exception throwedException;
            internal String denyMessage;

            internal RequestType type;
            internal byte[] data;
            internal int offset;
            internal int length;

            internal Boolean completed;

            AsyncCallback callback;
            Object state;
            ManualResetEvent resetEvent;


            internal ProgressResult(RequestType requestType,
                                    byte[] userData,
                                    int userOffset,
                                    int userLength,
                                    AsyncCallback userCallback,
                                    Object userState)
            {
                type = requestType;
                data = userData;
                offset = userOffset;
                length = userLength;
                callback = userCallback;
                state = userState;
                resetEvent = new ManualResetEvent(false);
                completed = false;
            }


            object IAsyncResult.AsyncState {
                get {
                    return state;
                }
            }


            System.Threading.WaitHandle IAsyncResult.AsyncWaitHandle {
                get {
                    return resetEvent;
                }
            }


            bool IAsyncResult.CompletedSynchronously {
                get {
                    return completed;
                }
            }


            bool IAsyncResult.IsCompleted {
                get {
                    return completed;
                }
            }


            internal void Compelete(Exception exception, String message) {
                if (completed == true) {
                    return;
                }

                throwedException = exception;
                denyMessage = message;
                completed = true;

                resetEvent.Set();

                if (callback != null) {
                    callback.Invoke(this);
                }
            }
        }
    }
}