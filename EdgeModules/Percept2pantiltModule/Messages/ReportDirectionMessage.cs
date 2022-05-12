using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PanAndTilt.Common
{
    public class ReportDirectionMessage : MessageBase
    {        
        public const string MessageType = "reportDirection";

        public ReportDirectionMessage() : base()
        {
            base.messageType = MessageType;
        }

        public int expectedPan { get; set; }

        public int expectedTilt { get; set; }

        public List<double> acc { get; set; }        
        
        public List<double> mag { get; set; }        
    }
}
