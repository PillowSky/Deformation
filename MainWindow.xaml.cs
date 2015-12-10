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

namespace Deformation {

    public partial class MainWindow : Window {
        //private GeometryModel3D mGeometry;
        
        public MainWindow() {
            InitializeComponent();
			//BuildSolid();
		}

		/*private void BuildSolid() {
			// Define 3D mesh object
			MeshGeometry3D mesh = new MeshGeometry3D();

			mesh.Positions.Add(new Point3D(-0.5, -0.5, 1));
			//mesh.Normals.Add(new Vector3D(0, 0, 1));
			mesh.Positions.Add(new Point3D(0.5, -0.5, 1));
			//mesh.Normals.Add(new Vector3D(0, 0, 1));
			mesh.Positions.Add(new Point3D(0.5, 0.5, 1));
			//mesh.Normals.Add(new Vector3D(0, 0, 1));
			mesh.Positions.Add(new Point3D(-0.5, 0.5, 1));
			//mesh.Normals.Add(new Vector3D(0, 0, 1));

			mesh.Positions.Add(new Point3D(-1, -1, -1));
			//mesh.Normals.Add(new Vector3D(0, 0, -1));
			mesh.Positions.Add(new Point3D(1, -1, -1));
			//mesh.Normals.Add(new Vector3D(0, 0, -1));
			mesh.Positions.Add(new Point3D(1, 1, -1));
			//mesh.Normals.Add(new Vector3D(0, 0, -1));
			mesh.Positions.Add(new Point3D(-1, 1, -1));
			//mesh.Normals.Add(new Vector3D(0, 0, -1));

			// Front face
			mesh.TriangleIndices.Add(0);
			mesh.TriangleIndices.Add(1);
			mesh.TriangleIndices.Add(2);
			mesh.TriangleIndices.Add(2);
			mesh.TriangleIndices.Add(3);
			mesh.TriangleIndices.Add(0);

			// Back face
			mesh.TriangleIndices.Add(6);
			mesh.TriangleIndices.Add(5);
			mesh.TriangleIndices.Add(4);
			mesh.TriangleIndices.Add(4);
			mesh.TriangleIndices.Add(7);
			mesh.TriangleIndices.Add(6);

			// Right face
			mesh.TriangleIndices.Add(1);
			mesh.TriangleIndices.Add(5);
			mesh.TriangleIndices.Add(2);
			mesh.TriangleIndices.Add(5);
			mesh.TriangleIndices.Add(6);
			mesh.TriangleIndices.Add(2);

			// Top face
			mesh.TriangleIndices.Add(2);
			mesh.TriangleIndices.Add(6);
			mesh.TriangleIndices.Add(3);
			mesh.TriangleIndices.Add(3);
			mesh.TriangleIndices.Add(6);
			mesh.TriangleIndices.Add(7);

			// Bottom face
			mesh.TriangleIndices.Add(5);
			mesh.TriangleIndices.Add(1);
			mesh.TriangleIndices.Add(0);
			mesh.TriangleIndices.Add(0);
			mesh.TriangleIndices.Add(4);
			mesh.TriangleIndices.Add(5);

			// Right face
			mesh.TriangleIndices.Add(4);
			mesh.TriangleIndices.Add(0);
			mesh.TriangleIndices.Add(3);
			mesh.TriangleIndices.Add(3);
			mesh.TriangleIndices.Add(7);
			mesh.TriangleIndices.Add(4);

			// Geometry creation
			mGeometry = new GeometryModel3D(mesh, new DiffuseMaterial(Brushes.YellowGreen));
			mGeometry.Transform = new Transform3DGroup();
			group.Children.Add(mGeometry);
		}*/

        private void Button_Click(object sender, RoutedEventArgs e) {
            for (int i = 0; i < mesh.Positions.Count; i++) {
                var point = mesh.Positions[i];
                point.X *= 2;
                mesh.Positions[i] = point;
            }
        }
    }
}
