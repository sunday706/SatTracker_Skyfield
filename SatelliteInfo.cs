using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Drawing.Drawing2D;
using System.Xml.Linq;

namespace SatTracker
{
    public class SatelliteInfo
    {
        public string Name { get; set; }
        public DateTime? StartVisibleTime { get; set; }
        public DateTime? EndVisibleTime { get; set; }
        public List<DateTime> TrackTimestamps { get; set; }
        public List<double> AzimuthAngles { get; set; }
        public List<double> ElevationAngles { get; set; }
        public List<double> LatitudeAngles { get; set; }
        public List<double> LongtitudeAngles { get; set; }
        public List<double> SolarAzimuth { get; set; }
        public List<double> SolarElevation { get; set; }
        public List<double> RelativeAzimuth { get; set; }
        public List<double> RelativeElevation { get; set; }      
    }
    public class SatelliteInfoDisplay
    {
        public string Name { get; set; }
        public DateTime Timestamp { get; set; }
        public double Azimuth { get; set; }
        public double Elevation { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
    public class SatelliteVisible
    {
        public string Name { get; set; }
        public DateTime? StartTimeVisible { get; set; }
        public DateTime? EndTimeVisible { get; set; }
    }
}