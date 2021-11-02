namespace PanTiltControllerModule
{ 
    using System;
    using Newtonsoft.Json;

    /// <summary>
    /// Pan and Tilt entity
    /// </summary>
    public class PanTilt
    {
        public int Pan{ get; set; }
        public int Tilt{ get; set; }

        
        /// <summary>
        /// From JSON constructor
        /// </summary>
        /// <param name="json"></param>
        /// <returns></returns>
        public static PanTilt FromJSON(string json)
        {
            PanTilt panTilt = JsonConvert.DeserializeObject<PanTilt>(json);

            return panTilt;
        }
    }
}