using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PanAndTilt.Common
{
    public class SetDirectionMessage : MessageBase
    {
        public const string MessageType = "setDirection";

        public SetDirectionMessage():base()
        {
            base.messageType = MessageType;
        }

        public int expectedPan { get; set; }

        public int expectedTilt { get; set; }
    }
}
