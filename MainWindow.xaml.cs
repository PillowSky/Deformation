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
using System.Windows.Media.Media3D;
using Microsoft.Win32;
using HelixToolkit.Wpf;
using _3DTools;

namespace Deformation {
    public partial class MainWindow : Window {
        private MeshGeometry3D mesh;
        private Point3D[] coords;
        private int[][] maps;
        private int xDim = 3;
        private int yDim = 3;
        private int zDim = 3;

        public MainWindow() {
            InitializeComponent();
            UpdateControlPoint();
            BindMouseToControlPoint();
            InitializeDeformation();
        }

        public void UpdateControlPoint() {
            ControlPoints.Children.Clear();
            Rect3D rect = Model.Children.FindBounds();

            for (int x = 0; x <= xDim; x++) {
                double xValue = rect.X + rect.SizeX * x / xDim;
                for (int y = 0; y <= yDim; y++) {
                    double yValue = rect.Y + rect.SizeY * y / yDim;
                    for (int z = 0; z <= zDim; z++) {
                        double zValue = rect.Z + rect.SizeZ * z / zDim;
                        PointsVisual3D p = new PointsVisual3D();
                        p.Size = 5;
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

        private void Load_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".obj";
            dialog.Filter = "Wavefront file (*.obj)|*.obj|Lightwave file (*.lwo)|*.lwo|Object File Format file (*.off)|*.off|StereoLithography file (*.stl)|*.stl|3D Studio file (*.3ds)|*.3ds";

            if (dialog.ShowDialog() == true) {
                Model3D model = (new ModelImporter()).Load(dialog.FileName);
                ModelVisual3D visual = new ModelVisual3D();
                visual.Content = model;
                Model.Children[0] = visual;
                UpdateControlPoint();
                InitializeDeformation();
            }
        }

        private void Deform_Click(object sender, RoutedEventArgs e) {
            for (int i = 0; i < mesh.Positions.Count; i++) {
                var point = mesh.Positions[i];
                point.X *= 1.1;
                mesh.Positions[i] = point;
            }
        }

        private void InitializeDeformation() {
            mesh = ((Model.Children[0] as ModelVisual3D).Content as GeometryModel3D).Geometry as MeshGeometry3D;
            coords = new Point3D[mesh.Positions.Count];
            maps = new int[mesh.Positions.Count][];

            Rect3D bound = Model.Children.FindBounds();
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Vector3D diff = mesh.Positions[i] - bound.Location;
                Point3D normalize = new Point3D(diff.X / bound.SizeX, diff.Y / bound.SizeY, diff.Z / bound.SizeZ);
                coords[i] = normalize;
                maps[i] = new int[64];
                double xId = normalize.X / 0.25;
                double yId = normalize.Y / 0.25;
                double zId = normalize.Z / 0.25;

                int id = 0;
                for (int x = 0; x < 4; x++) {
                    int xValue = clamp(x, xId) * 16;
                    for (int y = 0; y < 4; y++) {
                        int yValue = clamp(y, yId) * 4;
                        for (int z = 0; z < 4; z++) {
                            int zValue = clamp(z, zId);
                            maps[i][id++] = xValue + yValue + zValue;
                        }
                    }
                }
            }
            //Console.WriteLine(mesh.Positions);
            //Console.WriteLine(coords);
        }

        private int clamp(int pos, double value) {
            double result = 0;
            switch(pos) {
                case 0:
                    result =  Math.Floor(value) - 1;
                    break;
                case 1:
                    result = Math.Floor(value);
                    break;
                case 2:
                    result = Math.Ceiling(value);
                    break;
                case 3:
                    result = Math.Ceiling(value) + 1;
                    break;
            }

            if (result > 3) {
                return 3;
            } else if (result < 0) {
                return 0;
            } else {
                return (int)result;
            }
            
        }

        private void UpdateDeformation() {
            for (int i = 0; i < mesh.Positions.Count; i++) {
                Point3D coord = coords[i];
                int[] map = maps[i];
                Point3D pos = new Point3D();
                Point3D[] controls = ControlPoints.Children.Select(v => (v as PointsVisual3D).Points[0]).ToArray();

                int id = 0;
                for (int x = 0; x < 4; x++) {
                    for (int y = 0; y < 4; y++) {
                        for (int z = 0; z < 4; z++) {
                            double scale = getBoean(x, coord.X) * getBoean(y, coord.Y) * getBoean(z, coord.Z);
                            Point3D p = controls[map[id++]];
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
    }
}
