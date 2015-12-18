using System;
using System.Windows.Media.Media3D;

namespace Deformation {
    public class SceneArchive {
        public MeshGeometry3D Mesh;
        public Point3D[] Control;
        public int XDivision;
        public int YDivision;
        public int ZDivision;
        public int SubDivisionLevel;

        public SceneArchive() {

        }

        public SceneArchive(MeshGeometry3D mesh, Point3D[] control, int subDivisionLevel, int xDivision, int yDivision, int zDivision) {
            Mesh = mesh;
            Control = control;
            SubDivisionLevel = subDivisionLevel;
            XDivision = xDivision;
            YDivision = yDivision;
            ZDivision = zDivision;
        } 
    }
}
