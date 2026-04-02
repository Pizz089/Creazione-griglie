using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Media.Media3D;

// Forzo le classi WPF per evitare conflitti con la libreria di disegno
using Color = System.Windows.Media.Color;
using Colors = System.Windows.Media.Colors;

namespace DirectXEditor
{
    public class MeshData : INotifyPropertyChanged
    {
        public string Name { get; set; } = "Elemento Sconosciuto";
        public MeshGeometry3D Geometry { get; set; } = new MeshGeometry3D();
        public string TextureName { get; set; } = "";

        // Salvo il testo originario del file .x per non corromperlo durante il salvataggio
        public string OriginalXFileContent { get; set; } = "";
        public string OriginalFileName { get; set; } = "";

        // Parametri Materiale
        public string OriginalMaterialContent { get; set; } = "";
        public Color MeshColor { get; set; } = Colors.LightGray;
        public double Alpha { get; set; } = 1.0;
        public double Power { get; set; } = 0.0;
        public Color Specular { get; set; } = Colors.Black;
        public Color Emissive { get; set; } = Colors.Black;
        public bool HasColor { get; set; } = false;

        public GeometryModel3D Model3D { get; set; }
        public Material OriginalMaterial { get; set; }

        public string NewTexturePath { get; set; } = "";
        public bool RemoveTexture { get; set; } = false;

        public double TextureScale { get; set; } = 1.0;
        public double TextureRotation { get; set; } = 0;

        // Coordinate spaziali estratte dinamicamente dall'XML dello Stile
        private double _posX = 0, _posY = 0, _posZ = 0;
        private double _rotX = 0, _rotY = 0, _rotZ = 0;
        private double _scaleXYZ = 1;

        public double PosX { get => _posX; set { _posX = value; OnPropertyChanged(nameof(PosX)); } }
        public double PosY { get => _posY; set { _posY = value; OnPropertyChanged(nameof(PosY)); } }
        public double PosZ { get => _posZ; set { _posZ = value; OnPropertyChanged(nameof(PosZ)); } }
        public double RotX { get => _rotX; set { _rotX = value; OnPropertyChanged(nameof(RotX)); } }
        public double RotY { get => _rotY; set { _rotY = value; OnPropertyChanged(nameof(RotY)); } }
        public double RotZ { get => _rotZ; set { _rotZ = value; OnPropertyChanged(nameof(RotZ)); } }
        public double ScaleXYZ { get => _scaleXYZ; set { _scaleXYZ = value; OnPropertyChanged(nameof(ScaleXYZ)); } }

        public ObservableCollection<MeshData> Children { get; set; } = new ObservableCollection<MeshData>();
        public bool IsGroup { get; set; } = false;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        private bool _isExpanded = false;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}