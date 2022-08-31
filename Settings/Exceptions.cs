using System;
using System.Runtime.Serialization;

namespace Jannesen.Service.Settings
{
    [Serializable]
    public class AppSettingException : Exception
    {
        public AppSettingException(string message) : base(message)
        {
        }
        public AppSettingException(string message, Exception exception) : base(message, exception)
        {
        }

        protected AppSettingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
