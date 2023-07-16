using System;
using System.Runtime.Serialization;

namespace CBApp1
{
    [Serializable]
    internal class DataFetcherException : Exception
    {
        public DataFetcherException()
        {
        }

        public DataFetcherException(string message) : base(message)
        {
        }

        public DataFetcherException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DataFetcherException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}