using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using OxyPlot.Axes;
using System.Drawing.Drawing2D;

namespace SatTracker
{
    public partial class ElevationChartForm : Form
    {
        private readonly SatelliteInfo satellite;

        public ElevationChartForm(SatelliteInfo satelliteInfo)
        {
            InitializeComponent();
            satellite = satelliteInfo;
            DrawElevationChart(satelliteInfo);
        }
        private void DrawElevationChart(SatelliteInfo satellite)
        {
            var model = new PlotModel { Title = $"Biểu đồ góc ngẩng vệ tinh {satellite.Name}" };

            // Create DateTime axis for the timestamps
            model.Axes.Add(new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Thời gian",
                StringFormat = "HH:mm:ss" // Adjust format as needed
            }); 

            foreach (var time in satellite.TrackTimestamps.Take(15)) // In ra 5 giá trị đầu tiên
            {
                Console.WriteLine($"Timestamp: {time.ToLocalTime()} UTC Kind: {time.Kind}");
            }

            // Create linear axis for elevation
            model.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Góc (độ)" });

            // Create a line series for the elevation data
            var series = new LineSeries { Title = "Góc ngẩng" };

            // Populate the series with data points
            for (int i = 0; i < satellite.TrackTimestamps.Count; i++)
            {
                series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(satellite.TrackTimestamps[i]), satellite.ElevationAngles[i]));
            }

            // Add the series to the model
            model.Series.Add(series);

            // Set the Model property of the PlotView to display the chart
            plotView1.Model = model;
        }
    }
}
