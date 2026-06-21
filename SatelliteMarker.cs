using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Drawing.Drawing2D;
using GMap.NET;
using GMap.NET.WindowsForms;

namespace SatTracker
{
    public class GMapSatelliteMarker : GMapMarker
    {
        private double baseSizeInCm;
        private Color markerColor;
        private double baseZoom;

        // Khởi tạo marker với kích thước cm mong muốn tại một mức zoom cơ sở (Ví dụ: mặc định là Zoom 7)
        public GMapSatelliteMarker(PointLatLng p, double sizeInCm, Color color, double defaultZoom = 7) : base(p)
        {
            this.baseSizeInCm = sizeInCm;
            this.markerColor = color;
            this.baseZoom = defaultZoom;

            // Tính kích thước pixel ban đầu để khởi tạo vùng chọn cho Marker
            float dpi = 96f;
            int initPixel = (int)Math.Round((sizeInCm * dpi) / 2.54);
            this.Size = new Size(initPixel * 4, initPixel * 2); // Kích thước bao quát cả cánh
            this.Offset = new Point(-this.Size.Width / 2, -this.Size.Height / 2);
        }
        public override void OnRender(Graphics g)
        {
            if (Overlay == null || Overlay.Control == null) return;

            // Bật khử răng cưa giúp nét vẽ mịn màng
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. LẤY THÔNG TIN ZOOM HIỆN TẠI TỪ KHÔNG GIAN BẢN ĐỒ
            double currentZoom = Overlay.Control.Zoom;

            // Tính kích thước pixel cơ sở của ô vuông 0.5cm (~19 pixel trên màn hình 96 DPI)
            float dpi = 96f;
            double pixelPerCm = dpi / 2.54;
            double basePixel = baseSizeInCm * pixelPerCm;

            // 2. THUẬT TOÁN LOGARIT HÃM CỰC ĐẠI KÍCH THƯỚC (Dành cho dải Zoom 2 - 20)
            double zoomRatio = (currentZoom - 2) / (20 - 2);
            double maxScaleMultiplier = 4.0;
            double dynamicScale = 1.0 + (zoomRatio * (maxScaleMultiplier - 1.0));

            int boxSize = (int)Math.Round(basePixel * dynamicScale);

            // Giới hạn cứng bằng Pixel để không bao giờ bị quá to
            int absoluteMinPixel = (int)basePixel;
            int absoluteMaxPixel = (int)(basePixel * maxScaleMultiplier);

            if (boxSize < absoluteMinPixel) boxSize = absoluteMinPixel;
            if (boxSize > absoluteMaxPixel) boxSize = absoluteMaxPixel;

            // 3. TÍNH TOÁN LẠI VÙNG CHỨA MARKER
            int totalWidth = (int)(boxSize * 2.2);
            int totalHeight = boxSize;
            this.Size = new Size(totalWidth, totalHeight);
            this.Offset = new Point(-totalWidth / 2, -totalHeight / 2);

            // Xác định tâm vẽ đồ họa
            int centerX = LocalPosition.X + totalWidth / 2;
            int centerY = LocalPosition.Y + totalHeight / 2;

            // 4. VẼ Ô VUÔNG MÔ PHỎNG NÉT ĐỨT BÊN NGOÀI
            using (Pen penBox = new Pen(Color.FromArgb(90, markerColor), 1))
            {
                penBox.DashStyle = DashStyle.Dash;
                g.DrawRectangle(penBox, centerX - boxSize / 2, centerY - boxSize / 2, boxSize, boxSize);
            }

            // 5. CÂN ĐỐI TỶ LỆ: XOAY KHỐI TRỤ THÀNH DỌC ĐỂ VUÔNG GÓC VỚI CÁNH
            int cylinderWidth = (int)(boxSize * 0.28);   // Đây là đường kính thân trụ (trước là chiều cao)
            int cylinderHeight = (int)(boxSize * 0.55);  // Đây là chiều dài thân trụ đứng (trước là chiều rộng)
            int wingWidth = (int)(boxSize * 0.75);       // Chiều dài cánh panel ngang
            int wingHeight = (int)(cylinderWidth * 0.85); // Chiều cao cánh phẳng

            // 6. VẼ HAI CÁNH TẤM NĂNG LƯỢNG MẶT TRỜI HÌNH BÌNH HÀNH (VẪN NẰM NGANG)
            using (LinearGradientBrush wingBrush = new LinearGradientBrush(
                new Rectangle(centerX - totalWidth / 2, centerY - wingHeight / 2, totalWidth, wingHeight),
                Color.FromArgb(20, 50, 140), Color.FromArgb(70, 135, 245), 45f))
            {
                using (Pen wingPen = new Pen(Color.FromArgb(220, Color.LightCyan), 1f))
                {
                    // Cánh bên trái (nối vào hông trái thân trụ đứng)
                    Point[] leftWing = new Point[]
                    {
                new Point(centerX - cylinderWidth / 2 - wingWidth, centerY - wingHeight / 2 + (int)(wingHeight * 0.15)),
                new Point(centerX - cylinderWidth / 2, centerY - wingHeight / 2),
                new Point(centerX - cylinderWidth / 2, centerY + wingHeight / 2),
                new Point(centerX - cylinderWidth / 2 - wingWidth, centerY + wingHeight / 2 - (int)(wingHeight * 0.15))
                    };
                    g.FillPolygon(wingBrush, leftWing);
                    g.DrawPolygon(wingPen, leftWing);

                    // Cánh bên phải (nối vào hông phải thân trụ đứng)
                    Point[] rightWing = new Point[]
                    {
                new Point(centerX + cylinderWidth / 2, centerY - wingHeight / 2),
                new Point(centerX + cylinderWidth / 2 + wingWidth, centerY - wingHeight / 2 + (int)(wingHeight * 0.15)),
                new Point(centerX + cylinderWidth / 2 + wingWidth, centerY + wingHeight / 2 - (int)(wingHeight * 0.15)),
                new Point(centerX + cylinderWidth / 2, centerY + wingHeight / 2)
                    };
                    g.FillPolygon(wingBrush, rightWing);
                    g.DrawPolygon(wingPen, rightWing);

                    // Vẽ lưới ô vuông năng lượng mặt trời
                    if (wingWidth > 15)
                    {
                        int segments = 3;
                        for (int i = 1; i < segments; i++)
                        {
                            int xLeft = (centerX - cylinderWidth / 2) - (wingWidth * i / segments);
                            g.DrawLine(wingPen, xLeft, centerY - wingHeight / 2, xLeft, centerY + wingHeight / 2);

                            int xRight = (centerX + cylinderWidth / 2) + (wingWidth * i / segments);
                            g.DrawLine(wingPen, xRight, centerY - wingHeight / 2, xRight, centerY + wingHeight / 2);
                        }
                    }
                }
            }

            // 7. VẼ THÂN HÌNH TRỤ ĐỨNG NẰM VUÔNG GÓC (Ở TRUNG TÂM)
            Rectangle cylRect = new Rectangle(centerX - cylinderWidth / 2, centerY - cylinderHeight / 2, cylinderWidth, cylinderHeight);

            // Đổi LinearGradientMode sang Horizontal (Ngang) để tạo hiệu ứng đổ bóng khối trụ đứng
            using (LinearGradientBrush bodyBrush = new LinearGradientBrush(
                cylRect, Color.White, markerColor, LinearGradientMode.Horizontal))
            {
                ColorBlend cb = new ColorBlend(3);
                // Trộn dải màu tạo khối 3D: Sáng bên sườn trái -> Màu chủ đạo ở giữa -> Tối dần về sườn phải
                cb.Colors = new Color[] { ControlPaint.Light(markerColor), markerColor, ControlPaint.Dark(markerColor) };
                cb.Positions = new float[] { 0.0f, 0.2f, 1.0f };
                bodyBrush.InterpolationColors = cb;

                g.FillRectangle(bodyBrush, cylRect);

                using (Pen cylPen = new Pen(Color.FromArgb(180, markerColor), 1))
                {
                    g.DrawRectangle(cylPen, cylRect);
                }
            }

            // 8. VẼ MẶT BO TRÒN ĐẦU KHỐI TRỤ ĐỨC (Mặt elip nằm ở đỉnh trên để tạo góc nhìn 3D từ trên xuống)
            int ellipseHeight = cylinderWidth / 3;
            if (ellipseHeight > 0 && cylinderWidth > 0)
            {
                using (SolidBrush capBrush = new SolidBrush(ControlPaint.Light(markerColor)))
                {
                    g.FillEllipse(capBrush, centerX - cylinderWidth / 2, centerY - cylinderHeight / 2 - ellipseHeight / 2, cylinderWidth, ellipseHeight);
                }
            }
        }
    }
}
