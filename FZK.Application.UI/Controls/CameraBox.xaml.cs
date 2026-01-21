using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace FZK.Application.UI.Controls
{
    /// <summary>
    /// 选框事件数据实体
    /// </summary>
    public class RectEventArgs : RoutedEventArgs
    {
        public Rect SelectRect { get; }
        public double X { get; }
        public double Y { get; }
        public double Zoom { get; }
        public RectEventArgs(RoutedEvent routedEvent, object source, Rect selectRect, double x, double y, double zoom)
            : base(routedEvent, source)
        {
            SelectRect = selectRect;
            X = x;
            Y = y;
            Zoom = zoom;
        }
    }

    /// <summary>
    /// CameraBox.xaml 的交互逻辑
    /// </summary>
    public partial class CameraBox : UserControl
    {

        /// <summary>
        /// 图像源
        /// </summary>
        public System.Windows.Media.ImageSource ImageSource
        {
            get { return (System.Windows.Media.ImageSource)GetValue(ImageSourceProperty); }
            set { SetValue(ImageSourceProperty, value); }
        }

        public static readonly DependencyProperty ImageSourceProperty =
            DependencyProperty.Register("ImageSource", typeof(System.Windows.Media.ImageSource), typeof(CameraBox), new PropertyMetadata(callback));

        private static void callback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is CameraBox box)
            {
                var source = e.NewValue as System.Windows.Media.ImageSource;
                if (source != null)
                {
                    box.image.Source = source;
                }
            }
        }


        /// <summary>
        /// 鼠标是否框选图像
        /// </summary>
        public bool IsMouseSelect
        {
            get { return (bool)GetValue(IsMouseSelectProperty); }
            set { SetValue(IsMouseSelectProperty, value); }
        }

        public static readonly DependencyProperty IsMouseSelectProperty =
            DependencyProperty.Register("IsMouseSelect", typeof(bool), typeof(CameraBox), new PropertyMetadata(false));

        //定义路由事件
        public static readonly RoutedEvent RectChangedEvent
            = EventManager.RegisterRoutedEvent("RectChanged",
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(CameraBox));

        /// <summary>
        /// 选框更改事件
        /// </summary>
        public event RoutedEventHandler RectChanged
        {
            add { AddHandler(RectChangedEvent, value); }
            remove { RemoveHandler(RectChangedEvent, value); }
        }

        protected virtual void OnRectChangedEvent(RectEventArgs e)
        {
            RaiseEvent(e);
        }

        public void ClearSelectRect()
        {
            rect.Width = 0;
            rect.Height = 0;
            Canvas.SetLeft(rect, StartPoint.X);
            Canvas.SetTop(rect, StartPoint.Y);
        }

        public TransformGroup TransformGroup { get; private set; } = new TransformGroup();
        private ScaleTransform scaleTransform = new ScaleTransform();//缩放
        private TranslateTransform translateTransform = new TranslateTransform();//平移
        private Point MousePoint = new Point(-1, -1);
        private Point StartPoint = new Point(0, 0);
        private Point MouseDownPoint = new Point(0, 0);//鼠标按下位置
        private double scale = 1;//默认放大倍数
        private int viewportWidth = 0;
        private int viewportHeight = 0;
        private int imageBoxWidth = 0;
        private int imageBoxHeight = 0;
        private bool mousePressed = false;
        public double Scale { get; set; } = 1;
        /// <summary>
        /// 鼠标框选区域
        /// </summary>
        public Rect SelectRect { get; private set; }
        public CameraBox()
        {
            InitializeComponent();
            Loaded += CameraBox_Loaded;
        }

        private void CameraBox_Loaded(object sender, RoutedEventArgs e)
        {
            SetTransform();
            Loaded -= CameraBox_Loaded;//只加载一次SetTransform()
        }

        /// <summary>
        /// 根据viewport尺寸，设置图像的缩放和平移
        /// </summary>
        private void SetTransform()
        {
            TransformGroup.Children.Add(scaleTransform);
            TransformGroup.Children.Add(translateTransform);
            imagebox.RenderTransform = TransformGroup;
            viewportWidth = (int)viewport.ActualWidth;
            viewportHeight = (int)viewport.ActualHeight;
            imageBoxWidth = (int)imagebox.ActualWidth;
            imageBoxHeight = (int)imagebox.ActualHeight;

            scale = Math.Min(1.0 * viewportWidth / imageBoxWidth, 1.0 * viewportHeight / imageBoxHeight);//最初放大比
            scaleTransform.ScaleX = scale;
            scaleTransform.ScaleY = scale;

            var translateX = (viewportWidth - imageBoxWidth * scale) / 2;
            var translateY = (viewportHeight - imageBoxHeight * scale) / 2;
            translateTransform.X = translateX;
            translateTransform.Y = translateY;
            textblock.Text = $"({Math.Round(translateX)},{Math.Round(translateY)}) Zoom:{scale}";
        }

        //鼠标右键弹起：还原图像
        private void viewport_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            scaleTransform.ScaleX = scale;//还原最初放大比
            scaleTransform.ScaleY = scale;
            var translateX = (viewportWidth - imageBoxWidth * scale) / 2;
            var translateY = (viewportHeight - imageBoxHeight * scale) / 2;
            translateTransform.X = translateX;
            translateTransform.Y = translateY;
        }

        //鼠标左键按下
        private void viewport_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            mousePressed = true;
            MouseDownPoint = e.GetPosition(viewport);

            //选框初始位置
            StartPoint = e.GetPosition(viewport);
            rect.Width = 0;
            rect.Height = 0;
            Canvas.SetLeft(rect, StartPoint.X);
            Canvas.SetTop(rect, StartPoint.Y);

            //选框坐标显示
            rectLabel.Text = string.Empty;
            Canvas.SetLeft(rectLabel, StartPoint.X);
            Canvas.SetTop(rectLabel, StartPoint.Y);
            viewport.CaptureMouse();

        }


        //鼠标移动
        private void viewport_MouseMove(object sender, MouseEventArgs e)
        {
            Point point = e.GetPosition(viewport);
            MousePoint = point;//更新鼠标位置
            if (!mousePressed)
                return;
            if (IsMouseSelect)
            {
                //表示框选图像
                //rect.Width = point.X - StartPoint.X < 0 ? 0 : point.X - StartPoint.X;
                //rect.Height = point.Y - StartPoint.Y < 0 ? 0 : point.Y - StartPoint.Y;
                //SelectRect = new Rect(StartPoint.X, StartPoint.Y, rect.Width, rect.Height);
                //rectLabel.Text = $"{SelectRect}";
                //Canvas.SetLeft(rectLabel, SelectRect.X + SelectRect.Width);
                //Canvas.SetTop(rectLabel, StartPoint.Y);
                //if (SelectRect.Width + SelectRect.Height == 0)
                //{
                //    rectLabel.Text = string.Empty;
                //}
                double x = Math.Min(StartPoint.X, point.X);
                double y = Math.Min(StartPoint.Y, point.Y);
                double width = Math.Abs(point.X - StartPoint.X);
                double height = Math.Abs(point.Y - StartPoint.Y);

                SelectRect = new Rect(x, y, width, height);

                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, y);
                rect.Width = width;
                rect.Height = height;

                rectLabel.Text = $"{SelectRect}";
                Canvas.SetLeft(rectLabel, SelectRect.Right);
                Canvas.SetTop(rectLabel, SelectRect.Top);

                if (width + height == 0)
                {
                    rectLabel.Text = string.Empty;
                }

            }
            else
            {
                //表示移动图像
                translateTransform.X += point.X - MouseDownPoint.X;
                translateTransform.Y += point.Y - MouseDownPoint.Y;
                MouseDownPoint = point;
                textblock.Text = $"({Math.Round(translateTransform.X)},{Math.Round(translateTransform.Y)}) Zoom:{scale}";
            }
        }

        //鼠标左键弹起
        private void viewport_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mousePressed = false;
            MouseDownPoint = e.GetPosition(viewport);
            viewport.ReleaseMouseCapture();

            if (IsMouseSelect)
            {
                OnRectChangedEvent(new RectEventArgs(RectChangedEvent, this, SelectRect, translateTransform.X, translateTransform.Y, scaleTransform.ScaleX));
            }
        }

        private void viewport_MouseLeave(object sender, MouseEventArgs e)
        {
            MousePoint = new Point(-1, -1);
        }

        //放大缩小图像
        private void viewport_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            double scale = e.Delta * 0.0005;
            Point point = e.GetPosition(viewport);
            Point inverse = TransformGroup.Inverse.Transform(point);
            if ((scaleTransform.ScaleX + scale < 0.1) || (scaleTransform.ScaleX + scale > 2))
            {
                return;
            }
            scaleTransform.ScaleX += scale;
            scaleTransform.ScaleY += scale;
            translateTransform.X = -1 * (inverse.X * scaleTransform.ScaleX - point.X);
            translateTransform.Y = -1 * (inverse.Y * scaleTransform.ScaleY - point.Y);
            textblock.Text = $"({Math.Round(translateTransform.X)},{Math.Round(translateTransform.Y)}) Zoom:{scale}";
        }
    }
}
