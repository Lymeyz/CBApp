using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace CBApp1
{
    public class MessageType
    {
        public MessageType(string type, string message)
        {
            Type = type;
            Message = message;
        }
        public string Type { get; }
        public string Message { get; }
    }
}
