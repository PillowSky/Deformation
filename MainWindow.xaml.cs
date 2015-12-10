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

namespace Deformation {

    public partial class MainWindow : Window {

        public MainWindow() {
            InitializeComponent();
        }

        private void Load_Click(object sender, RoutedEventArgs e) {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".obj";
            dialog.Filter = "Wavefront file (*.obj)|*.obj|Lightwave file (*.lwo)|*.lwo|Object File Format file (*.off)|*.off|StereoLithography file (*.stl)|*.stl|3D Studio file (*.3ds)|*.3ds";

            if (dialog.ShowDialog() == true) {
                Model3D model = (new ModelImporter()).Load(dialog.FileName);
                ModelVisual3D visual = new ModelVisual3D();
                visual.Content = model;
                container.Children[0] = visual;
            }
        }

        private void Deform_Click(object sender, RoutedEventArgs e) {

        }
    }
}
