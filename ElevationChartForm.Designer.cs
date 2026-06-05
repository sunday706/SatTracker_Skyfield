namespace SatTracker
{
    partial class ElevationChartForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            plotView1 = new OxyPlot.WindowsForms.PlotView();
            SuspendLayout();
            // 
            // plotView1
            // 
            plotView1.Dock = DockStyle.Fill;
            plotView1.Location = new Point(0, 0);
            plotView1.Name = "plotView1";
            plotView1.PanCursor = Cursors.Hand;
            plotView1.Size = new Size(1618, 531);
            plotView1.TabIndex = 0;
            plotView1.Text = "plotView1";
            plotView1.ZoomHorizontalCursor = Cursors.SizeWE;
            plotView1.ZoomRectangleCursor = Cursors.SizeNWSE;
            plotView1.ZoomVerticalCursor = Cursors.SizeNS;
            // 
            // ElevationChartForm
            // 
            AutoScaleDimensions = new SizeF(12F, 30F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1618, 531);
            Controls.Add(plotView1);
            Name = "ElevationChartForm";
            Text = "Plot";
            ResumeLayout(false);
        }

        #endregion

        private OxyPlot.WindowsForms.PlotView plotView1;
    }
}