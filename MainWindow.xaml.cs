using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using HelixToolkit.Wpf;
using _3DTools;

namespace Deformation {
    public enum DisplayMode {
        Control,
        Point,
        Model
    }

    public enum SurfaceMode {
        Plan,
        Subdivide
    }

    public enum TimeMode {
        Linear,
        Easein,
        Easeout
    }

    public partial class MainWindow : Window {
        public DependencyProperty DisplayStatProperty = DependencyProperty.Register("DisplayStat", typeof(DisplayMode), typeof(MainWindow), new FrameworkPropertyMetadata(DisplayMode.Control));
        public DependencyProperty SurfaceStatProperty = DependencyProperty.Register("SurfaceStat", typeof(SurfaceMode), typeof(MainWindow), new FrameworkPropertyMetadata(SurfaceMode.Plan));
        public DependencyProperty XDivisionProperty = DependencyProperty.Register("XDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4));
        public DependencyProperty YDivisionProperty = DependencyProperty.Register("YDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4));
        public DependencyProperty ZDivisionProperty = DependencyProperty.Register("ZDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4));
        public DependencyProperty TimeStatProperty = DependencyProperty.Register("TimeStat", typeof(TimeMode), typeof(MainWindow), new FrameworkPropertyMetadata(TimeMode.Linear));
        public DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(1.0));
        public DependencyProperty ElapsedProperty = DependencyProperty.Register("Elapsed", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(0.0));

        public DisplayMode DisplayStat {
            get { return (DisplayMode)GetValue(DisplayStatProperty); }
            set { SetValue(DisplayStatProperty, value); }
        }

        public SurfaceMode SurfaceStat {
            get { return (SurfaceMode)GetValue(SurfaceStatProperty); }
            set { SetValue(SurfaceStatProperty, value); }
        }

        public int XDivision {
            get { return (int)GetValue(XDivisionProperty); }
            set { SetValue(XDivisionProperty, value); }
        }

        public int YDivision {
            get { return (int)GetValue(YDivisionProperty); }
            set { SetValue(YDivisionProperty, value); }
        }

        public int ZDivision {
            get { return (int)GetValue(ZDivisionProperty); }
            set { SetValue(ZDivisionProperty, value); }
        }

        public TimeMode TimeStat {
            get { return (TimeMode)GetValue(TimeStatProperty); }
            set { SetValue(TimeStatProperty, value); }
        }

        public double Duration {
            get { return (double)GetValue(DurationProperty); }
            set { SetValue(DurationProperty, value); }
        }

        public double Elapsed {
            get { return (double)GetValue(ElapsedProperty); }
            set { SetValue(ElapsedProperty, value); }
        }

        private MeshGeometry3D mesh;
        private Point3D[] coords;

        public MainWindow() {
            InitializeComponent();
            DataContext = this;

            UpdateControlPoint();
            BindMouseToControlPoint();
            InitializeDeformation();
        }

        public void UpdateControlPoint() {
            ControlPoints.Children.Clear();
            Rect3D rect = Model.Children.FindBounds();

            for (int x = 0; x < XDivision; x++) {
                double xValue = rect.X + rect.SizeX * x / (XDivision - 1);
                for (int y = 0; y < YDivision; y++) {
                    double yValue = rect.Y + rect.SizeY * y / (YDivision - 1);
                    for (int z = 0; z < ZDivision; z++) {
                        double zValue = rect.Z + rect.SizeZ * z / (ZDivision - 1);
                        PointsVisual3D p = new PointsVisual3D();
                        p.Size = 8;
                        p.Points.Add(new Point3D(xValue, yValue, zValue));
                        p.SetName("ControlPoint");
                        ControlPoints.Children.Add(p);
                    }
                }
            }
        }

        public void BindMouseToControlPoint() {
            Point startPoint = new Point();
            PointsVisual3D visualHit = null;

            Viewport.MouseLeftButtonDown += (sender, e) => {
                startPoint = e.GetPosition(Viewport);
                PointsVisual3D mayHit = VisualTreeHelper.HitTest(Viewport, startPoint).VisualHit as PointsVisual3D;
                if (mayHit != null && mayHit.GetName() == "ControlPoint") {
                    visualHit = mayHit;
                }
            };

            Viewport.MouseMove += (sender, e) => {
                if (visualHit != null && e.LeftButton == MouseButtonState.Pressed) {
                    Point endPoint = e.GetPosition(Viewport);
                    Vector3D vector3D = GetTranslationVector3D(visualHit, startPoint, endPoint);
                    Matrix3D matrix3D = visualHit.Transform.Value;
                    vector3D += new Vector3D(matrix3D.OffsetX, matrix3D.OffsetY, matrix3D.OffsetZ);

                    matrix3D.OffsetX = vector3D.X;
                    matrix3D.OffsetY = vector3D.Y;
                    matrix3D.OffsetZ = vector3D.Z;
                    visualHit.Points[0] += new Vector3D(matrix3D.OffsetX, matrix3D.OffsetY, matrix3D.OffsetZ);
                    //visualHit.Transform = new MatrixTransform3D(matrix3D);
                    startPoint = endPoint;
                    UpdateDeformation();
                };

            };

            Viewport.MouseLeftButtonUp += (sender, e) => {
                visualHit = null;
            };

        }

        private Vector3D GetTranslationVector3D(Visual3D modelHit, Point startPoint, Point endPoint) {
            Vector3D translationVector3D = new Vector3D();

            Viewport3DVisual viewport = null;
            bool success = false;

            Matrix3D matrix3D = MathUtils.TryTransformTo2DAncestor(modelHit, out viewport, out success);

            if (success && matrix3D.HasInverse) {
                matrix3D.Invert();
                Point3D startPoint3D = new Point3D(startPoint.X, startPoint.Y, 0);
                Point3D endPoint3D = new Point3D(endPoint.X, endPoint.Y, 0);
                Vector3D vector3D = endPoint3D - startPoint3D;
                translationVector3D = matrix3D.Transform(vector3D);
            }

            return translationVector3D;
        }

        private void InitializeDeformation() {
            var foo = (Model.Children[0] as ModelVisual3D).Content;
            mesh = ((Model.Children[0] as ModelVisual3D).Content as GeometryModel3D).Geometry as MeshGeometry3D;
            coords = new Point3D[mesh.Positions.Count];

            Rect3D bound = Model.Children.FindBounds();
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Vector3D diff = mesh.Positions[i] - bound.Location;
                Point3D normalize = new Point3D(diff.X / bound.SizeX, diff.Y / bound.SizeY, diff.Z / bound.SizeZ);
                coords[i] = normalize;
            }
        }

        private void UpdateDeformation() {
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Point3D coord = coords[i];
                //int[] map = maps[i];
                Point3D pos = new Point3D();
                Point3D[] controls = ControlPoints.Children.Select(v => (v as PointsVisual3D).Points[0]).ToArray();

                int id = 0;
                for (int x = 0; x < 4; x++) {
                    for (int y = 0; y < 4; y++) {
                        for (int z = 0; z < 4; z++) {
                            double scale = getBoean(x, coord.X) * getBoean(y, coord.Y) * getBoean(z, coord.Z);
                            Point3D p = controls[id++];
                            pos += new Vector3D(p.X * scale, p.Y * scale, p.Z * scale);
                        }
                    }
                }
                mesh.Positions[i] = pos;
            }
        }

        private double getBoean(int i, double u) {
            double result = 0;
            switch (i) {
                case 0:
                    result = Math.Pow(1 - u, 3);
                    break;
                case 1:
                    result = 3 * u * Math.Pow(1 - u, 2);
                    break;
                case 2:
                    result = 3 * Math.Pow(u, 2) * (1 - u);
                    break;
                case 3:
                    result = Math.Pow(u, 3);
                    break;
            }
            return result;
        }

        private void Open_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = Importers.DefaultExtension;
            dialog.Filter = Importers.Filter;

            if (dialog.ShowDialog() == true) {
                Model3DGroup group = (new ModelImporter()).Load(dialog.FileName);
                Model.Children.Clear();
                foreach (Model3D m in group.Children) {
                    ModelVisual3D v = new ModelVisual3D();
                    v.Content = m;
                    Model.Children.Add(v);
                }
                UpdateControlPoint();
                InitializeDeformation();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.DefaultExt = Exporters.DefaultExtension;
            dialog.Filter = Exporters.Filter;

            if (dialog.ShowDialog() == true) {

            }
        }

        private void Play_Click(object sender, RoutedEventArgs e) {

        }

        private void Render_Click(object sender, RoutedEventArgs e) {

        }

        private void Reset_Click(object sender, RoutedEventArgs e) {

        }

        private void Help_Click(object sender, RoutedEventArgs e) {

        }
    }
}
