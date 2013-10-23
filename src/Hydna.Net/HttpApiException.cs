using System;
using System.Net;

namespace Hydna.Net
{
    public class HttpApiException : Exception
    {
        WebExceptionStatus status;
        String denyMessage;


        internal HttpApiException (String message)
            : base(message)
        {
            status = WebExceptionStatus.UnknownError;
        }


        internal HttpApiException (String message, Boolean openDenied)
            : base(message)
        {
            denyMessage = message;
            status = WebExceptionStatus.Success;
        }


        internal HttpApiException (String message, WebExceptionStatus webStatus)
            : base(message)
        {
            status = webStatus;
        }


        internal HttpApiException (Exception innerException)
            : base("Unable to comeplete API Request", innerException)
        {
            status = WebExceptionStatus.UnknownError;
        }


        internal HttpApiException (WebException innerException)
            : base("Unable to comeplete API Request!!", innerException)
        {
            status = innerException.Status;
        }


        /// <summary>
        /// Indicates if the Send or Emit was denied due to Open Deny.
        /// </summary>
        /// <value>Returns True if the Request was denied due to an
        /// Open Deny, else False.</value>
        public Boolean OpenDenied
        {
            get
            {
                return !(denyMessage == null);
            }
        }


        /// <summary>
        /// Returns a message description the reason why the message was
        /// denied.
        /// </summary>
        /// <value>A reason why the messages was denied, or null</value>
        public String DenyMessage
        {
            get
            {
                return denyMessage;
            }
        }


        public WebExceptionStatus Status {
            get {
                return status;
            }
        }
    }
}

