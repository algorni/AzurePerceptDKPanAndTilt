using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


//{
//    "body": {
//        "NEURAL_NETWORK": [
//          {
//            "bbox": [
//              0.471,
//          0.394,
//          0.926,
//          1
//            ],
//        "label": "person",
//        "confidence": "0.989258",
//        "timestamp": "1652111961888937714"
//          },
//      {
//            "bbox": [
//              0.376,
//          0.588,
//          0.67,
//          0.979
//        ],
//        "label": "chair",
//        "confidence": "0.687988",
//        "timestamp": "1652111961888937714"
//      },
//      {
//            "bbox": [
//              0.768,
//          0.432,
//          0.977,
//          0.674
//        ],
//        "label": "tv",
//        "confidence": "0.653809",
//        "timestamp": "1652111961888937714"
//      }
//    ]
//  },
//  "enqueuedTime": "Mon May 09 2022 17:59:23 GMT+0200 (Central European Summer Time)"
//}

namespace percept2pantilt.Percept
{
    public class PerceptAIMessages
    {
        public List<NEURALNETWORK> NEURAL_NETWORK { get; set; }
    }

    public class NEURALNETWORK
    {
        //ulx,uly,drx,dry 0..1 from top left (0,0)
        public List<double> bbox { get; set; }
        public string label { get; set; }
        public string confidence { get; set; }

        // "timestamp": "1652111961888937714"    unixtime with milliseconds
        public string timestamp { get; set; }

        /// <summary>
        /// Calculate the Delta Position of the baricenter of the bounding box.
        /// </summary>
        /// <returns></returns>
        public DeltaPos GetDelta()
        { 
            var baricenterX = (bbox[2] + bbox[0]) / 2.0;
            var baricenterY = (bbox[3] + bbox[1]) / 2.0;

            return new DeltaPos() { deltaXFromCenter = -(baricenterX - 0.5) * 2.0, deltaYFromCenter = - (baricenterY - 0.5) * 2 };
        }
    }

    public class DeltaPos
    {
        /// <summary>
        /// -1..1
        /// </summary>
        public double deltaXFromCenter { get; set; }
        /// <summary>
        /// -1..1
        /// </summary>
        public double deltaYFromCenter { get; set; }
    }
    


}
