﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Xml.Serialization;
using Microsoft.Win32;
using HelixToolkit.Wpf;
using _3DTools;

namespace Deformation {
    public enum CameraMode {
        Perspective,
        Orthographic
    }

    public enum DisplayMode {
        Rendered,
        Wireframe
    }

    public enum EditMode {
        Show,
        Hide
    }

    public enum TimeMode {
        Linear,
        Easein,
        Easeout
    }

    public partial class MainWindow : Window {
        public DependencyProperty CameraStatProperty = DependencyProperty.Register("CameraStat", typeof(CameraMode), typeof(MainWindow), new FrameworkPropertyMetadata(CameraMode.Perspective, OnCameraStatPropertyChanged));
        public DependencyProperty DisplayStatProperty = DependencyProperty.Register("DisplayStat", typeof(DisplayMode), typeof(MainWindow), new FrameworkPropertyMetadata(DisplayMode.Rendered, OnDisplayStatPropertyChanged));
        public DependencyProperty EditStatProperty = DependencyProperty.Register("EditStat", typeof(EditMode), typeof(MainWindow), new FrameworkPropertyMetadata(EditMode.Show, OnEditStatPropertyChanged));
        public DependencyProperty SubdivisionLevelProperty = DependencyProperty.Register("SubdivisionLevel", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(0, OnSubDivisionPropertyChanged));
        public DependencyProperty XDivisionProperty = DependencyProperty.Register("XDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4, OnDivisionPropertyChanged));
        public DependencyProperty YDivisionProperty = DependencyProperty.Register("YDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4, OnDivisionPropertyChanged));
        public DependencyProperty ZDivisionProperty = DependencyProperty.Register("ZDivision", typeof(int), typeof(MainWindow), new FrameworkPropertyMetadata(4, OnDivisionPropertyChanged));
        public DependencyProperty TimeStatProperty = DependencyProperty.Register("TimeStat", typeof(TimeMode), typeof(MainWindow), new FrameworkPropertyMetadata(TimeMode.Linear));
        public DependencyProperty DurationProperty = DependencyProperty.Register("Duration", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(1.0));
        public DependencyProperty ElapsedProperty = DependencyProperty.Register("Elapsed", typeof(double), typeof(MainWindow), new FrameworkPropertyMetadata(0.0));

        private MeshGeometry3D mesh;
        private MeshGeometry3D srcMesh;

        private Point3D[] coords;
        private Point3D[] controls;
        static private Dictionary<int, int> factorialCache = new Dictionary<int, int>();

        public CameraMode CameraStat {
            get { return (CameraMode)GetValue(CameraStatProperty); }
            set { SetValue(CameraStatProperty, value); }
        }

        public DisplayMode DisplayStat {
            get { return (DisplayMode)GetValue(DisplayStatProperty); }
            set { SetValue(DisplayStatProperty, value); }
        }

        public EditMode EditStat {
            get { return (EditMode)GetValue(EditStatProperty); }
            set { SetValue(EditStatProperty, value); }
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
            if (srcMesh == null) {
                srcMesh = ((Model.Children[0] as ModelVisual3D).Content as GeometryModel3D).Geometry as MeshGeometry3D;
            } else {
                ModelVisual3D visual = new ModelVisual3D();
                GeometryModel3D model = new GeometryModel3D();
                model.Geometry = srcMesh;
                model.Material = FindResource("WhiteMaterial") as Material;

                visual.Content = model;
                Model.Children[0] = visual;
            }
            
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

            if (EditStat == EditMode.Show) {
                ModelVisual3D visual = generateCage();
                if (Model.Children.Count != 1) {
                    Model.Children[1] = visual;
                } else {
                    Model.Children.Add(visual);
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
            LoopSubdivision loop = new LoopSubdivision(srcMesh);
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

            if (DisplayStat == DisplayMode.Wireframe) {
                    Model.Children[0] = generateWireframe(mesh);
            }

            if (EditStat == EditMode.Show) {
                ModelVisual3D visual = generateCage();
                if (Model.Children.Count != 1) {
                    Model.Children[1] = visual;
                } else {
                    Model.Children.Add(visual);
                }
            }
        }

        private void updateControls(Rect3D src, Rect3D dst) {
            Matrix3D diff = new Matrix3D();
            diff.Scale(new Vector3D(dst.SizeX / src.SizeX, dst.SizeY / src.SizeY, dst.SizeZ / src.SizeZ));
            diff.Translate((dst.Location + (Vector3D)dst.Size / 2) - (src.Location + (Vector3D)src.Size / 2));

            controls = controls.Select(p => p * diff).ToArray();
            foreach (Visual3D v in ControlPoints.Children) {
                v.Transform = new MatrixTransform3D(v.Transform.Value * diff);
            }
        }

        private static double calcBerstein(int i, int n, double u) {
            return (calcFactorial(n) * Math.Pow(u, i) * Math.Pow(1 - u, n - i)) / (calcFactorial(i) * calcFactorial(n - i));
        }

        private static int calcFactorial(int n) {
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

        private static void OnCameraStatPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            MainWindow window = source as MainWindow;
            ProjectionCamera camera = null;

            switch ((CameraMode)e.NewValue) {
                case CameraMode.Perspective:
                        camera = new PerspectiveCamera();
                        break;
                case CameraMode.Orthographic:
                        camera = new OrthographicCamera();
                        break;
            }

            camera.Position = window.Viewport.Camera.Position;
            camera.LookDirection = window.Viewport.Camera.LookDirection;
            camera.UpDirection = window.Viewport.Camera.UpDirection;
            window.Viewport.Camera = camera;
        }

        private static void OnDisplayStatPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            MainWindow window = source as MainWindow;

            switch ((DisplayMode)e.NewValue) {
                case DisplayMode.Rendered:
                    ModelVisual3D visual = new ModelVisual3D();
                    MeshGeometry3D mesh = window.mesh;
                    GeometryModel3D model = new GeometryModel3D();
                    model.Geometry = mesh;
                    model.Material = window.FindResource("WhiteMaterial") as Material;

                    visual.Content = model;
                    window.Model.Children[0] = visual;
                    break;
                case DisplayMode.Wireframe:
                    window.Model.Children[0] = generateWireframe(window.mesh);
                    break;
            }
        }

        private static void OnEditStatPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            MainWindow window = source as MainWindow;

            switch ((EditMode)e.NewValue) {
                case EditMode.Show:
                    ModelVisual3D visual = window.generateCage();
                    if (window.Model.Children.Count != 1) {
                        window.Model.Children[1] = visual;
                    } else {
                        window.Model.Children.Add(visual);
                    }
                    window.Grid.Visible = true;
                    foreach (Point3D p in window.controls) {
                        PointsVisual3D v = new PointsVisual3D();
                        v.Size = 5;
                        v.Points.Add(p);
                        window.ControlPoints.Children.Add(v);
                    }
                    window.XAxis.Points.Add(new Point3D(-100, 0, 0));
                    window.XAxis.Points.Add(new Point3D(100,0,0));
                    window.YAxis.Points.Add(new Point3D(0, -100, 0));
                    window.YAxis.Points.Add(new Point3D(0, 100, 0));
                    window.ZAxis.Points.Add(new Point3D(0, 0, -100));
                    window.ZAxis.Points.Add(new Point3D(0, 0, 100));
                    break;
                case EditMode.Hide:
                    if (window.Model.Children.Count != 1) {
                        window.Model.Children.RemoveAt(1);
                    }
                    window.Grid.Visible = false;
                    window.ControlPoints.Children.Clear();
                    window.XAxis.Points.Clear();
                    window.YAxis.Points.Clear();
                    window.ZAxis.Points.Clear();
                    break;
            }
        }

        private static void OnSubDivisionPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            (source as MainWindow).updateSubdivision();
            (source as MainWindow).updateDeformation();
        }

        private static void OnDivisionPropertyChanged(DependencyObject source, DependencyPropertyChangedEventArgs e) {
            (source as MainWindow).initializeDeformation();
        }

        private void Open_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = "xml";
            dialog.Filter = "XML Files (*.xml)|*.xml";

            if (dialog.ShowDialog() == true) {
                using (FileStream reader = File.OpenRead(dialog.FileName)) {
                    SceneArchive archive = (SceneArchive)(new XmlSerializer(typeof(SceneArchive))).Deserialize(reader);

                    ModelVisual3D visual = new ModelVisual3D();
                    GeometryModel3D model = new GeometryModel3D();
                    model.Geometry = archive.Mesh;
                    model.Material = FindResource("WhiteMaterial") as Material;

                    visual.Content = model;
                    Model.Children[0] = visual;

                    srcMesh = archive.Mesh;
                    controls = archive.Control;

                    ControlPoints.Children.Clear();
                    foreach (Point3D p in controls) {
                        PointsVisual3D v = new PointsVisual3D();
                        v.Size = 5;
                        v.Points.Add(p);
                        ControlPoints.Children.Add(v);
                    }

                    SubdivisionLevel = archive.SubDivisionLevel;
                    XDivision = archive.XDivision;
                    YDivision = archive.YDivision;
                    ZDivision = archive.ZDivision;

                    initializeSubdivision();
                    updateDeformation();
                }

                MessageBox.Show(FindResource("SceneLoadText") as string + dialog.FileName, FindResource("SceneLoadTitle") as string, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e) {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.DefaultExt = "xml";
            dialog.Filter = "XML Files (*.xml)|*.xml";

            if (dialog.ShowDialog() == true) {
                SceneArchive archive = new SceneArchive(srcMesh, controls, SubdivisionLevel, XDivision, YDivision, ZDivision);

                using (FileStream writer = File.Create(dialog.FileName)) {
                    (new XmlSerializer(archive.GetType())).Serialize(writer, archive);
                }

                MessageBox.Show(FindResource("SceneSaveText") as string + dialog.FileName, FindResource("SceneSaveTitle") as string, MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = Importers.DefaultExtension;
            dialog.Filter = Importers.Filter;

            if (dialog.ShowDialog() == true) {
                GeometryModel3D model = new ModelImporter().Load(dialog.FileName).Children[0] as GeometryModel3D;
                srcMesh = model.Geometry as MeshGeometry3D;

                updateControls(Model.Children.FindBounds(), model.Bounds);

                initializeSubdivision();
                updateDeformation();
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

        private static ModelVisual3D generateWireframe(MeshGeometry3D mesh) {
            ModelVisual3D visual = new ModelVisual3D();
            for (int i = 0; i < mesh.TriangleIndices.Count; i += 3) {
                LinesVisual3D t = new LinesVisual3D();
                t.Points.Add(mesh.Positions[mesh.TriangleIndices[i]]);
                t.Points.Add(mesh.Positions[mesh.TriangleIndices[i + 1]]);
                t.Points.Add(mesh.Positions[mesh.TriangleIndices[i + 1]]);
                t.Points.Add(mesh.Positions[mesh.TriangleIndices[i + 2]]);
                t.Points.Add(mesh.Positions[mesh.TriangleIndices[i + 2]]);
                t.Points.Add(mesh.Positions[mesh.TriangleIndices[i]]);
                visual.Children.Add(t);
            }
            return visual;
        }

        private static Point3D[] generateBezierCurve(Point3D[] knots, int n) {
            int level = knots.Length - 1;
            int samples = knots.Length * n;
            Point3D[] smooth = new Point3D[samples + 1];

            for (int i = 0; i < samples; i++) {
                double u = (double)i / samples;
                Point3D pos = new Point3D();
                for (int k = 0; k < knots.Length; k++) {
                    pos += (Vector3D)knots[k].Multiply(calcBerstein(k, level, u));
                }
                smooth[i] = pos;
            }
            smooth[samples] = knots[knots.Length - 1];

            return smooth;
        }

        private ModelVisual3D generateCage() {
            ModelVisual3D visual = new ModelVisual3D();
            
            for (int x = 0; x < XDivision; x++) {
                int xIndex = x * YDivision * ZDivision;
                for (int y = 0; y < YDivision; y++) {
                    int yIndex = y * ZDivision;
                    Point3D[] knots = new Point3D[ZDivision];
                    for (int z = 0; z < ZDivision; z++) {
                        int index = xIndex + yIndex + z;
                        knots[z] = controls[index];
                    }
                    Point3D[] curve = generateBezierCurve(knots, 20);
                    LinesVisual3D polyline = new LinesVisual3D();
                    for (int i = 1; i < curve.Length; i++) {
                        polyline.Points.Add(curve[i - 1]);
                        polyline.Points.Add(curve[i]);
                    }
                    visual.Children.Add(polyline);
                }
            }

            for (int y = 0; y < YDivision; y++) {
                int yIndex = y * ZDivision;
                for (int z = 0; z < ZDivision; z++) {
                    Point3D[] knots = new Point3D[XDivision];
                    for (int x = 0; x < XDivision; x++) {
                        int xIndex = x * YDivision * ZDivision;
                        int index = xIndex + yIndex + z;
                        knots[x] = controls[index];
                    }
                    Point3D[] curve = generateBezierCurve(knots, 20);
                    LinesVisual3D polyline = new LinesVisual3D();
                    for (int i = 1; i < curve.Length; i++) {
                        polyline.Points.Add(curve[i - 1]);
                        polyline.Points.Add(curve[i]);
                    }
                    visual.Children.Add(polyline);
                }
            }

            for (int z = 0; z < ZDivision; z++) {
                for (int x = 0; x < XDivision; x++) {
                    int xIndex = x * YDivision * ZDivision;
                    Point3D[] knots = new Point3D[YDivision];
                    for (int y = 0; y < YDivision; y++) {
                        int yIndex = y * ZDivision;
                        int index = xIndex + yIndex + z;
                        knots[y] = controls[index];
                    }
                    Point3D[] curve = generateBezierCurve(knots, 20);
                    LinesVisual3D polyline = new LinesVisual3D();
                    for (int i = 1; i < curve.Length; i++) {
                        polyline.Points.Add(curve[i - 1]);
                        polyline.Points.Add(curve[i]);
                    }
                    visual.Children.Add(polyline);
                }
            }

            return visual;
        }

        static void ExceptionHandler(object sender, UnhandledExceptionEventArgs args) {
            Exception e = args.ExceptionObject as Exception;
            MessageBox.Show(e.StackTrace, e.Message, MessageBoxButton.OK, MessageBoxImage.Stop);
        }
    }
}
