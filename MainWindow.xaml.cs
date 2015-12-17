using System;
using System.IO;
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

        private MeshGeometry3D mesh;
        private Point3D[] coords;
        private Point3D[] controls;

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

        public MainWindow() {
            InitializeComponent();
            DataContext = this;

            initializeDeformation();
            initializeInteraction();
        }

        private void initializeDeformation() {
            mesh = ((Model.Children[0] as ModelVisual3D).Content as GeometryModel3D).Geometry as MeshGeometry3D;
            coords = new Point3D[mesh.Positions.Count];

            Rect3D bound = Model.Children.FindBounds();
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Vector3D diff = mesh.Positions[i] - bound.Location;
                coords[i] = new Point3D(diff.X / bound.SizeX, diff.Y / bound.SizeY, diff.Z / bound.SizeZ);
            }

            ControlPoints.Children.Clear();
            controls = new Point3D[XDivision * YDivision * ZDivision];

            int controlIndex = 0;
            for (int x = 0; x < XDivision; x++) {
                double xCoord = bound.X + bound.SizeX * x / (XDivision - 1);
                for (int y = 0; y < YDivision; y++) {
                    double yCoord = bound.Y + bound.SizeY * y / (YDivision - 1);
                    for (int z = 0; z < ZDivision; z++) {
                        double zCoord = bound.Z + bound.SizeZ * z / (ZDivision - 1);

                        Point3D p = new Point3D(xCoord, yCoord, zCoord);
                        controls[controlIndex++] = p;

                        PointsVisual3D v = new PointsVisual3D();
                        v.Size = 8;
                        v.Points.Add(p);
                        ControlPoints.Children.Add(v);
                    }
                }
            }
        }

        private void initializeInteraction() {
            Point beginPoint = new Point();
            Point3D beginPoint3D = new Point3D();
            Matrix3D beginTransform = new Matrix3D();
            Point endPoint = new Point();
            Matrix3D endTransform = new Matrix3D();
            PointsVisual3D visualHit = null;
            double distance = 1;
            int indexHit = -1;

            Viewport.MouseLeftButtonDown += (sender, e) => {
                beginPoint = e.GetPosition(Viewport);
                RayMeshGeometry3DHitTestResult result = VisualTreeHelper.HitTest(Viewport, beginPoint) as RayMeshGeometry3DHitTestResult;

                if (result != null) {
                    visualHit = result.VisualHit as PointsVisual3D;
                    if (visualHit != null) {
                        indexHit = ControlPoints.Children.IndexOf(visualHit);
                        ProjectionCamera camera = Viewport.Camera as ProjectionCamera;
                        distance = Vector3D.DotProduct(result.PointHit - camera.Position, camera.LookDirection) / camera.LookDirection.Length;
                        beginTransform = visualHit.Transform.Value;
                        beginPoint3D = controls[indexHit];
                    }
                }
            };

            Viewport.MouseMove += (sender, e) => {
                if (visualHit != null && e.LeftButton == MouseButtonState.Pressed) {
                    endPoint = e.GetPosition(Viewport);

                    endTransform = beginTransform;
                    endTransform.Translate(distance * getTranslationVector3D(visualHit, beginPoint, endPoint));

                    visualHit.Transform = new MatrixTransform3D(endTransform);
                    controls[indexHit] = beginPoint3D + new Vector3D(endTransform.OffsetX, endTransform.OffsetY, endTransform.OffsetZ);
                    
                    updateDeformation();
                };
            };

            Viewport.MouseLeftButtonUp += (sender, e) => {
                visualHit = null;
            };

        }

        private Vector3D getTranslationVector3D(DependencyObject modelHit, Point beginPoint, Point endPoint) {
            Vector3D translationVector3D = new Vector3D();
            Viewport3DVisual viewport = null;
            bool success = false;

            Matrix3D matrix3D = MathUtils.TryTransformTo2DAncestor(modelHit, out viewport, out success);

            if (success && matrix3D.HasInverse) {
                matrix3D.Invert();
                Point3D startPoint3D = new Point3D(beginPoint.X, beginPoint.Y, 0);
                Point3D endPoint3D = new Point3D(endPoint.X, endPoint.Y, 0);
                Vector3D vector3D = endPoint3D - startPoint3D;
                translationVector3D = matrix3D.Transform(vector3D);
            }

            return translationVector3D;
        }

        private void updateDeformation() {
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Point3D coord = coords[i];
                Point3D pos = new Point3D();

                int controlIndex = 0;
                for (int x = 0; x < XDivision; x++) {
                    for (int y = 0; y < YDivision; y++) {
                        for (int z = 0; z < ZDivision; z++) {
                            double scale = getBoean(x, coord.X) * getBoean(y, coord.Y) * getBoean(z, coord.Z);
                            Point3D p = controls[controlIndex++];
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
                ModelVisual3D visual = new ModelVisual3D();
                visual.Content = new ModelImporter().Load(dialog.FileName).Children[0];
                Model.Children[0] = visual;
                initializeDeformation();
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.DefaultExt = Exporters.DefaultExtension;
            dialog.Filter = Exporters.Filter;

            if (dialog.ShowDialog() == true) {
                using (FileStream writer = File.Create(dialog.FileName)) {
                    //new ObjExporter().Export(Model, writer);
                }
                MessageBox.Show("SceneSaveText" + dialog.FileName, "SceneSaveTitle", MessageBoxButton.OK, MessageBoxImage.Information);
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
