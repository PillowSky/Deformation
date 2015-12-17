using System;
using System.Collections.Generic;
using System.IO;
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

    public enum TimeMode {
        Linear,
        Easein,
        Easeout
    }

    public partial class MainWindow : Window {
        public DependencyProperty DisplayStatProperty = DependencyProperty.Register("DisplayStat", typeof(DisplayMode), typeof(MainWindow), new FrameworkPropertyMetadata(DisplayMode.Control));
        public DependencyProperty SubdivisionLevelProperty = DependencyProperty.Register("SubdivisionLevel", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(0, OnSubDivisionPropertyChanged));
        public DependencyProperty XDivisionProperty = DependencyProperty.Register("XDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4, OnDivisionPropertyChanged));
        public DependencyProperty YDivisionProperty = DependencyProperty.Register("YDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4, OnDivisionPropertyChanged));
        public DependencyProperty ZDivisionProperty = DependencyProperty.Register("ZDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4, OnDivisionPropertyChanged));
        public DependencyProperty TimeStatProperty = DependencyProperty.Register("TimeStat", typeof(TimeMode), typeof(MainWindow), new FrameworkPropertyMetadata(TimeMode.Linear));
        public DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(1.0));
        public DependencyProperty ElapsedProperty = DependencyProperty.Register("Elapsed", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(0.0));

        private MeshGeometry3D mesh;
        private MeshGeometry3D roughMesh;

        private Point3D[] coords;
        private Point3D[] controls;
        private Dictionary<int, int> factorialCache = new Dictionary<int, int>();

        public DisplayMode DisplayStat {
            get { return (DisplayMode)GetValue(DisplayStatProperty); }
            set { SetValue(DisplayStatProperty, value); }
        }

        public int SubdivisionLevel {
            get { return (int)GetValue(SubdivisionLevelProperty); }
            set { SetValue(SubdivisionLevelProperty, value); }
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

            AppDomain currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandler);

            initializeSubdivision();
            initializeDeformation();
            initializeInteraction();
        }

        private void initializeSubdivision() {
            roughMesh = ((Model.Children[0] as ModelVisual3D).Content as GeometryModel3D).Geometry as MeshGeometry3D;
            updateSubdivision();
        }

        private void initializeDeformation() {
            Rect3D bound = Model.Children.FindBounds();
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
                        v.Size = 5;
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

        private void updateSubdivision() {
            LoopSubdivision loop = new LoopSubdivision(roughMesh);
            loop.Subdivide(SubdivisionLevel);
            ((Model.Children[0] as ModelVisual3D).Content as GeometryModel3D).Geometry = mesh = loop.ToMeshGeometry3D();

            coords = new Point3D[mesh.Positions.Count];
            Rect3D bound = Model.Children.FindBounds();
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Vector3D diff = mesh.Positions[i] - bound.Location;
                coords[i] = new Point3D(diff.X / bound.SizeX, diff.Y / bound.SizeY, diff.Z / bound.SizeZ);
            }

        }

        private void updateDeformation() {
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Point3D coord = coords[i];
                Point3D pos = new Point3D();

                int controlIndex = 0;
                for (int x = 0; x < XDivision; x++) {
                    double xWeight = calcBerstein(x, XDivision - 1, coord.X);
                    for (int y = 0; y < YDivision; y++) {
                        double yWeight = calcBerstein(y, YDivision - 1, coord.Y);
                        for (int z = 0; z < ZDivision; z++) {
                            double zWeight = calcBerstein(z, ZDivision - 1, coord.Z);
                            double weight = xWeight * yWeight * zWeight;
                            pos += (Vector3D)controls[controlIndex++].Multiply(weight);
                        }
                    }
                }
                mesh.Positions[i] = pos;
            }
        }

        private double calcBerstein(int i, int n, double u) {
            return (calcFactorial(n) * Math.Pow(u, i) * Math.Pow(1 - u, n - i)) / (calcFactorial(i) * calcFactorial(n - i));
        }

        private int calcFactorial(int n) {
            int factorial;
            if (factorialCache.TryGetValue(n, out factorial)) {
                // done
            } else {
                if (n <= 1) {
                    factorial = factorialCache[n] = 1;
                } else {
                    factorial = factorialCache[n] = calcFactorial(n - 1) * n;
                }
            }
            return factorial;
        }

        private static void OnSubDivisionPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            (source as MainWindow).updateSubdivision();
            (source as MainWindow).updateDeformation();
        }

        private static void OnDivisionPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            (source as MainWindow).initializeDeformation();
        }

        private void Import_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = Importers.DefaultExtension;
            dialog.Filter = Importers.Filter;

            if (dialog.ShowDialog() == true) {
                ModelVisual3D visual = new ModelVisual3D();
                GeometryModel3D model = new ModelImporter().Load(dialog.FileName).Children[0] as GeometryModel3D;
                model.Material = FindResource("WhiteMaterial") as Material;

                visual.Content = model;
                Model.Children[0] = visual;

                initializeSubdivision();
                initializeDeformation();
            }
        }

        private void Export_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.DefaultExt = Exporters.DefaultExtension;
            dialog.Filter = Exporters.Filter;

            if (dialog.ShowDialog() == true) {
                using (FileStream writer = File.Create(dialog.FileName)) {
                    IExporter exporter = Exporters.Create(dialog.FileName);
                    if (exporter is BitmapExporter) {
                        exporter.Export(Viewport.Viewport, writer);
                    } else {
                        exporter.Export(Model, writer);
                    }
                }
                MessageBox.Show(FindResource("ModelExportText") as string + dialog.FileName, FindResource("ModelExportTitle") as string, MessageBoxButton.OK, MessageBoxImage.Information);
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

        static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            Exception e = args.ExceptionObject as Exception;
            MessageBox.Show(e.StackTrace, e.Message, MessageBoxButton.OK, MessageBoxImage.Stop);
        }
    }
}
