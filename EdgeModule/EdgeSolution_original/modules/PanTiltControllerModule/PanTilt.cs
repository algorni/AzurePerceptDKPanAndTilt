namespace PanTiltControllerModule
{ 
    using System;
    using Newtonsoft.Json;

    public class PanTilt
    {
        public int Pan{ get; set; }
        public int Tilt{ get; set; }

        
        public static PanTilt FromJSON(string json)
        {
            PanTilt panTilt = JsonConvert.DeserializeObject<PanTilt>(json);

            return panTilt;
        }
    }
}