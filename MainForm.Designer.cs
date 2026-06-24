namespace SatTracker
{
    partial class MainForm
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
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
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            DataGridViewCellStyle dataGridViewCellStyle3 = new DataGridViewCellStyle();
            DataGridViewCellStyle dataGridViewCellStyle4 = new DataGridViewCellStyle();
            openFileDialog1 = new OpenFileDialog();
            MainTimer = new System.Windows.Forms.Timer(components);
            toolStrip1 = new ToolStrip();
            btnZoomIn = new ToolStripButton();
            btnZoomOut = new ToolStripButton();
            btnCenter = new ToolStripButton();
            btnFile = new ToolStripButton();
            btnSetting = new ToolStripButton();
            panel5 = new Panel();
            panel1 = new Panel();
            panel8 = new Panel();
            btnStop = new GlassButton();
            btnAziLeft = new GlassButton();
            btnAziRight = new GlassButton();
            btnEleDown = new GlassButton();
            btnEleUp = new GlassButton();
            pnlAzi = new Panel();
            lblAziStt = new Label();
            btnAziGo = new Button();
            txbAziTargetPos = new TextBox();
            label8 = new Label();
            label2 = new Label();
            label1 = new Label();
            txbAziPos = new TextBox();
            txbAziSpeed = new TextBox();
            btnAziEnable = new Button();
            btnAziConnect = new Button();
            lblAziPosition = new Label();
            lblAziSpeed = new Label();
            lblAzi = new Label();
            panel9 = new Panel();
            lblEleStt = new Label();
            btnEleGo = new Button();
            txbEleTargetPos = new TextBox();
            label9 = new Label();
            label3 = new Label();
            label4 = new Label();
            txbElePos = new TextBox();
            txbEleSpeed = new TextBox();
            btnEleEnable = new Button();
            btnEleConnect = new Button();
            label5 = new Label();
            label6 = new Label();
            label7 = new Label();
            panel7 = new Panel();
            tabControl1 = new TabControl();
            tabPage1 = new TabPage();
            panel10 = new Panel();
            gMap = new GMap.NET.WindowsForms.GMapControl();
            tabPage2 = new TabPage();
            panel11 = new Panel();
            plotView2 = new OxyPlot.WindowsForms.PlotView();
            panel6 = new Panel();
            btnTracking = new Button();
            btnManual = new Button();
            lblModeControl = new Label();
            plotView1 = new OxyPlot.WindowsForms.PlotView();
            splitter1 = new Splitter();
            panel2 = new Panel();
            panel4 = new Panel();
            dataGridViewSatellites = new DataGridView();
            panel3 = new Panel();
            dataGridInforSatellites = new DataGridView();
            lblInfoTracking = new Label();
            toolStrip1.SuspendLayout();
            panel5.SuspendLayout();
            panel1.SuspendLayout();
            panel8.SuspendLayout();
            pnlAzi.SuspendLayout();
            panel9.SuspendLayout();
            panel7.SuspendLayout();
            tabControl1.SuspendLayout();
            tabPage1.SuspendLayout();
            panel10.SuspendLayout();
            tabPage2.SuspendLayout();
            panel11.SuspendLayout();
            panel6.SuspendLayout();
            panel2.SuspendLayout();
            panel4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewSatellites).BeginInit();
            panel3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridInforSatellites).BeginInit();
            SuspendLayout();
            // 
            // openFileDialog1
            // 
            openFileDialog1.FileName = "openFileDialog1";
            // 
            // MainTimer
            // 
            MainTimer.Enabled = true;
            MainTimer.Interval = 1000;
            MainTimer.Tick += MainTimer_Tick;
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new Size(28, 28);
            toolStrip1.Items.AddRange(new ToolStripItem[] { btnZoomIn, btnZoomOut, btnCenter, btnFile, btnSetting });
            toolStrip1.Location = new Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Padding = new Padding(0, 0, 2, 0);
            toolStrip1.Size = new Size(1309, 35);
            toolStrip1.TabIndex = 6;
            toolStrip1.Text = "toolStrip1";
            // 
            // btnZoomIn
            // 
            btnZoomIn.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnZoomIn.Image = (Image)resources.GetObject("btnZoomIn.Image");
            btnZoomIn.ImageTransparentColor = Color.Magenta;
            btnZoomIn.Name = "btnZoomIn";
            btnZoomIn.Size = new Size(32, 32);
            btnZoomIn.Text = "toolStripButton1";
            btnZoomIn.Click += btnZoomIn_Click;
            // 
            // btnZoomOut
            // 
            btnZoomOut.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnZoomOut.Image = (Image)resources.GetObject("btnZoomOut.Image");
            btnZoomOut.ImageTransparentColor = Color.Magenta;
            btnZoomOut.Name = "btnZoomOut";
            btnZoomOut.Size = new Size(32, 32);
            btnZoomOut.Text = "toolStripButton2";
            btnZoomOut.Click += btnZoomOut_Click;
            // 
            // btnCenter
            // 
            btnCenter.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnCenter.Image = (Image)resources.GetObject("btnCenter.Image");
            btnCenter.ImageTransparentColor = Color.Magenta;
            btnCenter.Name = "btnCenter";
            btnCenter.Size = new Size(32, 32);
            btnCenter.Text = "toolStripButton3";
            btnCenter.Click += btnCenter_Click;
            // 
            // btnFile
            // 
            btnFile.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnFile.Image = (Image)resources.GetObject("btnFile.Image");
            btnFile.ImageTransparentColor = Color.Magenta;
            btnFile.Name = "btnFile";
            btnFile.Size = new Size(32, 32);
            btnFile.Text = "toolStripButton1";
            btnFile.Click += btnFile_Click;
            // 
            // btnSetting
            // 
            btnSetting.DisplayStyle = ToolStripItemDisplayStyle.Image;
            btnSetting.Image = (Image)resources.GetObject("btnSetting.Image");
            btnSetting.ImageTransparentColor = Color.Magenta;
            btnSetting.Name = "btnSetting";
            btnSetting.Size = new Size(32, 32);
            btnSetting.Text = "toolStripButton1";
            // 
            // panel5
            // 
            panel5.Controls.Add(panel1);
            panel5.Controls.Add(splitter1);
            panel5.Controls.Add(panel2);
            panel5.Dock = DockStyle.Fill;
            panel5.Location = new Point(0, 35);
            panel5.Margin = new Padding(2);
            panel5.Name = "panel5";
            panel5.Size = new Size(1309, 570);
            panel5.TabIndex = 7;
            // 
            // panel1
            // 
            panel1.Controls.Add(panel8);
            panel1.Controls.Add(pnlAzi);
            panel1.Controls.Add(panel9);
            panel1.Controls.Add(panel7);
            panel1.Controls.Add(panel6);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(338, 0);
            panel1.Margin = new Padding(2);
            panel1.Name = "panel1";
            panel1.Size = new Size(971, 570);
            panel1.TabIndex = 7;
            // 
            // panel8
            // 
            panel8.BackColor = SystemColors.ActiveBorder;
            panel8.Controls.Add(btnStop);
            panel8.Controls.Add(btnAziLeft);
            panel8.Controls.Add(btnAziRight);
            panel8.Controls.Add(btnEleDown);
            panel8.Controls.Add(btnEleUp);
            panel8.Location = new Point(781, 384);
            panel8.Margin = new Padding(2);
            panel8.Name = "panel8";
            panel8.Size = new Size(184, 182);
            panel8.TabIndex = 0;
            // 
            // btnStop
            // 
            btnStop.Group = null;
            btnStop.Location = new Point(63, 63);
            btnStop.Margin = new Padding(2);
            btnStop.Name = "btnStop";
            btnStop.Size = new Size(58, 50);
            btnStop.TabIndex = 4;
            btnStop.Text = "STOP";
            btnStop.Value = null;
            // 
            // btnAziLeft
            // 
            btnAziLeft.Group = null;
            btnAziLeft.Location = new Point(3, 63);
            btnAziLeft.Margin = new Padding(2);
            btnAziLeft.Name = "btnAziLeft";
            btnAziLeft.Size = new Size(58, 50);
            btnAziLeft.TabIndex = 3;
            btnAziLeft.Text = "Azi Lef";
            btnAziLeft.Value = null;
            btnAziLeft.Click += AziLeft_Click;
            // 
            // btnAziRight
            // 
            btnAziRight.Group = null;
            btnAziRight.Location = new Point(123, 63);
            btnAziRight.Margin = new Padding(2);
            btnAziRight.Name = "btnAziRight";
            btnAziRight.Size = new Size(58, 50);
            btnAziRight.TabIndex = 2;
            btnAziRight.Text = "Azi Rig";
            btnAziRight.Value = null;
            btnAziRight.Click += AziRight_Click;
            // 
            // btnEleDown
            // 
            btnEleDown.Group = null;
            btnEleDown.Location = new Point(63, 118);
            btnEleDown.Margin = new Padding(2);
            btnEleDown.Name = "btnEleDown";
            btnEleDown.Size = new Size(58, 50);
            btnEleDown.TabIndex = 1;
            btnEleDown.Text = "Ele Down";
            btnEleDown.Value = null;
            btnEleDown.Click += EleDown_Click;
            // 
            // btnEleUp
            // 
            btnEleUp.Group = null;
            btnEleUp.Location = new Point(63, 8);
            btnEleUp.Margin = new Padding(2);
            btnEleUp.Name = "btnEleUp";
            btnEleUp.Size = new Size(58, 50);
            btnEleUp.TabIndex = 0;
            btnEleUp.Text = "Ele Up";
            btnEleUp.Value = null;
            btnEleUp.Click += EleUp_Click;
            // 
            // pnlAzi
            // 
            pnlAzi.BackColor = SystemColors.ActiveBorder;
            pnlAzi.Controls.Add(lblAziStt);
            pnlAzi.Controls.Add(btnAziGo);
            pnlAzi.Controls.Add(txbAziTargetPos);
            pnlAzi.Controls.Add(label8);
            pnlAzi.Controls.Add(label2);
            pnlAzi.Controls.Add(label1);
            pnlAzi.Controls.Add(txbAziPos);
            pnlAzi.Controls.Add(txbAziSpeed);
            pnlAzi.Controls.Add(btnAziEnable);
            pnlAzi.Controls.Add(btnAziConnect);
            pnlAzi.Controls.Add(lblAziPosition);
            pnlAzi.Controls.Add(lblAziSpeed);
            pnlAzi.Controls.Add(lblAzi);
            pnlAzi.Location = new Point(781, 202);
            pnlAzi.Name = "pnlAzi";
            pnlAzi.Size = new Size(184, 179);
            pnlAzi.TabIndex = 1;
            // 
            // lblAziStt
            // 
            lblAziStt.AutoSize = true;
            lblAziStt.Location = new Point(5, 126);
            lblAziStt.Name = "lblAziStt";
            lblAziStt.Size = new Size(96, 15);
            lblAziStt.TabIndex = 12;
            lblAziStt.Text = "Azimuth Status...";
            // 
            // btnAziGo
            // 
            btnAziGo.Location = new Point(127, 98);
            btnAziGo.Name = "btnAziGo";
            btnAziGo.Size = new Size(32, 25);
            btnAziGo.TabIndex = 11;
            btnAziGo.Text = "GO";
            btnAziGo.UseVisualStyleBackColor = true;
            // 
            // txbAziTargetPos
            // 
            txbAziTargetPos.Location = new Point(65, 99);
            txbAziTargetPos.Name = "txbAziTargetPos";
            txbAziTargetPos.Size = new Size(62, 23);
            txbAziTargetPos.TabIndex = 10;
            txbAziTargetPos.TextAlign = HorizontalAlignment.Right;
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(3, 103);
            label8.Name = "label8";
            label8.Size = new Size(65, 15);
            label8.TabIndex = 9;
            label8.Text = "Target Pos:";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(129, 70);
            label2.Name = "label2";
            label2.Size = new Size(29, 15);
            label2.TabIndex = 8;
            label2.Text = "DEG";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(127, 37);
            label1.Name = "label1";
            label1.Size = new Size(32, 15);
            label1.TabIndex = 7;
            label1.Text = "RPM";
            // 
            // txbAziPos
            // 
            txbAziPos.Enabled = false;
            txbAziPos.Location = new Point(65, 66);
            txbAziPos.Name = "txbAziPos";
            txbAziPos.ReadOnly = true;
            txbAziPos.Size = new Size(62, 23);
            txbAziPos.TabIndex = 6;
            txbAziPos.TextAlign = HorizontalAlignment.Right;
            // 
            // txbAziSpeed
            // 
            txbAziSpeed.Location = new Point(65, 33);
            txbAziSpeed.Name = "txbAziSpeed";
            txbAziSpeed.ReadOnly = true;
            txbAziSpeed.Size = new Size(62, 23);
            txbAziSpeed.TabIndex = 5;
            txbAziSpeed.TextAlign = HorizontalAlignment.Right;
            // 
            // btnAziEnable
            // 
            btnAziEnable.Enabled = false;
            btnAziEnable.Location = new Point(82, 146);
            btnAziEnable.Name = "btnAziEnable";
            btnAziEnable.Size = new Size(75, 23);
            btnAziEnable.TabIndex = 4;
            btnAziEnable.Text = "Enable";
            btnAziEnable.UseVisualStyleBackColor = true;
            // 
            // btnAziConnect
            // 
            btnAziConnect.Location = new Point(5, 146);
            btnAziConnect.Name = "btnAziConnect";
            btnAziConnect.Size = new Size(75, 23);
            btnAziConnect.TabIndex = 3;
            btnAziConnect.Text = "Connect";
            btnAziConnect.UseVisualStyleBackColor = true;
            // 
            // lblAziPosition
            // 
            lblAziPosition.AutoSize = true;
            lblAziPosition.Location = new Point(3, 70);
            lblAziPosition.Name = "lblAziPosition";
            lblAziPosition.Size = new Size(53, 15);
            lblAziPosition.TabIndex = 2;
            lblAziPosition.Text = "Position:";
            // 
            // lblAziSpeed
            // 
            lblAziSpeed.AutoSize = true;
            lblAziSpeed.Location = new Point(3, 37);
            lblAziSpeed.Name = "lblAziSpeed";
            lblAziSpeed.Size = new Size(42, 15);
            lblAziSpeed.TabIndex = 1;
            lblAziSpeed.Text = "Speed:";
            // 
            // lblAzi
            // 
            lblAzi.AutoSize = true;
            lblAzi.Font = new Font("Segoe UI", 12F);
            lblAzi.Location = new Point(52, 4);
            lblAzi.Name = "lblAzi";
            lblAzi.Size = new Size(68, 21);
            lblAzi.TabIndex = 0;
            lblAzi.Text = "Azimuth";
            // 
            // panel9
            // 
            panel9.BackColor = SystemColors.ActiveBorder;
            panel9.Controls.Add(lblEleStt);
            panel9.Controls.Add(btnEleGo);
            panel9.Controls.Add(txbEleTargetPos);
            panel9.Controls.Add(label9);
            panel9.Controls.Add(label3);
            panel9.Controls.Add(label4);
            panel9.Controls.Add(txbElePos);
            panel9.Controls.Add(txbEleSpeed);
            panel9.Controls.Add(btnEleEnable);
            panel9.Controls.Add(btnEleConnect);
            panel9.Controls.Add(label5);
            panel9.Controls.Add(label6);
            panel9.Controls.Add(label7);
            panel9.Location = new Point(781, 22);
            panel9.Name = "panel9";
            panel9.Size = new Size(184, 178);
            panel9.TabIndex = 2;
            // 
            // lblEleStt
            // 
            lblEleStt.AutoSize = true;
            lblEleStt.Location = new Point(5, 126);
            lblEleStt.Name = "lblEleStt";
            lblEleStt.Size = new Size(99, 15);
            lblEleStt.TabIndex = 15;
            lblEleStt.Text = "Elevation Status...";
            // 
            // btnEleGo
            // 
            btnEleGo.Location = new Point(127, 98);
            btnEleGo.Name = "btnEleGo";
            btnEleGo.Size = new Size(32, 25);
            btnEleGo.TabIndex = 14;
            btnEleGo.Text = "GO";
            btnEleGo.UseVisualStyleBackColor = true;
            // 
            // txbEleTargetPos
            // 
            txbEleTargetPos.Location = new Point(65, 99);
            txbEleTargetPos.Name = "txbEleTargetPos";
            txbEleTargetPos.Size = new Size(62, 23);
            txbEleTargetPos.TabIndex = 13;
            txbEleTargetPos.TextAlign = HorizontalAlignment.Right;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(3, 103);
            label9.Name = "label9";
            label9.Size = new Size(65, 15);
            label9.TabIndex = 12;
            label9.Text = "Target Pos:";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(127, 70);
            label3.Name = "label3";
            label3.Size = new Size(29, 15);
            label3.TabIndex = 8;
            label3.Text = "DEG";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(127, 37);
            label4.Name = "label4";
            label4.Size = new Size(32, 15);
            label4.TabIndex = 7;
            label4.Text = "RPM";
            // 
            // txbElePos
            // 
            txbElePos.Enabled = false;
            txbElePos.Location = new Point(65, 66);
            txbElePos.Name = "txbElePos";
            txbElePos.ReadOnly = true;
            txbElePos.Size = new Size(62, 23);
            txbElePos.TabIndex = 6;
            txbElePos.TextAlign = HorizontalAlignment.Right;
            // 
            // txbEleSpeed
            // 
            txbEleSpeed.Location = new Point(65, 33);
            txbEleSpeed.Name = "txbEleSpeed";
            txbEleSpeed.ReadOnly = true;
            txbEleSpeed.Size = new Size(62, 23);
            txbEleSpeed.TabIndex = 5;
            txbEleSpeed.TextAlign = HorizontalAlignment.Right;
            // 
            // btnEleEnable
            // 
            btnEleEnable.Enabled = false;
            btnEleEnable.Location = new Point(82, 146);
            btnEleEnable.Name = "btnEleEnable";
            btnEleEnable.Size = new Size(75, 23);
            btnEleEnable.TabIndex = 4;
            btnEleEnable.Text = "Enable";
            btnEleEnable.UseVisualStyleBackColor = true;
            // 
            // btnEleConnect
            // 
            btnEleConnect.Location = new Point(5, 146);
            btnEleConnect.Name = "btnEleConnect";
            btnEleConnect.Size = new Size(75, 23);
            btnEleConnect.TabIndex = 3;
            btnEleConnect.Text = "Connect";
            btnEleConnect.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(3, 70);
            label5.Name = "label5";
            label5.Size = new Size(53, 15);
            label5.TabIndex = 2;
            label5.Text = "Position:";
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(3, 37);
            label6.Name = "label6";
            label6.Size = new Size(42, 15);
            label6.TabIndex = 1;
            label6.Text = "Speed:";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("Segoe UI", 12F);
            label7.Location = new Point(50, 4);
            label7.Name = "label7";
            label7.Size = new Size(73, 21);
            label7.TabIndex = 0;
            label7.Text = "Elevation";
            // 
            // panel7
            // 
            panel7.Controls.Add(tabControl1);
            panel7.Location = new Point(0, 0);
            panel7.Margin = new Padding(2);
            panel7.Name = "panel7";
            panel7.Size = new Size(781, 380);
            panel7.TabIndex = 1;
            // 
            // tabControl1
            // 
            tabControl1.Controls.Add(tabPage1);
            tabControl1.Controls.Add(tabPage2);
            tabControl1.Dock = DockStyle.Left;
            tabControl1.Location = new Point(0, 0);
            tabControl1.Margin = new Padding(2);
            tabControl1.Name = "tabControl1";
            tabControl1.SelectedIndex = 0;
            tabControl1.Size = new Size(785, 380);
            tabControl1.TabIndex = 0;
            // 
            // tabPage1
            // 
            tabPage1.Controls.Add(panel10);
            tabPage1.Location = new Point(4, 24);
            tabPage1.Margin = new Padding(2);
            tabPage1.Name = "tabPage1";
            tabPage1.Padding = new Padding(2);
            tabPage1.Size = new Size(777, 352);
            tabPage1.TabIndex = 0;
            tabPage1.Text = "Map";
            tabPage1.UseVisualStyleBackColor = true;
            // 
            // panel10
            // 
            panel10.Controls.Add(gMap);
            panel10.Dock = DockStyle.Left;
            panel10.Location = new Point(2, 2);
            panel10.Margin = new Padding(2);
            panel10.Name = "panel10";
            panel10.Size = new Size(771, 348);
            panel10.TabIndex = 0;
            // 
            // gMap
            // 
            gMap.Bearing = 0F;
            gMap.BorderStyle = BorderStyle.Fixed3D;
            gMap.CanDragMap = true;
            gMap.Dock = DockStyle.Left;
            gMap.EmptyTileColor = Color.Navy;
            gMap.GrayScaleMode = false;
            gMap.HelperLineOption = GMap.NET.WindowsForms.HelperLineOptions.DontShow;
            gMap.LevelsKeepInMemory = 5;
            gMap.Location = new Point(0, 0);
            gMap.Margin = new Padding(2);
            gMap.MarkersEnabled = true;
            gMap.MaxZoom = 2;
            gMap.MinZoom = 2;
            gMap.MouseWheelZoomEnabled = true;
            gMap.MouseWheelZoomType = GMap.NET.MouseWheelZoomType.MousePositionAndCenter;
            gMap.Name = "gMap";
            gMap.NegativeMode = false;
            gMap.PolygonsEnabled = true;
            gMap.RetryLoadTile = 0;
            gMap.RoutesEnabled = true;
            gMap.ScaleMode = GMap.NET.WindowsForms.ScaleModes.Integer;
            gMap.SelectedAreaFillColor = Color.FromArgb(33, 65, 105, 225);
            gMap.ShowTileGridLines = false;
            gMap.Size = new Size(769, 348);
            gMap.TabIndex = 1;
            gMap.Zoom = 0D;
            // 
            // tabPage2
            // 
            tabPage2.Controls.Add(panel11);
            tabPage2.Location = new Point(4, 24);
            tabPage2.Margin = new Padding(2);
            tabPage2.Name = "tabPage2";
            tabPage2.Padding = new Padding(2);
            tabPage2.Size = new Size(777, 352);
            tabPage2.TabIndex = 1;
            tabPage2.Text = "Plot";
            tabPage2.UseVisualStyleBackColor = true;
            // 
            // panel11
            // 
            panel11.Controls.Add(plotView2);
            panel11.Dock = DockStyle.Fill;
            panel11.Location = new Point(2, 2);
            panel11.Margin = new Padding(2);
            panel11.Name = "panel11";
            panel11.Size = new Size(773, 348);
            panel11.TabIndex = 0;
            // 
            // plotView2
            // 
            plotView2.Dock = DockStyle.Fill;
            plotView2.Location = new Point(0, 0);
            plotView2.Margin = new Padding(2);
            plotView2.Name = "plotView2";
            plotView2.PanCursor = Cursors.Hand;
            plotView2.Size = new Size(773, 348);
            plotView2.TabIndex = 1;
            plotView2.Text = "plotView2";
            plotView2.ZoomHorizontalCursor = Cursors.SizeWE;
            plotView2.ZoomRectangleCursor = Cursors.SizeNWSE;
            plotView2.ZoomVerticalCursor = Cursors.SizeNS;
            // 
            // panel6
            // 
            panel6.Controls.Add(lblInfoTracking);
            panel6.Controls.Add(btnTracking);
            panel6.Controls.Add(btnManual);
            panel6.Controls.Add(lblModeControl);
            panel6.Controls.Add(plotView1);
            panel6.Location = new Point(0, 380);
            panel6.Margin = new Padding(2);
            panel6.Name = "panel6";
            panel6.Size = new Size(777, 190);
            panel6.TabIndex = 0;
            // 
            // btnTracking
            // 
            btnTracking.Location = new Point(670, 84);
            btnTracking.Name = "btnTracking";
            btnTracking.Size = new Size(105, 49);
            btnTracking.TabIndex = 7;
            btnTracking.Text = "Tracking";
            btnTracking.UseVisualStyleBackColor = true;
            btnTracking.Click += button1_Click;
            // 
            // btnManual
            // 
            btnManual.Location = new Point(670, 29);
            btnManual.Name = "btnManual";
            btnManual.Size = new Size(105, 49);
            btnManual.TabIndex = 6;
            btnManual.Text = "Manual";
            btnManual.UseVisualStyleBackColor = true;
            // 
            // lblModeControl
            // 
            lblModeControl.AutoSize = true;
            lblModeControl.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblModeControl.Location = new Point(669, 5);
            lblModeControl.Name = "lblModeControl";
            lblModeControl.Size = new Size(106, 21);
            lblModeControl.TabIndex = 5;
            lblModeControl.Text = "Mode Control";
            // 
            // plotView1
            // 
            plotView1.Location = new Point(6, 5);
            plotView1.Margin = new Padding(2);
            plotView1.Name = "plotView1";
            plotView1.PanCursor = Cursors.Hand;
            plotView1.Size = new Size(412, 181);
            plotView1.TabIndex = 4;
            plotView1.Text = "plotView1";
            plotView1.ZoomHorizontalCursor = Cursors.SizeWE;
            plotView1.ZoomRectangleCursor = Cursors.SizeNWSE;
            plotView1.ZoomVerticalCursor = Cursors.SizeNS;
            // 
            // splitter1
            // 
            splitter1.Location = new Point(335, 0);
            splitter1.Margin = new Padding(2);
            splitter1.Name = "splitter1";
            splitter1.Size = new Size(3, 570);
            splitter1.TabIndex = 6;
            splitter1.TabStop = false;
            // 
            // panel2
            // 
            panel2.Controls.Add(panel4);
            panel2.Controls.Add(panel3);
            panel2.Dock = DockStyle.Left;
            panel2.Location = new Point(0, 0);
            panel2.Margin = new Padding(2);
            panel2.Name = "panel2";
            panel2.Size = new Size(335, 570);
            panel2.TabIndex = 5;
            // 
            // panel4
            // 
            panel4.Controls.Add(dataGridViewSatellites);
            panel4.Dock = DockStyle.Fill;
            panel4.Location = new Point(0, 0);
            panel4.Margin = new Padding(2);
            panel4.Name = "panel4";
            panel4.Size = new Size(335, 345);
            panel4.TabIndex = 10;
            // 
            // dataGridViewSatellites
            // 
            dataGridViewSatellites.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridViewCellStyle3.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle3.BackColor = SystemColors.Control;
            dataGridViewCellStyle3.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle3.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = DataGridViewTriState.True;
            dataGridViewSatellites.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle3;
            dataGridViewSatellites.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewSatellites.Dock = DockStyle.Fill;
            dataGridViewSatellites.Location = new Point(0, 0);
            dataGridViewSatellites.Margin = new Padding(2);
            dataGridViewSatellites.Name = "dataGridViewSatellites";
            dataGridViewSatellites.RowHeadersWidth = 72;
            dataGridViewSatellites.Size = new Size(335, 345);
            dataGridViewSatellites.TabIndex = 7;
            // 
            // panel3
            // 
            panel3.Controls.Add(dataGridInforSatellites);
            panel3.Dock = DockStyle.Bottom;
            panel3.Location = new Point(0, 345);
            panel3.Margin = new Padding(2);
            panel3.Name = "panel3";
            panel3.Size = new Size(335, 225);
            panel3.TabIndex = 7;
            // 
            // dataGridInforSatellites
            // 
            dataGridInforSatellites.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            dataGridViewCellStyle4.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dataGridViewCellStyle4.BackColor = SystemColors.Control;
            dataGridViewCellStyle4.Font = new Font("Segoe UI", 9F);
            dataGridViewCellStyle4.ForeColor = SystemColors.WindowText;
            dataGridViewCellStyle4.SelectionBackColor = SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = DataGridViewTriState.True;
            dataGridInforSatellites.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle4;
            dataGridInforSatellites.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridInforSatellites.Dock = DockStyle.Fill;
            dataGridInforSatellites.Location = new Point(0, 0);
            dataGridInforSatellites.Margin = new Padding(2);
            dataGridInforSatellites.MultiSelect = false;
            dataGridInforSatellites.Name = "dataGridInforSatellites";
            dataGridInforSatellites.RowHeadersWidth = 72;
            dataGridInforSatellites.Size = new Size(335, 225);
            dataGridInforSatellites.TabIndex = 7;
            dataGridInforSatellites.SelectionChanged += dataGridInforSatellites_SelectionChanged;
            // 
            // lblInfoTracking
            // 
            lblInfoTracking.AutoSize = true;
            lblInfoTracking.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblInfoTracking.Location = new Point(423, 165);
            lblInfoTracking.Name = "lblInfoTracking";
            lblInfoTracking.Size = new Size(116, 21);
            lblInfoTracking.TabIndex = 8;
            lblInfoTracking.Text = "Traking Status...";
            lblInfoTracking.Click += label10_Click;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1309, 605);
            Controls.Add(panel5);
            Controls.Add(toolStrip1);
            Icon = (Icon)resources.GetObject("$this.Icon");
            KeyPreview = true;
            Margin = new Padding(2);
            Name = "MainForm";
            Text = "Sat Tracker";
            Load += Form1_Load;
            KeyDown += MainForm_KeyDown;
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            panel5.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel8.ResumeLayout(false);
            pnlAzi.ResumeLayout(false);
            pnlAzi.PerformLayout();
            panel9.ResumeLayout(false);
            panel9.PerformLayout();
            panel7.ResumeLayout(false);
            tabControl1.ResumeLayout(false);
            tabPage1.ResumeLayout(false);
            panel10.ResumeLayout(false);
            tabPage2.ResumeLayout(false);
            panel11.ResumeLayout(false);
            panel6.ResumeLayout(false);
            panel6.PerformLayout();
            panel2.ResumeLayout(false);
            panel4.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridViewSatellites).EndInit();
            panel3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridInforSatellites).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Timer MainTimer;
        private ToolStrip toolStrip1;
        private ToolStripButton btnZoomIn;
        private ToolStripButton btnZoomOut;
        private ToolStripButton btnCenter;
        private Panel panel5;
        private Panel panel2;
        private Panel panel3;
        private DataGridView dataGridInforSatellites;
        private Panel panel4;
        private DataGridView dataGridViewSatellites;
        private Splitter splitter1;
        private Panel panel1;
        private Panel panel7;
        private Panel panel6;
        private Panel panel8;
        private ToolStripSplitButton toolStripSplitButton1;
        private ToolStripButton btnFile;
        private GlassButton AziRight;
        private GlassButton AziLeft;
        private GlassButton EleDown;
        private GlassButton EleUp;
        private TabControl tabControl1;
        private TabPage tabPage1;
        private TabPage tabPage2;
        private Panel panel10;
        private GMap.NET.WindowsForms.GMapControl gMap;
        private Panel panel11;
        private OxyPlot.WindowsForms.PlotView plotView2;
        private GlassButton btnEleUp;
        private GlassButton btnAziLeft;
        private GlassButton btnAziRight;
        private GlassButton btnEleDown;
        private Panel pnlAzi;
        private Label lblAzi;
        private Label lblAziSpeed;
        private Label lblAziPosition;
        private Button btnAziConnect;
        private Button btnAziEnable;
        private TextBox txbAziPos;
        private TextBox txbAziSpeed;
        private Label label1;
        private Label label2;
        private Panel panel9;
        private Label label3;
        private Label label4;
        private TextBox txbElePos;
        private TextBox txbEleSpeed;
        private Button btnEleEnable;
        private Button btnEleConnect;
        private Label label5;
        private Label label6;
        private Label label7;
        private Label label8;
        private Button btnEleGo;
        private TextBox txbEleTargetPos;
        private Label label9;
        private Button btnAziGo;
        private TextBox txbAziTargetPos;
        private OxyPlot.WindowsForms.PlotView plotView1;
        private Label lblEleStt;
        private Label lblAziStt;
        private ToolStripButton btnSetting;
        private GlassButton btnStop;
        private Label lblModeControl;
        private Button btnTracking;
        private Button btnManual;
        private Label lblInfoTracking;
    }
}
