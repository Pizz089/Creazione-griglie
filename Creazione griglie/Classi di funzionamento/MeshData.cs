using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace DirectXEditor
{
    public class MeshData : INotifyPropertyChanged
    {
        // Dati Base
        public string Name { get; set; }
        public bool IsGroup { get; set; }

        // Espansione e Selezione nell'albero UI
        private bool _isExpanded;
        public bool IsExpanded
        {
            get { return _isExpanded; }
            set { _isExpanded = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public ObservableCollection<MeshData> Children { get; set; } = new ObservableCollection<MeshData>();

        // Dati Originali del File X
        public string OriginalXFileContent { get; set; }
        public string OriginalFileName { get; set; }

        // Elementi 3D (HelixToolkit / WPF)
        public MeshGeometry3D Geometry { get; set; }
        public Material OriginalMaterial { get; set; }
        public GeometryModel3D Model3D { get; set; }

        // Coordinate e Rotazioni Base
        public double PosX { get; set; }
        public double PosY { get; set; }
        public double PosZ { get; set; }
        public double RotX { get; set; }
        public double RotY { get; set; }
        public double RotZ { get; set; }

        // Scala uniforme (Mantenuta per l'importazione XML originaria)
        public double ScaleXYZ { get; set; } = 1.0;

        // NUOVE PROPRIETÀ PER LA DEFORMAZIONE FISICA (Assi separati)
        private double _scaleX = 1.0;
        public double ScaleX
        {
            get { return _scaleX; }
            set { _scaleX = value; OnPropertyChanged(); }
        }

        private double _scaleY = 1.0;
        public double ScaleY
        {
            get { return _scaleY; }
            set { _scaleY = value; OnPropertyChanged(); }
        }

        private double _scaleZ = 1.0;
        public double ScaleZ
        {
            get { return _scaleZ; }
            set { _scaleZ = value; OnPropertyChanged(); }
        }

        // Texture
        public string TextureName { get; set; }
        public string NewTexturePath { get; set; }
        public bool RemoveTexture { get; set; }
        public double TextureScale { get; set; } = 1.0;
        public double TextureRotation { get; set; }

        // Proprietà Materiale
        public Color MeshColor { get; set; } = Colors.LightGray;
        public bool HasColor { get; set; }
        public double Alpha { get; set; } = 1.0;
        public Color Specular { get; set; } = Colors.Transparent;
        public Color Emissive { get; set; } = Colors.Transparent;
        public double Power { get; set; }

        // Implementazione MVVM per notificare l'interfaccia quando un valore cambia
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}