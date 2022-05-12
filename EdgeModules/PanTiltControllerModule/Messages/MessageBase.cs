using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PanAndTilt.Common
{
    public abstract class  MessageBase
    {
        /// <summary>
        /// The Message Type
        /// </summary>
        public string messageType { get; set; }


        public static MessageBase ParseJson(string json)
        {
            //first of all detect the kind of json message is...  as multiple derived class is possible.
            MessageBase obj = null;

            dynamic jToken = JToken.Parse(json);

            string messageType = jToken.messageType;

            if (string.IsNullOrEmpty(messageType))
                return obj;

            if (messageType == ReportDirectionMessage.MessageType)
                obj = JsonSerializer.Deserialize<ReportDirectionMessage>(json);

            if (messageType == SetDirectionMessage.MessageType)
                obj = JsonSerializer.Deserialize<SetDirectionMessage>(json);
                    
            if (messageType == ReportPiError.MessageType)
                obj = JsonSerializer.Deserialize<ReportPiError>(json);          

            return obj;
        }

        public string ToJson()
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false
            };

            string messageJsonString = JsonSerializer.Serialize(this, this.GetType(), options);

            return messageJsonString;
        }
    }
}
