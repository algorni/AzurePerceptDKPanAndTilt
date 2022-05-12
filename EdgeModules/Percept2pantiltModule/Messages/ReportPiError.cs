using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PanAndTilt.Common
{
    internal class ReportPiError : MessageBase
    {
        public const string MessageType = "errorReporting";

        public ReportPiError() : base()
        {
            base.messageType = MessageType;
        }

        public string errorMessage { get; set; }
    }
}
